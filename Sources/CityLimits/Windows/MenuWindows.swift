import CityLimitsCore
import Foundation
import Hudini
import Raylib

/// The settings, in a modal window. A change applies at once through `onChange`, and closing the
/// window calls `onClose`, which writes them, so a crash later can't lose them.
@MainActor
final class SettingsWindow {
    private struct Row {
        let name: String
        let tab: String
        let options: Int
        let about: String
        let get: (GameSettings) -> Int
        let set: (inout GameSettings, Int) -> Void
    }

    private static func toggle(_ name: String, _ about: String, _ path: WritableKeyPath<GameSettings, Bool>, tab: String = "disastersTab") -> Row {
        Row(name: name, tab: tab, options: 2, about: about, get: { $0[keyPath: path] ? 1 : 0 }, set: { $0[keyPath: path] = $1 == 1 })
    }

    private static let tabs = ["gameTab", "disastersTab"]
    private static let rows: [Row] = [
        toggle("fullscreen", "Fill the screen, without borders, from the next time the game starts.", \.fullscreen, tab: "gameTab"),
        Row(
            name: "autosave", tab: "gameTab", options: 3, about: "Every 15 minutes of play, beside the city's own file.",
            get: { $0.autosaveSlots }, set: { $0.autosaveSlots = $1 }),
        toggle("disasters", "Whether disasters happen on their own; the Disasters tab picks which.", \.disasters, tab: "gameTab"),
        toggle("fires", "Fires break out now and then; a fire already burning spreads either way.", \.fires),
        toggle("floods", "Rivers and lakes now and then rise over the land beside them.", \.floods),
        toggle("earthquakes", "The ground shakes, wrecking buildings and starting fires of its own.", \.earthquakes),
        toggle("meltdowns", "A nuclear plant can melt down, leaving radioactive ground for decades.", \.meltdowns),
        toggle("airCrashes", "A plane can come down near the airport.", \.airCrashes),
        toggle("shipwrecks", "A ship can run aground off the seaport.", \.shipwrecks),
        toggle("tornadoes", "A tornado can touch down and tear across the city.", \.tornadoes),
        toggle("monsters", "A monster can come out of the water, drawn to pollution.", \.monsters),
    ]

    let frame: WindowFrame
    private let view = Interface.view("settings", fallbackSize: Vector2(x: 400, y: 330))
    private var settings = GameSettings()
    private var tab = "gameTab"
    var onChange: ((GameSettings) -> Void)?
    var onClose: ((GameSettings) -> Void)?

    init() {
        frame = WindowFrame(view: view, title: "Settings")
    }

    func open(_ current: GameSettings, in windows: WindowManager) {
        settings = current
        show("gameTab")
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .closeRequested:
                windows.close(frame)
                onClose?(settings)
            case .view(.pressed(control: let name)):
                if Self.tabs.contains(name) {
                    show(name)
                } else if let row = Self.rows.first(where: { name.hasPrefix($0.name) }), let option = Int(name.dropFirst(row.name.count)),
                    (0..<row.options).contains(option)
                {
                    row.set(&settings, option)
                    showChoices()
                    onChange?(settings)
                }
            default:
                break
            }
        }
    }

    private func show(_ chosen: String) {
        tab = chosen
        for each in Self.tabs { view.setSelected(each == chosen, named: each) }
        for row in Self.rows {
            let hidden = row.tab != chosen
            view.setHidden(hidden, named: row.name + "Label")
            for option in 0..<row.options { view.setHidden(hidden, named: "\(row.name)\(option)") }
        }
        showChoices()
    }

    private func showChoices() {
        for row in Self.rows {
            let chosen = row.get(settings)
            for option in 0..<row.options { view.setSelected(option == chosen, named: "\(row.name)\(option)") }
            // A disaster can't happen with Disasters off, whatever its own row says.
            if row.tab == "disastersTab" {
                for option in 0..<row.options { view.setDisabled(!settings.disasters, named: "\(row.name)\(option)") }
            }
        }
    }

    /// Says what the setting under the pointer does. Call each frame while it's open.
    func refresh(in windows: WindowManager) {
        guard let bounds = windows.bounds(of: frame) else { return }
        let content = WindowFrame.content(in: bounds)
        let origin = Vector2(x: content.x, y: content.y)
        let pointer = GetMousePosition()
        let hovered = Self.rows.first { row in
            row.tab == tab
                && (0..<row.options).map { "\(row.name)\($0)" }.appending(row.name + "Label")
                    .contains { view.bounds(named: $0, at: origin).map { CheckCollisionPointRec(pointer, $0) } ?? false }
        }
        let note = !settings.disasters && tab == "disastersTab" ? "Disasters is off on the Game tab, so none of these happen." : ""
        view.setWrapped(hovered?.about ?? note, named: "about")
    }
}

extension Array {
    fileprivate func appending(_ element: Element) -> [Element] { self + [element] }
}

/// A new city's name and seed, with the map the seed grows. `onStart` gets the name and the seed,
/// or nil for any.
@MainActor
final class NewCityWindow {
    let frame: WindowFrame
    private let view = Interface.view("newcity", fallbackSize: Vector2(x: 420, y: 388))
    private let preview = MapPreview()
    private var shownSeed: String?
    var onStart: ((String, Int32?) -> Void)?

    init() {
        frame = WindowFrame(view: view, title: "New city")
    }

    /// Opens it titled `title`, with a name to start from, as New scenario uses it too.
    func open(title: String = "New city", name: String = CitySimulation.defaultName, seed: Int32?, in windows: WindowManager) {
        frame.title = title
        view.setText(name, named: "name")
        view.setText(seed.map(String.init) ?? "", named: "seed")
        shownSeed = nil
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .closeRequested, .view(.pressed(control: "back")):
                windows.close(frame)
            case .view(.pressed(control: "generate")):
                view.setText(String(Int32.random(in: 0...Int32.max)), named: "seed")
            case .view(.pressed(control: "start")), .view(.submitted(control: _, text: _)):
                start(in: windows)
            default:
                break
            }
        }
    }

    // A blank seed is any seed; anything else must be a whole number a map can grow from.
    private var seed: Int32?? {
        let text = (view.text(named: "seed") ?? "").trimmingCharacters(in: .whitespaces)
        if text.isEmpty { return .some(nil) }
        return Int32(text).map { .some($0) }
    }

    private func start(in windows: WindowManager) {
        let name = view.text(named: "name") ?? ""
        guard CitySimulation.isValidName(name), let seed else { return }
        windows.close(frame)
        onStart?(name.trimmingCharacters(in: .whitespaces), seed)
    }

    /// Grows the map for a seed that changed, and says what's wrong, if anything. Call each frame
    /// while it's open, before `BeginDrawing`.
    func refresh() {
        let text = view.text(named: "seed") ?? ""
        if text != shownSeed {
            shownSeed = text
            switch seed {
            case .some(.some(let number)): preview.show(TerrainGenerator.generate(seed: number))
            case .some(.none): preview.show(note: "A seed is picked when you start")
            case .none: preview.show(note: "No map: the seed is a whole number")
            }
        }
        preview.refresh()
        let nameOK = CitySimulation.isValidName(view.text(named: "name"))
        let problem = !nameOK ? "A city's name needs 1 to \(CitySimulation.maxNameLength) characters" : seed == nil ? "The seed is a whole number, or blank for any" : ""
        view.setText(problem, named: "problem")
        view.setDisabled(!problem.isEmpty, named: "start")
    }

    /// Draws the window and its map, which the document leaves to the game.
    func draw(in bounds: Rectangle) {
        frame.draw(in: bounds)
        let content = WindowFrame.content(in: bounds)
        if let region = view.bounds(named: "preview", at: Vector2(x: content.x, y: content.y)) { preview.draw(in: region) }
    }

    func unload() { preview.unload() }
}

/// The menu Escape opens over a city. What each choice does is the session's, through `onChoice`.
@MainActor
final class GameMenuWindow {
    enum Choice: String {
        case save, load, settings, mainMenu, quit
    }

    let frame: WindowFrame
    var onChoice: ((Choice) -> Void)?

    init() {
        frame = WindowFrame(view: Interface.view("gamemenu", fallbackSize: Vector2(x: 220, y: 248)), title: "Menu")
    }

    func open(in windows: WindowManager) {
        windows.open(frame, modal: true, dimsBehind: true) { [unowned self, unowned windows] event in
            switch event {
            case .closeRequested, .view(.pressed(control: "resume")):
                windows.close(frame)
            case .view(.pressed(control: let name)):
                guard let choice = Choice(rawValue: name) else { return }
                windows.close(frame)
                onChoice?(choice)
            default:
                break
            }
        }
    }
}
