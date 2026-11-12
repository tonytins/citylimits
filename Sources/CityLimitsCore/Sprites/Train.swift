/// Which drawing of the train to show.
public enum TrainFrame: Sendable {
    case down
    case across
    case turningNorthwest
    case turningNortheast
    case tunnel
}

/// The train that runs along the city's rail. It's for show: the game moves it with random numbers
/// of its own, never the city's, so it can't change what the city does.
public struct Train: Sendable {
    public static let peopleBeforeTrains = 20
    public static let setOutChance = 26

    private static let tileSize = 16
    private static let stepsPerTile = 4
    private static let stepPixels = tileSize / stepsPerTile
    // North, east, south, and west; a direction's opposite is two along.
    private static let directions: [(dx: Int, dy: Int)] = [(0, -1), (1, 0), (0, 1), (-1, 0)]

    private var direction: Int?
    private var step = 0
    public private(set) var isRunning = false
    /// In pixels, 16 to a tile.
    public private(set) var x = 0
    public private(set) var y = 0
    public private(set) var frame = TrainFrame.across

    public init() {}

    /// Moves the train a quarter of a tile, or now and then sets one out on a city with people.
    public mutating func step<Random: RandomNumberGenerator>(map: TileMap, population: Int, random: inout Random) {
        guard isRunning else {
            setOut(map, population, &random)
            return
        }
        // After the turn, the train runs straight again; stopped, it shows as Micropolis' does.
        if frame == .turningNorthwest || frame == .turningNortheast {
            frame = switch direction {
            case 0, 2: .down
            case 1, 3: .across
            default: .tunnel
            }
        }
        if let direction {
            x += Self.directions[direction].dx * Self.stepPixels
            y += Self.directions[direction].dy * Self.stepPixels
        }
        if step % Self.stepsPerTile == 0 { chooseWay(map, &random) }
        step += 1
    }

    private mutating func setOut<Random: RandomNumberGenerator>(_ map: TileMap, _ population: Int, _ random: inout Random) {
        guard population > Self.peopleBeforeTrains, Int.random(in: 0..<Self.setOutChance, using: &random) == 0 else { return }
        var rail: [(x: Int, y: Int)] = []
        for tileY in 0..<map.height {
            for tileX in 0..<map.width where map[tileX, tileY].isRail { rail.append((tileX, tileY)) }
        }
        guard let start = rail.randomElement(using: &random) else { return }
        x = start.x * Self.tileSize + Self.tileSize / 2
        y = start.y * Self.tileSize + Self.tileSize / 2
        (direction, step, isRunning) = (nil, 0, true)
        frame = map[start.x, start.y].isTunnel ? .tunnel : .across
    }

    // At a tile's middle the train tries the four ways from a random one round, never straight
    // back while it's moving, and takes the first with rail; stuck, it stops, and stopped with
    // nowhere to go, it retires.
    private mutating func chooseWay<Random: RandomNumberGenerator>(_ map: TileMap, _ random: inout Random) {
        let (tileX, tileY) = (x / Self.tileSize, y / Self.tileSize)
        (x, y) = (tileX * Self.tileSize + Self.tileSize / 2, tileY * Self.tileSize + Self.tileSize / 2)
        let first = Int.random(in: 0..<Self.directions.count, using: &random)
        for turn in 0..<Self.directions.count {
            let way = (first + turn) % Self.directions.count
            if let direction, way == (direction + 2) % Self.directions.count { continue }
            let next = map.state(atOrDefault: tileX + Self.directions[way].dx, tileY + Self.directions[way].dy)
            guard next.isRail else { continue }
            // Ways that add up to 3 are north and west, or east and south.
            frame = if next.isTunnel {
                .tunnel
            } else if direction == nil || direction == way {
                way % 2 == 0 ? .down : .across
            } else if direction! + way == 3 {
                .turningNorthwest
            } else {
                .turningNortheast
            }
            direction = way
            return
        }
        if direction == nil {
            isRunning = false
        } else {
            direction = nil
        }
    }
}
