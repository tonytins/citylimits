import CityLimitsCore
import Hudini
import Raylib

/// The overview in the top-right corner (`Interface/overview.uix`), as Micropolis has it: the whole
/// city small, with buttons that switch it between the city and the simulation's maps. It outlines
/// what the main view shows, and a press or a drag on it moves the view there. It's drawn into a
/// texture of its own and redrawn only when the map or the mode changes, or the city starts a new
/// count, since its maps change once a cycle; without the texture it's drawn straight to the screen.
@MainActor
final class OverviewMap {
    /// What the overview shows. Its raw value is its button's name in the document.
    enum Mode: String, CaseIterable {
        case city, power, fire, police, value, people, traffic, pollution, crime
    }

    private static let pixelsPerTile = 2
    private static let margin: Float = 8
    // A value at or above these is shown in the highest band: a fully funded, powered, served
    // station on its own block, and a crowded block.
    private static let highCoverage = 218
    private static let highDensity = 200

    private let city: CitySimulation
    private let view: UIScriptView
    private var texture: RenderTexture2D?
    private var watcher: Int?
    private var stale = true
    private var drawnAt = -1
    private(set) var mode = Mode.city

    /// The overview of `city`. Call after `InitWindow`.
    init(city: CitySimulation) {
        self.city = city
        view = Interface.view("overview", fallbackSize: Vector2(x: 252, y: 278))
        let made = LoadRenderTexture(Int32(city.map.width * Self.pixelsPerTile), Int32(city.map.height * Self.pixelsPerTile))
        texture = IsRenderTextureValid(made) ? made : nil
        watcher = city.map.watch { [weak self] _, _ in self?.stale = true }
        choose(.city)
    }

    /// Stops watching the map and releases the texture. Call before `CloseWindow`.
    func unload() {
        if let texture { UnloadRenderTexture(texture) }
        texture = nil
        if let watcher { city.map.stopWatching(watcher) }
        watcher = nil
    }

    var origin: Vector2 { Vector2(x: Float(GetScreenWidth()) - view.windowSize.x - Self.margin, y: Self.margin) }

    private var panel: Rectangle {
        Rectangle(x: origin.x, y: origin.y, width: view.windowSize.x, height: view.windowSize.y)
    }

    func contains(_ point: Vector2) -> Bool { CheckCollisionPointRec(point, panel) }

    private func choose(_ chosen: Mode) {
        if chosen != mode { stale = true }
        mode = chosen
        for each in Mode.allCases { view.setSelected(each == chosen, named: each.rawValue) }
    }

    /// Answers the buttons, and moves the camera to the place under the pointer while the left
    /// button is held on the map.
    func update(camera: ViewportCamera, tileSize: Float) {
        for case .pressed(let name) in view.update(at: origin) {
            if let picked = Mode(rawValue: name) { choose(picked) }
        }
        let pointer = GetMousePosition()
        let map = mapBounds()
        guard IsMouseButtonDown(.left), CheckCollisionPointRec(pointer, map) else { return }
        let tileX = (pointer.x - map.x) / map.width * Float(city.map.width)
        let tileY = (pointer.y - map.y) / map.height * Float(city.map.height)
        camera.center(on: Vector2(x: tileX * tileSize, y: tileY * tileSize))
    }

    /// Redraws the texture when what it shows has changed. Call before `BeginDrawing`.
    func refresh() {
        guard let texture, stale || drawnAt != city.cityTime else { return }
        BeginTextureMode(texture)
        ClearBackground(.blank)
        drawTiles(left: 0, top: 0, pixels: Float(Self.pixelsPerTile))
        EndTextureMode()
        stale = false
        drawnAt = city.cityTime
    }

    func draw(camera: ViewportCamera, tileSize: Float) {
        Interface.theme.fillRectangle(panel, Palette.panel)
        Interface.theme.outlineRectangle(panel, Palette.panelBorder)
        view.draw(at: origin) { [self] name, _ in
            guard name == "map" else { return }
            let map = mapBounds()
            if let texture {
                // A render texture is stored upside down, so it's drawn with a negative height.
                let source = Rectangle(x: 0, y: 0, width: Float(texture.texture.width), height: -Float(texture.texture.height))
                DrawTexturePro(texture.texture, source, map, .zero, 0, .white)
            } else {
                drawTiles(left: map.x, top: map.y, pixels: map.width / Float(city.map.width))
            }
            drawView(camera, tileSize: tileSize, in: map)
        }
    }

    // The map keeps its shape, as big as fits the region and centred in it.
    private func mapBounds() -> Rectangle {
        guard let region = view.bounds(named: "map", at: origin) else { return Rectangle() }
        let (width, height) = (Float(city.map.width), Float(city.map.height))
        let scale = min(region.width / width, region.height / height)
        return Rectangle(
            x: region.x + (region.width - width * scale) / 2, y: region.y + (region.height - height * scale) / 2,
            width: width * scale, height: height * scale)
    }

    private func drawView(_ camera: ViewportCamera, tileSize: Float, in map: Rectangle) {
        let topLeft = camera.worldPoint(forScreenPoint: .zero)
        let bottomRight = camera.worldPoint(forScreenPoint: camera.screenSize)
        let scaleX = map.width / (Float(city.map.width) * tileSize)
        let scaleY = map.height / (Float(city.map.height) * tileSize)
        func clamp(_ value: Float, _ low: Float, _ length: Float) -> Float { min(max(value, low), low + length) }
        let left = clamp(map.x + topLeft.x * scaleX, map.x, map.width)
        let top = clamp(map.y + topLeft.y * scaleY, map.y, map.height)
        let right = clamp(map.x + bottomRight.x * scaleX, map.x, map.width)
        let bottom = clamp(map.y + bottomRight.y * scaleY, map.y, map.height)
        DrawRectangleLinesEx(Rectangle(x: left, y: top, width: right - left, height: bottom - top), 1, Palette.hover)
    }

    private func drawTiles(left: Float, top: Float, pixels: Float) {
        for y in 0..<city.map.height {
            for x in 0..<city.map.width {
                let cell = Rectangle(x: left + Float(x) * pixels, y: top + Float(y) * pixels, width: pixels, height: pixels)
                DrawRectangleRec(cell, color(x, y))
            }
        }
    }

    // MARK: Colours

    /// A tile as the City mode shows it, in flat colours: fire or water over it first, then what's
    /// built, then the ground.
    static func cityColor(_ city: CitySimulation, _ x: Int, _ y: Int) -> Color {
        if city.hazards.isBurning(x, y) { return Palette.fire }
        if city.hazards.isFlooded(x, y) { return Palette.flood }
        if city.hazards.isRadioactive(x, y) { return Palette.radiation }
        return Palette.tile(city.map[x, y])
    }

    // The city in flat colours; a map shows its values over the city dimmed. A fire or a flood
    // shows in every mode, so it can be found whatever the overview shows.
    private func color(_ x: Int, _ y: Int) -> Color {
        let base = Self.cityColor(city, x, y)
        if city.hazards.isBurning(x, y) || city.hazards.isFlooded(x, y) { return base }
        switch mode {
        case .city: return base
        case .power: return powerColor(x, y, base)
        case .fire: return Self.band(city.services.fireCoverage(x, y), Self.highCoverage, base)
        case .police: return Self.band(city.services.policeCoverage(x, y), Self.highCoverage, base)
        case .value: return Self.band(city.maps.landValue(x, y), CityMaps.maxLandValue, base)
        case .people: return Self.band(city.maps.density(x, y), Self.highDensity, base)
        case .traffic: return Self.band(city.traffic.density(x, y), TrafficMap.most, base)
        case .pollution: return Self.band(city.maps.pollution(x, y), CityMaps.mostPollution, base)
        case .crime: return Self.band(city.maps.crime(x, y), CityMaps.mostCrime, base)
        }
    }

    // As Micropolis shows power: a zone or building lit or dark by what its centre last had, the
    // lines that carry it, and everything else dimmed.
    private func powerColor(_ x: Int, _ y: Int, _ base: Color) -> Color {
        let state = city.map[x, y]
        if state.hasFootprint { return city.map.centerState(x, y).powered ? Palette.overviewPowered : Palette.overviewUnpowered }
        return state.hasPowerLine ? Palette.overviewLine : Self.dim(base)
    }

    // A value in one of four bands, from a quarter of the high mark up; nothing at all shows the
    // city dimmed beneath.
    private static func band(_ value: Int, _ high: Int, _ base: Color) -> Color {
        guard value > 0 else { return dim(base) }
        let bands = Palette.overlayBands
        return bands[min(value * bands.count / high, bands.count - 1)]
    }

    private static func dim(_ color: Color) -> Color { Color(r: color.r / 3, g: color.g / 3, b: color.b / 3, a: 255) }
}
