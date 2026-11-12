/// How busy a road looks.
public enum TrafficLevel: Int, Sendable {
    case none
    case light
    case heavy
}

/// The traffic trips leave on the roads, in 2 by 2 blocks as Micropolis keeps it, and the level
/// each road shows from the last time the scan passed it.
public final class TrafficMap {
    /// What one trip adds to each block it passes.
    public static let perTrip = 50
    /// The most a block holds.
    public static let most = 240

    // A busy block fades faster: by 34 above this, by 24 above the next, and to nothing below it.
    private static let busy = 200
    private static let busyFade = 34
    private static let quiet = 24
    private static let quietFade = 24
    // A road shows light traffic from this much on its block, and heavy from the next.
    private static let lightFrom = 64
    private static let heavyFrom = 192
    // Micropolis scales the average traffic on the built-up land by this, so it reads as a problem
    // from about 60.
    private static let averageScale = 2.4

    private let map: TileMap
    let blocksWide: Int
    let blocksHigh: Int
    var blocks: [Int]
    var levels: [TrafficLevel]

    /// The year's average traffic, as Micropolis' evaluation works it out.
    public internal(set) var average = 0
    /// The share of a trip's traffic it lays, in percent; parking fines lower it.
    public var tripPercent = 100

    public init(map: TileMap) {
        self.map = map
        blocksWide = (map.width + 1) / 2
        blocksHigh = (map.height + 1) / 2
        blocks = Array(repeating: 0, count: blocksWide * blocksHigh)
        levels = Array(repeating: .none, count: map.width * map.height)
        // As in Micropolis, where the level is part of a road's drawing, a tile that changes, such
        // as a road bulldozed or crossed by a line, starts again quiet.
        map.watch { [weak self] x, y in
            guard let self else { return }
            self.levels[y * self.map.width + x] = .none
        }
    }

    public func level(_ x: Int, _ y: Int) -> TrafficLevel {
        map.contains(x, y) ? levels[y * map.width + x] : .none
    }

    public func density(_ x: Int, _ y: Int) -> Int {
        x >= 0 && y >= 0 && x / 2 < blocksWide && y / 2 < blocksHigh ? blocks[(y / 2) * blocksWide + x / 2] : 0
    }

    public var busiest: Int { blocks.max() ?? 0 }

    /// Lays a trip's traffic along its route; Micropolis lays it on roads, bridges, and
    /// crossings, and nothing else a route passes.
    public func add(route: [(x: Int, y: Int)]) {
        let laid = tripPercent == 100 ? Self.perTrip : Self.perTrip * tripPercent / 100
        for (x, y) in route where map.contains(x, y) && map[x, y].isRoad {
            let block = (y / 2) * blocksWide + x / 2
            blocks[block] = min(blocks[block] + laid, Self.most)
        }
    }

    /// Updates the level a road shows from its block, and returns the level it showed before.
    func show(_ x: Int, _ y: Int) -> TrafficLevel {
        let index = y * map.width + x
        let shown = levels[index]
        let busy = density(x, y)
        levels[index] = busy < Self.lightFrom ? .none : busy < Self.heavyFrom ? .light : .heavy
        return shown
    }

    func updateAverage(_ maps: CityMaps) {
        // Micropolis starts the count at one, so a city with no land value averages nothing.
        var total = 0
        var count = 1
        for y in 0..<map.height {
            for x in 0..<map.width where maps.landValue(x, y) != 0 {
                total += density(x, y)
                count += 1
            }
        }
        average = Int((Double(total) / Double(count) * Self.averageScale).rounded(.toNearestOrAwayFromZero))
    }

    public func fade() {
        for index in blocks.indices {
            let value = blocks[index]
            blocks[index] = value > Self.busy ? value - Self.busyFade : value > Self.quiet ? value - Self.quietFade : 0
        }
    }
}

/// Where a trip ended up.
public enum TripOutcome: Sendable {
    case noRoad
    case noDestination
    case arrived
}

/// Drives a zone's trip as Micropolis does: from a road at the zone's edge, a random walk along
/// roads and rail, never straight back, of up to 30 steps, looking for a zone of a kind it wants.
public final class TripPlanner {
    public static let maxDistance = 30

    // The 12 tiles just outside a zone's edges, in the order a trip looks for a road: across the
    // top, down the right, back along the bottom, and up the left.
    private static let edge: [(dx: Int, dy: Int)] = [
        (-1, -2), (0, -2), (1, -2), (2, -1), (2, 0), (2, 1), (1, 2), (0, 2), (-1, 2), (-2, 1), (-2, 0), (-2, -1),
    ]
    // North, east, south, and west; a direction's opposite is two along.
    private static let directions: [(dx: Int, dy: Int)] = [(0, -1), (1, 0), (0, 1), (-1, 0)]

    private let traffic: TrafficMap?
    /// Every other tile of the last trip that arrived, the tiles that take its traffic.
    public private(set) var route: [(x: Int, y: Int)] = []

    public init(traffic: TrafficMap? = nil) {
        self.traffic = traffic
    }

    public func drive(map: TileMap, definitions: CityDefinitions, random: inout SeededGenerator, centerX: Int, centerY: Int) -> TripOutcome {
        route.removeAll(keepingCapacity: true)
        let spec = definitions.zone(map[centerX, centerY].use)
        guard let start = Self.edge.first(where: { map.state(atOrDefault: centerX + $0.dx, centerY + $0.dy).carriesTrips }) else {
            return .noRoad
        }

        var (x, y) = (centerX + start.dx, centerY + start.dy)
        var cameFrom: Int? = nil
        var step = 0
        while step < Self.maxDistance {
            if self.step(map, &random, &x, &y, &cameFrom, step) {
                if Self.isBesideDestination(map, spec, x, y) {
                    traffic?.add(route: route)
                    return .arrived
                }
            } else if !route.isEmpty {
                // A dead end: back up, at the cost of three steps.
                route.removeLast()
                step += 3
            } else {
                return .noDestination
            }
            step += 1
        }
        return .noDestination
    }

    // One step along the road or rail: the directions are tried from a random one round, never
    // straight back.
    private func step(_ map: TileMap, _ random: inout SeededGenerator, _ x: inout Int, _ y: inout Int, _ cameFrom: inout Int?, _ step: Int) -> Bool {
        let first = random.next(in: 0..<Self.directions.count)
        for turn in 0..<Self.directions.count {
            let direction = (first + turn) % Self.directions.count
            if direction == cameFrom { continue }
            let (dx, dy) = Self.directions[direction]
            guard map.state(atOrDefault: x + dx, y + dy).carriesTrips else { continue }
            x += dx
            y += dy
            cameFrom = (direction + 2) % Self.directions.count
            if step % 2 == 1 { route.append((x, y)) }
            return true
        }
        return false
    }

    private static func isBesideDestination(_ map: TileMap, _ spec: ZoneSpec, _ x: Int, _ y: Int) -> Bool {
        directions.contains { map.contains(x + $0.dx, y + $0.dy) && isDestination(map, spec, x + $0.dx, y + $0.dy) }
    }

    // A zoned tile of a kind the trip is looking for; a residential one only once it has people.
    private static func isDestination(_ map: TileMap, _ spec: ZoneSpec, _ x: Int, _ y: Int) -> Bool {
        let state = map[x, y]
        guard state.isZoned, spec.tripsTo.contains(state.use) else { return false }
        guard state.use == .residential else { return true }
        return state.hasHouse || map.centerState(x, y).development > 0
    }
}

/// The frame the roads' traffic is drawn at: Micropolis' four frames, faster at higher speeds.
public struct TrafficAnimation: Sendable {
    public static let frames = 4

    public private(set) var frame = 0
    private var carried = 0.0

    public init() {}

    public static func secondsPerFrame(_ speed: GameSpeed) -> Double {
        switch speed {
        case .paused: 0
        case .normal: 0.125
        case .fast, .superFast: 0.025
        }
    }

    /// Moves the frame on, and returns how many frames it moved.
    @discardableResult
    public mutating func advance(_ elapsedSeconds: Double, speed: GameSpeed) -> Int {
        let perFrame = Self.secondsPerFrame(speed)
        guard perFrame > 0, elapsedSeconds > 0, elapsedSeconds.isFinite else { return 0 }
        // A long stall moves the cars on a few frames at most, not one for every frame it missed.
        carried = min(carried + elapsedSeconds, perFrame * Double(Self.frames))
        let due = Int(carried / perFrame)
        carried -= Double(due) * perFrame
        frame = (frame + due) % Self.frames
        return due
    }
}
