/// A side of the map, and the neighbouring city beyond it.
public enum Side: Int, CaseIterable, Sendable {
    case north
    case east
    case south
    case west

    /// Its name in saves and specs.
    public var name: String {
        switch self {
        case .north: "north"
        case .east: "east"
        case .south: "south"
        case .west: "west"
        }
    }
}

/// What runs from the city to a neighbour across a side of the map.
public struct Connections: OptionSet, Hashable, Sendable {
    public let rawValue: UInt8
    public init(rawValue: UInt8) { self.rawValue = rawValue }

    public static let road = Connections(rawValue: 1)
    public static let rail = Connections(rawValue: 2)
    public static let power = Connections(rawValue: 4)
}

/// Which way the power goes in a deal.
public enum DealKind: Sendable {
    case cityBuys
    case citySells
}

/// A neighbouring city: its name and how big it is, picked by the seed, after SimCity 3000's.
public struct Neighbour: Hashable, Sendable {
    public let side: Side
    public let name: String
    /// Its people when the city has none.
    public let start: Int
    /// How much it grows with the city, in percent of the city's people.
    public let share: Int

    public func population(beside population: Int) -> Int { start + max(0, population) * share / 100 }
}

/// A power deal in force, until its end step.
public struct Deal: Hashable, Sendable {
    public var side: Side
    public var kind: DealKind
    /// Tiles' worth of power.
    public var power: Int
    /// A year's money.
    public var money: Int
    public var endStep: Int
}

/// A deal a neighbour has offered, open until it expires.
public struct Offer: Hashable, Sendable {
    public var side: Side
    public var kind: DealKind
    public var power: Int
    public var money: Int
    public var years: Int
    public var expiresStep: Int
}

/// What the news about a neighbour is.
public enum NeighbourNewsKind: Sendable {
    case offer
    case accepted
    case declined
    case lapsed
    case ended
    case broken
}

public struct NeighbourNews: Hashable, Sendable {
    public var neighbour: Neighbour
    public var kind: NeighbourNewsKind
    public var deal: DealKind
    public var power: Int
    public var money: Int
}

/// The cities beyond the map's edges, after SimCity 3000's neighbours: roads and rail to them lift
/// demand, and a power line to one lets the city trade power under deals they offer.
public final class CityNeighbours {
    public static let dealPopulation = 2000
    public static let termYears = 10
    public static let offerCounts = CityCalendar.countsPerMonth * 3
    public static let grudgeYears = 5
    public static let powerSold = 300
    public static let buyPricePer100 = 60
    public static let sellPricePer100 = 40
    public static let leastSpare = 400
    public static let mostBought = 500
    public static let roadDemand = 20
    public static let railDemand = 30

    // Names for the cities beyond the edges, picked by the seed.
    private static let names = [
        "Westbrook", "Eastmoor", "Northgate", "Southvale", "Riverton", "Pinecrest", "Ashford", "Millbury",
        "Clearwater", "Oakridge", "Fairhaven", "Stonebridge", "Brookfield", "Lakemont", "Hillcrest", "Maplewood",
        "Kingsport", "Elmstead", "Redfield", "Silverton", "Greenbay", "Harborview", "Copperton", "Windmere",
    ]

    private let map: TileMap
    /// The four neighbours, by side.
    public let all: [Neighbour]
    public internal(set) var deals: [Deal] = []
    public internal(set) var offer: Offer?
    var grudgeUntil = [0, 0, 0, 0]

    public init(map: TileMap, seed: Int32) {
        self.map = map
        // Four names, none twice, and a size for each, from a hash of the seed alone.
        var taken = Set<Int>()
        all = Side.allCases.map { side in
            let hash = Self.mix(UInt64(UInt32(bitPattern: seed)) << 2 | UInt64(side.rawValue))
            var name = Int(hash % UInt64(Self.names.count))
            while !taken.insert(name).inserted { name = (name + 1) % Self.names.count }
            return Neighbour(side: side, name: Self.names[name], start: 2000 + Int((hash >> 16) % 8001), share: 50 + Int((hash >> 32) % 101))
        }
    }

    public func neighbour(_ side: Side) -> Neighbour { all[side.rawValue] }

    public func deal(with side: Side) -> Deal? { deals.first { $0.side == side } }

    /// Whether a neighbour won't deal for a while after the city broke a deal with it.
    public func holdsGrudge(_ side: Side, at step: Int) -> Bool { step < grudgeUntil[side.rawValue] }

    /// What runs off the map on one side.
    public func connections(to side: Side) -> Connections {
        var found: Connections = []
        let count = side == .north || side == .south ? map.width : map.height
        for index in 0..<count {
            let (x, y) = switch side {
            case .north: (index, 0)
            case .south: (index, map.height - 1)
            case .west: (0, index)
            case .east: (map.width - 1, index)
            }
            let state = map[x, y]
            if state.hasRoad { found.insert(.road) }
            if state.isRail { found.insert(.rail) }
            if state.hasPowerLine { found.insert(.power) }
        }
        return found
    }

    /// What roads and rail to the neighbours add to demand each time it moves.
    func demandNudge() -> (residential: Int, commercial: Int, industrial: Int) {
        var (commercial, industrial) = (0, 0)
        for side in Side.allCases {
            let connections = connections(to: side)
            if connections.contains(.road) {
                commercial += Self.roadDemand
                industrial += Self.roadDemand
            }
            if connections.contains(.rail) { industrial += Self.railDemand }
        }
        return (0, commercial, industrial)
    }

    /// The power the deals bring in, or send out below zero, over lines still connected.
    func powerTraded() -> Int {
        deals.filter { connections(to: $0.side).contains(.power) }
            .reduce(0) { $0 + ($1.kind == .cityBuys ? $1.power : -$1.power) }
    }

    /// A year's money from the deals over lines still connected.
    func yearlyMoney() -> (income: Int, cost: Int) {
        var (income, cost) = (0, 0)
        for deal in deals where connections(to: deal.side).contains(.power) {
            if deal.kind == .cityBuys { cost += deal.money } else { income += deal.money }
        }
        return (income, cost)
    }

    /// Ends the deals whose term is up, and lets an unanswered offer lapse.
    func expire(at step: Int) -> (ended: [Deal], lapsed: Offer?) {
        let ended = deals.filter { step >= $0.endStep }
        deals.removeAll { step >= $0.endStep }
        var lapsed: Offer?
        if let open = offer, step >= open.expiresStep {
            lapsed = open
            offer = nil
        }
        return (ended, lapsed)
    }

    /// A neighbour with a power line to the city offers to sell it power in a brownout, or to buy
    /// its spare.
    func propose(population: Int, brownedOut: Bool, capacity: Int, used: Int, at step: Int) -> Offer? {
        guard offer == nil, population >= Self.dealPopulation else { return nil }
        let spare = capacity - used
        let (kind, power): (DealKind, Int)
        if brownedOut {
            (kind, power) = (.cityBuys, Self.powerSold)
        } else if spare >= Self.leastSpare {
            (kind, power) = (.citySells, min(Self.mostBought, spare / 2 / 50 * 50))
        } else {
            return nil
        }
        guard let neighbour = all.first(where: {
            deal(with: $0.side) == nil && !holdsGrudge($0.side, at: step) && connections(to: $0.side).contains(.power)
        }) else { return nil }
        let money = power / 100 * (kind == .cityBuys ? Self.buyPricePer100 : Self.sellPricePer100)
        offer = Offer(
            side: neighbour.side, kind: kind, power: power, money: money, years: Self.termYears,
            expiresStep: step + Self.offerCounts * CityCalendar.stepsPerCount)
        return offer
    }

    func accept(at step: Int) -> Deal? {
        guard let open = offer else { return nil }
        let deal = Deal(side: open.side, kind: open.kind, power: open.power, money: open.money, endStep: step + open.years * CityCalendar.stepsPerYear)
        deals.append(deal)
        offer = nil
        return deal
    }

    func decline() -> Offer? {
        defer { offer = nil }
        return offer
    }

    func breakDeal(with side: Side, at step: Int) -> Deal? {
        guard let index = deals.firstIndex(where: { $0.side == side }) else { return nil }
        grudgeUntil[side.rawValue] = step + Self.grudgeYears * CityCalendar.stepsPerYear
        return deals.remove(at: index)
    }

    // SplitMix64's finaliser: the same number from the same seed, on every machine.
    private static func mix(_ value: UInt64) -> UInt64 {
        var z = value &+ 0x9E37_79B9_7F4A_7C15
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }
}
