import CityLimitsCore
import Foundation
import Raylib

/// Playing a city: its map and tools, the clock that runs it at the speed the player picks, and
/// what the player is told. The editor will be a session of its own beside this one, not a flag
/// inside it.
@MainActor
final class PlaySession {
    // How long what a stroke came to stays beside the pointer once it's let go.
    private static let hintSeconds: Float = 3

    let city: CitySimulation
    let view: CityView
    private let palette: ToolPaletteView
    private let status = StatusBar()
    private let overview: OverviewMap
    private var clock = GameClock()
    private(set) var speed = GameSpeed.normal
    // The speed play brings back after a pause.
    private var lastSpeed = GameSpeed.normal
    private var hint: String?
    private var hintLeft: Float = 0
    private var messages = CityMessageFilter()
    private var newsDesk: NewsDesk
    // Headlines are for show, so they use random numbers of their own, never the city's.
    private var newsRandom = SystemRandomNumberGenerator()

    init(city: CitySimulation, assets: GameAssets) {
        self.city = city
        view = CityView(city: city, assets: assets)
        palette = ToolPaletteView(assets: assets, definitions: city.definitions)
        overview = OverviewMap(city: city)
        newsDesk = NewsDesk(city: city)
        status.ticker.filler = { [unowned self] in newsDesk.next(for: city, random: &newsRandom) }
        city.onEvent = { [unowned self] event in hear(event) }
    }

    /// Releases what the session drew with. Call before `CloseWindow`.
    func unload() {
        city.onEvent = nil
        status.ticker.filler = nil
        overview.unload()
        view.unload()
    }

    // A disaster goes on the ticker in red the moment it starts, every time, since each one is
    // news; the city's other messages go in yellow, unless one was said too lately. Ordinances
    // and neighbours have news of their own, in blue.
    private func hear(_ event: CityEvent) {
        view.show(event)
        let ticker = status.ticker
        switch event {
        case .message(let message) where message.isDisaster:
            ticker.post(NewsItem(message.text, kind: .disaster))
        case .message(let message) where messages.shouldPass(message, cityTime: city.cityTime):
            ticker.advise(message.text)
        case .ordinance(let news):
            ticker.post(NewsText.ordinance(news, city: city.name))
        case .neighbour(let news):
            ticker.post(NewsText.neighbour(news, city: city.name))
        case .challengeFinished(let won, _):
            ticker.advise(won ? "The challenge was won" : "The challenge was lost")
        default:
            break
        }
    }

    /// The bottom of the space the map has, above the bar.
    private var mapBottom: Int32 { Int32(status.origin.y) }

    private func isOverInterface(_ point: Vector2) -> Bool {
        palette.contains(point) || overview.contains(point) || status.contains(point)
    }

    // MARK: Each frame

    func update() {
        let elapsed = GetFrameTime()
        handleInput(elapsed)
        if speed != .paused {
            let due = clock.advance(Double(elapsed) * speed.multiplier)
            for _ in 0..<due { city.tick() }
        }
        view.animate(elapsed, speed: speed)
        hintLeft = max(0, hintLeft - elapsed)
        view.update()
    }

    private func setSpeed(_ chosen: GameSpeed) {
        speed = chosen
        if chosen != .paused { lastSpeed = chosen }
    }

    private func handleInput(_ elapsed: Float) {
        let pointer = GetMousePosition()
        let wheel = GetMouseWheelMove()
        if wheel != 0, !isOverInterface(pointer) { view.camera.zoom(at: pointer, by: wheel) }

        var pan = Vector2.zero
        if IsKeyDown(.a) || IsKeyDown(.left) { pan.x -= 1 }
        if IsKeyDown(.d) || IsKeyDown(.right) { pan.x += 1 }
        if IsKeyDown(.w) || IsKeyDown(.up) { pan.y -= 1 }
        if IsKeyDown(.s) || IsKeyDown(.down) { pan.y += 1 }
        if pan != .zero { view.pan(pan, elapsed: elapsed) }

        if IsKeyPressed(.space) { setSpeed(speed == .paused ? lastSpeed : .paused) }
        if IsKeyPressed(.comma) { setSpeed(GameSpeed(rawValue: max(speed.rawValue - 1, 0)) ?? .paused) }
        if IsKeyPressed(.period) { setSpeed(GameSpeed(rawValue: min(speed.rawValue + 1, GameSpeed.superFast.rawValue)) ?? .superFast) }
        if IsKeyPressed(.escape) { view.cancelStroke() }

        // The HUD takes the pointer only between strokes, so a drag across it doesn't click it.
        let takesPointer = view.stroke == nil
        if takesPointer { overview.update(camera: view.camera, tileSize: Float(MapRenderer.tileSize)) }
        let action = status.update(city: city, speed: speed, elapsed: elapsed, takesInput: takesPointer)
        switch action {
        case .pausePlay: setSpeed(speed == .paused ? lastSpeed : .paused)
        case .speed(let chosen): setSpeed(chosen)
        // The windows these open come in phase 4c.
        case .cityName, .demand, .funds, .help, .link, nil: break
        }

        handleStroke(pointer)
    }

    private func handleStroke(_ pointer: Vector2) {
        let tile = isOverInterface(pointer) ? nil : view.tile(at: pointer)
        guard view.stroke != nil else {
            guard IsMouseButtonPressed(.left) else { return }
            if palette.contains(pointer) {
                if let picked = palette.tool(at: pointer) { palette.toggle(picked) }
                return
            }
            if let action = palette.selected?.action, let tile {
                view.beginStroke(action, at: tile)
                hintLeft = 0
            }
            return
        }
        if IsMouseButtonDown(.left) {
            if let tile { view.stretchStroke(to: tile) }
            return
        }
        if let (action, result) = view.finishStroke() {
            hint = StrokeText.result(action, result, free: city.builder.buildsForFree)
            hintLeft = Self.hintSeconds
        }
    }

    // MARK: Drawing

    func draw() {
        // The map's cache and the overview draw into textures, which can't happen inside the frame.
        view.refresh()
        overview.refresh()
        BeginDrawing()
        ClearBackground(Palette.background)
        let pointer = GetMousePosition()
        let hover = isOverInterface(pointer) ? nil : view.tile(at: pointer)
        view.drawMap(hover: hover)
        palette.draw(pointer: pointer)
        overview.draw(camera: view.camera, tileSize: Float(MapRenderer.tileSize))
        status.draw(city: city, speed: speed, resumeSpeed: lastSpeed)

        // Beside the pointer: what the stroke being dragged would cost, what the last one came
        // to, or, with Inspect, what the tile under the pointer is.
        if view.stroke != nil {
            PointerHint.draw(at: pointer, StrokeText.preview(view.preview), bottom: mapBottom)
        } else if hintLeft > 0, let hint {
            PointerHint.draw(at: pointer, hint, bottom: mapBottom)
        } else if palette.selected == .inspect, let hover {
            PointerHint.draw(
                at: pointer, "Tile \(hover.x), \(hover.y)",
                city.map.describe(hover.x, hover.y, definitions: city.definitions, hazards: city.hazards), bottom: mapBottom)
        }
        EndDrawing()
    }
}
