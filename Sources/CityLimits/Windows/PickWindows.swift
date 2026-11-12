import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// The saved cities, newest first, in a modal window. Picking one reads it, to show its map and
/// what it holds; Open, or a double-click, hands it to `onOpen` with its file.
@MainActor
final class LoadWindow {
    let frame: WindowFrame
    private let view = Interface.view("loadgame", fallbackSize: Vector2(x: 660, y: 400))
    private let preview = MapPreview()
    private let folder: URL
    private let definitions: () -> CityDefinitions
    private var cities: [SavedCitySummary] = []
    private var picked: (file: URL, loaded: LoadedCity)?
    var onOpen: ((LoadedCity, URL) -> Void)?

    /// Lists the cities in `folder`, read with the definitions the game would play them with.
    init(folder: URL, definitions: @escaping () -> CityDefinitions) {
        self.folder = folder
        self.definitions = definitions
        frame = WindowFrame(view: view, title: "Load a city")
    }

    /// Whether there's any saved city to load, as of the last look.
    var hasCities: Bool { !cities.isEmpty }

    /// Looks at the folder again.
    func refreshList() {
        cities = CityFiles.list(folder)
    }

    func open(in windows: WindowManager) {
        refreshList()
        view.list(named: "cities")?.items = cities.map(Self.line)
        view.list(named: "cities")?.selectedIndex = nil
        view.setHidden(!cities.isEmpty, named: "empty")
        picked = nil
        preview.show(note: cities.isEmpty ? "" : "Pick a city to see it")
        show(stats: "", problem: "")
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .closeRequested, .view(.pressed(control: "back")):
                windows.close(frame)
            case .view(.selected(control: "cities", let index)):
                pick(index)
            case .view(.pressed(control: "open")), .view(.submitted(control: "cities", text: _)):
                guard let picked else { return }
                windows.close(frame)
                onOpen?(picked.loaded, picked.file)
            default:
                break
            }
        }
    }

    private static func line(_ city: SavedCitySummary) -> String {
        let kind = switch city.file.pathExtension.lowercased() {
        case MicropolisImport.fileExtension: "   Micropolis city, to import"
        case _ where Autosave.isAutosave(city.file): "   autosave"
        case CityFiles.flatExtension: "   flat file"
        default: ""
        }
        let date = "\(city.date.month < 10 ? "0" : "")\(city.date.month)/\(city.date.year)"
        return "\(city.name)   \(date)   \(Figures.count(city.population)) Pop\(kind)"
    }

    private func pick(_ index: Int) {
        guard cities.indices.contains(index) else { return }
        let chosen = cities[index]
        do throws(FormatError) {
            let loaded = try CityFiles.read(chosen.file, definitions: definitions())
            picked = (chosen.file, loaded)
            preview.show(loaded.city.map)
            let city = loaded.city
            let age = city.date.year - city.foundedYear
            var stats = [
                "\(city.name)",
                "\(city.date.month < 10 ? "0" : "")\(city.date.month)/\(city.date.year), \(age == 0 ? "founded this year" : age == 1 ? "1 year old" : "\(age) years old")",
                "Population \(Figures.count(city.census.cityPopulation))",
                "Funds \(Figures.money(city.treasury.funds)), tax \(city.budget.taxRate)%",
                "Saved \(Self.stamp(chosen.saved))",
            ]
            if let challenge = city.challenge { stats.append("Challenge: \(challenge.name)") }
            show(stats: stats.joined(separator: "\n"), problem: "")
        } catch {
            picked = nil
            preview.show(note: "No map: the file can't be read")
            show(stats: "", problem: "\(chosen.file.lastPathComponent): \(error.message)")
        }
    }

    private func show(stats: String, problem: String) {
        view.setText(stats, named: "stats")
        view.setWrapped(problem, named: "problem", fontSize: 12)
        view.setDisabled(picked == nil, named: "open")
    }

    // When it was saved, in the machine's time, as 2026-11-12 09:14.
    private static func stamp(_ date: Date) -> String {
        let parts = Calendar.current.dateComponents([.year, .month, .day, .hour, .minute], from: date)
        func two(_ value: Int?) -> String { String(format: "%02d", value ?? 0) }
        return "\(parts.year ?? 0)-\(two(parts.month))-\(two(parts.day)) \(two(parts.hour)):\(two(parts.minute))"
    }

    /// Draws the map of the city picked waiting, if any. Call before `BeginDrawing`.
    func refresh() { preview.refresh() }

    /// Draws the window and the map of the city picked.
    func draw(in bounds: Rectangle) {
        frame.draw(in: bounds)
        let content = WindowFrame.content(in: bounds)
        if let region = view.bounds(named: "preview", at: Vector2(x: content.x, y: content.y)) { preview.draw(in: region) }
    }

    func unload() { preview.unload() }
}

/// The challenges, the game's own and then the player's, in a modal window. Picking one shows its
/// map and what it asks; Play, or a double-click, hands the city it starts to `onPlay`.
@MainActor
final class ScenarioWindow {
    let frame: WindowFrame
    private let view = Interface.view("scenarios", fallbackSize: Vector2(x: 660, y: 400))
    private let preview = MapPreview()
    private let catalog: ScenarioCatalog
    private var listed: [(file: URL, scenario: Scenario?, problem: String?)] = []
    private var picked: Scenario?
    var onPlay: ((CitySimulation) -> Void)?
    /// Asked to start a new scenario.
    var onNew: (() -> Void)?
    /// Asked to edit the scenario in a file.
    var onEdit: ((URL) -> Void)?
    private var pickedFile: URL?
    /// Told when a challenge couldn't start, and why.
    var onProblem: ((String) -> Void)?

    init(folders: [URL]) {
        catalog = ScenarioCatalog(folders: folders)
        frame = WindowFrame(view: view, title: "Scenarios")
    }

    /// Whether there's any challenge to list, as of the last look.
    var hasScenarios: Bool { !listed.isEmpty }

    func refreshList() {
        listed = catalog.challenges()
    }

    func open(in windows: WindowManager) {
        refreshList()
        view.list(named: "scenarios")?.items = listed.map(Self.line)
        view.list(named: "scenarios")?.selectedIndex = nil
        view.setHidden(!listed.isEmpty, named: "empty")
        picked = nil
        pickedFile = nil
        view.setDisabled(true, named: "edit")
        preview.show(note: listed.isEmpty ? "" : "Pick a challenge to see it")
        view.setText(listed.isEmpty ? "" : "Pick a challenge to read what it asks of you.", named: "briefing")
        view.setDisabled(true, named: "play")
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .closeRequested, .view(.pressed(control: "back")):
                windows.close(frame)
            case .view(.selected(control: "scenarios", let index)):
                pick(index)
            case .view(.pressed(control: "new")):
                windows.close(frame)
                onNew?()
            case .view(.pressed(control: "edit")):
                guard let pickedFile else { return }
                windows.close(frame)
                onEdit?(pickedFile)
            case .view(.pressed(control: "play")), .view(.submitted(control: "scenarios", text: _)):
                guard let picked, picked.hasGoals else { return }
                do throws(FormatError) {
                    let city = try CitySimulation.begin(picked)
                    windows.close(frame)
                    onPlay?(city)
                } catch {
                    onProblem?(error.message)
                    view.setText(error.message, named: "briefing")
                }
            default:
                break
            }
        }
    }

    private static func line(_ entry: (file: URL, scenario: Scenario?, problem: String?)) -> String {
        guard let scenario = entry.scenario else { return "\(entry.file.lastPathComponent): can't be played" }
        let goals = scenario.goals.count
        let span = "\(scenario.date(at: 0)) to \(scenario.date(at: scenario.ticks))"
        return "\(scenario.name)   \(span)   " + (scenario.hasGoals ? (goals == 1 ? "1 goal" : "\(goals) goals") : "a draft, with no goals yet")
    }

    private func pick(_ index: Int) {
        guard listed.indices.contains(index) else { return }
        let entry = listed[index]
        guard let scenario = entry.scenario else {
            picked = nil
            pickedFile = nil
            view.setDisabled(true, named: "edit")
            preview.show(note: "No map: the file can't be read")
            view.setText("This file can't be played: \(entry.problem ?? "it couldn't be read")", named: "briefing")
            view.setDisabled(true, named: "play")
            return
        }
        picked = scenario
        pickedFile = entry.file
        view.setDisabled(false, named: "edit")
        if let file = scenario.cityFile {
            do throws(FormatError) {
                preview.show(try CityFiles.read(file).city.map)
            } catch {
                preview.show(note: "No map: its city can't be read")
            }
        } else {
            preview.show(TerrainGenerator.generate(seed: scenario.seed, width: scenario.mapWidth, height: scenario.mapHeight))
        }
        var briefing = scenario.description.map { [$0, ""] } ?? []
        if scenario.hasGoals {
            briefing.append("By \(scenario.date(at: scenario.ticks)):")
            briefing += scenario.goals.map { "  \($0.text)" }
        } else {
            briefing.append("A draft, with no goals yet: Edit it to add some.")
        }
        view.setText(briefing.joined(separator: "\n"), named: "briefing")
        view.setDisabled(!scenario.hasGoals, named: "play")
    }

    func refresh() { preview.refresh() }

    func draw(in bounds: Rectangle) {
        frame.draw(in: bounds)
        let content = WindowFrame.content(in: bounds)
        if let region = view.bounds(named: "preview", at: Vector2(x: content.x, y: content.y)) { preview.draw(in: region) }
    }

    func unload() { preview.unload() }
}
