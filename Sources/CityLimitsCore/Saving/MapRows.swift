/// How a save holds the map: as rows of characters, a layer for each part of a tile, in the flat
/// format, and as one 16-bit word a tile, column by column, in the binary format. Rail and
/// stadiums, seaports, and airports are saved in parts of their own, so the map holds what lies
/// beneath them; a hole in a standing building is written whole, and the fire part marks it.
enum MapRows {
    static let maxSide = 1024
    private static let noPart: Character = "."

    static let groundChars: [TileKind: Character] = [.grass: ".", .water: "~", .trees: "T", .rubble: "#"]
    static let useChars: [TileUse: Character] = [
        .none: ".", .road: "=", .residential: "R", .commercial: "C", .industrial: "I", .coalPowerPlant: "c",
        .nuclearPowerPlant: "n", .powerLine: "+", .roadAcrossPowerLine: "x", .roadDownPowerLine: "y",
        .fireStation: "F", .policeStation: "P", .park: "k", .hospital: "H", .church: "U",
    ]
    private static let groundOf = Dictionary(uniqueKeysWithValues: groundChars.map { ($1, $0) })
    private static let useOf = Dictionary(uniqueKeysWithValues: useChars.map { ($1, $0) })

    // A tile's word: bits 0 and 1 the ground, 2 to 5 the use, 6 to 9 the footprint part, 10 power,
    // and 11 to 15 a building centre's level (11 to 13) and land-value class (14 and 15), or a
    // house tile's house.
    private static let useShift = 2
    private static let partShift = 6
    private static let poweredBit = 1 << 10
    private static let extraShift = 11
    private static let classShift = 14

    /// What a tile holds once rail and the facilities are set aside, as the map part saves it.
    static func beneath(_ state: TileState) -> TileState {
        switch state.use {
        case .rail: TileState(state.kind)
        case .railAcrossRoad, .railDownRoad: TileState(.grass, .road)
        case .railAcrossPowerLine, .railDownPowerLine: TileState(.grass, .powerLine)
        case .stadium, .seaport, .airport: TileState(state.kind)
        default: state
        }
    }

    /// The map with every hole in a standing building, other than a facility's, drawn whole.
    static func wholeTiles(_ map: TileMap, _ hazards: Hazards) -> [TileState] {
        var tiles: [TileState] = []
        tiles.reserveCapacity(map.width * map.height)
        for y in 0..<map.height {
            for x in 0..<map.width {
                let whole = hazards.isHole(x, y) && !isHoleInFacility(map, hazards, x, y)
                tiles.append(beneath(whole ? hazards.wholeState(x, y) : map[x, y]))
            }
        }
        return tiles
    }

    static func isHoleInFacility(_ map: TileMap, _ hazards: Hazards, _ x: Int, _ y: Int) -> Bool {
        guard let owner = hazards.holeOwner(x, y) else { return false }
        return map[owner.x, owner.y].use.isFacility
    }

    // MARK: The flat layers

    static func encode(_ map: TileMap, _ hazards: Hazards) -> JSON {
        let tiles = wholeTiles(map, hazards)
        var ground: [JSON] = [], use: [JSON] = [], part: [JSON] = [], buildings: [JSON] = [], houses: [JSON] = []
        for y in 0..<map.height {
            var groundRow = "", useRow = "", partRow = ""
            for x in 0..<map.width {
                let state = tiles[y * map.width + x]
                groundRow.append(groundChars[state.kind]!)
                useRow.append(useChars[state.use]!)
                partRow.append(state.hasFootprint ? Character(String(state.zonePart, radix: 16)) : noPart)
                if state.isBuildingCenter {
                    buildings.append(.object([
                        ("x", .int(x)), ("y", .int(y)), ("development", .int(state.development)),
                        ("valueClass", .int(state.valueClass)), ("powered", .bool(state.powered)),
                    ]))
                } else if state.hasHouse {
                    houses.append(.object([("x", .int(x)), ("y", .int(y)), ("house", .int(state.house))]))
                }
            }
            ground.append(.string(groundRow))
            use.append(.string(useRow))
            part.append(.string(partRow))
        }
        return .object([
            ("width", .int(map.width)), ("height", .int(map.height)), ("ground", .array(ground)), ("use", .array(use)),
            ("part", .array(part)), ("buildings", .array(buildings)), ("houses", .array(houses)),
        ])
    }

    static func decode(_ value: JSON) throws(FormatError) -> TileMap {
        var fields = try JSONFields(value, at: "map")
        let width = try fields.requiredInt("width", in: 1...maxSide)
        let height = try fields.requiredInt("height", in: 1...maxSide)
        let ground = try rows(&fields, "ground", width, height)
        let uses = try rows(&fields, "use", width, height)
        let parts = try rows(&fields, "part", width, height)

        var centres: [Int: (development: Int, valueClass: Int, powered: Bool)] = [:]
        for (index, item) in (try fields.array("buildings") ?? []).enumerated() {
            var building = try JSONFields(item, at: "map.buildings[\(index)]")
            let x = try building.requiredInt("x"), y = try building.requiredInt("y")
            let entry = (
                development: try building.int("development", in: 0...TileState.maxDevelopment) ?? 0,
                valueClass: try building.int("valueClass", in: 0...TileState.maxValueClass) ?? 0,
                powered: try building.bool("powered") ?? false)
            try building.finish(noun: "a building has")
            guard (0..<width).contains(x), (0..<height).contains(y) else { throw FormatError("map.buildings: \(x), \(y) is off the map.") }
            guard centres.updateValue(entry, forKey: y * width + x) == nil else { throw FormatError("map.buildings: \(x), \(y) is listed twice.") }
        }
        var houses: [Int: Int] = [:]
        for (index, item) in (try fields.array("houses") ?? []).enumerated() {
            var house = try JSONFields(item, at: "map.houses[\(index)]")
            let x = try house.requiredInt("x"), y = try house.requiredInt("y")
            let drawing = try house.requiredInt("house", in: 0...TileState.houseDrawings)
            try house.finish(noun: "a house has")
            guard (0..<width).contains(x), (0..<height).contains(y) else { throw FormatError("map.houses: \(x), \(y) is off the map.") }
            guard houses.updateValue(drawing, forKey: y * width + x) == nil else { throw FormatError("map.houses: \(x), \(y) is listed twice.") }
        }
        try fields.finish(noun: "the map has")

        var tiles: [TileState] = []
        tiles.reserveCapacity(width * height)
        for y in 0..<height {
            for x in 0..<width {
                let at = "map at \(x), \(y)"
                guard let kind = groundOf[ground[y][x]] else { throw FormatError("\(at): '\(ground[y][x])' isn't a kind of ground.") }
                guard let use = useOf[uses[y][x]] else { throw FormatError("\(at): '\(uses[y][x])' isn't a use.") }
                let size = use.footprint
                let part: Int
                if size == 1 {
                    guard parts[y][x] == noPart else { throw FormatError("\(at): a \(use) tile has no footprint part, so its part is '\(noPart)'.") }
                    part = 0
                } else {
                    guard let digit = parts[y][x].hexDigitValue, digit < Footprint.parts(size) else {
                        throw FormatError("\(at): '\(parts[y][x])' isn't a part of a \(size) by \(size) \(use).")
                    }
                    part = digit
                }
                let building = centres[y * width + x]
                tiles.append(TileState(
                    kind, use, development: building?.development ?? 0, zonePart: part, valueClass: building?.valueClass ?? 0,
                    house: houses[y * width + x] ?? 0, powered: building?.powered ?? false))
            }
        }
        let map = TileMap(width: width, height: height, tiles: tiles)!
        try checkFootprints(map)
        for index in centres.keys where !map[index % width, index / width].isBuildingCenter {
            throw FormatError("map.buildings: \(index % width), \(index / width) isn't the centre of a building.")
        }
        for index in houses.keys where map[index % width, index / width].house == 0 {
            throw FormatError("map.houses: \(index % width), \(index / width) isn't a residential zone's outer tile.")
        }
        return map
    }

    private static func rows(_ fields: inout JSONFields, _ name: String, _ width: Int, _ height: Int) throws(FormatError) -> [[Character]] {
        let rows = try fields.strings(name) ?? []
        guard rows.count == height else { throw FormatError("map.\(name): needs \(height) rows, not \(rows.count).") }
        var result: [[Character]] = []
        for (y, row) in rows.enumerated() {
            let characters = Array(row)
            guard characters.count == width else { throw FormatError("map.\(name)[\(y)]: needs \(width) characters, not \(characters.count).") }
            result.append(characters)
        }
        return result
    }

    static func checkFootprints(_ map: TileMap) throws(FormatError) {
        for y in 0..<map.height {
            for x in 0..<map.width where map[x, y].hasFootprint {
                let state = map[x, y]
                let size = state.use.footprint
                let offset = Footprint.offsetFromCenter(part: state.zonePart, size: size)
                let (centreX, centreY) = (x - offset.dx, y - offset.dy)
                for part in 0..<Footprint.parts(size) {
                    let step = Footprint.offsetFromCenter(part: part, size: size)
                    let (tileX, tileY) = (centreX + step.dx, centreY + step.dy)
                    guard map.contains(tileX, tileY), map[tileX, tileY].use == state.use, map[tileX, tileY].zonePart == part else {
                        throw FormatError("map at \(x), \(y): the \(state.use) it belongs to isn't whole; its part \(part) should be at \(tileX), \(tileY).")
                    }
                }
            }
        }
    }

    // MARK: The binary words

    static func encodeWords(_ map: TileMap, _ hazards: Hazards) -> [UInt8] {
        let tiles = wholeTiles(map, hazards)
        var bytes: [UInt8] = []
        bytes.reserveCapacity(4 + map.width * map.height * 2)
        appendUInt16(map.width, to: &bytes)
        appendUInt16(map.height, to: &bytes)
        for x in 0..<map.width {
            for y in 0..<map.height {
                let state = tiles[y * map.width + x]
                let extra = state.isBuildingCenter ? state.development | (state.valueClass << (classShift - extraShift)) : state.house
                let word = state.kind.rawValue | (state.use.rawValue << useShift) | ((state.hasFootprint ? state.zonePart : 0) << partShift)
                    | (state.powered ? poweredBit : 0) | (extra << extraShift)
                appendUInt16(word, to: &bytes)
            }
        }
        return bytes
    }

    /// Reads the binary map into the same layers the flat format holds, so both are checked alike.
    static func decodeWords(_ bytes: ArraySlice<UInt8>) throws(FormatError) -> JSON {
        guard bytes.count >= 4 else { throw FormatError("MAP: is too short to hold the map's size.") }
        let start = bytes.startIndex
        let width = readUInt16(bytes, start), height = readUInt16(bytes, start + 2)
        guard (1...maxSide).contains(width), (1...maxSide).contains(height) else {
            throw FormatError("map: a \(width) by \(height) map is out of range; each side is 1 to \(maxSide).")
        }
        guard bytes.count == 4 + width * height * 2 else {
            throw FormatError("MAP: a \(width) by \(height) map needs \(4 + width * height * 2) bytes, not \(bytes.count).")
        }
        var ground = Array(repeating: Array(repeating: Character("."), count: width), count: height)
        var use = ground, part = ground
        var buildings: [JSON] = [], houses: [JSON] = []
        var offset = start + 4
        for x in 0..<width {
            for y in 0..<height {
                let word = readUInt16(bytes, offset)
                offset += 2
                guard let kind = TileKind(rawValue: word & 0b11), let tileUse = TileUse(rawValue: (word >> useShift) & 0b1111),
                    let groundChar = groundChars[kind], let useChar = useChars[tileUse]
                else {
                    throw FormatError("map at \(x), \(y): the tile's word, \(word), holds a ground or a use there isn't.")
                }
                let size = tileUse.footprint
                let tilePart = (word >> partShift) & 0b1111
                let extra = word >> extraShift
                ground[y][x] = groundChar
                use[y][x] = useChar
                part[y][x] = size > 1 ? Character(String(tilePart, radix: 16)) : noPart
                if size > 1 && tilePart == Footprint.centerPart(size) {
                    buildings.append(.object([
                        ("x", .int(x)), ("y", .int(y)), ("development", .int(extra & 0b111)),
                        ("valueClass", .int(extra >> (classShift - extraShift))), ("powered", .bool(word & poweredBit != 0)),
                    ]))
                } else if extra != 0 {
                    houses.append(.object([("x", .int(x)), ("y", .int(y)), ("house", .int(extra))]))
                }
            }
        }
        // The flat format lists buildings and houses row by row; reading doesn't care about order.
        return .object([
            ("width", .int(width)), ("height", .int(height)), ("ground", .array(ground.map { .string(String($0)) })),
            ("use", .array(use.map { .string(String($0)) })), ("part", .array(part.map { .string(String($0)) })),
            ("buildings", .array(buildings)), ("houses", .array(houses)),
        ])
    }

    static func appendUInt16(_ value: Int, to bytes: inout [UInt8]) {
        bytes.append(UInt8(truncatingIfNeeded: value >> 8))
        bytes.append(UInt8(truncatingIfNeeded: value))
    }

    static func readUInt16(_ bytes: ArraySlice<UInt8>, _ at: Int) -> Int { Int(bytes[at]) << 8 | Int(bytes[at + 1]) }

    // MARK: Per-tile layers

    /// A layer as rows of characters, one for each tile.
    static func layer(_ map: TileMap, _ character: (Int, Int) -> Character) -> JSON {
        .array((0..<map.height).map { y in .string(String((0..<map.width).map { x in character(x, y) })) })
    }

    /// Reads a layer's rows, calling `apply` with each tile's character that isn't `.`.
    static func readLayer(
        _ rows: [String]?, _ at: String, _ map: TileMap, _ apply: (Int, Int, Character) throws(FormatError) -> Void
    ) throws(FormatError) {
        guard let rows, rows.count == map.height else { throw FormatError("\(at): needs \(map.height) rows.") }
        for (y, row) in rows.enumerated() {
            let characters = Array(row)
            guard characters.count == map.width else { throw FormatError("\(at)[\(y)]: needs \(map.width) characters.") }
            for (x, character) in characters.enumerated() where character != "." {
                try apply(x, y, character)
            }
        }
    }
}
