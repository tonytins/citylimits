import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// Playing a city: the city on its screen, with the rules of play around it. It saves with Ctrl+S
/// and autosaves, both held off while a disaster is under way; opens other cities; and asks before
/// leaving a city with unsaved changes. The editor is a session of its own beside this one.
@MainActor
final class PlaySession {
    /// How the player left the city.
    enum Leave {
        case toMenu
        case quit
        /// Opens another city, read from its file.
        case open(LoadedCity, file: URL)
    }

    let screen: CityScreen
    private var city: CitySimulation { screen.city }
    private let gameMenu = GameMenuWindow()
    private let settingsWindow = SettingsWindow()
    private let load: LoadWindow
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

    /// Plays a city: a new one, or one opened from `file`, which is then the file Ctrl+S saves to.
    init(
        city: CitySimulation, file: URL? = nil, assets: GameAssets, settings: SettingsKeeper, citiesFolder: URL,
        definitions: @escaping () -> CityDefinitions
    ) {
        screen = CityScreen(city: city, assets: assets, speed: settings.settings.speed)
        load = LoadWindow(folder: citiesFolder, definitions: definitions)
        self.settings = settings
        self.citiesFolder = citiesFolder
        screen.add(gameMenu.frame, dims: true)
        screen.add(settingsWindow.frame, dims: true)
        screen.add(load.frame, dims: true) { [unowned load] bounds in load.draw(in: bounds) }
        screen.onEscape = { [unowned self] in gameMenu.open(in: screen.windows) }
        screen.onClosedByEscape = { [unowned self] frame in if frame === settingsWindow.frame { settings.save() } }
        gameMenu.onChoice = { [unowned self] choice in choose(choice) }
        load.onOpen = { [unowned self] loaded, file in leaveOrAsk(.open(loaded, file: file)) }
        settingsWindow.onChange = { [unowned settings, unowned city] chosen in
            settings.settings = chosen
            chosen.apply(to: city)
        }
        settingsWindow.onClose = { [unowned settings] _ in settings.save() }
        settings.onProblem = { [unowned screen] problem in screen.advise(problem) }
        settings.settings.apply(to: city)
        if let file {
            cityFile = file
            savedText = CityFiles.flatText(city)
            autosavedText = savedText
        }
        if let challenge = city.challenge, challenge.won == nil {
            screen.advise("\(challenge.name): meet its goals by \(challenge.ends); the city panel lists them")
        }
    }

    /// Puts a line from the game on the ticker, in yellow.
    func advise(_ text: String) { screen.advise(text) }

    /// Releases what the session drew with. Call before `CloseWindow`.
    func unload() {
        settings.settings.speed = screen.speed
        settings.onProblem = nil
        load.unload()
        screen.unload()
    }

    // MARK: Each frame

    func update() {
        let elapsed = GetFrameTime()
        screen.handleInput(elapsed)
        if screen.windows.isOpen(settingsWindow.frame) { settingsWindow.refresh(in: screen.windows) }
        if screen.takesKeys {
            // Ctrl+S saves, and Ctrl+O opens another city.
            let control = IsKeyDown(.leftControl) || IsKeyDown(.rightControl)
            if control, IsKeyPressed(.s) { save(confirmed: false) }
            if control, IsKeyPressed(.o) { load.open(in: screen.windows) }
        }
        screen.run(elapsed)
        keepSaving(elapsed)
    }

    func draw() {
        screen.draw { load.refresh() }
    }

    // MARK: Saving and leaving

    private func choose(_ choice: GameMenuWindow.Choice) {
        switch choice {
        case .save: save(confirmed: false)
        case .load: load.open(in: screen.windows)
        case .settings: settingsWindow.open(settings.settings, in: screen.windows)
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
            screen.advise("Saving disabled")
            return
        }
        guard let name = CityFiles.fileName(for: city.name, extension: saveExtension) else {
            screen.advise("\(city.name) can't be a file's name; rename the city first")
            return
        }
        let file = citiesFolder.appendingPathComponent(name)
        let own: URL? = cityFile.flatMap { file in
            if file.pathExtension.lowercased() == MicropolisImport.fileExtension { return nil }
            return Autosave.isAutosave(file) ? Autosave.cityFile(for: file) : file
        }
        if !confirmed, FileManager.default.fileExists(atPath: file.path), file.standardizedFileURL != own?.standardizedFileURL {
            screen.confirm.ask(
                "Replace a saved city?", "\(name) already holds a saved city, and saving would replace it.", yes: "Replace", no: "Cancel",
                in: screen.windows
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
            screen.advise(error.message)
            return
        }
        (cityFile, savedText, autosavedText) = (file, text, text)
        screen.advise("Saved \(city.name) to \(name)")
    }

    // Counts play towards the next autosave, which waits out a disaster, and says once when a
    // disaster stops saving.
    private func keepSaving(_ elapsed: Float) {
        let holding = city.holdsSaving
        if holding, !wasHoldingSaves { screen.advise("Saving disabled") }
        wasHoldingSaves = holding
        let slots = settings.settings.autosaveSlots
        guard slots > 0 else { return }
        if autosave.advance(Double(elapsed), running: screen.speed != .paused && !screen.windows.hasModal) { autosaveDue = true }
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
            screen.advise("Autosave failed: \(error.message)")
            return
        }
        autosavedText = text
        screen.advise("Autosaved \(city.name)")
    }

    // Leaving a city with changes since its last save asks first, so a city isn't lost by a click.
    private func leaveOrAsk(_ leave: Leave) {
        guard hasUnsavedChanges else {
            leaving = leave
            return
        }
        let whereTo = switch leave {
        case .toMenu: "for the menu"
        case .quit: "and quit"
        case .open(let loaded, _): "for \(loaded.city.name)"
        }
        screen.confirm.ask(
            "Leave \(city.name) \(whereTo)?", "It has changes since it was last saved. Ctrl+S saves it first.", yes: "Leave", no: "Stay",
            in: screen.windows
        ) { [unowned self] isYes in
            if isYes { leaving = leave }
        }
    }
}
