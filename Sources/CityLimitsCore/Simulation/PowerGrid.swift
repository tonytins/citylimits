extension TileState {
    /// The same tile with its power switched on or off.
    func powered(_ on: Bool) -> TileState {
        TileState(kind, use, development: development, zonePart: zonePart, valueClass: valueClass, house: house, powered: on)
    }
}

/// The city's power, as Micropolis traces it: the plants pool their capacity, and power spreads
/// from each along power lines and through buildings until the capacity runs out. In the base
/// game, roads also join the buildings along them, without a line beside every street.
public final class PowerGrid {
    var plants: [(x: Int, y: Int)] = []
    var poweredTiles: [Bool] = []
    var width = 0
    private var stack: [Int] = []
    private var joined: [Bool] = []
    private var networkQueue: [Int] = []
    private var carriers = CityDefinitions.base.infrastructure

    public internal(set) var coalPlants = 0
    public internal(set) var nuclearPlants = 0
    /// The zones the scan found this count.
    public internal(set) var zones = 0
    public internal(set) var poweredBuildings = 0
    public internal(set) var unpoweredBuildings = 0
    /// How many tiles the plants can power.
    public internal(set) var capacity = 0
    /// How many tiles the last trace powered.
    public internal(set) var poweredTileCount = 0
    /// Whether the last trace ran out of capacity.
    public internal(set) var isBrownedOut = false

    public init() {}

    public func hasPower(_ x: Int, _ y: Int) -> Bool {
        x >= 0 && y >= 0 && x < width && y * width + x < poweredTiles.count && poweredTiles[y * width + x]
    }

    public func startCount() {
        plants.removeAll(keepingCapacity: true)
        (coalPlants, nuclearPlants, zones, poweredBuildings, unpoweredBuildings) = (0, 0, 0, 0, 0)
    }

    /// Counts a building's centre for the next trace, and gives it the power the last trace
    /// found. Returns whether it has power.
    @discardableResult
    public func visit(_ map: TileMap, _ x: Int, _ y: Int) -> Bool {
        let center = map[x, y]
        let powered = center.use.isPowerPlant || hasPower(x, y)
        switch center.use {
        case .coalPowerPlant:
            coalPlants += 1
            plants.append((x, y))
        case .nuclearPowerPlant:
            nuclearPlants += 1
            plants.append((x, y))
        default:
            if center.isZoned { zones += 1 }
        }
        if powered { poweredBuildings += 1 } else { unpoweredBuildings += 1 }
        if center.powered != powered {
            map.setState(x, y, center.powered(powered))
        }
        return powered
    }

    /// Traces power from every plant the scan counted.
    public func trace(_ map: TileMap, definitions: CityDefinitions, capacityPercent: Int = 100, traded: Int = 0) {
        let tiles = map.width * map.height
        if poweredTiles.count != tiles {
            poweredTiles = Array(repeating: false, count: tiles)
            joined = poweredTiles
            stack = Array(repeating: 0, count: tiles)
            networkQueue = stack
        } else {
            for index in 0..<tiles {
                poweredTiles[index] = false
                joined[index] = false
            }
        }
        width = map.width
        carriers = definitions.infrastructure

        let pooled = coalPlants * definitions.plant(.coalPowerPlant).capacity
            + nuclearPlants * definitions.plant(.nuclearPowerPlant).capacity
        capacity = max(0, pooled * max(0, capacityPercent) / 100 + traded)
        poweredTileCount = 0
        isBrownedOut = false

        // The plant found last is traced from first, as Micropolis takes them from a stack.
        for plant in plants.reversed() {
            if isBrownedOut { break }
            let state = map[plant.x, plant.y]
            if state.isBuildingCenter && state.use.isPowerPlant {
                trace(map, from: plant.x, plant.y)
            }
        }
    }

    // A tile is marked as it is reached, so each is powered, and counted, once, and the stack
    // never holds more tiles than the map has.
    private func trace(_ map: TileMap, from startX: Int, _ startY: Int) {
        var depth = 0
        guard tryPower(startY * width + startX, &depth) else { return }
        while depth > 0 {
            depth -= 1
            let index = stack[depth]
            let (x, y) = (index % width, index / width)
            let state = map[x, y]
            for step in Sides.steps {
                let (nextX, nextY) = (x + step.dx, y + step.dy)
                guard map.contains(nextX, nextY) else { continue }
                let next = nextY * width + nextX
                let nextState = map[nextX, nextY]
                // A zone or building on a road or rail joins every other one along it.
                if state.hasFootprint && isNetwork(nextState) && !joined[next] && !joinNetwork(map, next, &depth) {
                    return
                }
                if !poweredTiles[next] && Network.power.runs(state, toward: step.side)
                    && Network.power.runs(nextState, toward: step.side.opposite) && !tryPower(next, &depth)
                {
                    return
                }
            }
        }
    }

    private func isNetwork(_ state: TileState) -> Bool {
        (carriers.roadCarriesPower && state.hasRoad) || (carriers.railCarriesPower && state.isRail)
    }

    private func joins(_ from: TileState, _ to: TileState, _ side: Sides) -> Bool {
        (carriers.roadCarriesPower && Network.road.runs(from, toward: side) && Network.road.runs(to, toward: side.opposite))
            || (carriers.railCarriesPower && Network.rail.runs(from, toward: side) && Network.rail.runs(to, toward: side.opposite))
    }

    // Walks the road or rail network from one of its tiles, nearest first, each tile once, and
    // powers each building beside it whole, in the order reached, so a plant short of capacity
    // lights the buildings nearest it along the road, not a strip of each. The network itself
    // isn't powered and uses no capacity. Returns false when the capacity runs out.
    private func joinNetwork(_ map: TileMap, _ start: Int, _ depth: inout Int) -> Bool {
        var (head, tail) = (0, 0)
        joined[start] = true
        networkQueue[tail] = start
        tail += 1
        while head < tail {
            let index = networkQueue[head]
            head += 1
            let (x, y) = (index % width, index / width)
            let state = map[x, y]
            for step in Sides.steps {
                let (nextX, nextY) = (x + step.dx, y + step.dy)
                guard map.contains(nextX, nextY) else { continue }
                let next = nextY * width + nextX
                let nextState = map[nextX, nextY]
                if nextState.hasFootprint {
                    if !poweredTiles[next] && !powerWhole(map, nextX, nextY, nextState, &depth) { return false }
                } else if !joined[next] && isNetwork(nextState) && joins(state, nextState, step.side) {
                    joined[next] = true
                    networkQueue[tail] = next
                    tail += 1
                }
            }
        }
        return true
    }

    // Powers every tile of the building a tile is part of, or none when the capacity left can't.
    private func powerWhole(_ map: TileMap, _ x: Int, _ y: Int, _ part: TileState, _ depth: inout Int) -> Bool {
        let size = part.use.footprint
        let (left, top) = (x - part.zonePart % size, y - part.zonePart / size)
        var needed = 0
        for row in top..<(top + size) {
            for column in left..<(left + size) where map.contains(column, row) && !poweredTiles[row * width + column] {
                needed += 1
            }
        }
        if poweredTileCount + needed > capacity {
            isBrownedOut = true
            return false
        }
        for row in top..<(top + size) {
            for column in left..<(left + size) where map.contains(column, row) {
                _ = tryPower(row * width + column, &depth)
            }
        }
        return true
    }

    private func tryPower(_ index: Int, _ depth: inout Int) -> Bool {
        if poweredTiles[index] { return true }
        if poweredTileCount >= capacity {
            isBrownedOut = true
            return false
        }
        poweredTiles[index] = true
        poweredTileCount += 1
        stack[depth] = index
        depth += 1
        return true
    }
}
