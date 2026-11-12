/// What a kind of zone is, as data: its name, price, and how it grows. A mod can change it.
public struct ZoneSpec: Hashable, Sendable {
    public var id: String
    public var use: TileUse
    public var name: String
    /// What one 3 by 3 zone costs to lay.
    public var price: Int
    /// The population of the zone's building at each level, from level 1 up. The number of
    /// levels is fixed by the drawings; a mod can change the numbers.
    public var populations: [Int]
    /// How many land-value classes its buildings come in, fixed by the drawings.
    public var valueClasses: Int
    /// How many single houses grow on its outer tiles before its first building; 0 to build
    /// straight away.
    public var houses: Int
    /// The kinds of zone a trip from here looks for.
    public var tripsTo: [TileUse]

    public var levels: Int { populations.count }

    /// A zone's population, read from its centre and, with no building, the houses around it.
    public func population(on map: TileMap, centerX: Int, centerY: Int) -> Int {
        let center = map[centerX, centerY]
        if center.development > 0 {
            return populations[min(center.development, levels) - 1]
        }
        var houses = 0
        for dy in -1...1 {
            for dx in -1...1 where map.state(atOrDefault: centerX + dx, centerY + dy).hasHouse {
                houses += 1
            }
        }
        return houses
    }
}

/// What a kind of power plant is: its name, price, and how many tiles it powers. The city's
/// plants pool their capacity, as in Micropolis.
public struct PlantSpec: Hashable, Sendable {
    public var id: String
    public var use: TileUse
    public var name: String
    public var price: Int
    public var capacity: Int
}

/// What a kind of service building is: its name, price, upkeep, and how far it covers.
public struct ServiceSpec: Hashable, Sendable {
    /// A station's strength in Micropolis, as a percentage.
    public static let micropolisStrength = 100
    /// How many times Micropolis smooths a coverage map.
    public static let micropolisReach = 3

    public var id: String
    public var use: TileUse
    public var name: String
    public var price: Int
    /// What one station costs each year at full funding.
    public var upkeep: Int
    /// How strongly it covers its block, as a percentage of Micropolis' station.
    public var strength = ServiceSpec.micropolisStrength
    /// How many times its coverage spreads to the blocks around, each pass further and fainter.
    public var reach = ServiceSpec.micropolisReach
}

/// Which of the city's maps an influence changes.
public enum InfluenceEffect: Hashable, Sendable {
    /// The land value, from 1 to 250 where something is built.
    case landValue
    /// The crime, from 0 to 250.
    case crime
    /// The pollution, from 0 to 255.
    case pollution
}

/// What a building does to the land around it: a strength at its 8 by 8 block, spreading and
/// fading to the blocks around once for each step of reach. Below zero it lowers the map.
public struct InfluenceSpec: Hashable, Sendable {
    public var effect: InfluenceEffect
    public var strength: Int
    public var reach: Int
}

/// What a stadium, seaport, or airport is: its name, price, the demand it lets grow past a cap,
/// and the pollution each of its tiles gives off.
public struct FacilitySpec: Hashable, Sendable {
    public var id: String
    public var use: TileUse
    public var name: String
    public var price: Int
    /// How big the demand it serves may grow before a city without one holds it at zero:
    /// residents for a stadium, industry for a seaport, and commerce for an airport.
    public var demandCap: Int
    public var pollution: Int
    /// What a powered one does to the land around it, each effect at most once.
    public var influences: [InfluenceSpec] = []

    public func influence(on effect: InfluenceEffect) -> InfluenceSpec? {
        influences.first { $0.effect == effect }
    }
}

/// What the city's infrastructure costs, per tile, and what carries power besides lines.
public struct InfrastructureSpec: Hashable, Sendable {
    public var road: Int
    public var bridge: Int
    public var rail: Int
    public var tunnel: Int
    public var powerLine: Int
    public var underwaterPowerLine: Int
    public var park: Int
    /// What the bulldozer costs for each tile, or each zone or building it knocks down.
    public var bulldozer: Int
    /// What a tool pays to clear a tile of trees or rubble before building there.
    public var clearing: Int
    /// Whether a road joins the buildings along it to the grid, without being powered itself.
    public var roadCarriesPower = false
    public var railCarriesPower = false
}

/// What a new city starts with: its money and tax rate. A scenario sets its own money.
public struct EconomySpec: Hashable, Sendable {
    public var startingFunds: Int
    /// In percent.
    public var taxRate: Int
}

/// What an ordinance can change, each in its own units.
public enum OrdinanceEffect: Hashable, Sendable {
    /// Added to residential demand each time demand moves.
    case residentialDemand
    case commercialDemand
    case industrialDemand
    /// Added to crime everywhere, where police coverage takes it away.
    case crime
    /// The pollution industry gives off, in percent: -30 leaves 70%.
    case industrialPollution
    /// The chance of a fire breaking out by itself, in percent: -50 halves it.
    case fires
    /// What the power plants can power, in percent: 15 makes it 115%.
    case powerCapacity
    /// The traffic each trip leaves, in percent: -10 leaves 90%.
    case traffic
}

/// A city ordinance, after SimCity 3000's: a rule the player enacts or repeals at any time, which
/// earns or costs money each year by population and nudges the city. A mod can change what one
/// does, but not add others, since the engine decides when each is asked for.
public struct OrdinanceSpec: Hashable, Sendable {
    public var id: String
    public var name: String
    /// What it earns each year for every 1,000 people, or below zero what it costs.
    public var money: Int
    public var effects: [OrdinanceEffect: Int]

    public func amount(of effect: OrdinanceEffect) -> Int { effects[effect] ?? 0 }

    /// What it earns, or costs below zero, in a year for a city of this size.
    public func yearlyMoney(population: Int) -> Int { max(0, population) * money / 1000 }
}

/// A mod a city's definitions were made with.
public struct AppliedMod: Hashable, Sendable {
    public var id: String
    public var name: String
    public var version: String?
}
