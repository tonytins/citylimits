import CityLimitsCore
import Hudini
import Raylib

/// Draws the city's map. What's part of the map (ground, roads, zones, buildings) is drawn into
/// cached 32 by 32 tile textures, redrawn only when a tile in them changes, so a frame draws a few
/// textures, not every tile. What moves each frame (traffic, fires, floods, radiation, the
/// hover, a tool's preview) is drawn over them.
@MainActor
final class MapRenderer {
    static let tileSize = 16
    static let chunkTiles = 32

    private let city: CitySimulation
    private let sheet: Texture2D?
    private var chunks: [RenderTexture2D] = []
    private var stale: [Bool] = []
    private var anyStale = true
    private let chunkColumns: Int
    private var watcher: Int?

    // Micropolis fills a powered stadium when the count and its place come round to a multiple of
    // 32, and empties it eight counts later.
    private static let stadiumGameRound = 32
    private static let stadiumGameCounts = 8

    init(city: CitySimulation, sheet: Texture2D?) {
        self.city = city
        self.sheet = sheet
        let map = city.map
        chunkColumns = (map.width + Self.chunkTiles - 1) / Self.chunkTiles
        let rows = (map.height + Self.chunkTiles - 1) / Self.chunkTiles
        for index in 0..<(chunkColumns * rows) {
            let tiles = Self.tiles(of: index, columns: chunkColumns, map: map)
            let chunk = LoadRenderTexture(Int32(tiles.width * Self.tileSize), Int32(tiles.height * Self.tileSize))
            guard IsRenderTextureValid(chunk) else {
                // Without a cache, the map is drawn tile by tile, which is slower but still right.
                for made in chunks { UnloadRenderTexture(made) }
                chunks = []
                break
            }
            SetTextureFilter(chunk.texture, .point)
            chunks.append(chunk)
        }
        stale = Array(repeating: true, count: chunks.count)
        // A tile's drawing can depend on the tiles around it, so a change stales the chunks of the
        // 3 by 3 around it.
        watcher = map.watch { [weak self] x, y in
            guard let self else { return }
            for dy in -1...1 {
                for dx in -1...1 where map.contains(x + dx, y + dy) {
                    self.stale[((y + dy) / Self.chunkTiles) * self.chunkColumns + (x + dx) / Self.chunkTiles] = true
                    self.anyStale = true
                }
            }
        }
    }

    var isCached: Bool { !chunks.isEmpty }

    /// Releases the chunks. Call before `CloseWindow`, and before dropping the renderer.
    func unload() {
        for chunk in chunks { UnloadRenderTexture(chunk) }
        chunks = []
        stale = []
        if let watcher { city.map.stopWatching(watcher) }
        watcher = nil
    }

    private static func tiles(of index: Int, columns: Int, map: TileMap) -> (x: Int, y: Int, width: Int, height: Int) {
        let x = index % columns * chunkTiles, y = index / columns * chunkTiles
        return (x, y, min(chunkTiles, map.width - x), min(chunkTiles, map.height - y))
    }

    /// Redraws the chunks that changed. Call before `BeginDrawing`: it draws into textures.
    func refresh() {
        guard anyStale, isCached else { return }
        for index in chunks.indices where stale[index] {
            let tiles = Self.tiles(of: index, columns: chunkColumns, map: city.map)
            BeginTextureMode(chunks[index])
            ClearBackground(Color(r: 0, g: 0, b: 0, a: 0))
            for y in tiles.y..<(tiles.y + tiles.height) {
                for x in tiles.x..<(tiles.x + tiles.width) {
                    drawTile(x, y, left: (x - tiles.x) * Self.tileSize, top: (y - tiles.y) * Self.tileSize)
                }
            }
            EndTextureMode()
            stale[index] = false
        }
        anyStale = false
    }

    // MARK: Drawing in the camera

    /// The tiles in view, clamped to the map.
    func visibleRange(_ camera: ViewportCamera) -> (minX: Int, minY: Int, maxX: Int, maxY: Int) {
        let topLeft = camera.worldPoint(forScreenPoint: .zero)
        let bottomRight = camera.worldPoint(forScreenPoint: camera.screenSize)
        let size = Float(Self.tileSize)
        return (
            max(0, Int((topLeft.x / size).rounded(.down))), max(0, Int((topLeft.y / size).rounded(.down))),
            min(city.map.width - 1, Int((bottomRight.x / size).rounded(.up))),
            min(city.map.height - 1, Int((bottomRight.y / size).rounded(.up))))
    }

    /// Draws the map and its overlays. Call inside `BeginMode2D`.
    func draw(_ camera: ViewportCamera, trafficFrame: Int, fireFrame: Int) {
        let view = visibleRange(camera)
        guard view.minX <= view.maxX, view.minY <= view.maxY else { return }
        if isCached {
            for row in (view.minY / Self.chunkTiles)...(view.maxY / Self.chunkTiles) {
                for column in (view.minX / Self.chunkTiles)...(view.maxX / Self.chunkTiles) {
                    let texture = chunks[row * chunkColumns + column].texture
                    // A render texture is stored upside down, so it's drawn with a negative source height.
                    let source = Rectangle(x: 0, y: 0, width: Float(texture.width), height: -Float(texture.height))
                    let destination = Rectangle(
                        x: Float(column * Self.chunkTiles * Self.tileSize), y: Float(row * Self.chunkTiles * Self.tileSize),
                        width: Float(texture.width), height: Float(texture.height))
                    DrawTexturePro(texture, source, destination, .zero, 0, .white)
                }
            }
        } else {
            for y in view.minY...view.maxY {
                for x in view.minX...view.maxX { drawTile(x, y, left: x * Self.tileSize, top: y * Self.tileSize) }
            }
        }

        let hazards = city.hazards
        let anyHazard = hazards.fires.count > 0 || hazards.floods.count > 0 || hazards.radiation.count > 0
        for y in view.minY...view.maxY {
            for x in view.minX...view.maxX {
                // Traffic moves every frame, so a road showing some is drawn again from its frame.
                let level = city.traffic.level(x, y)
                if level != .none, sheet != nil {
                    let road = index(of: x, y)
                    drawSheet(TileAtlas.traffic(road: road, level, frame: trafficFrame), x, y)
                }
                guard anyHazard else { continue }
                if hazards.isFlooded(x, y) { drawSheet(TileAtlas.flood(x, y), x, y, fallback: Palette.flood) }
                if hazards.isRadioactive(x, y) { drawSheet(TileAtlas.radiation, x, y, fallback: Palette.radiation) }
                if hazards.isBurning(x, y) { drawSheet(TileAtlas.fire(x, y, frame: fireFrame), x, y, fallback: Palette.fire) }
            }
        }
        drawStadiumGames(view, frame: fireFrame)

        if camera.zoom >= 0.75 {
            let size = Float(Self.tileSize)
            for x in view.minX...(view.maxX + 1) {
                DrawLineEx(Vector2(x: Float(x) * size, y: Float(view.minY) * size), Vector2(x: Float(x) * size, y: Float(view.maxY + 1) * size), 0.5, Palette.grid)
            }
            for y in view.minY...(view.maxY + 1) {
                DrawLineEx(Vector2(x: Float(view.minX) * size, y: Float(y) * size), Vector2(x: Float(view.maxX + 1) * size, y: Float(y) * size), 0.5, Palette.grid)
            }
        }
    }

    /// A lightning bolt over every zone and building without power, blinking twice a second.
    func drawUnpowered(_ camera: ViewportCamera, seconds: Double) {
        guard Int(seconds * 2) % 2 == 0 else { return }
        let view = visibleRange(camera)
        guard view.minX <= view.maxX, view.minY <= view.maxY else { return }
        for y in view.minY...view.maxY {
            for x in view.minX...view.maxX where city.map[x, y].isBuildingCenter && !city.map[x, y].powered {
                drawSheet(TileAtlas.lightningBolt, x, y, fallback: Palette.unpowered)
            }
        }
    }

    // A powered stadium partly in view has its centre at most a tile up or left of the view, and
    // two right or down of it.
    private func drawStadiumGames(_ view: (minX: Int, minY: Int, maxX: Int, maxY: Int), frame: Int) {
        guard sheet != nil else { return }
        let map = city.map
        for y in max(view.minY - 2, 0)...min(view.maxY + 1, map.height - 1) {
            for x in max(view.minX - 2, 0)...min(view.maxX + 1, map.width - 1) {
                let center = map[x, y]
                guard center.use == .stadium, center.isBuildingCenter, center.powered,
                    (city.cityTime + x + y) % Self.stadiumGameRound < Self.stadiumGameCounts
                else { continue }
                let size = TileUse.stadium.footprint
                for part in 0..<Footprint.parts(size) {
                    let offset = Footprint.offsetFromCenter(part: part, size: size)
                    let (px, py) = (x + offset.dx, y + offset.dy)
                    let state = map.state(atOrDefault: px, py)
                    // Only the stadium's own tiles fill: a hole, a fire, or a flood stays as it is.
                    guard state.use == .stadium, state.zonePart == part, !city.hazards.isBurning(px, py), !city.hazards.isFlooded(px, py) else {
                        continue
                    }
                    let drawing = switch part {
                    case TileAtlas.footballGamePart: TileAtlas.footballGame(lower: false, frame: frame)
                    case TileAtlas.footballGameLowerPart: TileAtlas.footballGame(lower: true, frame: frame)
                    default: TileAtlas.fullStadium(part: part)
                    }
                    drawSheet(drawing, px, py)
                }
            }
        }
    }

    func drawExplosion(_ x: Int, _ y: Int, frame: Int) {
        drawSheet(TileAtlas.explosion(frame: frame), x, y, fallback: Palette.fire)
    }

    /// The outline of the tile under the pointer.
    func drawHover(_ camera: ViewportCamera, tile: (x: Int, y: Int)?) {
        guard let tile else { return }
        let size = Float(Self.tileSize)
        DrawRectangleLinesEx(Rectangle(x: Float(tile.x) * size, y: Float(tile.y) * size, width: size, height: size), 1 / camera.zoom, Palette.hover)
    }

    /// A stroke as it would be built: each tile it changes, the roads, rail, and lines beside it
    /// as they'd join, and its outline, red when it's refused.
    func drawPreview(_ plan: BuildPlan, _ camera: ViewportCamera) {
        for tile in plan.tiles {
            drawPlanned(plan, tile.x, tile.y)
            for step in [(0, -1), (1, 0), (0, 1), (-1, 0)] {
                let (x, y) = (tile.x + step.0, tile.y + step.1)
                guard city.map.contains(x, y), plan.planned(x, y) == nil else { continue }
                let state = city.map[x, y]
                if state.hasRoad || state.isRail || state.hasPowerLine { drawPlanned(plan, x, y) }
            }
        }
        let bounds = plan.bounds, size = Float(Self.tileSize)
        DrawRectangleLinesEx(
            Rectangle(x: Float(bounds.x) * size, y: Float(bounds.y) * size, width: Float(bounds.width) * size, height: Float(bounds.height) * size),
            2 / camera.zoom, plan.refusal == .none ? Palette.hover : Palette.error)
    }

    // A planned zone is new, so it shows undeveloped; a planned tile is built on or cleared, so it
    // has no shore or wood edge.
    private func drawPlanned(_ plan: BuildPlan, _ x: Int, _ y: Int) {
        let state = plan.state(x, y)
        let drawing = TileAtlas.index(
            state, roads: plan.connections(.road, x, y), lines: plan.connections(.power, x, y), rails: plan.connections(.rail, x, y),
            ground: [], center: .grass, x: x, y: y)
        drawSheet(drawing, x, y, fallback: Palette.tile(state))
    }

    // MARK: Tiles

    private func index(of x: Int, _ y: Int) -> Int {
        let map = city.map
        return TileAtlas.index(
            map[x, y], roads: Network.road.connections(x, y, on: map), lines: Network.power.connections(x, y, on: map),
            rails: Network.rail.connections(x, y, on: map), ground: map.terrainSides(x, y), center: map.centerState(x, y), x: x, y: y)
    }

    private func drawTile(_ x: Int, _ y: Int, left: Int, top: Int) {
        let destination = Rectangle(x: Float(left), y: Float(top), width: Float(Self.tileSize), height: Float(Self.tileSize))
        guard let sheet else {
            DrawRectangleRec(destination, Palette.tile(city.map[x, y]))
            return
        }
        DrawTexturePro(sheet, TileAtlas.source(index(of: x, y)), destination, .zero, 0, .white)
    }

    private func drawSheet(_ drawing: Int, _ x: Int, _ y: Int, fallback: Color = Palette.fire) {
        let destination = Rectangle(
            x: Float(x * Self.tileSize), y: Float(y * Self.tileSize), width: Float(Self.tileSize), height: Float(Self.tileSize))
        guard let sheet else {
            DrawRectangleRec(destination, fallback)
            return
        }
        DrawTexturePro(sheet, TileAtlas.source(drawing), destination, .zero, 0, .white)
    }
}
