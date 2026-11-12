import Foundation

/// Writes scenarios as format 3.0 files, the format `ScenarioLoader` reads, so a scenario written
/// reads back the same.
public enum ScenarioWriter {
    /// The file's JSON. A saved city's file is written relative to `folder`, where the scenario
    /// goes; one outside it can't be, and is refused.
    public static func json(_ scenario: Scenario, folder: URL) throws(FormatError) -> JSON {
        var fields: [(key: String, value: JSON)] = [
            ("$schema", .string(SchemaLinks.url(for: "scenario", version: ScenarioLoader.version))),
            ("format", .string(ScenarioLoader.version.description)),
            ("name", .string(scenario.name)),
        ]
        if let description = scenario.description, !description.isEmpty { fields.append(("description", .string(description))) }
        if let city = scenario.cityFile {
            fields.append(("city", .string(try relativePath(city, from: folder))))
        } else {
            fields.append(("seed", .int(Int(scenario.seed))))
            fields.append(("map", .object([("width", .int(scenario.mapWidth)), ("height", .int(scenario.mapHeight))])))
            fields.append(("funds", .int(scenario.funds)))
            fields.append(("start", .int(scenario.startYear)))
        }
        let ends = try date(scenario, scenario.ticks, at: "ends")
        guard ends.week == 1 else { throw FormatError("ends: tick \(scenario.ticks) isn't the start of a month, so it can't be written as one.") }
        fields.append(("ends", .string(month(ends))))
        if scenario.disasters { fields.append(("disasters", .bool(true))) }

        if !scenario.actions.isEmpty {
            var list: [JSON] = []
            for action in scenario.actions {
                var entry = try dated(scenario, action.tick, at: "actions[\(action.index)]")
                entry.append(("tool", .string(action.tool.fileName)))
                entry.append(("from", point(action.fromX, action.fromY)))
                if action.toX != action.fromX || action.toY != action.fromY { entry.append(("to", point(action.toX, action.toY))) }
                if let status = action.expectStatus {
                    let reason = action.expectReason.map { ":" + $0.fileName } ?? ""
                    entry.append(("expect", .string(status.fileName + reason)))
                }
                list.append(.object(entry))
            }
            fields.append(("actions", .array(list)))
        }
        if !scenario.checks.isEmpty {
            var list: [JSON] = []
            for check in scenario.checks {
                var entry = try dated(scenario, check.tick, at: "checks[\(check.index)]")
                func add(_ key: String, _ value: Int?) { if let value { entry.append((key, .int(value))) } }
                switch check.subject {
                case .tile(let x, let y):
                    entry.append(("tile", point(x, y)))
                    if let kind = check.kind { entry.append(("kind", .string(kind.fileName))) }
                    if let use = check.use { entry.append(("use", .string(use.fileName))) }
                    add("development", check.development)
                    add("developmentAtLeast", check.developmentAtLeast)
                    add("developmentAtMost", check.developmentAtMost)
                    if let served = check.served { entry.append(("served", .bool(served))) }
                    if let powered = check.powered { entry.append(("powered", .bool(powered))) }
                case .count(let subject):
                    entry.append(("count", .string(subject.fileName)))
                    add("equals", check.equals)
                    add("atLeast", check.atLeast)
                    add("atMost", check.atMost)
                case .funds:
                    add("fundsAtLeast", check.atLeast)
                    add("fundsAtMost", check.atMost)
                    add("fundsEquals", check.equals)
                }
                list.append(.object(entry))
            }
            fields.append(("checks", .array(list)))
        }
        if !scenario.goals.isEmpty {
            fields.append(("goals", .array(scenario.goals.map { goal(GoalDetails(of: $0)) })))
        }
        if !scenario.events.isEmpty {
            var list: [JSON] = []
            for event in scenario.events {
                let at = try date(scenario, event.tick, at: "events[\(event.index)]")
                list.append(Self.event(EventDetails(at: at, disaster: event.disaster, repeatWeeks: event.repeatWeeks, repeatTimes: event.repeatTimes)))
            }
            fields.append(("events", .array(list)))
        }
        return .object(fields)
    }

    public static func text(_ scenario: Scenario, folder: URL) throws(FormatError) -> String {
        try json(scenario, folder: folder).text()
    }

    static func goal(_ goal: GoalDetails) -> JSON {
        var entry: [(key: String, value: JSON)] = [("measure", .string(goal.measure.fileName))]
        if let atLeast = goal.atLeast { entry.append(("atLeast", .int(atLeast))) }
        if let atMost = goal.atMost { entry.append(("atMost", .int(atMost))) }
        if let text = goal.text, !text.isEmpty { entry.append(("text", .string(text))) }
        return .object(entry)
    }

    static func event(_ event: EventDetails) -> JSON {
        var entry: [(key: String, value: JSON)] = [("at", .string(month(event.at)))]
        if event.at.week != 1 { entry.append(("week", .int(event.at.week))) }
        entry.append(("disaster", .string(event.disaster.fileName)))
        if let weeks = event.repeatWeeks, let times = event.repeatTimes {
            entry.append(("repeat", .object([("weeks", .int(weeks)), ("times", .int(times))])))
        }
        return .object(entry)
    }

    static func month(_ date: ScenarioDate) -> String {
        let year = String(date.year)
        return String(repeating: "0", count: max(0, 4 - year.count)) + year + (date.month < 10 ? "-0\(date.month)" : "-\(date.month)")
    }

    private static func point(_ x: Int, _ y: Int) -> JSON { .object([("x", .int(x)), ("y", .int(y))]) }

    // A time as the file names it: a month, and its week when that isn't the first. A time between
    // the calendar's weeks can't be written as a date.
    private static func date(_ scenario: Scenario, _ tick: Int, at: String) throws(FormatError) -> ScenarioDate {
        let date = scenario.date(at: tick)
        guard date.step - scenario.startStep == tick else {
            throw FormatError("\(at): tick \(tick) falls between the calendar's weeks, so it can't be written as a date.")
        }
        return date
    }

    private static func dated(_ scenario: Scenario, _ tick: Int, at: String) throws(FormatError) -> [(key: String, value: JSON)] {
        let date = try date(scenario, tick, at: at)
        var entry: [(key: String, value: JSON)] = [("at", .string(month(date)))]
        if date.week != 1 { entry.append(("week", .int(date.week))) }
        return entry
    }

    private static func relativePath(_ file: URL, from folder: URL) throws(FormatError) -> String {
        let root = folder.standardizedFileURL.path
        let full = file.standardizedFileURL.path
        let prefix = root.hasSuffix("/") ? root : root + "/"
        guard full.hasPrefix(prefix) else {
            throw FormatError("city: '\(file.lastPathComponent)' isn't beside the scenario, so the scenario can't name it.")
        }
        return String(full.dropFirst(prefix.count))
    }
}

/// A goal as the editor shows it: words of its own, or nil to word it from its measure.
public struct GoalDetails: Hashable, Sendable {
    public var measure: GoalMeasure
    public var atLeast: Int?
    public var atMost: Int?
    public var text: String?

    public init(measure: GoalMeasure, atLeast: Int? = nil, atMost: Int? = nil, text: String? = nil) {
        (self.measure, self.atLeast, self.atMost, self.text) = (measure, atLeast, atMost, text)
    }

    /// A goal's details; words that are the ones the loader would give it count as none.
    public init(of goal: ScenarioGoal) {
        let worded = ScenarioGoal.text(goal.measure, atLeast: goal.atLeast, atMost: goal.atMost)
        self.init(measure: goal.measure, atLeast: goal.atLeast, atMost: goal.atMost, text: goal.text == worded ? nil : goal.text)
    }
}

/// A disaster as the editor shows it: its date, and maybe how often it strikes again.
public struct EventDetails: Hashable, Sendable {
    public var at: ScenarioDate
    public var disaster: Disaster
    public var repeatWeeks: Int?
    public var repeatTimes: Int?

    public init(at: ScenarioDate, disaster: Disaster, repeatWeeks: Int? = nil, repeatTimes: Int? = nil) {
        (self.at, self.disaster, self.repeatWeeks, self.repeatTimes) = (at, disaster, repeatWeeks, repeatTimes)
    }
}

/// What the editor's details window changes: a scenario's name, briefing, dates, money, goals, and
/// disasters. Its strokes and checks, which the editor doesn't show, stay as they are.
public struct ScenarioDetails: Hashable, Sendable {
    public var name: String
    public var description: String?
    public var startYear: Int
    /// The month the goals are judged in.
    public var ends: ScenarioDate
    public var funds: Int
    public var goals: [GoalDetails]
    public var events: [EventDetails]

    public init(
        name: String, description: String?, startYear: Int, ends: ScenarioDate, funds: Int, goals: [GoalDetails], events: [EventDetails]
    ) {
        (self.name, self.description, self.startYear, self.ends, self.funds) = (name, description, startYear, ends, funds)
        (self.goals, self.events) = (goals, events)
    }

    public init(of scenario: Scenario) {
        self.init(
            name: scenario.name, description: scenario.description,
            startYear: scenario.cityFile == nil ? scenario.startYear : scenario.date(at: 0).year,
            ends: scenario.date(at: scenario.ticks), funds: scenario.funds,
            goals: scenario.goals.map(GoalDetails.init(of:)),
            events: scenario.events.map { EventDetails(at: scenario.date(at: $0.tick), disaster: $0.disaster, repeatWeeks: $0.repeatWeeks, repeatTimes: $0.repeatTimes) })
    }
}

/// A scenario being edited, with its city: what it was read or last saved as, to tell whether it
/// has changed; the city before a test began, to put it back; and where it's saved, in the
/// player's scenarios folder with its city beside it.
public final class ScenarioEdit {
    public static let newFunds = 20_000
    public static let newYears = 10

    public private(set) var scenario: Scenario
    /// The scenario's file, where it was read from or will be saved to.
    public private(set) var file: URL
    public let saveFolder: URL

    // The city as it was when the test began, flat; nil while it isn't tested.
    private var checkpoint: String?
    // The scenario and its city as last read or saved; nil for a new scenario not saved yet, or
    // a city not yet opened.
    private var savedScenario: String?
    private var savedCity: String?

    public var isTesting: Bool { checkpoint != nil }

    public init(scenario: Scenario, file: URL, saveFolder: URL) {
        self.scenario = scenario
        self.file = file
        self.saveFolder = saveFolder
        savedScenario = try? ScenarioWriter.text(scenario, folder: file.deletingLastPathComponent())
    }

    /// Opens a scenario file to edit.
    public static func open(_ file: URL, saveFolder: URL) throws(FormatError) -> ScenarioEdit {
        ScenarioEdit(scenario: try ScenarioLoader.load(file), file: file, saveFolder: saveFolder)
    }

    /// A new scenario, from a seed, starting in 1900 with the usual money and running ten years.
    public static func new(name: String, seed: Int32, saveFolder: URL) throws(FormatError) -> ScenarioEdit {
        let text = JSON.object([
            ("format", .string(ScenarioLoader.version.description)), ("name", .string(name)), ("seed", .int(Int(seed))),
            ("start", .int(CityCalendar.startYear)), ("ends", .string("\(CityCalendar.startYear + newYears)-01")), ("funds", .int(newFunds)),
        ]).text()
        let scenario = try ScenarioLoader.parse(text)
        let edit = ScenarioEdit(scenario: scenario, file: saveFolder.appendingPathComponent(try fileName(scenario.name, "json")), saveFolder: saveFolder)
        edit.savedScenario = nil
        return edit
    }

    /// The scenario's city as it starts, built on for free. A saved city's start and money,
    /// changed since it was saved, are put on it, as they will be when it's saved again.
    public func openCity() throws(FormatError) -> CitySimulation {
        checkpoint = nil
        let city = try ScenarioRunner.start(scenario)
        if scenario.cityFile == nil { city.rename(String(scenario.name.prefix(CitySimulation.maxNameLength))) }
        city.builder.buildsForFree = true
        if scenario.cityFile != nil, city.isAtFounding {
            if city.foundedYear != scenario.startYear { city.moveFounding(to: scenario.startYear) }
            city.treasury.restore(scenario.funds)
        }
        // The first city opened is the one the scenario was read with; a new scenario's has never
        // been saved.
        if savedScenario != nil, savedCity == nil { savedCity = CityFiles.flatText(city) }
        return city
    }

    public func hasChanges(_ city: CitySimulation) -> Bool {
        guard let savedScenario, let savedCity else { return true }
        return (try? ScenarioWriter.text(scenario, folder: file.deletingLastPathComponent())) != savedScenario
            || CityFiles.flatText(city) != savedCity
    }

    /// Whether the scenario's start and money can move: always from a seed, and from a saved
    /// city only while the city hasn't run.
    public func canMoveStart(_ city: CitySimulation) -> Bool { scenario.cityFile == nil || city.isAtFounding }

    /// The scenario with these details, checked as a file would be, without changing anything.
    public func change(_ details: ScenarioDetails, city: CitySimulation) throws(FormatError) -> Scenario {
        let folder = file.deletingLastPathComponent()
        var json = try ScenarioWriter.json(scenario, folder: folder)
        let years = details.startYear - scenario.startYear
        var asSaved: CitySimulation?
        if scenario.cityFile == nil {
            json = json.setting("start", to: .int(details.startYear)).setting("funds", to: .int(details.funds))
        } else if years != 0 || details.funds != scenario.funds {
            guard canMoveStart(city) else {
                throw FormatError("Its city has run since it was founded, so its start and its money are its own.")
            }
            let copy = try CityFiles.read(flat: CityFiles.flatText(city), definitions: city.definitions).city
            copy.treasury.restore(details.funds)
            if years != 0 { copy.moveFounding(to: details.startYear) }
            asSaved = copy
        }
        // The strokes and checks the editor doesn't show move with the start, keeping their place
        // in the scenario's story, as its city does.
        json = Self.shifting(json, "actions", years)
        json = Self.shifting(json, "checks", years)

        json = json.setting("name", to: .string(details.name)).removing("description")
        if let description = details.description?.trimmingWhitespace, !description.isEmpty {
            json = json.setting("description", to: .string(description))
        }
        json = json.setting("ends", to: .string(ScenarioWriter.month(details.ends))).removing("goals").removing("events")
        if !details.goals.isEmpty { json = json.setting("goals", to: .array(details.goals.map(ScenarioWriter.goal))) }
        if !details.events.isEmpty { json = json.setting("events", to: .array(details.events.map(ScenarioWriter.event))) }
        return try ScenarioLoader.parse(json.text(), folder: folder, cityAsSaved: asSaved)
    }

    /// Takes changed details on, moving the city's start and money with them. Not while testing.
    public func accept(_ changed: Scenario, city: CitySimulation) {
        precondition(!isTesting, "a scenario's details can't change while it's being tested")
        if changed.startYear != scenario.startYear { city.moveFounding(to: changed.startYear) }
        city.treasury.restore(changed.funds)
        scenario = changed
    }

    /// Where saving puts the scenario: the player's scenarios folder, under its name.
    public func savePath() throws(FormatError) -> URL {
        saveFolder.appendingPathComponent(try Self.fileName(scenario.name, "json"))
    }

    /// Saves the scenario and, beside it, its city as it stands, which it then starts from. Not
    /// while testing.
    public func save(_ city: CitySimulation) throws(FormatError) {
        guard !isTesting else { throw FormatError("A scenario can't be saved while it's tested: reset it first.") }
        let path = try savePath()
        let cityName = try Self.fileName(scenario.name, CityFiles.flatExtension)
        // The city written is what the scenario starts from now: its seed, map, money, and date.
        var fromSeed = scenario
        fromSeed.cityFile = nil
        var json = try ScenarioWriter.json(fromSeed, folder: saveFolder)
        for field in ["seed", "map", "funds", "start"] { json = json.removing(field) }
        json = json.setting("city", to: .string(cityName))
        try CityFiles.write(city, to: saveFolder.appendingPathComponent(cityName))
        try CityFiles.replace(path, with: Array(json.text().utf8))
        let saved: Scenario
        do throws(FormatError) {
            saved = try ScenarioLoader.load(path)
        } catch {
            throw FormatError("\(path.lastPathComponent) was written but doesn't read back: \(error.message)")
        }
        (scenario, file) = (saved, path)
        savedScenario = try? ScenarioWriter.text(saved, folder: saveFolder)
        savedCity = CityFiles.flatText(city)
    }

    /// Starts testing: the city as it stands is kept, and the scenario runs on it as a challenge.
    public func beginTest(_ city: CitySimulation) {
        precondition(!isTesting, "the scenario is already being tested")
        checkpoint = CityFiles.flatText(city)
        city.challenge = Challenge.begin(scenario, on: city)
    }

    /// Ends a test, giving back the city as it was when the test began.
    public func endTest() throws(FormatError) -> CitySimulation {
        guard let checkpoint else { throw FormatError("The scenario isn't being tested.") }
        let city: CitySimulation
        do throws(FormatError) {
            city = try CityFiles.read(flat: checkpoint).city
        } catch {
            throw FormatError("The city from before the test couldn't be read back: \(error.message)")
        }
        self.checkpoint = nil
        city.builder.buildsForFree = true
        return city
    }

    // A file name from the scenario's name, made safe as a city's is.
    static func fileName(_ name: String, _ extension: String) throws(FormatError) -> String {
        guard let file = CityFiles.fileName(for: String(name.prefix(CitySimulation.maxNameLength)), extension: `extension`) else {
            throw FormatError("'\(name)' can't be a file's name.")
        }
        return file
    }

    // Moves the months of a list's entries by a number of years, keeping their weeks.
    private static func shifting(_ json: JSON, _ list: String, _ years: Int) -> JSON {
        guard years != 0, case .object(let fields) = json, let index = fields.firstIndex(where: { $0.key == list }),
            case .array(let entries) = fields[index].value
        else { return json }
        let moved = entries.map { entry -> JSON in
            guard case .object(let parts) = entry, let at = parts.first(where: { $0.key == "at" }), case .string(let text) = at.value,
                let date = ScenarioDate.parse(text)
            else { return entry }
            return entry.setting("at", to: .string(ScenarioWriter.month(ScenarioDate(year: date.year + years, month: date.month))))
        }
        var changed = fields
        changed[index].value = .array(moved)
        return .object(changed)
    }
}

extension JSON {
    /// The object without a field; anything else as it is.
    func removing(_ key: String) -> JSON {
        guard case .object(let fields) = self else { return self }
        return .object(fields.filter { $0.key != key })
    }
}
