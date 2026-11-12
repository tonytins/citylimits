import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// Editing a scenario: its city on the screen, built on for free, with the editor's rules around
/// it. It opens paused; playing tests the scenario as a challenge on the city as it stands, and
/// the Escape menu resets the test or starts over. Ctrl+S saves the scenario with its city beside
/// it, the city panel's Edit scenario opens its details, and it never autosaves or opens another
/// city. Playing is a session of its own beside this one.
@MainActor
final class EditorSession {
    enum Leave {
        case toMenu
        case quit
    }

    private(set) var screen: CityScreen
    let edit: ScenarioEdit
    private let assets: GameAssets
    private let settings: SettingsKeeper
    private let menu = EditorMenuWindow()
    private let settingsWindow = SettingsWindow()
    private let details = ScenarioDetailsWindow()
    private(set) var leaving: Leave?

    private var city: CitySimulation { screen.city }

    /// Edits a scenario, starting from the city `edit.openCity()` gave.
    init(edit: ScenarioEdit, city: CitySimulation, assets: GameAssets, settings: SettingsKeeper) {
        self.edit = edit
        self.assets = assets
        self.settings = settings
        screen = CityScreen(city: city, assets: assets, speed: .paused)
        menu.onChoice = { [unowned self] choice in choose(choice) }
        settingsWindow.onChange = { [unowned self] chosen in
            settings.settings = chosen
            // The city on the screen now, which a reset or a fresh start replaces.
            chosen.apply(to: self.city)
        }
        settingsWindow.onClose = { [unowned settings] _ in settings.save() }
        details.onApply = { [unowned self] chosen in apply(chosen) }
        attach()
        screen.advise("Editing \(edit.scenario.name): building is free, and playing tests it; Escape's menu goes back")
    }

    // Puts the editor's windows and rules on the screen, which is new after a reset.
    private func attach() {
        screen.add(menu.frame, dims: true)
        screen.add(settingsWindow.frame, dims: true)
        screen.add(details.frame, dims: true)
        screen.onEscape = { [unowned self] in menu.open(testing: edit.isTesting, in: screen.windows) }
        screen.onClosedByEscape = { [unowned self] frame in if frame === settingsWindow.frame { settings.save() } }
        screen.isTyping = { [unowned self] in details.isTyping }
        screen.cityPanel.onEditScenario = { [unowned self] in
            guard !edit.isTesting else { return }
            details.open(edit.scenario, canMoveStart: edit.canMoveStart(city), in: screen.windows)
        }
        settings.onProblem = { [unowned self] problem in screen.advise(problem) }
        settings.settings.apply(to: city)
        refreshDraft()
    }

    // The city panel shows the scenario as a challenge not yet begun, until a test begins one.
    private func refreshDraft() {
        screen.cityPanel.draft = edit.isTesting ? nil : Challenge.begin(edit.scenario, on: city)
    }

    /// Releases what the session drew with. Call before `CloseWindow`.
    func unload() {
        settings.onProblem = nil
        screen.unload()
    }

    // MARK: Each frame

    func update() {
        let elapsed = GetFrameTime()
        screen.barName = "\(edit.isTesting ? "Testing" : "Editing") \(city.name)"
        screen.handleInput(elapsed)
        if screen.windows.isOpen(settingsWindow.frame) { settingsWindow.refresh(in: screen.windows) }
        if screen.takesKeys {
            let control = IsKeyDown(.leftControl) || IsKeyDown(.rightControl)
            if control, IsKeyPressed(.s) { save(confirmed: false) }
            if control, IsKeyPressed(.o) { screen.advise("No other city opens while a scenario is edited") }
        }
        // Playing tests the scenario, from the city as it stands.
        if screen.speed != .paused, !edit.isTesting, !screen.windows.hasModal { beginTest() }
        screen.run(elapsed)
    }

    func draw() { screen.draw() }

    private func beginTest() {
        screen.view.cancelStroke()
        edit.beginTest(city)
        refreshDraft()
        screen.advise("Testing \(edit.scenario.name): Escape's menu resets it")
    }

    // MARK: The menu

    private func choose(_ choice: EditorMenuWindow.Choice) {
        switch choice {
        case .save:
            save(confirmed: false)
        case .reset:
            guard edit.isTesting else { return }
            screen.confirm.ask(
                "Reset the test?", "The city goes back to how it was when you pressed play: the test, and everything built during it, are undone.",
                yes: "Reset", no: "Stay", in: screen.windows
            ) { [unowned self] isYes in
                if isYes { reopen("reset the test") { () throws(FormatError) in try edit.endTest() } }
            }
        case .startOver:
            screen.confirm.ask(
                "Start \(edit.scenario.name) over?", "Its city opens again as the scenario has it, and every change since is undone.",
                yes: "Start over", no: "Stay", in: screen.windows
            ) { [unowned self] isYes in
                if isYes { reopen("started over") { () throws(FormatError) in try edit.openCity() } }
            }
        case .settings:
            settingsWindow.open(settings.settings, in: screen.windows)
        case .mainMenu:
            leaveOrAsk(.toMenu)
        case .quit:
            leaveOrAsk(.quit)
        }
    }

    // Puts another city on the screen, looked at from where the last one was.
    private func reopen(_ what: String, _ open: () throws(FormatError) -> CitySimulation) {
        let opened: CitySimulation
        do throws(FormatError) {
            opened = try open()
        } catch {
            warn("Editing \(edit.scenario.name): couldn't have \(what): \(error.message)")
            screen.advise(error.message)
            return
        }
        let (center, zoom) = (screen.view.camera.centerPoint, screen.view.camera.zoom)
        screen.unload()
        screen = CityScreen(city: opened, assets: assets, speed: .paused)
        screen.view.camera.zoom(to: zoom)
        screen.view.camera.center(on: center)
        attach()
        screen.advise("\(edit.scenario.name): \(what)")
    }

    // MARK: Details, saving, and leaving

    private func apply(_ chosen: ScenarioDetails) -> String? {
        do throws(FormatError) {
            let changed = try edit.change(chosen, city: city)
            edit.accept(changed, city: city)
            refreshDraft()
            return nil
        } catch {
            return error.message
        }
    }

    /// Saves the scenario, with its city beside it, to the player's scenarios folder. Saving over
    /// its own file is plain saving; replacing another scenario of the same name asks first.
    private func save(confirmed: Bool) {
        guard !edit.isTesting else {
            screen.advise("A scenario can't be saved while it's tested: reset it first.")
            return
        }
        let path: URL
        do throws(FormatError) {
            path = try edit.savePath()
        } catch {
            screen.advise(error.message)
            return
        }
        if !confirmed, FileManager.default.fileExists(atPath: path.path), path.standardizedFileURL != edit.file.standardizedFileURL {
            screen.confirm.ask(
                "Replace a saved scenario?", "\(path.lastPathComponent) already holds a scenario, and saving would replace it and its city.",
                yes: "Replace", no: "Cancel", in: screen.windows
            ) { [unowned self] isYes in
                if isYes { save(confirmed: true) }
            }
            return
        }
        do throws(FormatError) {
            try edit.save(city)
        } catch {
            warn(error.message)
            screen.advise(error.message)
            return
        }
        refreshDraft()
        screen.advise("Saved \(edit.scenario.name) to \(edit.file.lastPathComponent)")
    }

    // Leaving with changes since the last save asks first.
    private func leaveOrAsk(_ leave: Leave) {
        guard edit.hasChanges(city) else {
            leaving = leave
            return
        }
        screen.confirm.ask(
            "Leave \(edit.scenario.name) \(leave == .toMenu ? "for the menu" : "and quit")?",
            "It has changes since it was last saved. Ctrl+S saves it first.", yes: "Leave", no: "Stay", in: screen.windows
        ) { [unowned self] isYes in
            if isYes { leaving = leave }
        }
    }
}
