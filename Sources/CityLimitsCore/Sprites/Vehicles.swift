/// A plane in the air, as Micropolis flies it: positions are in pixels, 16 to a tile, and its
/// frame is one of eight headings, 1 to 8 from north clockwise, or 9 to 11 while taking off.
public struct Plane: Hashable, Sendable {
    public internal(set) var x: Int
    public internal(set) var y: Int
    public internal(set) var frame: Int
    public internal(set) var destinationX: Int
    public internal(set) var destinationY: Int
}

/// The traffic helicopter, which flies from the airport for a while, then goes home.
public struct Helicopter: Hashable, Sendable {
    public internal(set) var x: Int
    public internal(set) var y: Int
    public internal(set) var frame: Int
    public internal(set) var destinationX: Int
    public internal(set) var destinationY: Int
    public internal(set) var homeX: Int
    public internal(set) var homeY: Int
    public internal(set) var movesLeft: Int
}

/// A ship called in by a seaport, sailing the channels.
public struct Ship: Hashable, Sendable {
    public internal(set) var x: Int
    public internal(set) var y: Int
    public internal(set) var frame: Int
    public internal(set) var newDirection: Int
    public internal(set) var wayBack: Int
    public internal(set) var count: Int
}

/// What came of the vehicles' move, for the city to answer.
public struct VehicleEvent: Hashable, Sendable {
    public enum Kind: Sendable {
        case planeCrash
        case helicopterCrash
        case shipwreck
        case heavyTraffic
    }

    public var kind: Kind
    public var x: Int
    public var y: Int
}

/// The city's plane, helicopter, and ship, as Micropolis keeps its sprites: at most one of each.
public final class Vehicles {
    public static let cycleLength = 960
    public static let collisionDistance = 30
    public static let helicopterFlight = 1500
    public static let heavyTraffic = 170
    static let tileSize = 16

    // Micropolis' directions are 1 to 8, north and on clockwise; 9 to 11 are the plane taking off.
    private static let planeDx = [0, 0, 6, 8, 6, 0, -6, -8, -6, 8, 8, 8]
    private static let planeDy = [0, -8, -6, 0, 6, 8, 6, 0, -6, 0, 0, 0]
    private static let helicopterDx = [0, 0, 3, 5, 3, 0, -3, -5, -3]
    private static let helicopterDy = [0, -5, -3, 0, 3, 5, 3, 0, -3]
    private static let shipTileDx = [0, 0, 1, 1, 1, 0, -1, -1, -1]
    private static let shipTileDy = [0, -1, -1, 0, 1, 1, 1, 0, -1]
    private static let shipDx = [0, 0, 2, 2, 2, 0, -2, -2, -2]
    private static let shipDy = [0, -2, -2, 0, 2, 2, 2, 0, -2]
    private static let directions = [0, 3, 2, 1, 3, 4, 5, 7, 6, 5, 7, 8, 1]
    // The way a ship faces as it comes in from each edge: south from the north, and so on.
    private static let fromNorth = 5
    private static let fromEast = 7
    private static let fromSouth = 1
    private static let fromWest = 3
    // A ship searching for a way has none to come back by.
    private static let noWayBack = 10

    public internal(set) var plane: Plane?
    public internal(set) var helicopter: Helicopter?
    public internal(set) var ship: Ship?
    public internal(set) var cycle = 0

    public init() {}

    /// A powered airport sends up a plane now and then, and the helicopter.
    func visitAirport(_ x: Int, _ y: Int, map: TileMap, random: inout SeededGenerator) {
        if random.next(in: 0..<6) == 0 && plane == nil {
            let (px, py) = Self.centre(x, y)
            let east = x <= map.width - 20
            plane = Plane(x: px, y: py, frame: east ? 11 : 7, destinationX: east ? px + 200 : px - 200, destinationY: py)
        }
        if random.next(in: 0..<13) == 0 && helicopter == nil {
            let (hx, hy) = Self.centre(x, y)
            let destinationX = random.next(in: 0..<map.width) * Self.tileSize + 8
            let destinationY = random.next(in: 0..<map.height) * Self.tileSize + 8
            helicopter = Helicopter(
                x: hx, y: hy, frame: 5, destinationX: destinationX, destinationY: destinationY,
                homeX: hx, homeY: hy, movesLeft: Self.helicopterFlight)
        }
    }

    /// A powered seaport calls a ship in from a channel at one edge of the map, picked at random.
    func visitSeaport(map: TileMap, random: inout SeededGenerator) {
        guard ship == nil else { return }
        let found: (x: Int, y: Int, facing: Int)?
        switch random.next(in: 0..<4) {
        case 0:
            found = (4..<max(4, map.width - 2)).first { Self.isChannel(map, $0, 0) }.map { ($0, 0, Self.fromNorth) }
        case 1:
            found = (1..<max(1, map.height - 2)).first { Self.isChannel(map, 0, $0) }.map { (0, $0, Self.fromWest) }
        case 2:
            found = (4..<max(4, map.width - 2)).first { Self.isChannel(map, $0, map.height - 1) }.map { ($0, map.height - 1, Self.fromSouth) }
        default:
            found = (1..<max(1, map.height - 2)).first { Self.isChannel(map, map.width - 1, $0) }.map { (map.width - 1, $0, Self.fromEast) }
        }
        if let found {
            let (sx, sy) = Self.centre(found.x, found.y)
            ship = Ship(x: sx, y: sy, frame: found.facing, newDirection: found.facing, wayBack: Self.noWayBack, count: 1)
        }
    }

    /// Moves every vehicle once. Micropolis moves its sprites twice a step, so `move` is 0 or 1.
    func move(
        step: Int, move: Int, map: TileMap, traffic: TrafficMap, random: inout SeededGenerator,
        airCrashes: Bool, shipwrecks: Bool, events: inout [VehicleEvent], quarry: (x: Int, y: Int)? = nil
    ) {
        cycle = (step * 2 + move) % Self.cycleLength
        movePlane(map, &random, airCrashes, &events)
        moveHelicopter(traffic, &random, &events, quarry)
        moveShip(map, &random, shipwrecks, &events)
    }

    private func movePlane(_ map: TileMap, _ random: inout SeededGenerator, _ airCrashes: Bool, _ events: inout [VehicleEvent]) {
        guard var plane else { return }
        var z = plane.frame
        if cycle % 5 == 0 {
            if z > 8 {
                // Still taking off: the last take-off frame turns it east.
                z -= 1
                if z < 9 { z = 3 }
            } else {
                z = Self.turn(z, toward: Self.direction(plane.x, plane.y, plane.destinationX, plane.destinationY))
            }
            plane.frame = z
        }
        if Self.distance(plane.x, plane.y, plane.destinationX, plane.destinationY) < 50 {
            plane.destinationX = random.next(in: 0..<map.width) * Self.tileSize + 8
            plane.destinationY = random.next(in: 0..<map.height) * Self.tileSize + 8
        }
        if airCrashes, let helicopter, Self.distance(plane.x, plane.y, helicopter.x, helicopter.y) < Self.collisionDistance {
            events.append(VehicleEvent(kind: .helicopterCrash, x: helicopter.x / Self.tileSize, y: helicopter.y / Self.tileSize))
            events.append(VehicleEvent(kind: .planeCrash, x: plane.x / Self.tileSize, y: plane.y / Self.tileSize))
            self.helicopter = nil
            self.plane = nil
            return
        }
        plane.x += Self.planeDx[z]
        plane.y += Self.planeDy[z]
        self.plane = plane
    }

    private func moveHelicopter(_ traffic: TrafficMap, _ random: inout SeededGenerator, _ events: inout [VehicleEvent], _ quarry: (x: Int, y: Int)?) {
        guard var helicopter else { return }
        if helicopter.movesLeft > 0 { helicopter.movesLeft -= 1 }
        // Its flight over, it chases the monster or the tornado, as Micropolis sends it to so it
        // comes down more often; with neither about, it heads home and lands there.
        if helicopter.movesLeft == 0, let quarry {
            (helicopter.destinationX, helicopter.destinationY) = quarry
        } else if helicopter.movesLeft == 0 {
            (helicopter.destinationX, helicopter.destinationY) = (helicopter.homeX, helicopter.homeY)
            if Self.distance(helicopter.x, helicopter.y, helicopter.homeX, helicopter.homeY) < Self.collisionDistance {
                self.helicopter = nil
                return
            }
        }
        if cycle % 200 == 0 {
            let (x, y) = (helicopter.x / Self.tileSize, helicopter.y / Self.tileSize)
            if traffic.density(x, y) > Self.heavyTraffic && random.next(in: 0..<8) == 0 {
                events.append(VehicleEvent(kind: .heavyTraffic, x: x, y: y))
            }
        }
        var z = helicopter.frame
        if cycle % 3 == 0 {
            z = Self.turn(z, toward: Self.direction(helicopter.x, helicopter.y, helicopter.destinationX, helicopter.destinationY))
            helicopter.frame = z
        }
        helicopter.x += Self.helicopterDx[z]
        helicopter.y += Self.helicopterDy[z]
        self.helicopter = helicopter
    }

    // As Micropolis' ship: every ninth move it turns toward its way, or, facing it, looks for the
    // next, at random among the eight, never straight back; between, it sails on two pixels. A
    // ship that looked and found only land runs aground, and one that leaves the map is gone.
    private func moveShip(_ map: TileMap, _ random: inout SeededGenerator, _ shipwrecks: Bool, _ events: inout [VehicleEvent]) {
        guard var ship else { return }
        var aground = false
        ship.count -= 1
        if ship.count <= 0 {
            ship.count = 9
            if ship.newDirection != ship.frame {
                ship.frame = Self.turn(ship.frame, toward: ship.newDirection)
                self.ship = ship
                return
            }
            let first = random.next(in: 0..<8)
            var found = false
            for tried in first..<(first + 8) {
                let z = tried % 8 + 1
                if z == ship.wayBack { continue }
                let (x, y) = (ship.x / Self.tileSize + Self.shipTileDx[z], ship.y / Self.tileSize + Self.shipTileDy[z])
                guard map.contains(x, y) else { continue }
                // The last tile looked at decides whether it runs aground, as in Micropolis.
                let state = map[x, y]
                aground = state.kind != .water
                if Self.isChannel(map, x, y) || Self.isBridge(state) || Self.isStraightThrough(state, wayBack: ship.wayBack, direction: z) {
                    ship.newDirection = z
                    ship.frame = Self.turn(ship.frame, toward: z)
                    ship.wayBack = z + 4 > 8 ? z - 4 : z + 4
                    found = true
                    aground = false
                    break
                }
            }
            if !found {
                ship.wayBack = Self.noWayBack
                ship.newDirection = random.next(in: 0..<8) + 1
            }
        } else if ship.frame == ship.newDirection {
            ship.x += Self.shipDx[ship.frame]
            ship.y += Self.shipDy[ship.frame]
        }

        if !map.contains(ship.x / Self.tileSize, ship.y / Self.tileSize) {
            self.ship = nil
            return
        }
        if aground && shipwrecks {
            events.append(VehicleEvent(kind: .shipwreck, x: ship.x / Self.tileSize, y: ship.y / Self.tileSize))
            self.ship = nil
            return
        }
        self.ship = ship
    }

    /// Brings the plane down, for a scenario's plane crash; returns the tile it fell on.
    func downPlane() -> (x: Int, y: Int)? {
        guard let plane else { return nil }
        self.plane = nil
        return (plane.x / Self.tileSize, plane.y / Self.tileSize)
    }

    /// Brings the helicopter down, as the tornado or the monster does when it catches it.
    func downHelicopter() -> (x: Int, y: Int)? {
        guard let helicopter else { return nil }
        self.helicopter = nil
        return (helicopter.x / Self.tileSize, helicopter.y / Self.tileSize)
    }

    /// Runs the ship aground, for a scenario's shipwreck; returns the tile it sank on.
    func sinkShip() -> (x: Int, y: Int)? {
        guard let ship else { return nil }
        self.ship = nil
        return (ship.x / Self.tileSize, ship.y / Self.tileSize)
    }

    static func centre(_ x: Int, _ y: Int) -> (x: Int, y: Int) { (x * tileSize + 8, y * tileSize + 8) }

    // Micropolis' channel is deep water; here, water with water on all four sides, and nothing on it.
    static func isChannel(_ map: TileMap, _ x: Int, _ y: Int) -> Bool {
        let state = map.state(atOrDefault: x, y)
        guard state.kind == .water && state.use == .none else { return false }
        return Sides.steps.allSatisfy { map.state(atOrDefault: x + $0.dx, y + $0.dy, fallback: TileState(.water)).kind == .water }
    }

    // A ship passes under a bridge, as under Micropolis' drawbridges.
    private static func isBridge(_ state: TileState) -> Bool { state.kind == .water && state.hasRoad }

    // It passes over a power line or a tunnel only going straight on, as in Micropolis.
    private static func isStraightThrough(_ state: TileState, wayBack: Int, direction: Int) -> Bool {
        let ahead = wayBack + 4 > 8 ? wayBack - 4 : wayBack + 4
        return direction == ahead && state.kind == .water && (state.hasPowerLine || state.isRail)
    }

    // Micropolis' getDir: which of the eight ways points most nearly from one place to another.
    static func direction(_ fromX: Int, _ fromY: Int, _ toX: Int, _ toY: Int) -> Int {
        var (dx, dy) = (toX - fromX, toY - fromY)
        var z = dx < 0 ? (dy < 0 ? 11 : 8) : (dy < 0 ? 2 : 5)
        (dx, dy) = (abs(dx), abs(dy))
        if dx * 2 < dy {
            z += 1
        } else if dy * 2 < dx {
            z -= 1
        }
        return (1...12).contains(z) ? directions[z] : 0
    }

    // Micropolis' turnTo: an eighth of a turn toward a way, the shorter way round.
    static func turn(_ facing: Int, toward: Int) -> Int {
        if facing == toward || toward == 0 { return facing }
        var turned = facing
        if facing < toward {
            turned += toward - facing < 4 ? 1 : -1
        } else {
            turned += facing - toward < 4 ? -1 : 1
        }
        return turned > 8 ? 1 : turned < 1 ? 8 : turned
    }

    static func distance(_ x0: Int, _ y0: Int, _ x1: Int, _ y1: Int) -> Int { abs(x0 - x1) + abs(y0 - y1) }
}
