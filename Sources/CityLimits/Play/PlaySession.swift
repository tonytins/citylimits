import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// Playing a city: its map and tools, the clock that runs it at the speed the player picks, and
/// what the player is told. The editor will be a session of its own beside this one, not a flag
/// inside it.
@MainActor
final class PlaySession {
    /// How the player left the city.
    enum Leave {
        case toMenu
        case quit
    }

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
    private let cityPanel: CityPanelWindow
    private let deal: DealWindow
    private let help = HelpWindow()
    private let gameMenu = GameMenuWindow()
    private let settingsWindow = SettingsWindow()
    private let settings: SettingsKeeper
    private let citiesFolder: URL
    private(set) var leaving: Leave?

    // The file Ctrl+S saves to, once the city has one, and the city as last saved, in the flat
    // format, which later saves and leaving are compared with whichever format the file is in.
    private var cityFile: URL?
    private var savedText: String?
    private var autosavedText: String?
    private var autosave = Autosave()
    private var autosaveDue = false
    private var wasHoldingSaves = false
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

    /// Plays a city: a new one, or one opened from `file`, which is then the file Ctrl+S saves to.
    init(city: CitySimulation, file: URL? = nil, assets: GameAssets, settings: SettingsKeeper, citiesFolder: URL) {
        self.city = city
        self.settings = settings
        self.citiesFolder = citiesFolder
        view = CityView(city: city, assets: assets)
        palette = ToolPaletteView(assets: assets, definitions: city.definitions)
        overview = OverviewMap(city: city)
        budget = BudgetWindow(city: city)
        demand = DemandWindow(city: city)
        cityPanel = CityPanelWindow(city: city)
        deal = DealWindow(city: city)
        newsDesk = NewsDesk(city: city)
        status.ticker.filler = { [unowned self] in newsDesk.next(for: city, random: &newsRandom) }
        city.onEvent = { [unowned self] event in hear(event) }
        cityPanel.onShowOffer = { [unowned self] in deal.open(in: windows) }
        cityPanel.onBreakDeal = { [unowned self] side in askToBreakDeal(side) }
        gameMenu.onChoice = { [unowned self] choice in choose(choice) }
        settingsWindow.onChange = { [unowned settings, unowned city] chosen in
            settings.settings = chosen
            chosen.apply(to: city)
        }
        settingsWindow.onClose = { [unowned settings] _ in settings.save() }
        settings.onProblem = { [unowned self] problem in status.ticker.advise(problem) }
        settings.settings.apply(to: city)
        speed = settings.settings.speed
        if speed != .paused { lastSpeed = speed }
        if let file {
            cityFile = file
            savedText = CityFiles.flatText(city)
            autosavedText = savedText
        }
        if let challenge = city.challenge, challenge.won == nil {
            status.ticker.advise("\(challenge.name): meet its goals by \(challenge.ends); the city panel lists them")
        }
    }

    /// Puts a line from the game on the ticker, in yellow.
    func advise(_ text: String) { status.ticker.advise(text) }

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

    /// Releases what the session drew with. Call before `CloseWindow`.
    func unload() {
        settings.settings.speed = speed
        settings.onProblem = nil
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
        case .challengeFinished(let won, let goals):
            showResult(won: won, goals: goals)
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
        keepSaving(elapsed)
        if windows.isOpen(budget.frame) { budget.show() }
        if windows.isOpen(demand.frame) { demand.show() }
        if windows.isOpen(cityPanel.frame) { cityPanel.refresh() }
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
        // While the city's name is typed, Escape drops the edit, not the panel.
        let wasTyping = cityPanel.isTyping
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
            } else if let front, front !== confirm.frame, !wasTyping, !cityPanel.isTyping, IsKeyPressed(.escape) {
                windows.close(front)
                if front === settingsWindow.frame { settings.save() }
            }
            if windows.isOpen(settingsWindow.frame) { settingsWindow.refresh(in: windows) }
            _ = status.update(city: city, speed: speed, elapsed: 0, takesInput: false)
            return
        }

        let wheel = GetMouseWheelMove()
        if wheel != 0, !isOverInterface(pointer) { view.camera.zoom(at: pointer, by: wheel) }

        // Ctrl+S saves; held Control keeps the letters from panning the map.
        let control = IsKeyDown(.leftControl) || IsKeyDown(.rightControl)
        if control, IsKeyPressed(.s) { save(confirmed: false) }

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
                gameMenu.open(in: windows)
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
        case .cityName: openCityPanel(.city)
        case .link(let item): openLink(item)
        case nil: break
        }

        if view.stroke != nil || !isOverWindow { handleStroke(pointer) }
    }

    private func openCityPanel(_ tab: CityPanelWindow.Tab) {
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

    // MARK: Saving and leaving

    private func choose(_ choice: GameMenuWindow.Choice) {
        switch choice {
        case .save: save(confirmed: false)
        case .settings: settingsWindow.open(settings.settings, in: windows)
        case .mainMenu: leaveOrAsk(.toMenu)
        case .quit: leaveOrAsk(.quit)
        }
    }

    // A city saves in its own format: flat when it came from a flat file, and otherwise binary,
    // a Micropolis city included.
    private var saveExtension: String {
        cityFile?.pathExtension.lowercased() == CityFiles.flatExtension ? CityFiles.flatExtension : CityFiles.binaryExtension
    }

    private var hasUnsavedChanges: Bool { savedText == nil || CityFiles.flatText(city) != savedText }

    /// Saves the city under its name. Saving over its own file is plain saving, an autosave's
    /// city counting as its own; replacing another city's file of the same name asks first. While
    /// a disaster is under way, it doesn't save at all.
    private func save(confirmed: Bool) {
        guard !city.holdsSaving else {
            status.ticker.advise("Saving disabled")
            return
        }
        guard let name = CityFiles.fileName(for: city.name, extension: saveExtension) else {
            status.ticker.advise("\(city.name) can't be a file's name; rename the city first")
            return
        }
        let file = citiesFolder.appendingPathComponent(name)
        let own: URL? = cityFile.flatMap { file in
            if file.pathExtension.lowercased() == MicropolisImport.fileExtension { return nil }
            return Autosave.isAutosave(file) ? Autosave.cityFile(for: file) : file
        }
        if !confirmed, FileManager.default.fileExists(atPath: file.path), file.standardizedFileURL != own?.standardizedFileURL {
            confirm.ask(
                "Replace a saved city?", "\(name) already holds a saved city, and saving would replace it.", yes: "Replace", no: "Cancel",
                in: windows
            ) { [unowned self] isYes in
                if isYes { save(confirmed: true) }
            }
            return
        }
        let text = CityFiles.flatText(city)
        do throws(FormatError) {
            try CityFiles.write(city, to: file)
        } catch {
            warn(error.message)
            status.ticker.advise(error.message)
            return
        }
        (cityFile, savedText, autosavedText) = (file, text, text)
        status.ticker.advise("Saved \(city.name) to \(name)")
    }

    // Counts play towards the next autosave, which waits out a disaster, and says once when a
    // disaster stops saving.
    private func keepSaving(_ elapsed: Float) {
        let holding = city.holdsSaving
        if holding, !wasHoldingSaves { status.ticker.advise("Saving disabled") }
        wasHoldingSaves = holding
        let slots = settings.settings.autosaveSlots
        guard slots > 0 else { return }
        if autosave.advance(Double(elapsed), running: speed != .paused && !windows.hasModal) { autosaveDue = true }
        guard autosaveDue, !holding else { return }
        autosaveDue = false
        autosaveNow(slots: slots)
    }

    // An autosave is a save beside the city's own, written only when the city has changed since it
    // was last saved either way. It doesn't count as the player saving, so leaving still asks.
    private func autosaveNow(slots: Int) {
        let text = CityFiles.flatText(city)
        guard text != savedText, text != autosavedText else { return }
        guard let file = Autosave.nextFile(in: citiesFolder, cityName: city.name, slots: slots, extension: saveExtension) else { return }
        do throws(FormatError) {
            try CityFiles.write(city, to: file)
        } catch {
            warn("Autosave failed: \(error.message)")
            status.ticker.advise("Autosave failed: \(error.message)")
            return
        }
        autosavedText = text
        status.ticker.advise("Autosaved \(city.name)")
    }

    // Leaving a city with changes since its last save asks first, so a city isn't lost by a click.
    private func leaveOrAsk(_ leave: Leave) {
        guard hasUnsavedChanges else {
            leaving = leave
            return
        }
        let whereTo = leave == .toMenu ? "for the menu" : "and quit"
        confirm.ask(
            "Leave \(city.name) \(whereTo)?", "It has changes since it was last saved. Ctrl+S saves it first.", yes: "Leave", no: "Stay",
            in: windows
        ) { [unowned self] isYes in
            if isYes { leaving = leave }
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
        let modal = [confirm.frame, notice.frame, budget.frame, cityPanel.frame, deal.frame, gameMenu.frame, settingsWindow.frame]
        for frame in windows.windows where !windows.isMinimized(frame) {
            guard let bounds = windows.bounds(of: frame) else { continue }
            if modal.contains(where: { $0 === frame }) {
                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Interface.theme.overlay)
            }
            if frame === cityPanel.frame {
                cityPanel.draw(in: bounds)
            } else {
                frame.draw(in: bounds)
            }
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
