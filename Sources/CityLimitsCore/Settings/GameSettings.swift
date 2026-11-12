import Foundation

/// The player's settings, kept in `settings.json`. The C# build keeps the same file under the same
/// names, so both read what the other writes; a name this one doesn't know is kept as it was.
public struct GameSettings: Equatable, Sendable {
    public static let windowWidths = 640...3840
    public static let windowHeights = 480...2160
    public static let speeds = GameSpeed.paused.rawValue...GameSpeed.superFast.rawValue
    public static let autosaveSlotCounts = 0...2
    /// The biggest settings file read; anything bigger isn't settings.
    public static let maxFileBytes = 64 * 1024

    public var windowWidth = 1280
    public var windowHeight = 720
    public var speed = GameSpeed.normal
    public var autosaveSlots = 1
    public var fullscreen = false
    public var disasters = true
    public var fires = true
    public var floods = true
    public var earthquakes = true
    public var meltdowns = true
    public var airCrashes = true
    public var shipwrecks = true
    public var tornadoes = true
    public var monsters = true
    /// The fields of the file this build doesn't use, such as the C# build's SaveOlderFormats,
    /// written back unchanged.
    public var kept: [(key: String, value: JSON)] = []

    public init() {}

    public static func == (lhs: GameSettings, rhs: GameSettings) -> Bool {
        lhs.switches == rhs.switches && lhs.windowWidth == rhs.windowWidth && lhs.windowHeight == rhs.windowHeight
            && lhs.speed == rhs.speed && lhs.autosaveSlots == rhs.autosaveSlots && JSON.object(lhs.kept) == JSON.object(rhs.kept)
    }

    // The on-or-off settings, by their names in the file.
    private static var switchNames: [(name: String, path: WritableKeyPath<GameSettings, Bool>)] {
        [
            ("Fullscreen", \.fullscreen), ("Disasters", \.disasters), ("Fires", \.fires), ("Floods", \.floods),
            ("Earthquakes", \.earthquakes), ("Meltdowns", \.meltdowns), ("AirCrashes", \.airCrashes),
            ("Shipwrecks", \.shipwrecks), ("Tornadoes", \.tornadoes), ("Monsters", \.monsters),
        ]
    }

    private var switches: [Bool] { Self.switchNames.map { self[keyPath: $0.path] } }

    /// Turns the city's disasters on or off as the settings have them: each needs Disasters on too.
    public func apply(to city: CitySimulation) {
        city.randomFires = disasters && fires
        city.randomFloods = disasters && floods
        city.randomEarthquakes = disasters && earthquakes
        city.randomMeltdowns = disasters && meltdowns
        city.randomAirCrashes = disasters && airCrashes
        city.randomShipwrecks = disasters && shipwrecks
        city.randomTornadoes = disasters && tornadoes
        city.randomMonsters = disasters && monsters
    }

    // MARK: Reading and writing

    /// Reads settings from the file's text. A setting that's missing takes its default quietly; one
    /// of the wrong kind or out of range takes its default too, and the warning names it.
    public static func read(_ text: String) -> (settings: GameSettings, warning: String?) {
        guard case .object(let fields)? = try? JSON.parse(text) else {
            return (GameSettings(), "it holds no settings, so the defaults are in use")
        }
        var settings = GameSettings()
        var refused: [String] = []
        let numbers: [(String, ClosedRange<Int>, (inout GameSettings, Int) -> Void)] = [
            ("WindowWidth", windowWidths, { $0.windowWidth = $1 }),
            ("WindowHeight", windowHeights, { $0.windowHeight = $1 }),
            ("Speed", speeds, { $0.speed = GameSpeed(rawValue: $1) ?? .normal }),
            ("AutosaveSlots", autosaveSlotCounts, { $0.autosaveSlots = $1 }),
        ]
        for (key, value) in fields {
            if let (_, range, set) = numbers.first(where: { $0.0 == key }) {
                guard case .number(let digits) = value, let number = Int(digits) else {
                    if value != .null { refused.append("\(key) that isn't a whole number") }
                    continue
                }
                guard range.contains(number) else {
                    refused.append("\(key) \(number), outside \(range.lowerBound) to \(range.upperBound)")
                    continue
                }
                set(&settings, number)
            } else if let path = switchNames.first(where: { $0.name == key })?.path {
                guard case .bool(let on) = value else {
                    if value != .null { refused.append("\(key) that isn't true or false") }
                    continue
                }
                settings[keyPath: path] = on
            } else {
                settings.kept.append((key, value))
            }
        }
        guard !refused.isEmpty else { return (settings, nil) }
        let these = refused.count == 1 ? "that one is" : "those are"
        return (settings, "it had \(refused.joined(separator: ", ")), so \(these) back to the default; the rest are kept")
    }

    /// The file's text, under the names the C# build uses, with the fields kept from the file read.
    public func text() -> String {
        var fields: [(key: String, value: JSON)] = [
            ("WindowWidth", .int(windowWidth)), ("WindowHeight", .int(windowHeight)), ("Speed", .int(speed.rawValue)),
            ("AutosaveSlots", .int(autosaveSlots)),
        ]
        fields += Self.switchNames.map { ($0.name, .bool(self[keyPath: $0.path])) }
        fields += kept
        return JSON.object(fields).text()
    }

    /// Reads the settings file. A missing file gives the defaults quietly; one that can't be read
    /// gives them with a warning saying why.
    public static func load(_ file: URL) -> (settings: GameSettings, warning: String?) {
        let manager = FileManager.default
        guard manager.fileExists(atPath: file.path) else { return (GameSettings(), nil) }
        do {
            let attributes = try manager.attributesOfItem(atPath: file.path)
            if let size = attributes[.size] as? Int, size > maxFileBytes {
                return (GameSettings(), "The settings file \(file.path) is bigger than \(maxFileBytes / 1024) KB, so the defaults are in use.")
            }
            let (settings, warning) = read(String(decoding: try Data(contentsOf: file), as: UTF8.self))
            return (settings, warning.map { "The settings file \(file.path): \($0)." })
        } catch {
            return (GameSettings(), "The settings file \(file.path) couldn't be read (\(error.localizedDescription)), so the defaults are in use.")
        }
    }

    public func save(to file: URL) throws(FormatError) {
        try CityFiles.replace(file, with: Array(text().utf8))
    }
}
