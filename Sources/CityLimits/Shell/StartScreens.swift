import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// The logo on the splash colour, with phrases scrolling by under it, for a moment or until any
/// key or click.
@MainActor
final class LoadingScreen {
    private static let seconds: Float = 2.5
    private static let margin: Float = 24

    private let assets: GameAssets
    private let phrases = NewsTicker(fontSize: 20)
    private var next = 0
    private var elapsed: Float = 0
    private var skipped = false

    init(assets: GameAssets) {
        self.assets = assets
        phrases.filler = { [unowned self] in
            let phrase = Headlines.loading[next % Headlines.loading.count]
            next += 1
            return NewsItem(phrase, kind: .headline)
        }
    }

    var isDone: Bool { skipped || elapsed >= Self.seconds }

    /// Where the logo sits while loading: the middle of the screen.
    static func logoPosition(_ size: Vector2) -> Vector2 {
        Vector2(x: (Float(GetScreenWidth()) - size.x) / 2, y: (Float(GetScreenHeight()) - size.y) / 2)
    }

    func update() {
        let frame = GetFrameTime()
        elapsed += frame
        // The first phrase starts a third of the way across, so there's something to read at once.
        phrases.update(frame, width: Float(GetScreenWidth()), startShare: 1.0 / 3)
        if GetKeyPressed() != 0 || IsMouseButtonPressed(.left) || IsMouseButtonPressed(.right) || IsMouseButtonPressed(.middle) {
            skipped = true
        }
    }

    func draw() {
        BeginDrawing()
        ClearBackground(Palette.splash)
        if let logo = assets.logo {
            DrawTextureV(logo, Self.logoPosition(Vector2(x: Float(logo.width), y: Float(logo.height))), .white)
        }
        let height: Float = 24
        phrases.draw(in: Rectangle(x: 0, y: Float(GetScreenHeight()) - height - Self.margin, width: Float(GetScreenWidth()), height: height))
        EndDrawing()
    }
}

/// The start menu: the logo, which glides up from where the loading screen left it, and New Game,
/// Load Game, Scenarios, Settings, and Quit under it. Its windows open over it.
@MainActor
final class StartMenu {
    enum Choice {
        case newCity(name: String, seed: Int32?)
        case loadCity(LoadedCity, file: URL)
        case playChallenge(CitySimulation)
        case editScenario(ScenarioEdit, city: CitySimulation)
        case quit
    }

    private enum Item: CaseIterable {
        case newGame, loadGame, scenarios, settings, quit

        var title: String {
            switch self {
            case .newGame: "New Game"
            case .loadGame: "Load Game"
            case .scenarios: "Scenarios"
            case .settings: "Settings"
            case .quit: "Quit"
            }
        }
    }

    private static let fontSize: Int32 = 20
    private static let itemSpacing: Float = 36
    private static let glideSeconds: Float = 0.3

    private let assets: GameAssets
    private let version: String
    private let settings: SettingsKeeper
    private let windows = WindowManager()
    private let newCity = NewCityWindow()
    private let newScenario = NewCityWindow()
    private let notice = NoticeWindow()
    private let scenariosFolder: URL
    private let settingsWindow = SettingsWindow()
    private let load: LoadWindow
    private let scenarios: ScenarioWindow
    private var glide: Float = 0
    private var commandLineSeed: Int32?
    private var choice: Choice?

    /// A menu that loads cities from `load` and lists challenges from `scenarios`.
    init(assets: GameAssets, version: String, settings: SettingsKeeper, load: LoadWindow, scenarios: ScenarioWindow, scenariosFolder: URL) {
        self.assets = assets
        self.scenariosFolder = scenariosFolder
        self.version = version
        self.settings = settings
        self.load = load
        self.scenarios = scenarios
        newCity.onStart = { [unowned self] name, seed in choice = .newCity(name: name, seed: seed) }
        load.onOpen = { [unowned self] loaded, file in choice = .loadCity(loaded, file: file) }
        scenarios.onPlay = { [unowned self] city in choice = .playChallenge(city) }
        scenarios.onNew = { [unowned self] in newScenario.open(title: "New scenario", name: "New Scenario", seed: commandLineSeed, in: windows) }
        scenarios.onEdit = { [unowned self] file in
            startEditing("open it") { () throws(FormatError) in try ScenarioEdit.open(file, saveFolder: scenariosFolder) }
        }
        newScenario.onStart = { [unowned self] name, seed in
            startEditing("start it") { () throws(FormatError) in
                try ScenarioEdit.new(name: name, seed: seed ?? Int32.random(in: 0...Int32.max), saveFolder: scenariosFolder)
            }
        }
        settingsWindow.onChange = { [unowned settings] chosen in settings.settings = chosen }
        settingsWindow.onClose = { [unowned settings] _ in settings.save() }
    }

    // Opens a scenario in the editor, with its city, or says why it couldn't.
    private func startEditing(_ what: String, _ make: () throws(FormatError) -> ScenarioEdit) {
        do throws(FormatError) {
            let edit = try make()
            choice = .editScenario(edit, city: try edit.openCity())
        } catch {
            warn(error.message)
            notice.show("The scenario couldn't \(what)", error.message, in: windows)
        }
    }

    func unload() {
        newScenario.unload()
        newCity.unload()
        load.unload()
        scenarios.unload()
    }

    /// Shows the menu afresh: straight from the loading screen, the logo glides into place.
    func show(fromLoading: Bool, seed: Int32?) {
        glide = fromLoading ? 0 : Self.glideSeconds
        commandLineSeed = seed
        choice = nil
        // Load Game and Scenarios are dimmed while there's nothing to open.
        load.refreshList()
        scenarios.refreshList()
    }

    /// Runs the menu until the player chooses, or the window closes, which counts as quitting.
    func run() -> Choice {
        while !WindowShouldClose() {
            update()
            if let choice { return choice }
            draw()
        }
        return .quit
    }

    private func update() {
        glide = min(glide + GetFrameTime(), Self.glideSeconds)
        let wasOverWindow = windows.contains(GetMousePosition())
        windows.update()
        if windows.isOpen(newCity.frame) { newCity.refresh() }
        if windows.isOpen(newScenario.frame) { newScenario.refresh() }
        if windows.isOpen(settingsWindow.frame) { settingsWindow.refresh(in: windows) }
        load.refresh()
        scenarios.refresh()
        if IsKeyPressed(.escape), let front = windows.frontWindow {
            windows.close(front)
            if front === settingsWindow.frame { settings.save() }
        }
        guard !windows.hasModal, !wasOverWindow, IsMouseButtonPressed(.left), let item = item(at: GetMousePosition()) else { return }
        switch item {
        case .newGame: newCity.open(seed: commandLineSeed, in: windows)
        case .settings: settingsWindow.open(settings.settings, in: windows)
        case .quit: choice = .quit
        case .loadGame: load.open(in: windows)
        case .scenarios: scenarios.open(in: windows)
        }
    }

    private func isEnabled(_ item: Item) -> Bool {
        switch item {
        case .loadGame: load.hasCities
        case .scenarios: scenarios.hasScenarios
        default: true
        }
    }

    private func item(at point: Vector2) -> Item? {
        Item.allCases.first { isEnabled($0) && CheckCollisionPointRec(point, bounds(of: $0)) }
    }

    private var logoSize: Vector2 { assets.logo.map { Vector2(x: Float($0.width), y: Float($0.height)) } ?? .zero }

    // A sixth of the way down, as the mock-up has it; on the way there, eased out from the middle.
    private var logoPosition: Vector2 {
        let size = logoSize
        let target = Vector2(x: (Float(GetScreenWidth()) - size.x) / 2, y: Float(GetScreenHeight()) / 6)
        let start = LoadingScreen.logoPosition(size)
        let t = glide / Self.glideSeconds
        let eased = 1 - (1 - t) * (1 - t) * (1 - t)
        return Vector2(x: start.x + (target.x - start.x) * eased, y: start.y + (target.y - start.y) * eased)
    }

    // The items sit under the logo's final place, so they don't move while it glides.
    private func bounds(of item: Item) -> Rectangle {
        let index = Float(Item.allCases.firstIndex(of: item) ?? 0)
        let top = Float(GetScreenHeight()) / 6 + logoSize.y + 24 + index * Self.itemSpacing
        let width = Float(MeasureText(item.title, Self.fontSize))
        return Rectangle(x: (Float(GetScreenWidth()) - width) / 2, y: top, width: width, height: Float(Self.fontSize))
    }

    private func draw() {
        BeginDrawing()
        ClearBackground(Palette.splash)
        if let logo = assets.logo { DrawTextureV(logo, logoPosition, .white) }
        let pointer = GetMousePosition()
        let hovered = windows.hasModal ? nil : item(at: pointer)
        for item in Item.allCases {
            let bounds = bounds(of: item)
            let color = !isEnabled(item) ? Palette.textDim : item == hovered ? Palette.accent : Palette.text
            DrawText(item.title, Int32(bounds.x), Int32(bounds.y), Self.fontSize, color)
        }
        let size: Int32 = 10
        DrawText(version, GetScreenWidth() - MeasureText(version, size) - 16, GetScreenHeight() - size - 16, size, Palette.text)

        for frame in windows.windows where !windows.isMinimized(frame) {
            guard let bounds = windows.bounds(of: frame) else { continue }
            DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Interface.theme.overlay)
            if frame === newCity.frame {
                newCity.draw(in: bounds)
            } else if frame === newScenario.frame {
                newScenario.draw(in: bounds)
            } else if frame === load.frame {
                load.draw(in: bounds)
            } else if frame === scenarios.frame {
                scenarios.draw(in: bounds)
            } else {
                frame.draw(in: bounds)
            }
        }
        windows.drawTooltip()
        EndDrawing()
    }
}

/// The settings, shared by the start menu and the city being played, and the file they're kept in.
@MainActor
final class SettingsKeeper {
    var settings: GameSettings
    let file: URL
    /// Told what went wrong when the settings couldn't be saved.
    var onProblem: ((String) -> Void)?

    init(file: URL) {
        self.file = file
        let (loaded, warning) = GameSettings.load(file)
        settings = loaded
        if let warning { warn(warning) }
    }

    func save() {
        do throws(FormatError) {
            try settings.save(to: file)
        } catch {
            warn(error.message)
            onProblem?(error.message)
        }
    }
}
