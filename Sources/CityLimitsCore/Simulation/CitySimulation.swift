/// A city and everything that runs it, stepped one fixed-size tick at a time. It never touches the
/// engine: the game reads it to draw and calls it to build, and a test or a headless run can drive
/// it the same way.
///
/// As in Micropolis, 16 steps make a count. The first step of a count starts its tallies, the next
/// eight scan the map a band of columns at a time, and the rest each do one of the city's jobs:
/// the census and taxes, messages, power, land value, police, density, and fire.
public final class CitySimulation {
    public static let defaultName = "CityLimits"
    public static let maxNameLength = 32

    // The steps of a count that do more than scan the map.
    private static let censusPhase = 9
    private static let messagePhase = 10
    private static let powerPhase = 11
    private static let landValuePhase = 12
    private static let policePhase = 13
    private static let densityPhase = 14
    private static let firePhase = 15

    // The messages are checked at set counts in a 64-count round, as in Micropolis.
    private static let messageRound = 64
    private static let messageChecks: [(count: Int, message: CityMessage)] = [
        (18, .needRail), (22, .needPower), (26, .needStadium), (28, .needSeaport), (30, .needAirport),
        (32, .blackouts), (35, .highPollution), (42, .highCrime), (45, .needFireStation), (48, .needPoliceStation),
        (51, .highTaxes), (54, .roadsNeedFunding), (57, .fireNeedsFunding), (60, .policeNeedsFunding), (63, .trafficJams),
    ]

    // A city of more than this many people needs a fire and a police station, and minds when
    // they are funded below this effect.
    private static let peopleBeforeStationsAreNeeded = 60
    private static let peopleBeforeStationFundingMatters = 20
    private static let serviceEffectNeedingFunding = 700
    // Taxes above this are too high; roads funded below this, with more of them than this, need funding.
    private static let highestFairTax = 12
    private static let roadEffectNeedingFunding = 20
    private static let roadsBeforeFundingMatters = 30
    // Roads funded below this crumble now and then: a tile has a 1 in 512 chance a cycle to be
    // weighed, and crumbles if a roll from 0 to 31 is above its funding.
    private static let roadEffectBeforeDecay = 30
    private static let decayChance = 512
    // A bridge counts as five road tiles toward upkeep, and rail as two, as in Micropolis, which
    // asks for more rail once a city has more than 50 zones and fewer rail tiles than zones.
    private static let bridgeRoadTiles = 5
    private static let roadTilesPerRail = 2
    private static let zonesBeforeRailIsNeeded = 50
    // A city needs power once it has more zones than this, and has blackouts below this share powered.
    private static let zonesBeforePowerIsNeeded = 10
    private static let poweredShareBeforeBlackouts = 0.7
    private static let mostTrafficBeforeJams = 60
    private static let mostPollutionBeforeComplaints = 60
    private static let mostCrimeBeforeComplaints = 100

    // As Micropolis on Easy: once a cycle a disaster comes one time in 481, and is one of nine
    // kinds: two a fire, two a flood, one nothing, one a tornado, one an earthquake, and two the
    // monster, which comes only to a city whose pollution averages over 60.
    private static let disasterChance = 481
    private static let disasterKinds = 9
    private static let fireKinds = 2
    private static let floodKinds = 2
    private static let tornadoKind = 5
    private static let earthquakeKind = 6
    private static let firstMonsterKind = 7
    private static let monsterPollution = 60
    // A flood looks this many times for a shore to rise onto; a flooded tile spreads to each side
    // one time in eight while the flood lasts, and drains one time in sixteen after.
    private static let floodSearches = 300
    private static let floodSpreadChance = 8
    private static let floodDrainChance = 16
    // An earthquake shakes 300 to 1,000 tiles, and wrecks an outer tile of a building it shakes,
    // to rubble three times in four and to fire otherwise.
    private static let fewestQuakeTiles = 300
    private static let quakeTileRange = 701
    private static let quakeFireChance = 4
    // A fire started on purpose takes up to 40 tries to find something that can burn.
    private static let fireSearches = 40
    // Plants, stations, and airports redraw their bare holes every 8 counts; hospitals,
    // churches, stadiums, and seaports every 16, as in Micropolis.
    private static let buildingRepairCounts = 8
    private static let communityRepairCounts = 16
    // A burning tile acts one visit in four, spreads to each side one time in eight, and burns
    // out sooner the better the fire stations cover it, as in Micropolis.
    private static let fireActsChance = 4
    private static let fireSpreadChance = 8
    private static let strongFireCover = 100
    private static let someFireCover = 20
    // An explosion sets fire to the four tiles at the corners of its centre.
    private static let explosionCorners: [(dx: Int, dy: Int)] = [(-1, -1), (1, -1), (-1, 1), (1, 1)]
    // Micropolis' odds of a nuclear plant melting down as the scan passes it, at its easiest, and
    // how many tiles around it a meltdown tries to make radioactive.
    private static let meltdownChance = 30000
    private static let meltdownTries = 200

    public let map: TileMap
    public let seed: Int32
    public let definitions: CityDefinitions
    /// The city's own random numbers. Everything random in the city draws from it, so the same
    /// seed and the same work make the same city.
    public internal(set) var random: SeededGenerator
    public internal(set) var name = CitySimulation.defaultName
    public internal(set) var foundedYear: Int
    /// The steps the city has run since 1900.
    public internal(set) var tickCount: Int
    /// The counts the city has run since 1900; a count is 16 steps.
    public internal(set) var cityTime: Int

    public let treasury: Treasury
    public let roads = RoadNetwork()
    public let hazards: Hazards
    public let builder: CityBuilder
    public let census = CityCensus()
    public let demand = CityDemand()
    public let maps: CityMaps
    public let services: ServiceCoverage
    public let influences: FacilityInfluences
    public let traffic: TrafficMap
    public let trips: TripPlanner
    public let power = PowerGrid()
    public let budget: CityBudget
    public let community = CommunityBuildings()
    public let ordinances: CityOrdinances
    public let facilities = Facilities()
    public let neighbours: CityNeighbours
    public let vehicles = Vehicles()
    public let menaces = Menaces()
    private let growth: ZoneGrowth
    // What came of the vehicles' last move, kept so a step doesn't make a list.
    private var vehicleEvents: [VehicleEvent] = []
    private var trampled: [(x: Int, y: Int)] = []

    /// The road and rail tiles the scan has counted this count.
    public internal(set) var roadTiles = 0
    public internal(set) var railTiles = 0
    // The road and rail tiles and stations as of the last count's budget, which an estimate uses.
    var budgetedRoadTiles = 0
    var budgetedRailTiles = 0
    var budgetedFireStations = 0
    var budgetedPoliceStations = 0

    /// Whether each disaster can strike on its own. Turned off, it still rolls its dice as if it
    /// found nothing to harm, so the switches never change the rest of the city's dice.
    public var randomFires = true
    public var randomFloods = true
    public var randomEarthquakes = true
    public var randomMeltdowns = true
    public var randomAirCrashes = true
    public var randomShipwrecks = true
    public var randomTornadoes = true
    public var randomMonsters = true

    /// Called with everything that happens, as it happens.
    public var onEvent: ((CityEvent) -> Void)?

    /// Starts a city on a map. `startYear` is when its calendar begins.
    public init(map: TileMap, seed: Int32, funds: Int? = nil, definitions: CityDefinitions = .base, startYear: Int = CityCalendar.startYear) {
        precondition((CityCalendar.startYear...CityCalendar.latestStartYear).contains(startYear), "start year \(startYear) is out of range")
        self.map = map
        self.seed = seed
        self.definitions = definitions
        random = SeededGenerator(seed: seed)
        foundedYear = startYear
        tickCount = (startYear - CityCalendar.startYear) * CityCalendar.stepsPerYear
        cityTime = tickCount / CityCalendar.stepsPerCount
        treasury = Treasury(funds: funds ?? definitions.economy.startingFunds)
        hazards = Hazards(map: map)
        builder = CityBuilder(map: map, treasury: treasury, roads: roads, definitions: definitions, hazards: hazards)
        maps = CityMaps(map: map, definitions: definitions)
        services = ServiceCoverage(map: map, definitions: definitions)
        influences = FacilityInfluences(map: map, definitions: definitions)
        traffic = TrafficMap(map: map)
        trips = TripPlanner(traffic: traffic)
        budget = CityBudget(fireStationUpkeep: definitions.service(.fireStation).upkeep, policeStationUpkeep: definitions.service(.policeStation).upkeep)
        budget.setTaxRate(definitions.economy.taxRate)
        ordinances = CityOrdinances(definitions: definitions)
        neighbours = CityNeighbours(map: map, seed: seed)
        growth = ZoneGrowth(map: map, trips: trips, definitions: definitions, demand: demand, maps: maps, community: community, hazards: hazards)
    }

    // MARK: Name and date

    /// Whether a name can be a city's: 1 to 32 characters once trimmed, with no control characters.
    public static func isValidName(_ name: String?) -> Bool {
        guard let trimmed = name?.trimmingWhitespace, !trimmed.isEmpty, trimmed.count <= maxNameLength else { return false }
        return !trimmed.unicodeScalars.contains { $0.properties.generalCategory == .control }
    }

    /// Renames the city, and says whether the name could be its.
    @discardableResult
    public func rename(_ name: String) -> Bool {
        guard Self.isValidName(name) else { return false }
        self.name = name.trimmingWhitespace
        return true
    }

    public var date: GameDate { CityCalendar.date(at: cityTime) }

    /// Whether the city hasn't run a step since it was founded.
    public var isAtFounding: Bool {
        tickCount == (foundedYear - CityCalendar.startYear) * CityCalendar.stepsPerYear
    }

    /// Moves a city that hasn't started yet to another year, and says whether it could.
    @discardableResult
    public func moveFounding(to year: Int) -> Bool {
        guard isAtFounding, (CityCalendar.startYear...CityCalendar.latestStartYear).contains(year) else { return false }
        tickCount = (year - CityCalendar.startYear) * CityCalendar.stepsPerYear
        cityTime = tickCount / CityCalendar.stepsPerCount
        foundedYear = year
        return true
    }

    // MARK: The step

    /// Runs one step.
    public func tick() {
        tickCount += 1
        let phase = tickCount % CityCalendar.stepsPerCount
        switch phase {
        case 0:
            cityTime += 1
            if cityTime % 2 == 0 {
                demand.update(census: census, taxRate: budget.taxEffect, caps: facilities.caps, nudge: demandNudge())
            }
            census.clear()
            power.startCount()
            services.startCount()
            facilities.startCount()
            community.startCount()
            roadTiles = 0
            railTiles = 0
        case 1...CityCalendar.scanBands:
            scanBand(phase - 1)
        case Self.censusPhase:
            if cityTime % CityCalendar.countsPerMonth == 0 {
                census.recordMonth()
                community.updateNeeds(residents: census.residential)
                askForOrdinance()
                dealWithNeighbours()
            }
            collectTaxes()
        case Self.messagePhase:
            // As in Micropolis, the traffic fades before the city has its say.
            traffic.fade()
            sendMessages()
        case Self.powerPhase:
            power.trace(map, definitions: definitions, capacityPercent: ordinances.scaled(.powerCapacity, 100), traded: neighbours.powerTraded())
            if power.isBrownedOut { send(.brownouts) }
        case Self.landValuePhase:
            influences.update(.landValue)
            influences.update(.pollution)
            maps.updateLandValue(traffic: traffic, hazards: hazards, influences: influences)
        case Self.policePhase:
            services.spreadPolice()
            influences.update(.crime)
            maps.updateCrime(services: services, influences: influences, ordinances: ordinances.effect(.crime))
        case Self.densityPhase:
            maps.updateDensity()
        case Self.firePhase:
            services.spreadFire()
            // As in Micropolis, the flood's time runs down before the dice are rolled.
            hazards.recedeFlood()
            rollForDisaster()
        default:
            break
        }
        moveVehicles()
    }

    /// Runs a number of steps.
    public func run(steps: Int) {
        for _ in 0..<max(0, steps) { tick() }
    }

    private func send(_ message: CityMessage) { onEvent?(.message(message)) }

    // What demand is nudged by each time it moves: the ordinances and the roads and rail to
    // neighbours, CityLimits' own.
    private func demandNudge() -> (residential: Int, commercial: Int, industrial: Int) {
        let roads = neighbours.demandNudge()
        return (
            roads.residential + ordinances.effect(.residentialDemand),
            roads.commercial + ordinances.effect(.commercialDemand),
            roads.industrial + ordinances.effect(.industrialDemand))
    }

    // MARK: The scan

    // The scan reaches each zone and building at its centre, as in Micropolis: it counts plants for
    // the power trace, and gives each zone its power, then its census and its chance to grow.
    private func scanBand(_ band: Int) {
        roads.ensureCurrent(map)
        for y in 0..<map.height {
            for x in CityCalendar.bandColumns(band, mapWidth: map.width) {
                if hazards.isBurning(x, y) {
                    visitFire(x, y)
                    continue
                }
                if hazards.isFlooded(x, y) {
                    visitFlood(x, y)
                    continue
                }
                // Radioactive ground decays now and then, as Micropolis' does.
                if hazards.isRadioactive(x, y) {
                    if random.next(in: 0..<Hazards.radiationDecayChance) == 0 { hazards.decay(x, y) }
                    continue
                }

                let state = map[x, y]
                if state.isRoad {
                    visitRoad(x, y, state)
                    continue
                }
                if state.isRail {
                    visitRail(x, y, state)
                    continue
                }
                guard state.isBuildingCenter else { continue }

                let powered = power.visit(map, x, y)
                if state.use.isCommunityBuilding {
                    repairNowAndThen(x, y, every: Self.communityRepairCounts)
                    community.visit(map, &random, x, y, hazards: hazards)
                } else if state.use.isPowerPlant {
                    // As Micropolis does, a nuclear plant may melt down as the scan passes it; the
                    // dice are rolled whether meltdowns are on or not.
                    if state.use == .nuclearPowerPlant && random.next(in: 0..<(Self.meltdownChance + 1)) == 0 && randomMeltdowns {
                        meltdown(x, y)
                        continue
                    }
                    repairNowAndThen(x, y, every: Self.buildingRepairCounts)
                } else if state.use.isFacility {
                    // A powered airport sends up aircraft, and a powered seaport calls in a ship.
                    if powered && state.use == .airport {
                        vehicles.visitAirport(x, y, map: map, random: &random)
                    } else if powered && state.use == .seaport {
                        vehicles.visitSeaport(map: map, random: &random)
                    }
                    repairNowAndThen(x, y, every: state.use == .airport ? Self.buildingRepairCounts : Self.communityRepairCounts)
                    facilities.count(state.use)
                } else if state.use.isService {
                    repairNowAndThen(x, y, every: Self.buildingRepairCounts)
                    let effect = state.use == .fireStation ? budget.fireEffect : budget.policeEffect
                    services.addStation(x, y, use: state.use, fundingEffect: effect, powered: powered, served: roads.isZoneServed(x, y))
                } else if state.isZoneCenter {
                    census.add(state.use, population: definitions.zone(state.use).population(on: map, centerX: x, centerY: y))
                    growth.visit(x, y, random: &random)
                }
            }
        }
    }

    // As in Micropolis, the scan counts each road tile for upkeep, and when roads are underfunded
    // a tile now and then crumbles: to rubble on land, back to water as a bridge. A road crossing
    // a power line never crumbles. A road shows the traffic its block has now, and one that showed
    // heavy traffic counts again.
    private func visitRoad(_ x: Int, _ y: Int, _ state: TileState) {
        roadTiles += 1
        if crumbles(state) {
            map.setState(x, y, TileState(state.isBridge ? .water : .rubble))
            roads.markDirty()
            return
        }
        if state.isBridge { roadTiles += Self.bridgeRoadTiles - 1 }
        if traffic.show(x, y) == .heavy { roadTiles += 1 }
    }

    // Rail is counted and crumbles as roads do: back to water in a tunnel.
    private func visitRail(_ x: Int, _ y: Int, _ state: TileState) {
        railTiles += 1
        if crumbles(state) {
            map.setState(x, y, TileState(state.isTunnel ? .water : .rubble))
            roads.markDirty()
        }
    }

    private func crumbles(_ state: TileState) -> Bool {
        budget.roadEffect < Self.roadEffectBeforeDecay
            && random.next(in: 0..<Self.decayChance) == 0
            && !state.hasPowerLine
            && budget.roadEffect < random.next(in: 0..<CityBudget.fullRoadEffect)
    }

    // As Micropolis repairs its buildings: every so many counts, the holes in one that are bare
    // ground are redrawn, at no cost.
    private func repairNowAndThen(_ x: Int, _ y: Int, every counts: Int) {
        if cityTime % counts == 0 { hazards.repairBareHoles(centerX: x, centerY: y) }
    }

    // MARK: Money and messages

    // As in Micropolis, a 48th of the year's budget is set aside each count, and the year is
    // settled when the count comes round to a whole year, the first time at the very start.
    private func collectTaxes() {
        (budgetedRoadTiles, budgetedRailTiles, budgetedFireStations, budgetedPoliceStations) =
            (roadTiles, railTiles, services.fireStations, services.policeStations)
        budget.accrue(estimateBudget())
        if cityTime % CityBudget.countsPerYear == 0 {
            let year = budget.settle(into: treasury)
            // Micropolis works the traffic average out after the taxes, and only for a city with
            // people in it; an empty one keeps the last.
            if demand.taxablePopulation != 0 { traffic.updateAverage(maps) }
            onEvent?(.yearSettled(year))
        }
    }

    /// What a full year would bring in and cost at today's numbers.
    public func estimateBudget() -> BudgetYear {
        let ordinanceMoney = ordinances.yearlyMoney(population: census.cityPopulation)
        let dealMoney = neighbours.yearlyMoney()
        return budget.estimate(
            population: demand.taxablePopulation, landValueAverage: maps.landValueAverage,
            roadTiles: budgetedRoadTiles + budgetedRailTiles * Self.roadTilesPerRail,
            fireStations: budgetedFireStations, policeStations: budgetedPoliceStations, funds: treasury.funds,
            ordinanceIncome: ordinanceMoney.income, ordinanceCost: ordinanceMoney.cost,
            dealIncome: dealMoney.income, dealCost: dealMoney.cost)
    }

    /// Enacts or repeals an ordinance, and says whether anything changed.
    @discardableResult
    public func setOrdinance(_ id: String, enacted: Bool) -> Bool {
        guard ordinances.set(id, enacted: enacted), let spec = definitions.ordinance(id) else { return false }
        applyOrdinances()
        onEvent?(.ordinance(OrdinanceNews(ordinance: spec, kind: enacted ? .enacted : .repealed)))
        return true
    }

    // The settings other parts of the city read from the ordinances, set again when they change.
    func applyOrdinances() {
        traffic.tripPercent = ordinances.scaled(.traffic, 100)
        maps.industryPollutionPercent = ordinances.scaled(.industrialPollution, 100)
    }

    // Each quarter the city asks for the ordinance it most wants, as SimCity 3000's petitions do.
    private func askForOrdinance() {
        guard cityTime % (CityCalendar.countsPerMonth * 3) == 0,
            let wanted = ordinances.mostWanted(
                population: census.cityPopulation, brownedOut: power.isBrownedOut, crimeAverage: maps.crimeAverage,
                pollutionAverage: maps.pollutionAverage, burning: hazards.fires.count,
                budgetNeedsAttention: budget.lastYear?.needsAttention ?? false,
                commercialDemand: demand.commercial, industrialDemand: demand.industrial)
        else { return }
        onEvent?(.ordinance(OrdinanceNews(ordinance: wanted, kind: .petition)))
    }

    private func sendMessages() {
        let count = cityTime % Self.messageRound
        guard let check = Self.messageChecks.first(where: { $0.count == count }), isDue(check.message) else { return }
        send(check.message)
    }

    // Whether the city should hear a message on the count it's checked.
    private func isDue(_ message: CityMessage) -> Bool {
        let people = demand.taxablePopulation
        switch message {
        case .needRail:
            return power.zones > Self.zonesBeforeRailIsNeeded && power.zones > railTiles
        case .needPower:
            return power.zones > Self.zonesBeforePowerIsNeeded && power.coalPlants + power.nuclearPlants == 0
        case .needStadium:
            return facilities.checkStadium(census, cap: definitions.facility(.stadium).demandCap)
        case .needSeaport:
            return facilities.checkSeaport(census, cap: definitions.facility(.seaport).demandCap)
        case .needAirport:
            return facilities.checkAirport(census, cap: definitions.facility(.airport).demandCap)
        case .blackouts:
            let buildings = power.poweredBuildings + power.unpoweredBuildings
            return buildings > 0 && Double(power.poweredBuildings) / Double(buildings) < Self.poweredShareBeforeBlackouts
        case .highPollution:
            return maps.pollutionAverage > Self.mostPollutionBeforeComplaints
        case .highCrime:
            return maps.crimeAverage > Self.mostCrimeBeforeComplaints
        case .highTaxes:
            return budget.taxRate > Self.highestFairTax
        case .roadsNeedFunding:
            return budget.roadEffect < Self.roadEffectNeedingFunding && roadTiles > Self.roadsBeforeFundingMatters
        case .needFireStation:
            return people > Self.peopleBeforeStationsAreNeeded && services.fireStations == 0
        case .needPoliceStation:
            return people > Self.peopleBeforeStationsAreNeeded && services.policeStations == 0
        case .fireNeedsFunding:
            return budget.fireEffect < Self.serviceEffectNeedingFunding && people > Self.peopleBeforeStationFundingMatters
        case .policeNeedsFunding:
            return budget.policeEffect < Self.serviceEffectNeedingFunding && people > Self.peopleBeforeStationFundingMatters
        case .trafficJams:
            return traffic.average > Self.mostTrafficBeforeJams
        default:
            return false
        }
    }

    // MARK: Disasters

    // As Micropolis does at the end of each cycle: a disaster one time in 481, and then which kind.
    private func rollForDisaster() {
        guard random.next(in: 0..<Self.disasterChance) == 0 else { return }
        let kind = random.next(in: 0..<Self.disasterKinds)
        if kind < Self.fireKinds {
            let (x, y) = (random.next(in: 0..<map.width), random.next(in: 0..<map.height))
            // Smoke detectors stop some fires breaking out: a die is rolled only while they are
            // enacted, so a city without them rolls as it always did.
            let prevented = ordinances.effect(.fires)
            if randomFires && isArsonable(x, y) && (prevented == 0 || random.next(in: 0..<100) >= -prevented) {
                ignite(x, y)
                send(.fireReported)
            }
        } else if kind < Self.fireKinds + Self.floodKinds {
            _ = startFlood(allowed: randomFloods)
        } else if kind == Self.tornadoKind {
            _ = callTornado(allowed: randomTornadoes)
        } else if kind == Self.earthquakeKind {
            _ = quake(allowed: randomEarthquakes)
        } else if kind >= Self.firstMonsterKind && maps.pollutionAverage > Self.monsterPollution {
            _ = callMonster(allowed: randomMonsters)
        }
    }

    /// Sets off a disaster, as a scenario or the player's menu does. Says whether it struck.
    @discardableResult
    public func strike(_ disaster: Disaster) -> Bool {
        switch disaster {
        case .fire: startFireOnPurpose()
        case .flood: startFlood(allowed: true)
        case .earthquake: quake(allowed: true)
        case .meltdown: meltDownFirstPlant()
        case .planeCrash: crashPlane()
        case .shipwreck: wreckShip()
        case .tornado: callTornado(allowed: true) || menaces.tornado != nil
        case .monster: callMonster(allowed: true) || menaces.monster != nil
        }
    }

    // As in Micropolis' fire: one visit in four, the fire may spread to each side, one time in
    // eight, and then may burn out: one time in two where fire stations cover it well, in three
    // where they cover it some, in four where they barely do, and in eleven where they don't.
    private func visitFire(_ x: Int, _ y: Int) {
        guard random.next(in: 0..<Self.fireActsChance) == 0 else { return }
        for step in Sides.steps where random.next(in: 0..<Self.fireSpreadChance) == 0 && hazards.canBurn(x + step.dx, y + step.dy) {
            ignite(x + step.dx, y + step.dy)
        }
        let cover = services.fireCoverage(x, y)
        let odds = cover > Self.strongFireCover ? 1 : cover > Self.someFireCover ? 2 : cover != 0 ? 3 : 10
        if random.next(in: 0..<(odds + 1)) == 0 { hazards.putOut(x, y) }
    }

    // A building whose centre catches may explode: as in Micropolis, industry with a building,
    // power plants, stations, stadiums, seaports, and airports do.
    func ignite(_ x: Int, _ y: Int) {
        guard let building = hazards.ignite(x, y) else { return }
        let explodes: Bool
        switch building.use {
        case .industrial: explodes = building.development > 0
        case .coalPowerPlant, .nuclearPowerPlant, .fireStation, .policeStation, .stadium, .seaport, .airport: explodes = true
        default: explodes = false
        }
        if explodes { explode(x, y) }
    }

    // The explosion sets fire to the tiles at the centre's corners that can burn, or are bare
    // ground, but never to another building's centre, at once, as Tony Bark chose.
    func explode(_ x: Int, _ y: Int) {
        for corner in Self.explosionCorners {
            let (cx, cy) = (x + corner.dx, y + corner.dy)
            let state = map.state(atOrDefault: cx, cy, fallback: TileState(.water))
            let bare = state.use == .none && state.kind == .grass && !hazards.isFlooded(cx, cy)
            if !state.isBuildingCenter && (bare || hazards.canBurn(cx, cy)) {
                hazards.ignite(cx, cy)
            }
        }
        send(.explosionReported)
        onEvent?(.exploded(x: x, y: y))
    }

    // A scenario's meltdown strikes the first nuclear plant on the map, row by row.
    private func meltDownFirstPlant() -> Bool {
        for y in 0..<map.height {
            for x in 0..<map.width where map[x, y].isBuildingCenter && map[x, y].use == .nuclearPowerPlant {
                meltdown(x, y)
                return true
            }
        }
        return false
    }

    // As Micropolis' doMeltdown: explosions at the plant's four corners, the plant ablaze, and 200
    // tries at the ground around it, 20 tiles either side and 15 above and below, each making
    // radioactive whatever can burn, or bare ground, but never a building's centre.
    private func meltdown(_ x: Int, _ y: Int) {
        ignite(x, y)
        for (cx, cy) in [(x - 1, y - 1), (x - 1, y + 2), (x + 2, y - 1), (x + 2, y + 2)] {
            explode(cx, cy)
            onEvent?(.exploded(x: cx, y: cy))
        }
        for _ in 0..<Self.meltdownTries {
            let rx = x - 20 + random.next(in: 0..<41)
            let ry = y - 15 + random.next(in: 0..<31)
            guard map.contains(rx, ry) else { continue }
            let state = map[rx, ry]
            let bare = state.use == .none && state.kind == .grass
            if !state.isBuildingCenter && (bare || hazards.canBurn(rx, ry) || hazards.isBurning(rx, ry)) {
                hazards.putOut(rx, ry)
                hazards.drain(rx, ry)
                hazards.contaminate(rx, ry)
            }
        }
        send(.meltdownReported)
    }

    private func startFireOnPurpose() -> Bool {
        for _ in 0..<Self.fireSearches {
            let (x, y) = (random.next(in: 0..<map.width), random.next(in: 0..<map.height))
            if !map[x, y].isBuildingCenter && hazards.canBurn(x, y) {
                ignite(x, y)
                send(.fireReported)
                return true
            }
        }
        return false
    }

    // As Micropolis does: up to 300 tries at a shore, a stretch of water with land beside it, and
    // the first side with land the water can rise onto: bare ground, trees, or a road, rail, a
    // line, or a park. A flood can't start on a building or rubble, but spreads onto them.
    private func startFlood(allowed: Bool) -> Bool {
        for _ in 0..<Self.floodSearches {
            let (x, y) = (random.next(in: 0..<map.width), random.next(in: 0..<map.height))
            guard isShore(x, y) else { continue }
            for step in Sides.steps where canFloodStart(x + step.dx, y + step.dy) {
                if allowed {
                    hazards.flood(x + step.dx, y + step.dy)
                    hazards.riseFlood()
                    send(.floodReported)
                }
                return true
            }
        }
        return false
    }

    private func isShore(_ x: Int, _ y: Int) -> Bool {
        let state = map[x, y]
        guard state.kind == .water && state.use == .none else { return false }
        return Sides.steps.contains { map.contains(x + $0.dx, y + $0.dy) && map[x + $0.dx, y + $0.dy].kind != .water }
    }

    private func canFloodStart(_ x: Int, _ y: Int) -> Bool {
        guard hazards.canFlood(x, y) else { return false }
        let state = map[x, y]
        return state.use == .none ? state.kind == .grass || state.kind == .trees : !state.hasFootprint
    }

    // As Micropolis does: 300 to 1,000 tiles shaken at random, and each outer tile of a building
    // among them wrecked, to rubble or to fire. With earthquakes off, the same tiles are picked,
    // and nothing happens to them.
    private func quake(allowed: Bool) -> Bool {
        let shaken = random.next(in: 0..<Self.quakeTileRange) + Self.fewestQuakeTiles
        for _ in 0..<shaken {
            let (x, y) = (random.next(in: 0..<map.width), random.next(in: 0..<map.height))
            let state = map[x, y]
            guard state.hasFootprint, !state.isBuildingCenter else { continue }
            let burns = random.next(in: 0..<Self.quakeFireChance) == 0
            guard allowed else { continue }
            if burns {
                hazards.ignite(x, y)
            } else {
                map.setState(x, y, TileState(.rubble))
            }
        }
        if allowed {
            send(.earthquakeReported)
            onEvent?(.earthquake)
        }
        return true
    }

    // As in Micropolis' flood: while it lasts, a flooded tile spreads to each side one time in
    // eight; after, it drains one time in sixteen, leaving bare ground.
    private func visitFlood(_ x: Int, _ y: Int) {
        if hazards.floodCyclesLeft > 0 {
            for step in Sides.steps where random.next(in: 0..<Self.floodSpreadChance) == 0 {
                hazards.flood(x + step.dx, y + step.dy)
            }
            return
        }
        if random.next(in: 0..<Self.floodDrainChance) == 0 { hazards.drain(x, y) }
    }

    // A fire starts only on an outer tile of a building with something on it, as Micropolis'
    // does: a house, a zone's building, a plant, a station, a hospital, or a church.
    private func isArsonable(_ x: Int, _ y: Int) -> Bool {
        let state = map[x, y]
        guard state.hasFootprint, !state.isBuildingCenter else { return false }
        return state.use != .residential || state.hasHouse || map.centerState(x, y).development > 0
    }

    // MARK: Sprites

    // As Micropolis moves its sprites twice for each step, the vehicles move twice a step, and the
    // city answers what came of each move: fires where aircraft came down or a ship was wrecked,
    // and the helicopter's word on the traffic.
    private func moveVehicles() {
        for move in 0..<2 {
            vehicleEvents.removeAll(keepingCapacity: true)
            trampled.removeAll(keepingCapacity: true)
            vehicles.move(
                step: tickCount, move: move, map: map, traffic: traffic, random: &random, airCrashes: randomAirCrashes,
                shipwrecks: randomShipwrecks, events: &vehicleEvents, quarry: menaces.quarry)
            menaces.move(
                map: map, random: &random, vehicles: vehicles, airCrashes: randomAirCrashes, shipwrecks: randomShipwrecks,
                events: &vehicleEvents, trampled: &trampled)
            for (x, y) in trampled { trample(x, y) }
            for happened in vehicleEvents {
                switch happened.kind {
                case .heavyTraffic:
                    send(.heavyTrafficReported)
                case .shipwreck:
                    wreck(happened.x, happened.y)
                    send(.shipwreckReported)
                case .planeCrash, .helicopterCrash:
                    crash(happened.x, happened.y)
                    send(happened.kind == .planeCrash ? .planeCrashReported : .helicopterCrashReported)
                }
            }
        }
    }

    // An aircraft coming down sets fire where it lands, and at the corners around, at once.
    private func crash(_ x: Int, _ y: Int) {
        let state = map.state(atOrDefault: x, y, fallback: TileState(.water))
        let bare = state.use == .none && state.kind == .grass && !hazards.isFlooded(x, y) && !hazards.isRadioactive(x, y)
        if !state.isBuildingCenter && (bare || hazards.canBurn(x, y)) { ignite(x, y) }
        explode(x, y)
        onEvent?(.exploded(x: x, y: y))
    }

    // A ship run aground blows up and wrecks what it hit, as Micropolis' destroyTile: what stands
    // on the water is lost to it, and what stands on land burns.
    private func wreck(_ x: Int, _ y: Int) {
        crash(x, y)
        let state = map.state(atOrDefault: x, y)
        if state.kind == .water && state.use != .none { map.setState(x, y, TileState(.water)) }
    }

    // A tornado touches down, or the one blowing lasts its full time again.
    private func callTornado(allowed: Bool) -> Bool {
        guard menaces.callTornado(map: map, random: &random, allowed: allowed) else { return false }
        send(.tornadoReported)
        return true
    }

    // The monster comes, or the one about makes for the most polluted place again.
    private func callMonster(allowed: Bool) -> Bool {
        guard menaces.callMonster(map: map, random: &random, mostPolluted: maps.mostPolluted, allowed: allowed) else { return false }
        send(.monsterReported)
        return true
    }

    // As Micropolis' destroyTile, which the tornado and the monster do to each tile they pass:
    // what stands on the water is lost to it; a building's centre goes up as fire takes it, since
    // CityLimits can't leave its other tiles standing loose; anything else that can burn is blown
    // to rubble. Bare ground, rubble, water, and what is burning are left be.
    private func trample(_ x: Int, _ y: Int) {
        guard map.contains(x, y) else { return }
        let state = map[x, y]
        if state.kind == .water {
            if state.use != .none {
                map.setState(x, y, TileState(.water))
                onEvent?(.exploded(x: x, y: y))
            }
            return
        }
        guard hazards.canBurn(x, y) else { return }
        if state.isBuildingCenter {
            ignite(x, y)
        } else {
            map.setState(x, y, TileState(.rubble))
        }
        onEvent?(.exploded(x: x, y: y))
    }

    private func crashPlane() -> Bool {
        guard let at = vehicles.downPlane() else { return false }
        crash(at.x, at.y)
        send(.planeCrashReported)
        return true
    }

    private func wreckShip() -> Bool {
        guard let at = vehicles.sinkShip() else { return false }
        wreck(at.x, at.y)
        send(.shipwreckReported)
        return true
    }

    // MARK: Neighbours

    /// Takes the offer on the table, and says whether there was one.
    @discardableResult
    public func acceptOffer() -> Bool {
        guard let deal = neighbours.accept(at: tickCount) else { return false }
        tell(.accepted, deal.side, deal.kind, deal.power, deal.money)
        return true
    }

    /// Turns the offer on the table down, and says whether there was one.
    @discardableResult
    public func declineOffer() -> Bool {
        guard let offer = neighbours.decline() else { return false }
        tell(.declined, offer.side, offer.kind, offer.power, offer.money)
        return true
    }

    /// Breaks the deal with a neighbour, paying a year's money, and says whether there was one.
    @discardableResult
    public func breakDeal(with side: Side) -> Bool {
        guard let deal = neighbours.breakDeal(with: side, at: tickCount) else { return false }
        treasury.settle(-deal.money)
        tell(.broken, deal.side, deal.kind, deal.power, deal.money)
        return true
    }

    // Each month the deals over and an offer unanswered lapse; every six months, offset from the
    // petitions, a neighbour with a power line to the city may offer one.
    private func dealWithNeighbours() {
        let (ended, lapsed) = neighbours.expire(at: tickCount)
        for deal in ended { tell(.ended, deal.side, deal.kind, deal.power, deal.money) }
        if let lapsed { tell(.lapsed, lapsed.side, lapsed.kind, lapsed.power, lapsed.money) }
        if cityTime % (CityCalendar.countsPerMonth * 6) == CityCalendar.countsPerMonth * 3,
            let offer = neighbours.propose(
                population: census.cityPopulation, brownedOut: power.isBrownedOut, capacity: power.capacity,
                used: power.poweredTileCount, at: tickCount)
        {
            tell(.offer, offer.side, offer.kind, offer.power, offer.money)
        }
    }

    private func tell(_ kind: NeighbourNewsKind, _ side: Side, _ deal: DealKind, _ power: Int, _ money: Int) {
        onEvent?(.neighbour(NeighbourNews(neighbour: neighbours.neighbour(side), kind: kind, deal: deal, power: power, money: money)))
    }

    // MARK: Power on demand

    /// Traces power at once, outside the cycle, as a newly loaded or edited city needs.
    public func tracePowerNow() {
        power.startCount()
        visitBuildings()
        power.trace(map, definitions: definitions)
        // The second visit records on each centre the power the trace just gave it.
        power.startCount()
        visitBuildings()
        power.startCount()
    }

    private func visitBuildings() {
        for y in 0..<map.height {
            for x in 0..<map.width where map[x, y].isBuildingCenter {
                power.visit(map, x, y)
            }
        }
    }
}

extension String {
    /// The string without spaces, tabs, or line breaks at either end.
    var trimmingWhitespace: String {
        String(drop(while: \.isWhitespace).reversed().drop(while: \.isWhitespace).reversed())
    }
}
