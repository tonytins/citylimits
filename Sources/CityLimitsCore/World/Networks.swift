/// The three things laid in lines (roads, rail, and power lines) and which way each tile of them
/// runs. A crossing carries two of them, each running one way.
public enum Network: Sendable {
    case road
    case rail
    case power

    /// Whether a tile carries this network toward one side.
    public func runs(_ state: TileState, toward side: Sides) -> Bool {
        switch (self, state.use) {
        case (.road, .road), (.rail, .rail), (.power, .powerLine):
            true
        case (.road, .roadAcrossPowerLine), (.road, .railDownRoad),
            (.rail, .railAcrossRoad), (.rail, .railAcrossPowerLine),
            (.power, .roadDownPowerLine), (.power, .railDownPowerLine):
            side == .east || side == .west
        case (.road, .roadDownPowerLine), (.road, .railAcrossRoad),
            (.rail, .railDownRoad), (.rail, .railDownPowerLine),
            (.power, .roadAcrossPowerLine), (.power, .railAcrossPowerLine):
            side == .north || side == .south
        case (.power, _):
            // A zone or building takes power from any side.
            state.hasFootprint
        default:
            false
        }
    }

    /// Whether a tile carries this network at all.
    public func isOn(_ state: TileState) -> Bool {
        switch self {
        case .road: state.hasRoad
        case .rail: state.isRail
        case .power: state.hasPowerLine
        }
    }

    /// Which neighbours this network joins a tile to, reading tiles through `lookup`, so a plan
    /// that hasn't been built yet can be asked too.
    public func connections(_ x: Int, _ y: Int, lookup: (Int, Int) -> TileState) -> Sides {
        let state = lookup(x, y)
        guard isOn(state) else { return [] }
        var joined: Sides = []
        for step in Sides.steps
        where runs(state, toward: step.side) && runs(lookup(x + step.dx, y + step.dy), toward: step.side.opposite) {
            joined.insert(step.side)
        }
        return joined
    }

    /// Which neighbours this network joins a tile to on the map.
    public func connections(_ x: Int, _ y: Int, on map: TileMap) -> Sides {
        connections(x, y) { map.state(atOrDefault: $0, $1) }
    }
}

/// Where trips can go: every road and rail tile, kept as a flat grid so the simulation can look
/// one up quickly. It's rebuilt from the map when something changes it.
public final class RoadNetwork {
    private var roads: [Bool] = []
    private var width = 0
    private var height = 0
    public private(set) var isDirty = true
    public private(set) var roadCount = 0

    public init() {}

    public func markDirty() { isDirty = true }

    public func ensureCurrent(_ map: TileMap) {
        if isDirty { rebuild(map) }
    }

    public func rebuild(_ map: TileMap) {
        width = map.width
        height = map.height
        if roads.count != width * height {
            roads = Array(repeating: false, count: width * height)
        } else {
            for index in roads.indices { roads[index] = false }
        }
        roadCount = 0
        for y in 0..<height {
            for x in 0..<width where map[x, y].carriesTrips {
                roads[y * width + x] = true
                roadCount += 1
            }
        }
        isDirty = false
    }

    public func hasRoad(_ x: Int, _ y: Int) -> Bool {
        x >= 0 && y >= 0 && x < width && y < height && roads[y * width + x]
    }

    /// Whether a road or rail touches the outside of a zone, given its centre.
    public func isZoneServed(_ centerX: Int, _ centerY: Int) -> Bool {
        for d in -1...1 {
            if hasRoad(centerX + d, centerY - 2) || hasRoad(centerX + 2, centerY + d)
                || hasRoad(centerX + d, centerY + 2) || hasRoad(centerX - 2, centerY + d)
            {
                return true
            }
        }
        return false
    }
}

/// How parks look: most are woods, one in five a fountain, picked by place so a park keeps its look.
public enum Parks {
    public static let looks = 5
    public static let fountainLook = looks - 1

    public static func look(_ x: Int, _ y: Int) -> Int {
        // A small integer hash, so neighbouring tiles don't fall into a visible pattern. It wraps
        // on 32 bits, as the C# build's did.
        let mixed = (Int32(truncatingIfNeeded: x) &* 73_856_093) ^ (Int32(truncatingIfNeeded: y) &* 19_349_663)
        var hash = UInt32(bitPattern: mixed)
        hash ^= hash >> 13
        hash &*= 0x5BD1_E995
        hash ^= hash >> 15
        return Int(hash % UInt32(looks))
    }

    public static func isWooded(_ x: Int, _ y: Int) -> Bool { look(x, y) != fountainLook }
}
