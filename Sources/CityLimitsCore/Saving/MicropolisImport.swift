extension JSON {
    /// The value with the field at a path of object keys set; missing objects along it are made.
    func setting(_ path: ArraySlice<String>, to value: JSON) -> JSON {
        guard let key = path.first else { return value }
        var fields: [(key: String, value: JSON)] = if case .object(let existing) = self { existing } else { [] }
        if let index = fields.firstIndex(where: { $0.key == key }) {
            fields[index].value = fields[index].value.setting(path.dropFirst(), to: value)
        } else {
            fields.append((key, JSON.null.setting(path.dropFirst(), to: value)))
        }
        return .object(fields)
    }

    func setting(_ path: String, to value: JSON) -> JSON {
        setting(path.split(separator: ".").map(String.init)[...], to: value)
    }
}

/// Imports the city files of Micropolis and SimCity Classic, `.cty`; CityLimits never writes them.
/// A file is 27,120 bytes, or 27,248 with the header some editions add, of big-endian 16-bit
/// numbers: six histories of 240, a block of 120 settings, then the 120 by 100 map, column by
/// column, a Micropolis tile number in the low 10 bits and power in the highest.
public enum MicropolisImport {
    public static let fileExtension = "cty"
    public static let fileBytes = 27120
    public static let headerBytes = 128

    private static let width = 120
    private static let height = 100
    private static let historyLength = 240
    private static let histories = 6
    private static let miscLength = 120
    private static let mapOffset = (histories * historyLength + miscLength) * 2
    private static let tileMask = 1023
    private static let poweredFlag = 1 << 15

    // The settings block's numbers, by their place in it, as Micropolis writes them.
    private static let residentialPopulationAt = 2, commercialPopulationAt = 3, industrialPopulationAt = 4
    private static let residentialValveAt = 5, commercialValveAt = 6, industrialValveAt = 7
    private static let cityTimeAt = 8, landValueAverageAt = 12, fundsAt = 50, taxAt = 56
    private static let policeFundingAt = 58, fireFundingAt = 60, roadFundingAt = 62
    private static let fullFundingFixed = 65536.0

    // Micropolis' tile numbers, which CityLimits' tile sheet shares.
    private static let roads = 64...207, roadDrawings = 16
    private static let bridgeAcross = 64, bridgeDown = 65, roadAcrossPowerLine = 77, roadDownPowerLine = 78, openBridge = 79
    private static let underwaterPowerLines = 208...209, powerLines = 208...220
    private static let railAcrossPowerLine = 221, railDownPowerLine = 222, railAcrossRoad = 237, railDownRoad = 238
    private static let tunnels = 224...225, rails = 224...238
    private static let houses = 249...260
    private static let drawbridges = 828...831, downDrawbridges = 948...951, fountains = 840...843

    // Each building's drawings: its first tile, its size, its use, and for zones, how many levels
    // and value classes they come in.
    private struct Building {
        var first: Int
        var size: Int
        var use: TileUse
        var levels: Int
        var valueClasses: Int
    }

    private static let buildings = [
        Building(first: 240, size: 3, use: .residential, levels: 0, valueClasses: 1),
        Building(first: 261, size: 3, use: .residential, levels: 4, valueClasses: 4),
        Building(first: 405, size: 3, use: .hospital, levels: 0, valueClasses: 1),
        Building(first: 414, size: 3, use: .church, levels: 0, valueClasses: 1),
        Building(first: 423, size: 3, use: .commercial, levels: 0, valueClasses: 1),
        Building(first: 432, size: 3, use: .commercial, levels: 5, valueClasses: 4),
        Building(first: 612, size: 3, use: .industrial, levels: 0, valueClasses: 1),
        Building(first: 621, size: 3, use: .industrial, levels: 4, valueClasses: 2),
        Building(first: 693, size: 4, use: .seaport, levels: 0, valueClasses: 1),
        Building(first: 709, size: 6, use: .airport, levels: 0, valueClasses: 1),
        Building(first: 745, size: 4, use: .coalPowerPlant, levels: 0, valueClasses: 1),
        Building(first: 761, size: 3, use: .fireStation, levels: 0, valueClasses: 1),
        Building(first: 770, size: 3, use: .policeStation, levels: 0, valueClasses: 1),
        Building(first: 779, size: 4, use: .stadium, levels: 0, valueClasses: 1),
        // A stadium with a game on is drawn apart from an empty one, but is the same stadium.
        Building(first: 795, size: 4, use: .stadium, levels: 0, valueClasses: 1),
        Building(first: 811, size: 4, use: .nuclearPowerPlant, levels: 0, valueClasses: 1),
    ]

    /// Imports a Micropolis city. `name` is the file's name, which becomes the city's.
    public static func read(_ given: [UInt8], name: String) throws(FormatError) -> LoadedCity {
        var bytes = given[...]
        if bytes.count == fileBytes + headerBytes { bytes = bytes.dropFirst(headerBytes) }
        guard bytes.count == fileBytes else {
            throw FormatError("A Micropolis city is \(fileBytes) bytes, or \(fileBytes + headerBytes) with a header, not \(given.count).")
        }
        let data = Array(bytes)
        var replaced: [String: Int] = [:]
        let map = try readMap(data, &replaced)
        let city = try build(data, map, name: cityName(name), seed: seed(of: data))
        let notes = replaced.keys.sorted().map { what in
            let count = replaced[what]!
            return "\(count) \(what)\(count == 1 ? "" : "s") became bare ground: CityLimits doesn't have them yet."
        }
        return LoadedCity(city: city, notes: notes)
    }

    // The tiles a tile at a time, then the buildings from their centres, so each comes in whole.
    private static func readMap(_ bytes: [UInt8], _ replaced: inout [String: Int]) throws(FormatError) -> TileMap {
        var numbers = Array(repeating: 0, count: width * height)
        for x in 0..<width {
            for y in 0..<height { numbers[y * width + x] = word(bytes, mapOffset + (x * height + y) * 2) }
        }
        var tiles = numbers.map { tile($0 & tileMask, &replaced) }
        var covered = Array(repeating: false, count: width * height)
        for y in 0..<height {
            for x in 0..<width { try stamp(numbers, x, y, &tiles, &covered) }
        }
        // A building's tile with no centre to belong to is what's left of one, and becomes ground.
        for index in tiles.indices where !covered[index] && building(at: numbers[index] & tileMask) != nil {
            tiles[index] = .grass
            replaced["part of a building with no centre", default: 0] += 1
        }
        let map = TileMap(width: width, height: height, tiles: tiles)!
        try MapRows.checkFootprints(map)
        return map
    }

    private static func tile(_ tile: Int, _ replaced: inout [String: Int]) -> TileState {
        if roads.contains(tile) {
            // The road's drawings repeat with each level of traffic.
            switch roads.lowerBound + (tile - roads.lowerBound) % roadDrawings {
            case bridgeAcross, bridgeDown, openBridge: return TileState(.water, .road)
            case roadAcrossPowerLine: return TileState(.grass, .roadAcrossPowerLine)
            case roadDownPowerLine: return TileState(.grass, .roadDownPowerLine)
            default: return TileState(.grass, .road)
            }
        }
        func lost(_ what: String) -> TileState {
            replaced[what, default: 0] += 1
            return .grass
        }
        switch tile {
        case ...1: return .grass
        case ...20: return TileState(.water)
        case ...39: return TileState(.trees)
        case ...43: return TileState(.grass, .park)
        case ...47: return TileState(.rubble)
        case ...51: return lost("flooded tile")
        case ...55: return lost("radioactive tile")
        case ...63: return lost("burning tile")
        case underwaterPowerLines: return TileState(.water, .powerLine)
        case powerLines: return TileState(.grass, .powerLine)
        case railAcrossPowerLine: return TileState(.grass, .railAcrossPowerLine)
        case railDownPowerLine: return TileState(.grass, .railDownPowerLine)
        case railAcrossRoad: return TileState(.grass, .railAcrossRoad)
        case railDownRoad: return TileState(.grass, .railDownRoad)
        case tunnels: return TileState(.water, .rail)
        case rails: return TileState(.grass, .rail)
        case drawbridges, downDrawbridges: return TileState(.water, .road)
        case fountains: return TileState(.grass, .park)
        // A building's tiles are stamped from its centre; anything else, such as an animation, is ground.
        default: return .grass
        }
    }

    // Stamps the building whose centre this is, whole: a zone at the level and class its number
    // says, with the houses on an empty residential zone's outer tiles.
    private static func stamp(_ numbers: [Int], _ x: Int, _ y: Int, _ tiles: inout [TileState], _ covered: inout [Bool]) throws(FormatError) {
        // A house belongs to the zone around it and is never a centre, though its number falls where one would.
        let tile = numbers[y * width + x] & tileMask
        guard !houses.contains(tile), let building = building(at: tile) else { return }
        let parts = Footprint.parts(building.size)
        let drawing = (tile - building.first) / parts
        guard (tile - building.first) % parts == Footprint.centerPart(building.size) else { return }
        let (development, valueClass) = building.levels == 0 ? (0, 0) : (drawing % building.levels + 1, drawing / building.levels)
        let powered = numbers[y * width + x] & poweredFlag != 0
        for part in 0..<parts {
            let offset = Footprint.offsetFromCenter(part: part, size: building.size)
            let (tileX, tileY) = (x + offset.dx, y + offset.dy)
            guard (0..<width).contains(tileX), (0..<height).contains(tileY) else {
                throw FormatError("map at \(x), \(y): the building centred here runs off the map.")
            }
            let index = tileY * width + tileX
            covered[index] = true
            let outer = numbers[index] & tileMask
            let house = building.use == .residential && development == 0 && houses.contains(outer) ? outer - houses.lowerBound + 1 : 0
            tiles[index] = TileState(.grass, building.use, development: development, zonePart: part, valueClass: valueClass, house: house, powered: powered)
        }
    }

    private static func building(at tile: Int) -> Building? {
        for building in buildings {
            let drawings = building.levels == 0 ? 1 : building.levels * building.valueClasses
            if tile >= building.first && tile < building.first + drawings * Footprint.parts(building.size) { return building }
        }
        // A house stands on an empty residential zone's outer tile.
        return houses.contains(tile) ? buildings[0] : nil
    }

    // A city made from the map, saved, and given the file's numbers, then loaded, so an import
    // passes every check a save does; power is traced at once, as Micropolis does when it loads.
    private static func build(_ bytes: [UInt8], _ map: TileMap, name: String, seed: Int32) throws(FormatError) -> CitySimulation {
        let misc = histories * historyLength * 2
        func signedWord(_ at: Int) -> Int { Int(Int16(truncatingIfNeeded: word(bytes, misc + at * 2))) }
        func long(_ at: Int) -> Int { Int(Int32(truncatingIfNeeded: word(bytes, misc + at * 2) << 16 | word(bytes, misc + at * 2 + 2))) }
        func percent(_ fixed: Int) -> JSON {
            .int(min(max(Int((Double(fixed) / fullFundingFixed * Double(CityBudget.fullFunding)).rounded(.toNearestOrEven)), 0), CityBudget.fullFunding))
        }
        func history(_ index: Int) -> JSON {
            .array((0..<CityCensus.historyMonths).map { month in
                .int(max(0, Int(Int16(truncatingIfNeeded: word(bytes, (index * historyLength + month) * 2)))))
            })
        }

        var save = CitySimulation(map: map, seed: seed, funds: 0).capture()
        let cityTime = max(0, long(cityTimeAt))
        // Micropolis puts a tax outside 0 to 20 back to 7, and the funding is a fraction in 16.16.
        let tax = signedWord(taxAt)
        let taxRate = (0...CityBudget.maxTaxRate).contains(tax) ? tax : CityBudget.defaultTaxRate
        let (residents, commerce, industry) = (max(0, signedWord(residentialPopulationAt)), max(0, signedWord(commercialPopulationAt)), max(0, signedWord(industrialPopulationAt)))
        let changes: [(String, JSON)] = [
            ("name", .string(name)), ("cityTime", .int(cityTime)), ("tick", .int(cityTime * CityCalendar.stepsPerCount)),
            ("funds", .int(long(fundsAt))), ("budget.taxRate", .int(taxRate)), ("budget.taxEffect", .int(taxRate)),
            ("budget.roadFunding", percent(long(roadFundingAt))), ("budget.fireFunding", percent(long(fireFundingAt))),
            ("budget.policeFunding", percent(long(policeFundingAt))),
            ("census.residentPopulation", .int(residents * 20)), ("census.commercialPopulation", .int(commerce * 8 * 20)),
            ("census.industrialPopulation", .int(industry * 8 * 20)),
            ("census.residentialHistory", history(0)), ("census.commercialHistory", history(1)), ("census.industrialHistory", history(2)),
            ("demand.residential", .int(min(max(signedWord(residentialValveAt), -CityDemand.residentialLimit), CityDemand.residentialLimit))),
            ("demand.commercial", .int(min(max(signedWord(commercialValveAt), -CityDemand.businessLimit), CityDemand.businessLimit))),
            ("demand.industrial", .int(min(max(signedWord(industrialValveAt), -CityDemand.businessLimit), CityDemand.businessLimit))),
            ("cityMaps.landValueAverage", .int(min(max(signedWord(landValueAverageAt), 0), CityMaps.maxLandValue))),
        ]
        for (path, value) in changes { save = save.setting(path, to: value) }
        // An imported city runs on the base game, as Micropolis' cities were played.
        let city = try CitySimulation.restore(save, definitions: .base).city
        city.tracePowerNow()
        return city
    }

    private static func word(_ bytes: [UInt8], _ at: Int) -> Int { Int(bytes[at]) << 8 | Int(bytes[at + 1]) }

    // The file says nothing of a seed, so one is made from its contents: the same file always
    // imports as the same city.
    private static func seed(of bytes: [UInt8]) -> Int32 {
        var hash: UInt32 = 2_166_136_261
        for byte in bytes { hash = (hash ^ UInt32(byte)) &* 16_777_619 }
        return Int32(bitPattern: hash)
    }

    private static func cityName(_ fileName: String) -> String {
        let name = String(fileName.trimmingWhitespace.prefix(CitySimulation.maxNameLength)).trimmingWhitespace
        return CitySimulation.isValidName(name) ? name : "Micropolis city"
    }
}
