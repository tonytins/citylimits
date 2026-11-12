import CityLimitsCore

/// A tool in the palette: one that builds, or Inspect, which reads what a tile is.
enum Tool: CaseIterable, Hashable {
    case inspect, road, residential, commercial, industrial, bulldozer, coalPowerPlant, nuclearPowerPlant
    case powerLine, fireStation, policeStation, park, rail, stadium, seaport, airport

    /// The palette's rows, Micropolis' own.
    static let rows: [[Tool]] = [
        [.bulldozer, .powerLine, .park],
        [.road, .rail],
        [.residential, .commercial, .industrial],
        [.fireStation, .inspect, .policeStation],
        [.coalPowerPlant, .nuclearPowerPlant],
        [.stadium, .seaport],
        [.airport],
    ]

    /// What the tool does to the map, or nil for Inspect.
    var action: BuildAction? {
        switch self {
        case .inspect: nil
        case .road: .road
        case .residential: .zoneResidential
        case .commercial: .zoneCommercial
        case .industrial: .zoneIndustrial
        case .bulldozer: .bulldoze
        case .coalPowerPlant: .coalPowerPlant
        case .nuclearPowerPlant: .nuclearPowerPlant
        case .powerLine: .powerLine
        case .fireStation: .fireStation
        case .policeStation: .policeStation
        case .park: .park
        case .rail: .rail
        case .stadium: .stadium
        case .seaport: .seaport
        case .airport: .airport
        }
    }

    var name: String {
        switch self {
        case .inspect: "Inspect"
        case .road: "Road"
        case .residential: "Residential"
        case .commercial: "Commercial"
        case .industrial: "Industrial"
        case .bulldozer: "Bulldozer"
        case .coalPowerPlant: "Coal plant"
        case .nuclearPowerPlant: "Nuclear plant"
        case .powerLine: "Power line"
        case .fireStation: "Fire station"
        case .policeStation: "Police station"
        case .park: "Park"
        case .rail: "Rail"
        case .stadium: "Stadium"
        case .seaport: "Seaport"
        case .airport: "Airport"
        }
    }

    /// A few letters, for a button whose icon is missing.
    var shortName: String {
        switch self {
        case .inspect: "?"
        case .road: "Rd"
        case .residential: "R"
        case .commercial: "C"
        case .industrial: "I"
        case .bulldozer: "Doze"
        case .coalPowerPlant: "Coal"
        case .nuclearPowerPlant: "Nuc"
        case .powerLine: "Line"
        case .fireStation: "Fire"
        case .policeStation: "Pol"
        case .park: "Park"
        case .rail: "Rail"
        case .stadium: "Stad"
        case .seaport: "Port"
        case .airport: "Air"
        }
    }

    /// Its icon's file in `Assets/tools`, without `.png`; the highlighted one adds `hi`.
    var iconName: String {
        switch self {
        case .inspect: "icqry"
        case .road: "icroad"
        case .residential: "icres"
        case .commercial: "iccom"
        case .industrial: "icind"
        case .bulldozer: "icbulldozer"
        case .coalPowerPlant: "iccoal"
        case .nuclearPowerPlant: "icnuc"
        case .powerLine: "icwire"
        case .fireStation: "icfire"
        case .policeStation: "icpol"
        case .park: "icpark"
        case .rail: "icrail"
        case .stadium: "icstad"
        case .seaport: "icseap"
        case .airport: "icairp"
        }
    }

    /// What the palette says it costs, before clearing the ground.
    func price(_ definitions: CityDefinitions) -> String {
        action.map { "$\(definitions.cost(of: $0))" } ?? "free"
    }
}

/// What the player reads beside the pointer about a stroke: what it would cost while it's being
/// dragged, and what it came to once it's let go.
enum StrokeText {
    static func preview(_ plan: BuildPlan) -> String {
        switch plan.refusal {
        case .offTheMap: return "Runs off the map"
        case .notEnoughFunds: return "$\(plan.cost) - not enough money"
        default:
            if plan.tiles.isEmpty { return "Nothing to do here" }
            return plan.isFree ? "Free" : "$\(plan.cost)"
        }
    }

    static func result(_ action: BuildAction, _ result: BuildResult, free: Bool) -> String {
        func count(_ number: Int, _ thing: String) -> String { number == 1 ? "1 \(thing)" : "\(number) \(thing)s" }
        let price = free ? "for free" : "for $\(result.cost)"
        let tiles = count(result.tilesChanged, "tile")
        switch (result.status, result.reason) {
        case (.built, _):
            switch action {
            case .bulldoze: return "Cleared \(tiles) \(price)"
            case .road: return "Built \(tiles) of road \(price)"
            case .rail: return "Laid \(tiles) of rail \(price)"
            case .park: return "Laid \(tiles) of park \(price)"
            case .powerLine: return "Strung \(tiles) of power line \(price)"
            case .coalPowerPlant: return "Built a coal power plant \(price)"
            case .nuclearPowerPlant: return "Built a nuclear power plant \(price)"
            case .fireStation: return "Built a fire station \(price)"
            case .policeStation: return "Built a police station \(price)"
            case .stadium: return "Built a stadium \(price)"
            case .seaport: return "Built a seaport \(price)"
            case .airport: return "Built an airport \(price)"
            case .zoneResidential, .zoneCommercial, .zoneIndustrial:
                return "Zoned \(count(result.tilesChanged / ZoneLayout.parts, "zone")) \(price)"
            }
        case (.unchanged, .alreadyDone): return "The land is already like that"
        case (.unchanged, _): return "There was nothing to clear"
        case (.refused, .partOfAZone): return "Bulldoze its centre to clear it"
        case (.refused, .onFire): return "It's on fire: wait for it to burn out"
        case (.refused, .flooded): return "It's under water: wait for it to drain"
        case (.refused, .radioactive): return "It's radioactive: nothing can be built there for years"
        case (.refused, .notEnoughFunds): return "Not enough money, it costs $\(result.cost)"
        case (.refused, .cannotBuildHere): return "That ground cannot be built on"
        case (.refused, .offTheMap): return "That runs off the map"
        case (.refused, _): return "The work was refused"
        }
    }
}
