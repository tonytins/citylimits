/// The city save format, 2.0: everything a city needs to carry on exactly where it was, so a city
/// saved at any step, paused or not, loads and runs on step for step as if it had never stopped.
/// The flat format writes it as JSON; the binary format writes the same sections as chunks.
public enum CitySaveFormat {
    public static let version = FormatVersion(2, 0)
    public static let maxBytes = 16 * 1024 * 1024
    static let int32Max = Int(Int32.max)

    /// The features this version writes, by name.
    static var featureNames: [String] {
        var names: [String] = []
        for feature in SaveShapes.features where feature.name != "challenge" && !names.contains(feature.feature) {
            names.append(feature.feature)
        }
        return names
    }
}

/// A city read from a save, with what the player should be told about it.
public struct LoadedCity {
    public let city: CitySimulation
    /// Things the save said that the player should hear, such as parts this version left out.
    public let notes: [String]
}

extension CitySimulation {
    // MARK: Capture

    /// The city as a save: the whole model, as the flat format writes it.
    func capture() -> JSON {
        var fields: [(key: String, value: JSON)] = [
            ("$schema", .string(SchemaLinks.url(for: "city-save", version: CitySaveFormat.version))),
            ("format", .string(CitySaveFormat.version.description)),
            ("features", .array(CitySaveFormat.featureNames.map(JSON.string))),
            ("name", .string(name)), ("seed", .int(Int(seed))), ("random", .string(Self.hex(random.state))),
            ("tick", .int(tickCount)), ("cityTime", .int(cityTime)), ("funds", .int(treasury.funds)),
            ("mods", .array(definitions.mods.map { mod in
                .object([("id", .string(mod.id)), ("name", .string(mod.name)), ("version", mod.version.map(JSON.string) ?? .null)])
            })),
            ("map", MapRows.encode(map, hazards)),
        ]
        fields += [
            ("budget", captureBudget()), ("census", captureCensus()), ("demand", captureDemand()), ("power", capturePower()),
            ("services", captureServices()), ("community", captureCommunity()), ("cityMaps", captureCityMaps()),
            ("cycle", .object([
                ("roadTiles", .int(roadTiles)), ("budgetedRoadTiles", .int(budgetedRoadTiles)),
                ("budgetedFireStations", .int(budgetedFireStations)), ("budgetedPoliceStations", .int(budgetedPoliceStations)),
            ])),
            ("traffic", .object([("density", ints(traffic.blocks))])),
            ("roadTraffic", .object([
                ("levels", MapRows.layer(map) { x, y in Character(String(self.traffic.levels[y * self.map.width + x].rawValue)) }),
                ("average", .int(traffic.average)),
            ])),
            ("rail", .object([
                ("track", MapRows.layer(map) { x, y in
                    switch self.map[x, y].use {
                    case .rail: "r"
                    case .railAcrossRoad, .railAcrossPowerLine: "a"
                    case .railDownRoad, .railDownPowerLine: "d"
                    default: "."
                    }
                }),
                ("tiles", .int(railTiles)), ("budgetedTiles", .int(budgetedRailTiles)),
            ])),
            ("pollution", .object([("blocks", ints(maps.pollution)), ("average", .int(maps.pollutionAverage))])),
            ("crime", .object([("blocks", ints(maps.crime)), ("average", .int(maps.crimeAverage))])),
            ("fire", .object([("tiles", MapRows.layer(map) { x, y in
                // A hole in a stadium, seaport, or airport is written as what it is, since the map
                // holds bare ground beneath them; the facilities part holds the hole.
                let hole = self.hazards.isHole(x, y) && !MapRows.isHoleInFacility(self.map, self.hazards, x, y)
                return self.hazards.isBurning(x, y) ? (hole ? "b" : "f") : (hole ? "x" : ".")
            })])),
            ("flood", .object([
                ("tiles", MapRows.layer(map) { x, y in self.hazards.isFlooded(x, y) ? "w" : "." }),
                ("cyclesLeft", .int(hazards.floodCyclesLeft)),
            ])),
            ("facilities", captureFacilities()),
            ("founding", .object([("year", .int(foundedYear))])),
            ("ordinances", .object([
                ("enacted", .array(ordinances.enacted.map(JSON.string))),
                ("incomeFund", .int(budget.ordinanceIncomeFund)), ("costFund", .int(budget.ordinanceCostFund)),
                ("lastYear", budget.lastYear.map { ints([$0.ordinanceIncome, $0.ordinanceCost]) } ?? .null),
                ("estimated", budget.estimated.map { ints([$0.ordinanceIncome, $0.ordinanceCost]) } ?? .null),
            ])),
            ("connections", captureConnections()),
            ("radiation", .object([("tiles", MapRows.layer(map) { x, y in self.hazards.isRadioactive(x, y) ? "r" : "." })])),
            ("vehicles", .object([
                ("plane", vehicles.plane.map { ints([$0.x, $0.y, $0.frame, $0.destinationX, $0.destinationY]) } ?? .null),
                ("helicopter", vehicles.helicopter.map {
                    ints([$0.x, $0.y, $0.frame, $0.destinationX, $0.destinationY, $0.homeX, $0.homeY, $0.movesLeft])
                } ?? .null),
                ("ship", vehicles.ship.map { ints([$0.x, $0.y, $0.frame, $0.newDirection, $0.wayBack, $0.count]) } ?? .null),
            ])),
            ("menaces", .object([
                ("tornado", menaces.tornado.map { ints([$0.x, $0.y, $0.frame, $0.movesLeft, $0.rising ? 1 : 0]) } ?? .null),
                ("monster", menaces.monster.map {
                    ints([$0.x, $0.y, $0.frame, $0.destinationX, $0.destinationY, $0.homeX, $0.homeY, $0.step, $0.headingHome ? 1 : 0])
                } ?? .null),
            ])),
        ]
        return .object(fields)
    }

    private func ints(_ values: [Int]) -> JSON { .array(values.map(JSON.int)) }

    private static func hex(_ value: UInt64) -> String {
        let digits = String(value, radix: 16)
        return String(repeating: "0", count: 16 - digits.count) + digits
    }

    private func captureBudget() -> JSON {
        func year(_ year: BudgetYear?) -> JSON {
            guard let year else { return .null }
            return .object([
                ("taxRate", .int(year.taxRate)), ("taxIncome", .int(year.taxIncome)), ("roadRequest", .int(year.roadRequest)),
                ("roadFunded", .int(year.roadFunded)), ("fireRequest", .int(year.fireRequest)), ("fireFunded", .int(year.fireFunded)),
                ("policeRequest", .int(year.policeRequest)), ("policeFunded", .int(year.policeFunded)), ("cut", .bool(year.cut)),
            ])
        }
        return .object([
            ("taxRate", .int(budget.taxRate)), ("roadFunding", .int(budget.roadFunding)), ("fireFunding", .int(budget.fireFunding)),
            ("policeFunding", .int(budget.policeFunding)), ("taxEffect", .int(budget.taxEffect)), ("roadEffect", .int(budget.roadEffect)),
            ("fireEffect", .int(budget.fireEffect)), ("policeEffect", .int(budget.policeEffect)), ("taxFund", .int(budget.taxFund)),
            ("roadFund", .int(budget.roadFund)), ("fireFund", .int(budget.fireFund)), ("policeFund", .int(budget.policeFund)),
            ("cut", .bool(budget.cutThisYear)), ("lastYear", year(budget.lastYear)), ("estimated", year(budget.estimated)),
        ])
    }

    private func captureCensus() -> JSON {
        .object([
            ("residential", .int(census.residential)), ("commercial", .int(census.commercial)), ("industrial", .int(census.industrial)),
            ("residentPopulation", .int(census.residentPopulation)), ("commercialPopulation", .int(census.commercialPopulation)),
            ("industrialPopulation", .int(census.industrialPopulation)), ("residentialHistory", ints(census.residentialHistory)),
            ("commercialHistory", ints(census.commercialHistory)), ("industrialHistory", ints(census.industrialHistory)),
        ])
    }

    private func captureDemand() -> JSON {
        .object([
            ("residential", .int(demand.residential)), ("commercial", .int(demand.commercial)), ("industrial", .int(demand.industrial)),
            ("taxablePopulation", .int(demand.taxablePopulation)), ("residentialPull", .int(demand.residentialPull)),
            ("commercialPull", .int(demand.commercialPull)), ("industrialPull", .int(demand.industrialPull)),
            ("taxPushApplied", .int(demand.taxPushApplied)), ("jobsPerResident", .number(String(demand.jobsPerResident))),
            ("workersPerJob", .number(String(demand.workersPerJob))), ("historyCount", .int(demand.historyCount)),
            ("residentialHistory", ints(demand.residentialHistory)), ("commercialHistory", ints(demand.commercialHistory)),
            ("industrialHistory", ints(demand.industrialHistory)),
        ])
    }

    private func capturePower() -> JSON {
        let rows: JSON = power.width == 0 ? .array([]) : .array((0..<(power.poweredTiles.count / power.width)).map { y in
            .string(String((0..<power.width).map { x in power.poweredTiles[y * power.width + x] ? "1" : "0" }))
        })
        return .object([
            ("plants", ints(power.plants.flatMap { [$0.x, $0.y] })), ("powered", rows), ("coalPlants", .int(power.coalPlants)),
            ("nuclearPlants", .int(power.nuclearPlants)), ("zones", .int(power.zones)), ("poweredBuildings", .int(power.poweredBuildings)),
            ("unpoweredBuildings", .int(power.unpoweredBuildings)), ("capacity", .int(power.capacity)),
            ("poweredTiles", .int(power.poweredTileCount)), ("brownedOut", .bool(power.isBrownedOut)),
        ])
    }

    private func captureServices() -> JSON {
        .object([
            ("fireStations", .int(services.fireStations)), ("policeStations", .int(services.policeStations)),
            ("fireRaw", ints(services.fireRaw)), ("policeRaw", ints(services.policeRaw)), ("fire", ints(services.fire)),
            ("police", ints(services.police)),
        ])
    }

    private func captureCommunity() -> JSON {
        .object([
            ("hospitals", .int(community.hospitals)), ("churches", .int(community.churches)),
            ("hospitalNeed", .int(community.hospitalNeed)), ("churchNeed", .int(community.churchNeed)),
        ])
    }

    private func captureCityMaps() -> JSON {
        .object([
            ("centerX", .int(maps.centerX)), ("centerY", .int(maps.centerY)), ("landValueAverage", .int(maps.landValueAverage)),
            ("landValue", ints(maps.landValues)), ("density", ints(maps.density)), ("terrain", ints(maps.terrain)),
            ("commercialRate", ints(maps.commercialRates)),
        ])
    }

    private func captureFacilities() -> JSON {
        var buildings: [JSON] = []
        for y in 0..<map.height {
            for x in 0..<map.width where map[x, y].isBuildingCenter && map[x, y].use.isFacility {
                let state = map[x, y]
                let size = state.use.footprint
                let holes = (0..<Footprint.parts(size)).filter { part in
                    let offset = Footprint.offsetFromCenter(part: part, size: size)
                    guard let owner = hazards.holeOwner(x + offset.dx, y + offset.dy) else { return false }
                    return owner.x == x && owner.y == y
                }
                buildings.append(.object([
                    ("use", .string(Self.facilityName(state.use))), ("x", .int(x)), ("y", .int(y)),
                    ("powered", .bool(state.powered)), ("holes", ints(holes)),
                ]))
            }
        }
        return .object([
            ("stadiums", .int(facilities.stadiums)), ("seaports", .int(facilities.seaports)), ("airports", .int(facilities.airports)),
            ("residentialCapped", .bool(facilities.caps.contains(.residential))),
            ("commercialCapped", .bool(facilities.caps.contains(.commercial))),
            ("industrialCapped", .bool(facilities.caps.contains(.industrial))), ("buildings", .array(buildings)),
        ])
    }

    private static func facilityName(_ use: TileUse) -> String {
        switch use {
        case .stadium: "Stadium"
        case .seaport: "Seaport"
        default: "Airport"
        }
    }

    private func captureConnections() -> JSON {
        func kind(_ kind: DealKind) -> JSON { .string(kind == .cityBuys ? "cityBuys" : "citySells") }
        return .object([
            ("deals", .array(neighbours.deals.map { deal in
                .object([
                    ("side", .string(deal.side.name)), ("kind", kind(deal.kind)), ("power", .int(deal.power)),
                    ("money", .int(deal.money)), ("endStep", .int(deal.endStep)),
                ])
            })),
            ("offer", neighbours.offer.map { offer in
                .object([
                    ("side", .string(offer.side.name)), ("kind", kind(offer.kind)), ("power", .int(offer.power)),
                    ("money", .int(offer.money)), ("years", .int(offer.years)), ("expiresStep", .int(offer.expiresStep)),
                ])
            } ?? .null),
            ("grudgeUntil", ints(neighbours.grudgeUntil)), ("incomeFund", .int(budget.dealIncomeFund)),
            ("costFund", .int(budget.dealCostFund)),
            ("lastYear", budget.lastYear.map { ints([$0.dealIncome, $0.dealCost]) } ?? .null),
            ("estimated", budget.estimated.map { ints([$0.dealIncome, $0.dealCost]) } ?? .null),
        ])
    }

    // MARK: Restore

    /// Reads a save, refusing it whole on the first problem, saying what and where. A city is
    /// never half loaded. `definitions` are what it runs on: the base game and the mods installed now.
    static func restore(_ save: JSON, definitions: CityDefinitions) throws(FormatError) -> LoadedCity {
        var fields = try JSONFields(save, at: "")
        var notes: [String] = []

        guard let version = FormatVersion(try fields.required("format")) else { throw FormatError("format: needs a version such as \"2.0\".") }
        guard version.major == CitySaveFormat.version.major else {
            throw FormatError("format: this is format \(version); this version of CityLimits reads \(CitySaveFormat.version.major).x. A city from 0.4 or before can be opened in 0.5 and saved again there.")
        }
        _ = try fields.strings("features")

        // A section this version doesn't know is a newer version's feature, left out here and
        // when the city is saved; anything else unknown is refused.
        let known = Set(["$schema", "format", "features", "name", "seed", "random", "tick", "cityTime", "funds", "mods", "map"]
            + SaveShapes.core.map(\.name) + SaveShapes.features.map(\.name))
        var leftOut: [String] = []
        for key in fields.keys where !known.contains(key) {
            guard case .object = try fields.required(key) else {
                throw FormatError("\(key): is not a field a save has, nor a part a newer version added.")
            }
            leftOut.append(key)
        }
        if !leftOut.isEmpty {
            notes.append("A newer CityLimits saved this city with parts this version doesn't use.")
            notes += leftOut.map { "Its \($0) part is left out, and is left out when you save it." }
        }

        let name = try fields.requiredString("name")
        guard isValidName(name) else { throw FormatError("name: needs 1 to \(maxNameLength) characters and no control characters.") }
        let seed = Int32(try fields.requiredInt("seed", in: Int(Int32.min)...Int(Int32.max)))
        let randomText = try fields.requiredString("random")
        guard randomText.utf8.count == 16, let randomState = UInt64(randomText, radix: 16) else {
            throw FormatError("random: needs 16 hexadecimal digits.")
        }
        let tick = try fields.requiredInt("tick", in: 0...Int.max)
        let cityTime = try fields.requiredInt("cityTime")
        guard cityTime == tick / CityCalendar.stepsPerCount else {
            throw FormatError("cityTime: a city \(tick) steps old is at count \(tick / CityCalendar.stepsPerCount), not \(cityTime).")
        }
        let funds = try fields.requiredInt("funds", in: Int(Int32.min)...CitySaveFormat.int32Max)

        // The mods it was played with, which may differ from the ones it opens with.
        var playedWith: [AppliedMod] = []
        for (index, item) in (try fields.array("mods") ?? []).enumerated() {
            var mod = try JSONFields(item, at: "mods[\(index)]")
            let id = try mod.string("id")?.trimmingWhitespace ?? "", modName = try mod.string("name")?.trimmingWhitespace ?? ""
            guard !id.isEmpty, !modName.isEmpty else { throw FormatError("mods: each needs an id and a name.") }
            playedWith.append(AppliedMod(id: id, name: modName, version: try mod.string("version")))
            try mod.finish(noun: "a mod has")
        }
        let differs = playedWith.map(\.id) != definitions.mods.map(\.id)
        if differs {
            notes.append("It was played with \(describe(playedWith)), and opens with \(describe(definitions.mods)), so it may play differently.")
        }

        let map = try MapRows.decode(try fields.required("map"))
        var section = try Sections(&fields, width: map.width, height: map.height)
        try section.rail.applyRail(to: map)
        do throws(FormatError) {
            try checkZones(map, definitions)
        } catch where differs {
            throw FormatError("\(error.message) It was played with \(describe(playedWith)); install them to open it.")
        }

        let city = CitySimulation(map: map, seed: seed, funds: 0, definitions: definitions)
        try section.facilities.applyFacilities(to: city)
        city.name = name.trimmingWhitespace
        city.random = SeededGenerator(state: randomState)
        city.tickCount = tick
        city.cityTime = cityTime
        city.treasury.restore(funds)
        try section.budget.restoreBudget(city.budget)
        try section.census.restoreCensus(city.census)
        try section.demand.restoreDemand(city.demand)
        try section.power.restorePower(city.power, map)
        try section.services.restoreServices(city.services)
        try section.community.restoreCommunity(city.community)
        try section.cityMaps.restoreCityMaps(city.maps, map)
        try section.pollution.restoreBlocks(&city.maps.pollution, average: &city.maps.pollutionAverage, most: CityMaps.mostPollution, "pollution")
        try section.crime.restoreBlocks(&city.maps.crime, average: &city.maps.crimeAverage, most: CityMaps.mostCrime, "crime")
        try section.fire.restoreFire(city)
        try section.flood.restoreFlood(city)
        try section.radiation.restoreRadiation(city)
        try section.vehicles.restoreVehicles(city.vehicles)
        try section.menaces.restoreMenaces(city.menaces)
        city.foundedYear = try section.founding.requiredInt("year", in: CityCalendar.startYear...max(CityCalendar.startYear, city.date.year))
        try section.ordinances.restoreOrdinances(city)
        city.applyOrdinances()
        try section.connections.restoreConnections(city)
        try section.cycle.restoreCycle(city)
        try section.traffic.restoreTraffic(city.traffic)
        try section.roadTraffic.restoreRoadTraffic(city.traffic, map)
        city.railTiles = try section.rail.requiredInt("tiles", in: 0...CitySaveFormat.int32Max)
        city.budgetedRailTiles = try section.rail.requiredInt("budgetedTiles", in: 0...CitySaveFormat.int32Max)
        try section.finish()
        if section.challenge != nil {
            notes.append("This city was being played as a challenge; this version opens it as a free city.")
        }
        try fields.finish(noun: "a save has")
        return LoadedCity(city: city, notes: notes)
    }

    private static func describe(_ mods: [AppliedMod]) -> String {
        mods.isEmpty ? "no mods" : mods.map { mod in mod.version.map { "\(mod.name) \($0)" } ?? mod.name }.joined(separator: ", ")
    }

    // A zone's building can't be a level or a land-value class its spec doesn't have.
    private static func checkZones(_ map: TileMap, _ definitions: CityDefinitions) throws(FormatError) {
        for y in 0..<map.height {
            for x in 0..<map.width where map[x, y].isZoneCenter {
                let state = map[x, y]
                let spec = definitions.zone(state.use)
                if state.development > spec.levels || state.valueClass >= max(spec.valueClasses, 1) {
                    throw FormatError("map.buildings: the \(spec.name) at \(x), \(y) is level \(state.development), class \(state.valueClass); its spec has \(spec.levels) levels and \(spec.valueClasses) classes.")
                }
            }
        }
    }
}

/// The sections of a save being read, each filled in as the version that added it would fill it
/// when the save leaves it out.
private struct Sections {
    var budget, census, demand, power, services, community, cityMaps, cycle: JSONFields
    var traffic, roadTraffic, rail, pollution, crime, fire, flood, facilities, founding: JSONFields
    var ordinances, connections, radiation, vehicles, menaces: JSONFields
    var challenge: JSONFields?

    init(_ save: inout JSONFields, width: Int, height: Int) throws(FormatError) {
        func core(_ name: String) throws(FormatError) -> JSONFields {
            guard let section = try save.object(name) else { throw FormatError("\(name): missing.") }
            return section
        }
        func feature(_ name: String, _ filled: JSON) throws(FormatError) -> JSONFields {
            if let section = try save.object(name) { return section }
            return try JSONFields(filled, at: name)
        }
        let blank = JSON.array(Array(repeating: .string(String(repeating: ".", count: width)), count: height))
        let blocks = JSON.array(Array(repeating: .int(0), count: ((width + 1) / 2) * ((height + 1) / 2)))
        budget = try core("budget")
        census = try core("census")
        demand = try core("demand")
        power = try core("power")
        services = try core("services")
        community = try core("community")
        cityMaps = try core("cityMaps")
        cycle = try core("cycle")
        traffic = try feature("traffic", .object([("density", blocks)]))
        roadTraffic = try feature("roadTraffic", .object([
            ("levels", .array(Array(repeating: .string(String(repeating: "0", count: width)), count: height))), ("average", .int(0)),
        ]))
        rail = try feature("rail", .object([("track", blank), ("tiles", .int(0)), ("budgetedTiles", .int(0))]))
        pollution = try feature("pollution", .object([("blocks", blocks), ("average", .int(0))]))
        crime = try feature("crime", .object([("blocks", blocks), ("average", .int(0))]))
        fire = try feature("fire", .object([("tiles", blank)]))
        flood = try feature("flood", .object([("tiles", blank), ("cyclesLeft", .int(0))]))
        facilities = try feature("facilities", .object([
            ("stadiums", .int(0)), ("seaports", .int(0)), ("airports", .int(0)), ("residentialCapped", .bool(false)),
            ("commercialCapped", .bool(false)), ("industrialCapped", .bool(false)), ("buildings", .array([])),
        ]))
        founding = try feature("founding", .object([("year", .int(CityCalendar.startYear))]))
        ordinances = try feature("ordinances", .object([("enacted", .array([])), ("incomeFund", .int(0)), ("costFund", .int(0))]))
        connections = try feature("connections", .object([
            ("deals", .array([])), ("grudgeUntil", .array([.int(0), .int(0), .int(0), .int(0)])),
        ]))
        radiation = try feature("radiation", .object([("tiles", blank)]))
        vehicles = try feature("vehicles", .object([]))
        menaces = try feature("menaces", .object([]))
        challenge = try save.object("challenge")
    }

    func finish() throws(FormatError) {
        for section in [budget, census, demand, power, services, community, cityMaps, cycle, traffic, roadTraffic, rail,
                        pollution, crime, fire, flood, facilities, founding, ordinances, connections, radiation, vehicles, menaces]
        {
            try section.finish(noun: "the save's \(section.path) has")
        }
    }
}

private extension JSONFields {
    /// A list of whole numbers that must be exactly as long as the part it fills.
    mutating func exactInts(_ key: String, count: Int, in range: ClosedRange<Int> = Int.min...Int.max) throws(FormatError) -> [Int] {
        let values = try ints(key, in: range) ?? []
        guard values.count == count else { throw FormatError("\(at(key)): needs \(count) numbers, not \(values.count).") }
        return values
    }

    mutating func applyRail(to map: TileMap) throws(FormatError) {
        try MapRows.readLayer(strings("track"), at("track"), map) { x, y, character throws(FormatError) in
            let beneath = map[x, y]
            let rail: TileUse? = switch (character, beneath.use, beneath.kind) {
            case ("r", .none, .grass), ("r", .none, .water): .rail
            case ("a", .road, .grass): .railAcrossRoad
            case ("d", .road, .grass): .railDownRoad
            case ("a", .powerLine, .grass): .railAcrossPowerLine
            case ("d", .powerLine, .grass): .railDownPowerLine
            default: nil
            }
            guard let rail else { throw FormatError("rail.track[\(y)]: '\(character)' at \(x) can't go on \(beneath.kind) \(beneath.use).") }
            map.setState(x, y, TileState(beneath.kind, rail))
        }
    }

    mutating func applyFacilities(to city: CitySimulation) throws(FormatError) {
        let map = city.map
        var taken = Set<Int>()
        for (index, item) in (try requiredArray("buildings")).enumerated() {
            let at = "facilities.buildings[\(index)]"
            var building = try JSONFields(item, at: at)
            let name = try building.requiredString("use")
            let use: TileUse
            switch name {
            case "Stadium": use = .stadium
            case "Seaport": use = .seaport
            case "Airport": use = .airport
            default: throw FormatError("\(at).use: '\(name)' isn't a stadium, a seaport, or an airport (Stadium, Seaport, or Airport).")
            }
            let x = try building.requiredInt("x"), y = try building.requiredInt("y")
            let powered = try building.bool("powered") ?? false
            let size = use.footprint, centre = Footprint.centerPart(size)
            var holes = Set<Int>()
            for hole in try building.ints("holes") ?? [] {
                guard (0..<Footprint.parts(size)).contains(hole), hole != centre, holes.insert(hole).inserted else {
                    throw FormatError("\(at).holes: \(hole) isn't an outer part of a \(size) by \(size) \(use), or is listed twice.")
                }
            }
            try building.finish(noun: "a facility has")
            for part in 0..<Footprint.parts(size) {
                let offset = Footprint.offsetFromCenter(part: part, size: size)
                let (tileX, tileY) = (x + offset.dx, y + offset.dy)
                guard map.contains(tileX, tileY), taken.insert(tileY * map.width + tileX).inserted else {
                    throw FormatError("\(at): the \(use) at \(x), \(y) runs off the map or onto another one at \(tileX), \(tileY).")
                }
                let beneath = map[tileX, tileY]
                guard beneath.use == .none, beneath.kind == .grass || (holes.contains(part) && beneath.kind == .rubble) else {
                    throw FormatError("\(at): the \(use) at \(x), \(y) can't stand on \(beneath.kind) with \(beneath.use) at \(tileX), \(tileY).")
                }
                if !holes.contains(part) {
                    map.setState(tileX, tileY, TileState(.grass, use, zonePart: part, powered: part == centre && powered))
                }
            }
        }
        let facilities = city.facilities
        facilities.stadiums = try requiredInt("stadiums", in: 0...CitySaveFormat.int32Max)
        facilities.seaports = try requiredInt("seaports", in: 0...CitySaveFormat.int32Max)
        facilities.airports = try requiredInt("airports", in: 0...CitySaveFormat.int32Max)
        var caps: DemandCaps = []
        if try bool("residentialCapped") == true { caps.insert(.residential) }
        if try bool("commercialCapped") == true { caps.insert(.commercial) }
        if try bool("industrialCapped") == true { caps.insert(.industrial) }
        facilities.caps = caps
    }

    mutating func restoreBudget(_ budget: CityBudget) throws(FormatError) {
        func year(_ key: String) throws(FormatError) -> BudgetYear? {
            guard var fields = try object(key) else { return nil }
            let result = BudgetYear(
                taxRate: try fields.requiredInt("taxRate", in: 0...CityBudget.maxTaxRate), taxIncome: try fields.requiredInt("taxIncome"),
                roadRequest: try fields.requiredInt("roadRequest"), roadFunded: try fields.requiredInt("roadFunded"),
                fireRequest: try fields.requiredInt("fireRequest"), fireFunded: try fields.requiredInt("fireFunded"),
                policeRequest: try fields.requiredInt("policeRequest"), policeFunded: try fields.requiredInt("policeFunded"),
                cut: try fields.bool("cut") ?? false)
            try fields.finish(noun: "a budget year has")
            return result
        }
        budget.taxRate = try requiredInt("taxRate", in: 0...CityBudget.maxTaxRate)
        budget.roadFunding = try requiredInt("roadFunding", in: 0...CityBudget.fullFunding)
        budget.fireFunding = try requiredInt("fireFunding", in: 0...CityBudget.fullFunding)
        budget.policeFunding = try requiredInt("policeFunding", in: 0...CityBudget.fullFunding)
        budget.taxEffect = try requiredInt("taxEffect", in: 0...CityBudget.maxTaxRate)
        budget.roadEffect = try requiredInt("roadEffect", in: 0...CityBudget.fullRoadEffect)
        budget.fireEffect = try requiredInt("fireEffect", in: 0...CityBudget.fullServiceEffect)
        budget.policeEffect = try requiredInt("policeEffect", in: 0...CityBudget.fullServiceEffect)
        budget.taxFund = try requiredInt("taxFund", in: 0...Int.max)
        budget.roadFund = try requiredInt("roadFund", in: 0...Int.max)
        budget.fireFund = try requiredInt("fireFund", in: 0...Int.max)
        budget.policeFund = try requiredInt("policeFund", in: 0...Int.max)
        budget.cutThisYear = try bool("cut") ?? false
        budget.lastYear = try year("lastYear")
        budget.estimated = try year("estimated")
    }

    mutating func restoreCensus(_ census: CityCensus) throws(FormatError) {
        let positive = 0...CitySaveFormat.int32Max
        census.residential = try requiredInt("residential", in: positive)
        census.commercial = try requiredInt("commercial", in: positive)
        census.industrial = try requiredInt("industrial", in: positive)
        census.residentPopulation = try requiredInt("residentPopulation", in: positive)
        census.commercialPopulation = try requiredInt("commercialPopulation", in: positive)
        census.industrialPopulation = try requiredInt("industrialPopulation", in: positive)
        census.cityPopulation = min(CitySaveFormat.int32Max, census.residentPopulation + census.commercialPopulation + census.industrialPopulation)
        census.residentialHistory = try exactInts("residentialHistory", count: CityCensus.historyMonths)
        census.commercialHistory = try exactInts("commercialHistory", count: CityCensus.historyMonths)
        census.industrialHistory = try exactInts("industrialHistory", count: CityCensus.historyMonths)
    }

    mutating func restoreDemand(_ demand: CityDemand) throws(FormatError) {
        demand.residential = try requiredInt("residential", in: -CityDemand.residentialLimit...CityDemand.residentialLimit)
        demand.commercial = try requiredInt("commercial", in: -CityDemand.businessLimit...CityDemand.businessLimit)
        demand.industrial = try requiredInt("industrial", in: -CityDemand.businessLimit...CityDemand.businessLimit)
        demand.taxablePopulation = try requiredInt("taxablePopulation", in: 0...CitySaveFormat.int32Max)
        demand.residentialPull = try requiredInt("residentialPull")
        demand.commercialPull = try requiredInt("commercialPull")
        demand.industrialPull = try requiredInt("industrialPull")
        demand.taxPushApplied = try requiredInt("taxPushApplied")
        guard let jobs = try double("jobsPerResident") else { throw FormatError("demand.jobsPerResident: needs a number.") }
        guard let workers = try double("workersPerJob") else { throw FormatError("demand.workersPerJob: needs a number.") }
        demand.jobsPerResident = jobs
        demand.workersPerJob = workers
        demand.historyCount = try requiredInt("historyCount", in: 0...CityDemand.historyLength)
        demand.residentialHistory = try exactInts("residentialHistory", count: CityDemand.historyLength)
        demand.commercialHistory = try exactInts("commercialHistory", count: CityDemand.historyLength)
        demand.industrialHistory = try exactInts("industrialHistory", count: CityDemand.historyLength)
    }

    mutating func restorePower(_ power: PowerGrid, _ map: TileMap) throws(FormatError) {
        let plants = try ints("plants") ?? []
        guard plants.count % 2 == 0 else { throw FormatError("power.plants: needs x and y pairs.") }
        power.plants = []
        for index in stride(from: 0, to: plants.count, by: 2) {
            guard map.contains(plants[index], plants[index + 1]) else {
                throw FormatError("power.plants: \(plants[index]), \(plants[index + 1]) is off the map.")
            }
            power.plants.append((plants[index], plants[index + 1]))
        }
        // A map that hasn't been traced yet has no rows.
        let rows = try strings("powered") ?? []
        if rows.isEmpty {
            (power.poweredTiles, power.width) = ([], 0)
        } else {
            guard rows.count == map.height else { throw FormatError("power.powered: needs \(map.height) rows, not \(rows.count).") }
            power.width = map.width
            power.poweredTiles = []
            power.poweredTiles.reserveCapacity(map.width * map.height)
            for (y, row) in rows.enumerated() {
                let characters = Array(row)
                guard characters.count == map.width, characters.allSatisfy({ $0 == "0" || $0 == "1" }) else {
                    throw FormatError("power.powered[\(y)]: needs \(map.width) characters, each 0 or 1.")
                }
                power.poweredTiles += characters.map { $0 == "1" }
            }
        }
        let positive = 0...CitySaveFormat.int32Max
        power.coalPlants = try requiredInt("coalPlants", in: positive)
        power.nuclearPlants = try requiredInt("nuclearPlants", in: positive)
        power.zones = try requiredInt("zones", in: positive)
        power.poweredBuildings = try requiredInt("poweredBuildings", in: positive)
        power.unpoweredBuildings = try requiredInt("unpoweredBuildings", in: positive)
        power.capacity = try requiredInt("capacity", in: positive)
        power.poweredTileCount = try requiredInt("poweredTiles", in: positive)
        power.isBrownedOut = try bool("brownedOut") ?? false
    }

    mutating func restoreServices(_ services: ServiceCoverage) throws(FormatError) {
        let blocks = services.width * services.height
        services.fireStations = try requiredInt("fireStations", in: 0...CitySaveFormat.int32Max)
        services.policeStations = try requiredInt("policeStations", in: 0...CitySaveFormat.int32Max)
        services.fireRaw = try exactInts("fireRaw", count: blocks)
        services.policeRaw = try exactInts("policeRaw", count: blocks)
        services.fire = try exactInts("fire", count: blocks)
        services.police = try exactInts("police", count: blocks)
    }

    mutating func restoreCommunity(_ community: CommunityBuildings) throws(FormatError) {
        community.hospitals = try requiredInt("hospitals", in: 0...CitySaveFormat.int32Max)
        community.churches = try requiredInt("churches", in: 0...CitySaveFormat.int32Max)
        community.hospitalNeed = try requiredInt("hospitalNeed", in: -1...1)
        community.churchNeed = try requiredInt("churchNeed", in: -1...1)
    }

    mutating func restoreCityMaps(_ maps: CityMaps, _ map: TileMap) throws(FormatError) {
        maps.centerX = try requiredInt("centerX", in: 0...map.width)
        maps.centerY = try requiredInt("centerY", in: 0...map.height)
        maps.landValueAverage = try requiredInt("landValueAverage", in: 0...CityMaps.maxLandValue)
        maps.landValues = try exactInts("landValue", count: maps.halfWidth * maps.halfHeight)
        maps.density = try exactInts("density", count: maps.halfWidth * maps.halfHeight)
        maps.terrain = try exactInts("terrain", count: maps.quarterWidth * maps.quarterHeight)
        maps.commercialRates = try exactInts("commercialRate", count: maps.commercialRates.count)
    }

    mutating func restoreBlocks(_ blocks: inout [Int], average: inout Int, most: Int, _ name: String) throws(FormatError) {
        average = try requiredInt("average", in: 0...most)
        blocks = try exactInts("blocks", count: blocks.count, in: 0...most)
    }

    mutating func restoreFire(_ city: CitySimulation) throws(FormatError) {
        let map = city.map, hazards = city.hazards
        try MapRows.readLayer(strings("tiles"), "fire.tiles", map) { x, y, character throws(FormatError) in
            let state = map[x, y]
            switch character {
            case "f" where state.kind == .rubble && state.use == .none:
                hazards.restoreBurning(x, y)
            case "x" where state.hasFootprint && !state.isBuildingCenter, "b" where state.hasFootprint && !state.isBuildingCenter:
                map.setState(x, y, TileState(.rubble))
                if character == "b" { hazards.restoreBurning(x, y) }
            case "f":
                throw FormatError("fire.tiles[\(y)]: the fire at \(x) is on \(state.kind) with \(state.use), not rubble.")
            case "x", "b":
                throw FormatError("fire.tiles[\(y)]: the hole at \(x) isn't an outer tile of a building.")
            default:
                throw FormatError("fire.tiles[\(y)]: needs '.', 'f', 'x', or 'b' for each tile, not '\(character)' at \(x).")
            }
        }
    }

    // A flood or radiation lies on bare ground, or on a hole in a building.
    private static func isBareOrHole(_ city: CitySimulation, _ x: Int, _ y: Int) -> Bool {
        let state = city.map[x, y]
        return state.use == .none && (state.kind == .grass || (state.kind == .rubble && city.hazards.isHole(x, y)))
    }

    mutating func restoreFlood(_ city: CitySimulation) throws(FormatError) {
        city.hazards.restoreFloodCyclesLeft(try requiredInt("cyclesLeft", in: 0...Hazards.floodDuration))
        try MapRows.readLayer(strings("tiles"), "flood.tiles", city.map) { x, y, character throws(FormatError) in
            guard character == "w" else { throw FormatError("flood.tiles[\(y)]: needs '.' or 'w' for each tile, not '\(character)' at \(x).") }
            guard !city.hazards.isBurning(x, y) else { throw FormatError("flood.tiles[\(y)]: the flood at \(x) is on a fire.") }
            let state = city.map[x, y]
            guard Self.isBareOrHole(city, x, y) else {
                throw FormatError("flood.tiles[\(y)]: the flood at \(x) is on \(state.kind) with \(state.use), not bare ground.")
            }
            city.map.setState(x, y, .grass)
            city.hazards.restoreFlooded(x, y)
        }
    }

    mutating func restoreRadiation(_ city: CitySimulation) throws(FormatError) {
        try MapRows.readLayer(strings("tiles"), "radiation.tiles", city.map) { x, y, character throws(FormatError) in
            guard character == "r" else { throw FormatError("radiation.tiles[\(y)]: needs '.' or 'r' for each tile, not '\(character)' at \(x).") }
            guard !city.hazards.isBurning(x, y), !city.hazards.isFlooded(x, y) else {
                throw FormatError("radiation.tiles[\(y)]: the radiation at \(x) is on a fire or a flood.")
            }
            let state = city.map[x, y]
            guard Self.isBareOrHole(city, x, y) else {
                throw FormatError("radiation.tiles[\(y)]: the radiation at \(x) is on \(state.kind) with \(state.use), not bare ground.")
            }
            city.map.setState(x, y, .grass)
            city.hazards.restoreRadioactive(x, y)
        }
    }

    // A list of numbers for a sprite: empty or missing when it isn't about, else exactly so long.
    private mutating func sprite(_ key: String, count: Int) throws(FormatError) -> [Int]? {
        let values = try ints(key) ?? []
        if values.isEmpty { return nil }
        guard values.count == count else { throw FormatError("\(at(key)): needs \(count) numbers.") }
        return values
    }

    private static func range(_ value: Int, _ range: ClosedRange<Int>, _ at: String) throws(FormatError) -> Int {
        try JSONFields.check(value, in: range, at: at)
    }

    mutating func restoreVehicles(_ vehicles: Vehicles) throws(FormatError) {
        vehicles.plane = try sprite("plane", count: 5).map { n throws(FormatError) in
            Plane(x: n[0], y: n[1], frame: try Self.range(n[2], 1...11, "vehicles.plane frame"), destinationX: n[3], destinationY: n[4])
        }
        vehicles.helicopter = try sprite("helicopter", count: 8).map { n throws(FormatError) in
            Helicopter(
                x: n[0], y: n[1], frame: try Self.range(n[2], 1...8, "vehicles.helicopter frame"), destinationX: n[3], destinationY: n[4],
                homeX: n[5], homeY: n[6], movesLeft: try Self.range(n[7], 0...Vehicles.helicopterFlight, "vehicles.helicopter moves left"))
        }
        vehicles.ship = try sprite("ship", count: 6).map { n throws(FormatError) in
            Ship(
                x: n[0], y: n[1], frame: try Self.range(n[2], 1...8, "vehicles.ship frame"),
                newDirection: try Self.range(n[3], 1...8, "vehicles.ship new direction"),
                wayBack: try Self.range(n[4], 1...10, "vehicles.ship way back"), count: try Self.range(n[5], 0...9, "vehicles.ship count"))
        }
    }

    mutating func restoreMenaces(_ menaces: Menaces) throws(FormatError) {
        menaces.tornado = try sprite("tornado", count: 5).map { n throws(FormatError) in
            Tornado(
                x: n[0], y: n[1], frame: try Self.range(n[2], 1...3, "menaces.tornado frame"),
                movesLeft: try Self.range(n[3], 0...Menaces.tornadoMoves, "menaces.tornado moves left"),
                rising: try Self.range(n[4], 0...1, "menaces.tornado rising") == 1)
        }
        menaces.monster = try sprite("monster", count: 9).map { n throws(FormatError) in
            guard n[7] == -1 || n[7] == 1 else { throw FormatError("menaces.monster step: needs -1 or 1, not \(n[7]).") }
            return Monster(
                x: n[0], y: n[1], frame: try Self.range(n[2], 1...16, "menaces.monster frame"), destinationX: n[3], destinationY: n[4],
                homeX: n[5], homeY: n[6], step: n[7], headingHome: try Self.range(n[8], 0...1, "menaces.monster heading home") == 1)
        }
    }

    // A section's share of last year's and the latest estimate's money, as income and cost.
    private mutating func money(_ key: String) throws(FormatError) -> (income: Int, cost: Int)? {
        guard let values = try ints(key), !values.isEmpty else { return nil }
        guard values.count == 2, values[0] >= 0, values[1] >= 0 else {
            throw FormatError("\(at(key)): needs the income and the cost, each 0 or more.")
        }
        return (values[0], values[1])
    }

    mutating func restoreOrdinances(_ city: CitySimulation) throws(FormatError) {
        let ordinances = city.ordinances
        for index in ordinances.enactedFlags.indices { ordinances.enactedFlags[index] = false }
        for id in try strings("enacted") ?? [] {
            guard let index = ordinances.all.firstIndex(where: { $0.id == id }) else {
                throw FormatError("ordinances.enacted: '\(id)' is not an ordinance.")
            }
            ordinances.enactedFlags[index] = true
        }
        ordinances.sum()
        let budget = city.budget
        budget.ordinanceIncomeFund = try requiredInt("incomeFund", in: 0...Int.max)
        budget.ordinanceCostFund = try requiredInt("costFund", in: 0...Int.max)
        if let last = try money("lastYear"), budget.lastYear != nil {
            budget.lastYear!.ordinanceIncome = last.income
            budget.lastYear!.ordinanceCost = last.cost
        }
        if let estimated = try money("estimated"), budget.estimated != nil {
            budget.estimated!.ordinanceIncome = estimated.income
            budget.estimated!.ordinanceCost = estimated.cost
        }
    }

    mutating func restoreConnections(_ city: CitySimulation) throws(FormatError) {
        func side(_ name: String, _ at: String) throws(FormatError) -> Side {
            guard let side = Side.allCases.first(where: { $0.name == name }) else {
                throw FormatError("\(at): '\(name)' is not north, east, south, or west.")
            }
            return side
        }
        func kind(_ name: String, _ at: String) throws(FormatError) -> DealKind {
            switch name {
            case "cityBuys": return .cityBuys
            case "citySells": return .citySells
            default: throw FormatError("\(at): '\(name)' is not cityBuys or citySells.")
            }
        }
        let neighbours = city.neighbours
        neighbours.deals = []
        for (index, item) in (try array("deals") ?? []).enumerated() {
            let at = "connections.deals[\(index)]"
            guard item != .null else { throw FormatError("\(at): needs a deal, not null.") }
            var deal = try JSONFields(item, at: at)
            let dealSide = try side(try deal.requiredString("side"), "\(at).side")
            guard neighbours.deal(with: dealSide) == nil else { throw FormatError("\(at).side: '\(dealSide.name)' has two deals.") }
            neighbours.deals.append(Deal(
                side: dealSide, kind: try kind(try deal.requiredString("kind"), "\(at).kind"),
                power: try deal.requiredInt("power", in: 1...CitySaveFormat.int32Max),
                money: try deal.requiredInt("money", in: 0...CitySaveFormat.int32Max), endStep: try deal.requiredInt("endStep")))
            try deal.finish(noun: "a deal has")
        }
        if var offer = try object("offer") {
            neighbours.offer = Offer(
                side: try side(try offer.requiredString("side"), "connections.offer.side"),
                kind: try kind(try offer.requiredString("kind"), "connections.offer.kind"),
                power: try offer.requiredInt("power", in: 1...CitySaveFormat.int32Max),
                money: try offer.requiredInt("money", in: 0...CitySaveFormat.int32Max),
                years: try offer.requiredInt("years", in: 1...100), expiresStep: try offer.requiredInt("expiresStep"))
            try offer.finish(noun: "an offer has")
        } else {
            neighbours.offer = nil
        }
        let grudges = try ints("grudgeUntil") ?? [0, 0, 0, 0]
        guard grudges.count == 4 else { throw FormatError("connections.grudgeUntil: needs a step for each of the four sides.") }
        neighbours.grudgeUntil = grudges
        let budget = city.budget
        budget.dealIncomeFund = try int("incomeFund", in: 0...Int.max) ?? 0
        budget.dealCostFund = try int("costFund", in: 0...Int.max) ?? 0
        if let last = try money("lastYear"), budget.lastYear != nil {
            budget.lastYear!.dealIncome = last.income
            budget.lastYear!.dealCost = last.cost
        }
        if let estimated = try money("estimated"), budget.estimated != nil {
            budget.estimated!.dealIncome = estimated.income
            budget.estimated!.dealCost = estimated.cost
        }
    }

    mutating func restoreCycle(_ city: CitySimulation) throws(FormatError) {
        let positive = 0...CitySaveFormat.int32Max
        city.roadTiles = try requiredInt("roadTiles", in: positive)
        city.budgetedRoadTiles = try requiredInt("budgetedRoadTiles", in: positive)
        city.budgetedFireStations = try requiredInt("budgetedFireStations", in: positive)
        city.budgetedPoliceStations = try requiredInt("budgetedPoliceStations", in: positive)
    }

    mutating func restoreTraffic(_ traffic: TrafficMap) throws(FormatError) {
        traffic.blocks = try exactInts("density", count: traffic.blocks.count, in: 0...TrafficMap.most)
    }

    // A level on a tile that isn't a road, as an older game that kept this part while changing
    // the city could leave, is dropped rather than refused: the road it belonged to has gone.
    mutating func restoreRoadTraffic(_ traffic: TrafficMap, _ map: TileMap) throws(FormatError) {
        try MapRows.readLayer(strings("levels"), "roadTraffic.levels", map) { x, y, character throws(FormatError) in
            guard let digit = character.wholeNumberValue, let level = TrafficLevel(rawValue: digit), character.isASCII else {
                throw FormatError("roadTraffic.levels[\(y)]: needs 0, 1, or 2 for each tile, not '\(character)' at \(x).")
            }
            traffic.levels[y * map.width + x] = map[x, y].isRoad ? level : .none
        }
        traffic.average = try requiredInt("average", in: 0...Int((Double(TrafficMap.most) * 2.4).rounded(.up)))
    }
}
