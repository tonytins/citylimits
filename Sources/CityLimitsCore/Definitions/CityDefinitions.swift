/// The definitions a city runs on: the base game's, with any mods applied on top. The same
/// definitions, seed, and work make the same city.
public struct CityDefinitions: Hashable, Sendable {
    public private(set) var zones: [TileUse: ZoneSpec]
    public private(set) var plants: [TileUse: PlantSpec]
    public private(set) var services: [TileUse: ServiceSpec]
    public private(set) var facilities: [TileUse: FacilitySpec]
    public var infrastructure: InfrastructureSpec
    public var economy: EconomySpec
    /// In the order the player is shown them.
    public var ordinances: [OrdinanceSpec]
    /// The mods applied, in order; empty for the base game.
    public var mods: [AppliedMod] = []

    public init(
        zones: [ZoneSpec], plants: [PlantSpec], services: [ServiceSpec], facilities: [FacilitySpec],
        infrastructure: InfrastructureSpec, economy: EconomySpec, ordinances: [OrdinanceSpec], mods: [AppliedMod] = []
    ) {
        self.zones = Dictionary(uniqueKeysWithValues: zones.map { ($0.use, $0) })
        self.plants = Dictionary(uniqueKeysWithValues: plants.map { ($0.use, $0) })
        self.services = Dictionary(uniqueKeysWithValues: services.map { ($0.use, $0) })
        self.facilities = Dictionary(uniqueKeysWithValues: facilities.map { ($0.use, $0) })
        self.infrastructure = infrastructure
        self.economy = economy
        self.ordinances = ordinances
        self.mods = mods
    }

    // The specs for a use. Every zone, plant, service, and facility use has one in the base game,
    // and a mod can only change them, so asking for one that isn't there is a programmer error.

    public func zone(_ use: TileUse) -> ZoneSpec {
        guard let spec = zones[use] else { preconditionFailure("\(use) is not a zone") }
        return spec
    }

    public func plant(_ use: TileUse) -> PlantSpec {
        guard let spec = plants[use] else { preconditionFailure("\(use) is not a power plant") }
        return spec
    }

    public func service(_ use: TileUse) -> ServiceSpec {
        guard let spec = services[use] else { preconditionFailure("\(use) is not a service building") }
        return spec
    }

    public func facility(_ use: TileUse) -> FacilitySpec {
        guard let spec = facilities[use] else { preconditionFailure("\(use) is not a stadium, seaport, or airport") }
        return spec
    }

    public func ordinance(_ id: String) -> OrdinanceSpec? {
        ordinances.first { $0.id == id }
    }

    /// The name a player sees for what is on a tile.
    public func name(of use: TileUse) -> String {
        zones[use]?.name ?? plants[use]?.name ?? services[use]?.name ?? facilities[use]?.name ?? use.displayName
    }

    /// What a tool costs before clearing the ground, as the palette shows it.
    public func cost(of action: BuildAction) -> Int {
        switch action {
        case .road: infrastructure.road
        case .bulldoze: infrastructure.bulldozer
        case .powerLine: infrastructure.powerLine
        case .park: infrastructure.park
        case .rail: infrastructure.rail
        default:
            switch action.use {
            case let use where use.isZone: zone(use).price
            case let use where use.isPowerPlant: plant(use).price
            case let use where use.isService: service(use).price
            case let use where use.isFacility: facility(use).price
            default: 0
            }
        }
    }
}

extension CityDefinitions {
    /// The base game, with Micropolis' numbers; ordinances and influences are CityLimits' own, as
    /// Tony Bark chose them.
    public static let base = CityDefinitions(
        zones: [
            ZoneSpec(
                id: "residential", use: .residential, name: "Residential zone", price: 100,
                populations: [16, 24, 32, 40], valueClasses: 4, houses: 8, tripsTo: [.commercial, .industrial]),
            ZoneSpec(
                id: "commercial", use: .commercial, name: "Commercial zone", price: 100,
                populations: [1, 2, 3, 4, 5], valueClasses: 4, houses: 0, tripsTo: [.residential, .commercial, .industrial]),
            ZoneSpec(
                id: "industrial", use: .industrial, name: "Industrial zone", price: 100,
                populations: [1, 2, 3, 4], valueClasses: 2, houses: 0, tripsTo: [.residential]),
        ],
        plants: [
            PlantSpec(id: "coal", use: .coalPowerPlant, name: "Coal power plant", price: 3000, capacity: 700),
            PlantSpec(id: "nuclear", use: .nuclearPowerPlant, name: "Nuclear power plant", price: 5000, capacity: 2000),
        ],
        services: [
            ServiceSpec(id: "fire", use: .fireStation, name: "Fire station", price: 500, upkeep: 100),
            ServiceSpec(id: "police", use: .policeStation, name: "Police station", price: 500, upkeep: 100),
        ],
        facilities: [
            // Seaports and airports pollute as a coal plant does, as in Micropolis. A stadium lifts
            // the land around it, and a seaport's and an airport's noise lowers it.
            FacilitySpec(
                id: "stadium", use: .stadium, name: "Stadium", price: 5000, demandCap: 500, pollution: 0,
                influences: [InfluenceSpec(effect: .landValue, strength: 20, reach: 2)]),
            FacilitySpec(
                id: "seaport", use: .seaport, name: "Seaport", price: 3000, demandCap: 70, pollution: 100,
                influences: [InfluenceSpec(effect: .landValue, strength: -20, reach: 2)]),
            FacilitySpec(
                id: "airport", use: .airport, name: "Airport", price: 10000, demandCap: 100, pollution: 100,
                influences: [InfluenceSpec(effect: .landValue, strength: -30, reach: 3)]),
        ],
        infrastructure: InfrastructureSpec(
            road: 10, bridge: 50, rail: 20, tunnel: 100, powerLine: 5, underwaterPowerLine: 25, park: 10,
            bulldozer: 1, clearing: 1,
            // Power reaches the buildings along a road without a line beside every street;
            // Micropolis needs lines.
            roadCarriesPower: true, railCarriesPower: false),
        economy: EconomySpec(startingFunds: 20000, taxRate: 7),
        // Four that earn money at some cost, and six that cost money for some good. Money is per
        // 1,000 people a year, against a city of 10,000 collecting about $500 in taxes at 7%;
        // demand is in the units the tax rate pushes it by, a percent of tax being 20 to 30.
        ordinances: [
            OrdinanceSpec(id: "salesTax", name: "Sales tax", money: 6, effects: [.commercialDemand: -40]),
            OrdinanceSpec(id: "incomeTax", name: "Income tax", money: 8, effects: [.residentialDemand: -60]),
            OrdinanceSpec(id: "legalizedGambling", name: "Legalized gambling", money: 10, effects: [.crime: 15]),
            OrdinanceSpec(id: "parkingFines", name: "Parking fines", money: 2, effects: [.traffic: -10]),
            OrdinanceSpec(id: "neighbourhoodWatch", name: "Neighbourhood watch", money: -5, effects: [.crime: -15]),
            OrdinanceSpec(id: "smokeDetectors", name: "Smoke detectors", money: -3, effects: [.fires: -50]),
            OrdinanceSpec(
                id: "pollutionControls", name: "Pollution controls", money: -6,
                effects: [.industrialPollution: -30, .industrialDemand: -40]),
            OrdinanceSpec(id: "energyConservation", name: "Energy conservation", money: -4, effects: [.powerCapacity: 15]),
            OrdinanceSpec(id: "tourismPromotion", name: "Tourism promotion", money: -5, effects: [.commercialDemand: 50]),
            OrdinanceSpec(id: "industrialPromotion", name: "Industrial promotion", money: -5, effects: [.industrialDemand: 50]),
        ])
}
