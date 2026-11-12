/// A tornado touching down, in pixels, 16 to a tile.
public struct Tornado: Hashable, Sendable {
    public internal(set) var x: Int
    public internal(set) var y: Int
    /// 1 to 3, spinning up and down.
    public internal(set) var frame: Int
    public internal(set) var movesLeft: Int
    public internal(set) var rising = false
}

/// The monster, come up out of the water for the city's most polluted place.
public struct Monster: Hashable, Sendable {
    public internal(set) var x: Int
    public internal(set) var y: Int
    /// 1 to 12 walking, three to each of four diagonals, and 13 to 16 turning.
    public internal(set) var frame: Int
    public internal(set) var destinationX: Int
    public internal(set) var destinationY: Int
    public internal(set) var homeX: Int
    public internal(set) var homeY: Int
    /// Which way its legs are going through their frames: 1 or -1.
    public internal(set) var step: Int
    public internal(set) var headingHome = false
}

/// The tornado and the monster, as Micropolis' sprites: they wreck what they pass and bring down
/// the vehicles they meet.
public final class Menaces {
    public static let tornadoMoves = 200
    public static let monsterArrival = 60

    private static let tileSize = Vehicles.tileSize
    private static let monsterTries = 300
    private static let tornadoEndChance = 501
    private static let monsterTurnChance = 11
    // The tornado's moves, one picked at random each time, as Micropolis' CDx and CDy.
    private static let tornadoDx = [2, 3, 2, 0, -2, -3]
    private static let tornadoDy = [-2, 0, 2, 3, 2, 0]
    // The monster walks diagonally, 0 to 3 northeast and on clockwise; 4 is standing to turn.
    private static let monsterDx = [2, 2, -2, -2, 0]
    private static let monsterDy = [-2, 2, 2, -2, 0]
    // Micropolis' ND1, ND2, nn1, and nn2: the frames it turns through, a quarter turn either way.
    private static let turnLeft = [0, 1, 2, 3]
    private static let turnRight = [1, 2, 3, 0]
    private static let fromTurnLeft = [2, 5, 8, 11]
    private static let fromTurnRight = [11, 2, 5, 8]

    public internal(set) var tornado: Tornado?
    public internal(set) var monster: Monster?

    public init() {}

    /// Where the helicopter goes once its flight is over: the monster, or else the tornado.
    public var quarry: (x: Int, y: Int)? {
        if let monster { return (monster.x, monster.y) }
        if let tornado { return (tornado.x, tornado.y) }
        return nil
    }

    /// A tornado touches down somewhere away from the edges, or the one blowing lasts its full
    /// time again. Not allowed, only the dice for where are rolled. Says whether a new one came.
    func callTornado(map: TileMap, random: inout SeededGenerator, allowed: Bool = true) -> Bool {
        if tornado != nil {
            if allowed { tornado?.movesLeft = Self.tornadoMoves }
            return false
        }
        var (x, y) = (map.width / 2, map.height / 2)
        if map.width >= 20 && map.height >= 20 {
            x = random.next(in: 0..<(map.width - 19)) + 10
            y = random.next(in: 0..<(map.height - 19)) + 10
        }
        guard allowed else { return false }
        let (px, py) = Vehicles.centre(x, y)
        tornado = Tornado(x: px, y: py, frame: 1, movesLeft: Self.tornadoMoves)
        return true
    }

    /// The monster comes up out of a channel and makes for the most polluted place, or the one
    /// about makes for it again. Not allowed, only the dice for where are rolled.
    func callMonster(map: TileMap, random: inout SeededGenerator, mostPolluted: (x: Int, y: Int), allowed: Bool = true) -> Bool {
        let (destinationX, destinationY) = Vehicles.centre(mostPolluted.x, mostPolluted.y)
        if monster != nil {
            // Micropolis sends it to the place's tile number rather than its pixels here, a slip
            // that sends it to the map's corner; CityLimits sends it to the place.
            if allowed {
                monster?.destinationX = destinationX
                monster?.destinationY = destinationY
                monster?.headingHome = false
            }
            return false
        }
        var (x, y) = (map.width / 2, map.height / 2)
        if map.width >= 20 && map.height >= 10 {
            for _ in 0..<Self.monsterTries {
                let tx = random.next(in: 0..<(map.width - 19)) + 10
                let ty = random.next(in: 0..<(map.height - 9)) + 5
                if Vehicles.isChannel(map, tx, ty) {
                    (x, y) = (tx, ty)
                    break
                }
            }
        }
        guard allowed else { return false }
        let (px, py) = Vehicles.centre(x, y)
        let east = x > map.width / 2
        let south = y > map.height / 2
        monster = Monster(
            x: px, y: py, frame: east ? (south ? 10 : 7) : (south ? 1 : 4),
            destinationX: destinationX, destinationY: destinationY, homeX: px, homeY: py, step: 1)
        return true
    }

    /// Moves the monster and the tornado once, adding the tiles they pass to `trampled`.
    func move(
        map: TileMap, random: inout SeededGenerator, vehicles: Vehicles, airCrashes: Bool, shipwrecks: Bool,
        events: inout [VehicleEvent], trampled: inout [(x: Int, y: Int)]
    ) {
        moveMonster(map, &random, vehicles, airCrashes, shipwrecks, &events, &trampled)
        moveTornado(map, &random, vehicles, airCrashes, shipwrecks, &events, &trampled)
    }

    // As Micropolis' monster: it walks diagonally, two pixels a move, its legs going back and
    // forth through three frames, and now and then stops to turn a quarter toward where it heads.
    // Near the most polluted place it turns for home, and there it goes. Off the map, it's gone.
    private func moveMonster(
        _ map: TileMap, _ random: inout SeededGenerator, _ vehicles: Vehicles, _ airCrashes: Bool, _ shipwrecks: Bool,
        _ events: inout [VehicleEvent], _ trampled: inout [(x: Int, y: Int)]
    ) {
        guard var monster else { return }
        var d = (monster.frame - 1) / 3
        var z = (monster.frame - 1) % 3
        if d < 4 {
            if z == 2 { monster.step = -1 }
            if z == 0 { monster.step = 1 }
            z += monster.step
            if Vehicles.distance(monster.x, monster.y, monster.destinationX, monster.destinationY) < Self.monsterArrival {
                if monster.headingHome {
                    self.monster = nil
                    return
                }
                (monster.destinationX, monster.destinationY, monster.headingHome) = (monster.homeX, monster.homeY, true)
            }
            let heading = (Vehicles.direction(monster.x, monster.y, monster.destinationX, monster.destinationY) - 1) / 2
            if heading != d && random.next(in: 0..<Self.monsterTurnChance) == 0 {
                z = random.next(in: 0..<2) == 0 ? Self.turnLeft[d] : Self.turnRight[d]
                d = 4
            }
        } else if random.next(in: 0..<4) == 0 {
            let turned = (monster.frame - 13) % 4
            let frame = random.next(in: 0..<2) == 0 ? Self.fromTurnLeft[turned] : Self.fromTurnRight[turned]
            (d, z) = ((frame - 1) / 3, (frame - 1) % 3)
        } else {
            d = 4
        }
        monster.frame = d * 3 + z + 1
        monster.x += Self.monsterDx[d]
        monster.y += Self.monsterDy[d]
        guard map.contains(monster.x / Self.tileSize, monster.y / Self.tileSize) else {
            self.monster = nil
            return
        }
        self.monster = monster
        Self.bringDown(monster.x, monster.y, vehicles, airCrashes, shipwrecks, &events)
        trampled.append((monster.x / Self.tileSize, monster.y / Self.tileSize))
    }

    // As Micropolis' tornado: it spins through its three frames and lurches one of six ways at
    // random. Off the map it's gone, and once its time is up it blows itself out one move in 501.
    private func moveTornado(
        _ map: TileMap, _ random: inout SeededGenerator, _ vehicles: Vehicles, _ airCrashes: Bool, _ shipwrecks: Bool,
        _ events: inout [VehicleEvent], _ trampled: inout [(x: Int, y: Int)]
    ) {
        guard var tornado else { return }
        if tornado.frame == 2 {
            tornado.frame = tornado.rising ? 3 : 1
        } else {
            tornado.rising = tornado.frame == 1
            tornado.frame = 2
        }
        if tornado.movesLeft > 0 { tornado.movesLeft -= 1 }
        Self.bringDown(tornado.x, tornado.y, vehicles, airCrashes, shipwrecks, &events)
        let way = random.next(in: 0..<Self.tornadoDx.count)
        tornado.x += Self.tornadoDx[way]
        tornado.y += Self.tornadoDy[way]
        guard map.contains(tornado.x / Self.tileSize, tornado.y / Self.tileSize) else {
            self.tornado = nil
            return
        }
        if tornado.movesLeft == 0 && random.next(in: 0..<Self.tornadoEndChance) == 0 {
            self.tornado = nil
            return
        }
        self.tornado = tornado
        trampled.append((tornado.x / Self.tileSize, tornado.y / Self.tileSize))
    }

    // Micropolis' collision: a vehicle within 30 pixels comes down. With the switch for its kind
    // off, it passes unharmed.
    private static func bringDown(_ x: Int, _ y: Int, _ vehicles: Vehicles, _ airCrashes: Bool, _ shipwrecks: Bool, _ events: inout [VehicleEvent]) {
        let near = { (vx: Int, vy: Int) in Vehicles.distance(x, y, vx, vy) < Vehicles.collisionDistance }
        if airCrashes, let plane = vehicles.plane, near(plane.x, plane.y), let at = vehicles.downPlane() {
            events.append(VehicleEvent(kind: .planeCrash, x: at.x, y: at.y))
        }
        if airCrashes, let copter = vehicles.helicopter, near(copter.x, copter.y), let at = vehicles.downHelicopter() {
            events.append(VehicleEvent(kind: .helicopterCrash, x: at.x, y: at.y))
        }
        if shipwrecks, let ship = vehicles.ship, near(ship.x, ship.y), let at = vehicles.sinkShip() {
            events.append(VehicleEvent(kind: .shipwreck, x: at.x, y: at.y))
        }
    }
}
