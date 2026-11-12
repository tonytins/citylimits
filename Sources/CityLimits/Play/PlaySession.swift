import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// Playing a city: its map and tools, the clock that runs it at the speed the player picks, and
/// what the player is told. The editor will be a session of its own beside this one, not a flag
/// inside it.
@MainActor
final class PlaySession {
    // How long what a stroke came to stays beside the pointer once it's let go.
    private static let hintSeconds: Float = 3
    private static let iconSize = Vector2(x: 110, y: 22)

    let city: CitySimulation
    let view: CityView
    private let palette: ToolPaletteView
    private let status = StatusBar()
    private let overview: OverviewMap
    private let windows = WindowManager()
    private let confirm = ConfirmWindow()
    private let notice = NoticeWindow()
    private let budget: BudgetWindow
    private let demand: DemandWindow
    private let help = HelpWindow()
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
        budget = BudgetWindow(city: city)
        demand = DemandWindow(city: city)
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
        case .yearSettled(let year):
            // A year that needs attention opens the budget by itself, unless a question is open.
            if let reason = BudgetWindow.reason(for: year), !windows.hasModal {
                view.cancelStroke()
                budget.open(reason: reason, in: windows)
            }
        case .challengeFinished(let won, _):
            ticker.advise(won ? "The challenge was won" : "The challenge was lost")
        default:
            break
        }
    }

    /// The bottom of the space the map has, above the bar.
    private var mapBottom: Int32 { Int32(status.origin.y) }

    private func isOverInterface(_ point: Vector2) -> Bool {
        windows.contains(point) || minimizedIcon(at: point) != nil
            || palette.contains(point) || overview.contains(point) || status.contains(point)
    }

    // Minimized windows wait as icons above the bar; a click restores one.
    private func iconBounds(_ index: Int) -> Rectangle {
        Rectangle(
            x: 8 + Float(index) * (Self.iconSize.x + 6), y: status.origin.y - Self.iconSize.y - 6,
            width: Self.iconSize.x, height: Self.iconSize.y)
    }

    private func minimizedIcon(at point: Vector2) -> WindowFrame? {
        windows.minimizedWindows.enumerated().first { CheckCollisionPointRec(point, iconBounds($0.offset)) }?.element
    }

    // MARK: Each frame

    func update() {
        let elapsed = GetFrameTime()
        handleInput(elapsed)
        // Every modal window pauses the city.
        if speed != .paused, !windows.hasModal {
            let due = clock.advance(Double(elapsed) * speed.multiplier)
            for _ in 0..<due { city.tick() }
        }
        view.animate(elapsed, speed: windows.hasModal ? .paused : speed)
        if windows.isOpen(budget.frame) { budget.show() }
        if windows.isOpen(demand.frame) { demand.show() }
        hintLeft = max(0, hintLeft - elapsed)
        view.update()
    }

    private func setSpeed(_ chosen: GameSpeed) {
        speed = chosen
        if chosen != .paused { lastSpeed = chosen }
    }

    private func handleInput(_ elapsed: Float) {
        let pointer = GetMousePosition()
        let isOverWindow = windows.contains(pointer)
        // Windows sit above everything else, so they take the pointer first; their handlers act.
        windows.update()
        if windows.hasModal {
            view.cancelStroke()
            // The budget closes from the keyboard too; a question waits for its answer.
            if windows.frontWindow === budget.frame, IsKeyPressed(.escape) || IsKeyPressed(.enter) || IsKeyPressed(.b) {
                windows.close(budget.frame)
            }
            _ = status.update(city: city, speed: speed, elapsed: 0, takesInput: false)
            return
        }

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
        if IsKeyPressed(.escape) {
            if view.stroke != nil {
                view.cancelStroke()
            } else if let front = windows.frontWindow {
                windows.close(front)
            }
        }
        if IsKeyPressed(.b) { openBudget() }
        if IsKeyPressed(.f1) { help.toggle(in: windows) }

        // The HUD takes the pointer only between strokes and off the windows, so a drag across it
        // doesn't click it.
        let takesPointer = view.stroke == nil && !isOverWindow
        if takesPointer {
            overview.update(camera: view.camera, tileSize: Float(MapRenderer.tileSize))
            if IsMouseButtonPressed(.left), let icon = minimizedIcon(at: pointer) { windows.restore(icon) }
        }
        let action = status.update(city: city, speed: speed, elapsed: elapsed, takesInput: takesPointer)
        switch action {
        case .pausePlay: setSpeed(speed == .paused ? lastSpeed : .paused)
        case .speed(let chosen): setSpeed(chosen)
        case .funds: openBudget()
        case .demand: demand.open(in: windows)
        case .help: help.toggle(in: windows)
        // The city panel these open comes in phase 4c's second half.
        case .cityName, .link, nil: break
        }

        if view.stroke != nil || !isOverWindow { handleStroke(pointer) }
    }

    private func openBudget() {
        view.cancelStroke()
        budget.open(in: windows)
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
        let hover = isOverInterface(pointer) || windows.hasModal ? nil : view.tile(at: pointer)
        view.drawMap(hover: hover)
        palette.draw(pointer: pointer)
        overview.draw(camera: view.camera, tileSize: Float(MapRenderer.tileSize))
        status.draw(city: city, speed: windows.hasModal ? .paused : speed, resumeSpeed: lastSpeed)
        drawMinimizedIcons(pointer)
        drawWindows()

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
        windows.drawTooltip()
        EndDrawing()
    }

    // The windows, back to front. Hudini's manager would draw them itself, but its frames give no
    // way to draw into a view's region, and the demand chart is one; so the session draws each
    // frame and fills the chart after it. A modal window dims what's behind it.
    private func drawWindows() {
        let modal = [confirm.frame, notice.frame, budget.frame]
        for frame in windows.windows where !windows.isMinimized(frame) {
            guard let bounds = windows.bounds(of: frame) else { continue }
            if modal.contains(where: { $0 === frame }) {
                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Interface.theme.overlay)
            }
            frame.draw(in: bounds)
            if frame === demand.frame { demand.drawChart(window: bounds) }
        }
    }

    private func drawMinimizedIcons(_ pointer: Vector2) {
        let theme = Interface.theme
        for (index, frame) in windows.minimizedWindows.enumerated() {
            let bounds = iconBounds(index)
            let isHovered = CheckCollisionPointRec(pointer, bounds)
            theme.fillRectangle(bounds, isHovered ? theme.selectedItem : theme.titleBar)
            theme.outlineRectangle(bounds, theme.border)
            let size = theme.textSizes.body
            theme.drawText(
                frame.title, x: Int(bounds.x) + 8, y: Int(bounds.y + (bounds.height - Float(size)) / 2), size: size,
                color: isHovered ? theme.accent : theme.text)
        }
    }
}
