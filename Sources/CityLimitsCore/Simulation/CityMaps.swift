/// The city's overlay maps, as Micropolis keeps them: land value, pollution, crime, and population
/// density in 2 by 2 tile blocks, the worth of the terrain in 4 by 4, and the commercial rate in
/// 8 by 8.
public final class CityMaps {
    public static let maxLandValue = 250
    public static let mostPollution = 255
    public static let mostCrime = 250

    // Land value starts from 34 blocks out from the centre, times four, and stops falling at 32.
    private static let landValueReach = 34
    private static let farthestDistance = 32
    private static let terrainPerTile = 15
    // What each tile adds to its block's pollution, as Micropolis' core has it: every tile of an
    // industrial building, a coal plant, a seaport, and an airport, and a road by the traffic it
    // shows. A block holds at most 255.
    private static let industryPollution = 50
    private static let coalPlantPollution = 100
    private static let lightTrafficPollution = 50
    private static let heavyTrafficPollution = 75
    private static let firePollution = 90
    private static let mostBlockPollution = 255
    // Micropolis' crime: 128 less land value, plus density, no more than 300, less police
    // coverage, from 0 to 250. Land value loses 20 where crime is above 190.
    private static let crimeBase = 128
    private static let mostCrimeBeforePolice = 300
    private static let highCrime = 190
    private static let highCrimeLandValue = 20

    private let map: TileMap
    private let definitions: CityDefinitions
    let halfWidth: Int
    let halfHeight: Int
    let quarterWidth: Int
    let quarterHeight: Int
    let eighthWidth: Int
    var landValues: [Int]
    var pollution: [Int]
    private var pollutionRaw: [Int]
    private var pollutionScratch: [Int]
    var crime: [Int]
    var density: [Int]
    private var densityScratch: [Int]
    var terrain: [Int]
    private var terrainScratch: [Int]
    var commercialRates: [Int]

    /// The centre of the city's zones, which land value falls away from.
    public internal(set) var centerX: Int
    public internal(set) var centerY: Int
    public internal(set) var landValueAverage = 0
    public internal(set) var pollutionAverage = 0
    public internal(set) var crimeAverage = 0
    /// The share of industry's pollution it gives off, in percent; pollution controls lower it.
    public var industryPollutionPercent = 100

    public init(map: TileMap, definitions: CityDefinitions = .base) {
        self.map = map
        self.definitions = definitions
        halfWidth = (map.width + 1) / 2
        halfHeight = (map.height + 1) / 2
        quarterWidth = (map.width + 3) / 4
        quarterHeight = (map.height + 3) / 4
        eighthWidth = (map.width + 7) / 8
        let halves = Array(repeating: 0, count: halfWidth * halfHeight)
        (landValues, pollution, pollutionRaw, pollutionScratch, crime, density, densityScratch) =
            (halves, halves, halves, halves, halves, halves, halves)
        terrain = Array(repeating: 0, count: quarterWidth * quarterHeight)
        terrainScratch = terrain
        commercialRates = Array(repeating: 0, count: eighthWidth * ((map.height + 7) / 8))
        centerX = map.width / 2
        centerY = map.height / 2
    }

    private func half(_ x: Int, _ y: Int) -> Int { (y / 2) * halfWidth + x / 2 }

    public func landValue(_ x: Int, _ y: Int) -> Int { map.contains(x, y) ? landValues[half(x, y)] : 0 }
    public func pollution(_ x: Int, _ y: Int) -> Int { map.contains(x, y) ? pollution[half(x, y)] : 0 }
    public func crime(_ x: Int, _ y: Int) -> Int { map.contains(x, y) ? crime[half(x, y)] : 0 }
    public func density(_ x: Int, _ y: Int) -> Int { map.contains(x, y) ? density[half(x, y)] : 0 }
    public func commercialRate(_ x: Int, _ y: Int) -> Int {
        map.contains(x, y) ? commercialRates[(y / 8) * eighthWidth + x / 8] : 0
    }

    /// The top-left tile of the most polluted block, where the monster makes for.
    public var mostPolluted: (x: Int, y: Int) {
        var most = 0
        for block in pollution.indices.dropFirst() where pollution[block] > pollution[most] { most = block }
        return ((most % halfWidth) * 2, (most / halfWidth) * 2)
    }

    /// Works out land value and pollution from what is built, the traffic, fires, and facilities.
    public func updateLandValue(traffic: TrafficMap, hazards: Hazards, influences: FacilityInfluences? = nil) {
        for index in terrainScratch.indices { terrainScratch[index] = 0 }
        var total = 0
        var count = 0
        for blockY in 0..<halfHeight {
            for blockX in 0..<halfWidth {
                var built = false
                var blockPollution = 0
                for y in (blockY * 2)...(blockY * 2 + 1) {
                    for x in (blockX * 2)...(blockX * 2 + 1) {
                        if hazards.isBurning(x, y) {
                            blockPollution += Self.firePollution
                            continue
                        }
                        let state = map.state(atOrDefault: x, y)
                        let natural = state.use == .none
                            ? state.kind == .trees || state.kind == .water
                            : state.use == .park && Parks.isWooded(x, y)
                        if state.use != .none && !natural {
                            built = true
                            blockPollution += pollution(of: state, x, y, traffic)
                        } else if natural {
                            terrainScratch[(blockY / 2) * quarterWidth + blockX / 2] += Self.terrainPerTile
                        }
                    }
                }

                let block = blockY * halfWidth + blockX
                pollutionRaw[block] = min(blockPollution, Self.mostBlockPollution)
                var value = 0
                if built {
                    value = (Self.landValueReach - distanceToCenter(blockX, blockY)) * 4
                        + terrain[(blockY / 2) * quarterWidth + blockX / 2]
                    value -= pollution[block]
                    if crime[block] > Self.highCrime { value -= Self.highCrimeLandValue }
                    value += influences?.at(.landValue, blockX * 2, blockY * 2) ?? 0
                    value = min(max(value, 1), Self.maxLandValue)
                    total += value
                    count += 1
                }
                landValues[block] = value
            }
        }
        landValueAverage = count > 0 ? total / count : 0

        Self.smooth(pollutionRaw, into: &pollutionScratch, halfWidth, halfHeight)
        Self.smooth(pollutionScratch, into: &pollution, halfWidth, halfHeight)
        if let influences {
            for block in pollution.indices {
                let added = influences.at(.pollution, (block % halfWidth) * 2, (block / halfWidth) * 2)
                pollution[block] = min(max(pollution[block] + added, 0), Self.mostPollution)
            }
        }
        pollutionAverage = Self.averageOfNonZero(pollution)
        smoothTerrain()
    }

    /// Works out crime from land value, density, police, facilities, and ordinances.
    public func updateCrime(services: ServiceCoverage, influences: FacilityInfluences? = nil, ordinances: Int = 0) {
        var total = 0
        var count = 0
        for blockY in 0..<halfHeight {
            for blockX in 0..<halfWidth {
                let block = blockY * halfWidth + blockX
                let value = landValues[block]
                guard value != 0 else {
                    crime[block] = 0
                    continue
                }
                var blockCrime = min(Self.crimeBase - value + density[block], Self.mostCrimeBeforePolice)
                blockCrime -= services.policeCoverage(blockX * 2, blockY * 2)
                blockCrime += (influences?.at(.crime, blockX * 2, blockY * 2) ?? 0) + ordinances
                blockCrime = min(max(blockCrime, 0), Self.mostCrime)
                crime[block] = blockCrime
                total += blockCrime
                count += 1
            }
        }
        crimeAverage = count > 0 ? total / count : 0
    }

    /// Works out population density, the city's centre, and the commercial rate around it.
    public func updateDensity() {
        for index in density.indices { density[index] = 0 }
        var (totalX, totalY, zones) = (0, 0, 0)
        for y in 0..<map.height {
            for x in 0..<map.width where map[x, y].isZoneCenter {
                let state = map[x, y]
                // Residents count as they are; each commercial and industrial unit as eight.
                let population = definitions.zone(state.use).population(on: map, centerX: x, centerY: y)
                let weight = state.use == .residential ? population : population * 8
                density[half(x, y)] = min(weight * 8, 254)
                totalX += x
                totalY += y
                zones += 1
            }
        }
        Self.smooth(density, into: &densityScratch, halfWidth, halfHeight)
        Self.smooth(densityScratch, into: &density, halfWidth, halfHeight)
        Self.smooth(density, into: &densityScratch, halfWidth, halfHeight)
        for index in density.indices { density[index] = densityScratch[index] * 2 }

        (centerX, centerY) = zones > 0 ? (totalX / zones, totalY / zones) : (map.width / 2, map.height / 2)
        for index in commercialRates.indices {
            let (blockX, blockY) = (index % eighthWidth, index / eighthWidth)
            commercialRates[index] = 64 - distanceToCenter(blockX * 4, blockY * 4) / 4
        }
    }

    // A tile's pollution: a developed industrial zone's or a coal plant's, whichever tile of it,
    // or a road's by the traffic it showed when the scan last passed.
    private func pollution(of state: TileState, _ x: Int, _ y: Int, _ traffic: TrafficMap) -> Int {
        switch state.use {
        case .industrial:
            map.centerState(x, y).development > 0 ? Self.industryPollution * industryPollutionPercent / 100 : 0
        case .coalPowerPlant:
            Self.coalPlantPollution
        case .seaport, .airport:
            definitions.facility(state.use).pollution
        default:
            switch traffic.level(x, y) {
            case .light: Self.lightTrafficPollution
            case .heavy: Self.heavyTrafficPollution
            case .none: 0
            }
        }
    }

    // In 2 by 2 blocks, across plus down, no farther than 32.
    private func distanceToCenter(_ blockX: Int, _ blockY: Int) -> Int {
        min(abs(blockX - centerX / 2) + abs(blockY - centerY / 2), Self.farthestDistance)
    }

    // The average of the blocks that have any, as Micropolis counts it.
    private static func averageOfNonZero(_ blocks: [Int]) -> Int {
        var (total, count) = (0, 0)
        for value in blocks where value != 0 {
            total += value
            count += 1
        }
        return count > 0 ? total / count : 0
    }

    // Each cell becomes a quarter of itself and its four neighbours, no more than 255.
    private static func smooth(_ from: [Int], into to: inout [Int], _ width: Int, _ height: Int) {
        for y in 0..<height {
            for x in 0..<width {
                var z = from[y * width + x]
                if x > 0 { z += from[y * width + x - 1] }
                if x + 1 < width { z += from[y * width + x + 1] }
                if y > 0 { z += from[(y - 1) * width + x] }
                if y + 1 < height { z += from[(y + 1) * width + x] }
                to[y * width + x] = min(z / 4, 255)
            }
        }
    }

    // The terrain's worth spreads a quarter to its neighbours and keeps half of itself.
    private func smoothTerrain() {
        for y in 0..<quarterHeight {
            for x in 0..<quarterWidth {
                var around = 0
                if x > 0 { around += terrainScratch[y * quarterWidth + x - 1] }
                if x + 1 < quarterWidth { around += terrainScratch[y * quarterWidth + x + 1] }
                if y > 0 { around += terrainScratch[(y - 1) * quarterWidth + x] }
                if y + 1 < quarterHeight { around += terrainScratch[(y + 1) * quarterWidth + x] }
                terrain[y * quarterWidth + x] = around / 4 + terrainScratch[y * quarterWidth + x] / 2
            }
        }
    }
}
