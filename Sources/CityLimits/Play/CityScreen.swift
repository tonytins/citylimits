import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// A city on the screen: its map in a camera, the tools, the bar and ticker, the overview, the
/// windows about the city (budget, demand, help, the city panel, deals, questions, and notices),
/// and the clock that runs it at the speed picked. Playing and editing are sessions that each own
/// one and add their own rules around it; it knows nothing of saving or of either session.
///
/// A frame runs as the session calls it: `handleInput`, then the session's own keys, then `run`.
@MainActor
final class CityScreen {
    // How long what a stroke came to stays beside the pointer once it's let go.
    private static let hintSeconds: Float = 3
    private static let iconSize = Vector2(x: 110, y: 22)

    let city: CitySimulation
    let view: CityView
    let windows = WindowManager()
    let confirm = ConfirmWindow()
    let notice = NoticeWindow()
    let status = StatusBar()
    let cityPanel: CityPanelWindow
    private let palette: ToolPaletteView
    private let overview: OverviewMap
    private let budget: BudgetWindow
    private let demand: DemandWindow
    private let deal: DealWindow
    private let help = HelpWindow()

    private var clock = GameClock()
    private(set) var speed = GameSpeed.paused
    // The speed play brings back after a pause.
    private(set) var lastSpeed = GameSpeed.normal
    private var hint: String?
    private var hintLeft: Float = 0
    private var messages = CityMessageFilter()
    private var newsDesk: NewsDesk
    // Headlines are for show, so they use random numbers of their own, never the city's.
    private var newsRandom = SystemRandomNumberGenerator()

    // The session's own windows: which dim what's behind them, and how to draw one with a region.
    private var dimming: [WindowFrame] = []
    private var drawers: [ObjectIdentifier: (Rectangle) -> Void] = [:]

    /// What the bar shows in place of the city's name, if anything.
    var barName: String?
    /// Told when Escape is pressed and there's no stroke or window for it to close.
    var onEscape: (() -> Void)?
    /// Told when Escape closes a window, so a session can act on it, as settings are saved.
    var onClosedByEscape: ((WindowFrame) -> Void)?
    /// Told whether the player may type in a window, so Escape leaves the window alone.
    var isTyping: () -> Bool = { false }

    init(city: CitySimulation, assets: GameAssets, speed: GameSpeed) {
        self.city = city
        view = CityView(city: city, assets: assets)
        palette = ToolPaletteView(assets: assets, definitions: city.definitions)
        overview = OverviewMap(city: city)
        budget = BudgetWindow(city: city)
        demand = DemandWindow(city: city)
        cityPanel = CityPanelWindow(city: city)
        deal = DealWindow(city: city)
        newsDesk = NewsDesk(city: city)
        setSpeed(speed)
        dimming = [confirm.frame, notice.frame, budget.frame, cityPanel.frame, deal.frame]
        drawers[ObjectIdentifier(cityPanel.frame)] = { [unowned self] bounds in cityPanel.draw(in: bounds) }
        drawers[ObjectIdentifier(demand.frame)] = { [unowned self] bounds in
            demand.frame.draw(in: bounds)
            demand.drawChart(window: bounds)
        }
        status.ticker.filler = { [unowned self] in newsDesk.next(for: city, random: &newsRandom) }
        city.onEvent = { [unowned self] event in hear(event) }
        cityPanel.onShowOffer = { [unowned self] in deal.open(in: windows) }
        cityPanel.onBreakDeal = { [unowned self] side in askToBreakDeal(side) }
    }

    /// Takes a session's window: whether it dims what's behind it, and how to draw it when the
    /// frame alone can't, as with a map in a region.
    func add(_ frame: WindowFrame, dims: Bool, draw: ((Rectangle) -> Void)? = nil) {
        if dims { dimming.append(frame) }
        if let draw { drawers[ObjectIdentifier(frame)] = draw }
    }

    /// Releases what the screen drew with. Call before `CloseWindow`.
    func unload() {
        city.onEvent = nil
        status.ticker.filler = nil
        overview.unload()
        view.unload()
    }

    /// Puts a line from the game on the ticker, in yellow.
    func advise(_ text: String) { status.ticker.advise(text) }

    func setSpeed(_ chosen: GameSpeed) {
        speed = chosen
        if chosen != .paused { lastSpeed = chosen }
    }

    /// Whether the session's keys may act: no window holds the city, and nothing is typed.
    var takesKeys: Bool { !windows.hasModal && !isTyping() }

    // MARK: What the city says

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
        case .challengeFinished(let won, let goals):
            showResult(won: won, goals: goals)
        default:
            break
        }
    }

    // The challenge's result, on the ticker and in a notice. Win or lose, the city plays on.
    private func showResult(won: Bool, goals: [GoalReport]) {
        guard let challenge = city.challenge else { return }
        let missed = goals.filter { !$0.met }.map(\.text)
        let title = won ? "Challenge won!" : "Challenge lost"
        let message = won
            ? "\(challenge.name): every goal was met by \(challenge.ends). The city is yours to play on."
            : "\(challenge.name): by \(challenge.ends), \(missed.count) of \(goals.count) goals weren't met: \(missed.joined(separator: "; ")). The city is yours to play on."
        status.ticker.advise(title)
        view.cancelStroke()
        notice.show(title, message, in: windows)
    }

    private func askToBreakDeal(_ side: Side) {
        guard let standing = city.neighbours.deal(with: side) else { return }
        let name = city.neighbours.neighbour(side).name
        confirm.ask(
            "Break the deal with \(name)?",
            "It costs \(Figures.money(standing.money)) now, a year of the deal's money, and \(name) won't deal again for \(CityNeighbours.grudgeYears) years.",
            yes: "Break", no: "Keep", in: windows
        ) { [unowned self] isYes in
            if isYes { _ = city.breakDeal(with: side) }
        }
    }

    // MARK: Each frame

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

    /// Handles the frame's input: the windows first, then, while no window holds the city, the
    /// keys and the pointer on the map and the HUD.
    func handleInput(_ elapsed: Float) {
        let pointer = GetMousePosition()
        let isOverWindow = windows.contains(pointer)
        // While a name is typed, Escape drops the edit, not the window.
        let wasTyping = isTyping() || cityPanel.isTyping
        // Windows sit above everything else, so they take the pointer first; their handlers act.
        windows.update()
        cityPanel.handleOrdinances(isFront: windows.frontWindow === cityPanel.frame)
        if windows.hasModal {
            view.cancelStroke()
            // Escape closes the window in front, and the budget closes with Enter or B too; a
            // question waits for its answer.
            let front = windows.frontWindow
            if front === budget.frame, IsKeyPressed(.escape) || IsKeyPressed(.enter) || IsKeyPressed(.b) {
                windows.close(budget.frame)
            } else if let front, front !== confirm.frame, !wasTyping, !isTyping(), !cityPanel.isTyping, IsKeyPressed(.escape) {
                windows.close(front)
                onClosedByEscape?(front)
            }
            _ = status.update(city: city, name: barName, speed: speed, elapsed: 0, takesInput: false)
            return
        }

        let wheel = GetMouseWheelMove()
        if wheel != 0, !isOverInterface(pointer) { view.camera.zoom(at: pointer, by: wheel) }

        // Held Control keeps the letters from panning the map, for the sessions' Ctrl keys.
        let control = IsKeyDown(.leftControl) || IsKeyDown(.rightControl)
        var pan = Vector2.zero
        if !control {
            if IsKeyDown(.a) || IsKeyDown(.left) { pan.x -= 1 }
            if IsKeyDown(.d) || IsKeyDown(.right) { pan.x += 1 }
            if IsKeyDown(.w) || IsKeyDown(.up) { pan.y -= 1 }
            if IsKeyDown(.s) || IsKeyDown(.down) { pan.y += 1 }
        }
        if pan != .zero { view.pan(pan, elapsed: elapsed) }

        if IsKeyPressed(.space) { setSpeed(speed == .paused ? lastSpeed : .paused) }
        if IsKeyPressed(.comma) { setSpeed(GameSpeed(rawValue: max(speed.rawValue - 1, 0)) ?? .paused) }
        if IsKeyPressed(.period) { setSpeed(GameSpeed(rawValue: min(speed.rawValue + 1, GameSpeed.superFast.rawValue)) ?? .superFast) }
        if IsKeyPressed(.escape) {
            if view.stroke != nil {
                view.cancelStroke()
            } else if let front = windows.frontWindow {
                windows.close(front)
            } else {
                onEscape?()
            }
        }
        if !control, IsKeyPressed(.b) { openBudget() }
        if IsKeyPressed(.f1) { help.toggle(in: windows) }

        // The HUD takes the pointer only between strokes and off the windows, so a drag across it
        // doesn't click it.
        let takesPointer = view.stroke == nil && !isOverWindow
        if takesPointer {
            overview.update(camera: view.camera, tileSize: Float(MapRenderer.tileSize))
            if IsMouseButtonPressed(.left), let icon = minimizedIcon(at: pointer) { windows.restore(icon) }
        }
        let action = status.update(city: city, name: barName, speed: speed, elapsed: elapsed, takesInput: takesPointer)
        switch action {
        case .pausePlay: setSpeed(speed == .paused ? lastSpeed : .paused)
        case .speed(let chosen): setSpeed(chosen)
        case .funds: openBudget()
        case .demand: demand.open(in: windows)
        case .help: help.toggle(in: windows)
        case .cityName: openCityPanel(.city)
        case .link(let item): openLink(item)
        case nil: break
        }

        if view.stroke != nil || !isOverWindow { handleStroke(pointer) }
    }

    /// Runs the city for the frame's time at the speed picked, and moves the animations on.
    /// Every modal window pauses the city.
    func run(_ elapsed: Float) {
        if speed != .paused, !windows.hasModal {
            let due = clock.advance(Double(elapsed) * speed.multiplier)
            for _ in 0..<due { city.tick() }
        }
        view.animate(elapsed, speed: windows.hasModal ? .paused : speed)
        if windows.isOpen(budget.frame) { budget.show() }
        if windows.isOpen(demand.frame) { demand.show() }
        if windows.isOpen(cityPanel.frame) { cityPanel.refresh() }
        hintLeft = max(0, hintLeft - elapsed)
        view.update()
    }

    func openCityPanel(_ tab: CityPanelWindow.Tab) {
        view.cancelStroke()
        cityPanel.open(tab, in: windows)
    }

    // A blue line on the ticker opens what it's about: a neighbour's offer while it stands, or
    // else the city's neighbours, or its ordinances.
    private func openLink(_ item: NewsItem) {
        if item.kind == .neighbour, city.neighbours.offer != nil {
            view.cancelStroke()
            deal.open(in: windows)
        } else {
            openCityPanel(item.kind == .neighbour ? .neighbours : .ordinances)
        }
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

    /// Draws the frame. `beforeFrame` draws the session's textures, which can't be drawn inside it.
    func draw(beforeFrame: () -> Void = {}) {
        view.refresh()
        overview.refresh()
        beforeFrame()
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
    // way to draw into a view's region, as the demand chart and the city panel's ordinances need;
    // so each frame is drawn here, with its drawer when it has one. A modal one dims what's behind.
    private func drawWindows() {
        for frame in windows.windows where !windows.isMinimized(frame) {
            guard let bounds = windows.bounds(of: frame) else { continue }
            if dimming.contains(where: { $0 === frame }) {
                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Interface.theme.overlay)
            }
            if let drawer = drawers[ObjectIdentifier(frame)] {
                drawer(bounds)
            } else {
                frame.draw(in: bounds)
            }
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
