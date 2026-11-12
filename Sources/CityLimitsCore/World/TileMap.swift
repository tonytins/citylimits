/// The city's map: a grid of tiles, and the only record of what is where. The renderer and the
/// simulation read it, and everything that changes it goes through `setState`, so whoever is
/// watching hears about it.
public final class TileMap {
    /// The size of a new city's map, Micropolis' 120 by 100.
    public static let defaultWidth = 120
    public static let defaultHeight = 100

    public let width: Int
    public let height: Int
    private var tiles: [TileState]
    private var watchers: [Int: (Int, Int) -> Void] = [:]
    private var nextWatcher = 0

    /// Makes a map from its tiles, row by row. Returns nil when the sizes don't fit.
    public init?(width: Int, height: Int, tiles: [TileState]) {
        guard width > 0, height > 0 else { return nil }
        let (count, overflow) = width.multipliedReportingOverflow(by: height)
        guard !overflow, tiles.count == count else { return nil }
        self.width = width
        self.height = height
        self.tiles = tiles
    }

    /// Makes a map of bare ground.
    public init(width: Int, height: Int, terrain: [TileKind]) {
        precondition(width > 0 && height > 0 && terrain.count == width * height, "terrain doesn't fit a \(width) by \(height) map")
        self.width = width
        self.height = height
        tiles = terrain.map { TileState($0) }
    }

    public func contains(_ x: Int, _ y: Int) -> Bool {
        x >= 0 && y >= 0 && x < width && y < height
    }

    /// The tile at a place on the map. Asking for one off the map is a programmer error; use
    /// `state(atOrDefault:)` where the place may be off it.
    public subscript(x: Int, y: Int) -> TileState {
        precondition(contains(x, y), "(\(x), \(y)) is outside a \(width) by \(height) map")
        return tiles[y * width + x]
    }

    /// The tile at a place, or `fallback` off the map.
    public func state(atOrDefault x: Int, _ y: Int, fallback: TileState = .grass) -> TileState {
        contains(x, y) ? tiles[y * width + x] : fallback
    }

    /// Changes a tile, and tells the watchers when it changed.
    public func setState(_ x: Int, _ y: Int, _ state: TileState) {
        precondition(contains(x, y), "(\(x), \(y)) is outside a \(width) by \(height) map")
        let index = y * width + x
        guard tiles[index] != state else { return }
        tiles[index] = state
        for watcher in watchers.values {
            watcher(x, y)
        }
    }

    /// Calls `handler` with each tile that changes, until `stopWatching` is called with the token.
    @discardableResult
    public func watch(_ handler: @escaping (Int, Int) -> Void) -> Int {
        let token = nextWatcher
        nextWatcher += 1
        watchers[token] = handler
        return token
    }

    public func stopWatching(_ token: Int) {
        watchers[token] = nil
    }

    /// The centre of the zone or building a tile belongs to, or the tile itself.
    public func centerOf(_ x: Int, _ y: Int) -> (x: Int, y: Int) {
        let state = self[x, y]
        guard state.hasFootprint else { return (x, y) }
        let offset = Footprint.offsetFromCenter(part: state.zonePart, size: state.use.footprint)
        return (x - offset.dx, y - offset.dy)
    }

    /// The centre tile of the zone or building a tile belongs to.
    public func centerState(_ x: Int, _ y: Int) -> TileState {
        let center = centerOf(x, y)
        return state(atOrDefault: center.x, center.y, fallback: self[x, y])
    }

    /// Which of a tile's four neighbours are the same kind of ground, for water and trees; the
    /// map's edge counts as alike. Other ground has no sides.
    public func terrainSides(_ x: Int, _ y: Int) -> Sides {
        guard contains(x, y) else { return [] }
        let kind = self[x, y].kind
        guard kind == .water || kind == .trees else { return [] }
        var sides: Sides = []
        for step in Sides.steps where !contains(x + step.dx, y + step.dy) || self[x + step.dx, y + step.dy].kind == kind {
            sides.insert(step.side)
        }
        return sides
    }
}

/// Whether trees with these sides can be drawn as part of a wood: two sides that meet at a corner.
public func isWood(_ trees: Sides) -> Bool {
    trees.isSuperset(of: [.north, .east]) || trees.isSuperset(of: [.east, .south])
        || trees.isSuperset(of: [.south, .west]) || trees.isSuperset(of: [.west, .north])
}
