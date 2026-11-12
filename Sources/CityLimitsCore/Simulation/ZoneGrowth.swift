/// How zones grow and decline, as Micropolis weighs them each time the scan reaches one: a trip
/// to find, then a score from demand and land against a random roll. Residential zones grow
/// houses, then a building; commerce and industry grow straight to buildings.
final class ZoneGrowth {
    // A score this low or lower never grows, and this high or higher never declines.
    private static let growthFloor = -350
    private static let declineCeiling = 350
    // The roll a score is measured against runs from -32768 to 32767; this offset makes growth rare.
    private static let rollOffset = 26380
    // A zone with no power scores this, so it never grows and now and then declines.
    private static let unpoweredScore = -500
    // A residential zone's worth: land value times 32, at most 6000, less 3000.
    private static let mostResidentialWorth = 6000
    // The density around a residential zone its houses need before it builds.
    private static let densityForBuilding = 64
    // Micropolis' residential zones don't grow where pollution is above this.
    private static let mostPollutionForHomes = 128

    // The outer tiles in the order a house is looked for: across the top, the sides, the bottom.
    private static let lots: [(dx: Int, dy: Int)] = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]
    // The order a declining zone loses a house: down each column, left to right.
    private static let houseLossOrder: [(dx: Int, dy: Int)] = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    private let map: TileMap
    private let trips: TripPlanner
    private let definitions: CityDefinitions
    private let demand: CityDemand
    private let maps: CityMaps
    private let community: CommunityBuildings
    private let hazards: Hazards

    init(map: TileMap, trips: TripPlanner, definitions: CityDefinitions, demand: CityDemand, maps: CityMaps, community: CommunityBuildings, hazards: Hazards) {
        self.map = map
        self.trips = trips
        self.definitions = definitions
        self.demand = demand
        self.maps = maps
        self.community = community
        self.hazards = hazards
    }

    func visit(_ x: Int, _ y: Int, random: inout SeededGenerator) {
        let center = map[x, y]
        let spec = definitions.zone(center.use)
        switch center.use {
        case .residential: visitResidential(x, y, center, spec, &random)
        case .commercial: visitCommercial(x, y, center, spec, &random)
        default: visitIndustrial(x, y, center, spec, &random)
        }
    }

    private func drive(_ x: Int, _ y: Int, if wanted: Bool, _ random: inout SeededGenerator) -> TripOutcome {
        wanted ? trips.drive(map: map, definitions: definitions, random: &random, centerX: x, centerY: y) : .arrived
    }

    private func visitResidential(_ x: Int, _ y: Int, _ center: TileState, _ spec: ZoneSpec, _ random: inout SeededGenerator) {
        let population = spec.population(on: map, centerX: x, centerY: y)
        let trip = drive(x, y, if: population > random.next(in: 0..<36), &random)
        if trip == .noRoad {
            declineResidential(x, y, center, spec, valueClass(x, y), &random)
            return
        }
        // An empty zone, with no building, is weighed every time; any other one time in eight.
        if center.development > 0 && random.next(in: 0..<8) != 0 { return }

        let score = center.powered ? demand.residential + residentialWorth(x, y) : Self.unpoweredScore
        if grows(score, &random) {
            // One time in four an empty zone becomes a hospital or a church, if the city needs one.
            if population == 0 && random.next(in: 0..<4) == 0 {
                _ = community.build(on: map, x, y, hazards: hazards)
                return
            }
            growResidential(x, y, center, spec, valueClass(x, y), &random)
            return
        }
        if declines(score, &random) {
            declineResidential(x, y, center, spec, valueClass(x, y), &random)
        }
    }

    private func visitCommercial(_ x: Int, _ y: Int, _ center: TileState, _ spec: ZoneSpec, _ random: inout SeededGenerator) {
        let level = center.development
        let trip = drive(x, y, if: level > random.next(in: 0..<6), &random)
        if trip == .noRoad {
            setLevel(x, y, center, max(level - 1, 0), valueClass(x, y))
            return
        }
        if random.next(in: 0..<8) != 0 { return }

        let score = center.powered ? demand.commercial + maps.commercialRate(x, y) : Self.unpoweredScore
        if trip != .noDestination && grows(score, &random) {
            // Commerce grows only as far as land value can carry it.
            if level < spec.levels && level <= maps.landValue(x, y) / 32 {
                setLevel(x, y, center, level + 1, valueClass(x, y))
            }
            return
        }
        if declines(score, &random) && level > 0 {
            setLevel(x, y, center, level - 1, valueClass(x, y))
        }
    }

    private func visitIndustrial(_ x: Int, _ y: Int, _ center: TileState, _ spec: ZoneSpec, _ random: inout SeededGenerator) {
        let level = center.development
        let trip = drive(x, y, if: level > random.next(in: 0..<6), &random)
        if trip == .noRoad {
            setLevel(x, y, center, max(level - 1, 0), random.next(in: 0..<2))
            return
        }
        if random.next(in: 0..<8) != 0 { return }

        // Industry cares only for demand; its land-value class is a toss of a coin.
        let score = center.powered ? demand.industrial : Self.unpoweredScore
        if grows(score, &random) {
            let valueClass = random.next(in: 0..<2)
            if level < spec.levels { setLevel(x, y, center, level + 1, valueClass) }
            return
        }
        if declines(score, &random) && level > 0 {
            setLevel(x, y, center, level - 1, random.next(in: 0..<2))
        }
    }

    private func grows(_ score: Int, _ random: inout SeededGenerator) -> Bool {
        score > Self.growthFloor && score - Self.rollOffset > roll(&random)
    }

    private func declines(_ score: Int, _ random: inout SeededGenerator) -> Bool {
        score < Self.declineCeiling && score + Self.rollOffset < roll(&random)
    }

    private func roll(_ random: inout SeededGenerator) -> Int { random.next(in: 0..<0x10000) - 0x8000 }

    // Land value, less pollution, times 32, between -3000 and 3000.
    private func residentialWorth(_ x: Int, _ y: Int) -> Int {
        let value = maps.landValue(x, y) - maps.pollution(x, y)
        return min(max(value, 0) * 32, Self.mostResidentialWorth) - Self.mostResidentialWorth / 2
    }

    // The land-value class a building takes, from land value less pollution: 30, 80, and 150.
    private func valueClass(_ x: Int, _ y: Int) -> Int {
        switch maps.landValue(x, y) - maps.pollution(x, y) {
        case ..<30: 0
        case ..<80: 1
        case ..<150: 2
        default: 3
        }
    }

    private func growResidential(_ x: Int, _ y: Int, _ center: TileState, _ spec: ZoneSpec, _ valueClass: Int, _ random: inout SeededGenerator) {
        if maps.pollution(x, y) > Self.mostPollutionForHomes { return }
        if center.development > 0 {
            if center.development < spec.levels { setLevel(x, y, center, center.development + 1, valueClass) }
            return
        }
        if countHouses(x, y) < spec.houses {
            buildHouse(x, y, valueClass, &random)
            return
        }
        if maps.density(x, y) > Self.densityForBuilding && spec.levels > 0 && !hazards.anyActiveIn(centerX: x, centerY: y) {
            for lot in Self.lots { setHouse(x + lot.dx, y + lot.dy, 0) }
            setLevel(x, y, center, 1, valueClass)
        }
    }

    private func declineResidential(_ x: Int, _ y: Int, _ center: TileState, _ spec: ZoneSpec, _ valueClass: Int, _ random: inout SeededGenerator) {
        if center.development > 1 {
            setLevel(x, y, center, center.development - 1, valueClass)
            return
        }
        if center.development == 1 {
            // The building makes way for a full set of houses again.
            guard setLevel(x, y, center, 0, 0) else { return }
            for lot in Self.lots.prefix(max(0, spec.houses)) {
                setHouse(x + lot.dx, y + lot.dy, valueClass * 3 + random.next(in: 0..<3) + 1)
            }
            return
        }
        if let lot = Self.houseLossOrder.first(where: { map[x + $0.dx, y + $0.dy].hasHouse }) {
            setHouse(x + lot.dx, y + lot.dy, 0)
        }
    }

    // A house goes on the free outer tile with the most roads beside it; a tie goes to a later
    // tile one time in eight.
    private func buildHouse(_ x: Int, _ y: Int, _ valueClass: Int, _ random: inout SeededGenerator) {
        var best: Int?
        var bestScore = 0
        for (index, offset) in Self.lots.enumerated() {
            let (lotX, lotY) = (x + offset.dx, y + offset.dy)
            let lot = map[lotX, lotY]
            if lot.hasHouse || lot.use != .residential { continue }
            let score = 1 + roadsBeside(lotX, lotY)
            if score > bestScore {
                bestScore = score
                best = index
            } else if score == bestScore && random.next(in: 0..<8) == 0 {
                best = index
            }
        }
        if let best {
            setHouse(x + Self.lots[best].dx, y + Self.lots[best].dy, valueClass * 3 + random.next(in: 0..<3) + 1)
        }
    }

    private func countHouses(_ x: Int, _ y: Int) -> Int {
        Self.lots.filter { map[x + $0.dx, y + $0.dy].hasHouse }.count
    }

    private func setHouse(_ x: Int, _ y: Int, _ house: Int) {
        let lot = map[x, y]
        map.setState(x, y, TileState(lot.kind, lot.use, zonePart: lot.zonePart, house: house))
    }

    // A zone is redrawn at its new level, holes and all, unless any of it is burning or flooded;
    // says whether it was.
    @discardableResult
    private func setLevel(_ x: Int, _ y: Int, _ center: TileState, _ level: Int, _ valueClass: Int) -> Bool {
        if hazards.anyActiveIn(centerX: x, centerY: y) { return false }
        hazards.repairHoles(centerX: x, centerY: y)
        map.setState(x, y, TileState(center.kind, center.use, development: level, valueClass: level > 0 ? valueClass : 0, powered: center.powered))
        return true
    }

    private func roadsBeside(_ x: Int, _ y: Int) -> Int {
        Sides.steps.filter { map.state(atOrDefault: x + $0.dx, y + $0.dy).isRoad }.count
    }
}
