import Foundation

/// A case named in files as its Swift name with the first letter capitalized, as the C# build's
/// enum names were: `zoneResidential` is `ZoneResidential`.
protocol FileNamed: CaseIterable {}

extension FileNamed {
    var fileName: String {
        let name = String(describing: self)
        return name.prefix(1).uppercased() + name.dropFirst()
    }

    /// The case a file names, matched exactly.
    static func named(_ text: String, at: String) throws(FormatError) -> Self {
        guard let match = allCases.first(where: { $0.fileName == text }) else {
            throw FormatError("\(at): '\(text)' is not one of \(allCases.map(\.fileName).joined(separator: ", ")).")
        }
        return match
    }
}

extension BuildAction: FileNamed {}
extension BuildRefusal: FileNamed {}
extension BuildResult.Status: FileNamed {}
extension TileKind: FileNamed {}
extension TileUse: FileNamed {}
extension Disaster: FileNamed {}

/// A date as scenarios name them: a month, written `YYYY-MM`, and the week of it, 1 to 4.
public struct ScenarioDate: Hashable, Sendable, CustomStringConvertible {
    public static let weeksPerMonth = CityCalendar.countsPerMonth

    public let year: Int
    public let month: Int
    public let week: Int

    public init(year: Int, month: Int, week: Int = 1) {
        self.year = year
        self.month = month
        self.week = week
    }

    /// The count it falls on, from January 1900.
    public var count: Int { (year - CityCalendar.startYear) * CityCalendar.countsPerYear + (month - 1) * CityCalendar.countsPerMonth + (week - 1) }
    public var step: Int { count * CityCalendar.stepsPerCount }

    public static func at(count: Int) -> ScenarioDate {
        let date = CityCalendar.date(at: count)
        return ScenarioDate(year: date.year, month: date.month, week: count % CityCalendar.countsPerMonth + 1)
    }

    /// Reads `YYYY-MM`, from 1900-01 to 9999-12.
    public static func parse(_ text: String, week: Int = 1) -> ScenarioDate? {
        let characters = Array(text)
        guard characters.count == 7, characters[4] == "-",
            characters.enumerated().allSatisfy({ $0.offset == 4 || ($0.element.isASCII && $0.element.isNumber) }),
            let year = Int(String(characters[0..<4])), let month = Int(String(characters[5..<7])),
            (CityCalendar.startYear...CityCalendar.latestStartYear).contains(year), (1...12).contains(month), (1...weeksPerMonth).contains(week)
        else { return nil }
        return ScenarioDate(year: year, month: month, week: week)
    }

    public var description: String {
        let month = GameDate(year: year, month: self.month).description
        return week == 1 ? month : "\(month) week \(week)"
    }
}

/// What a goal measures.
public enum GoalMeasure: String, CaseIterable, Sendable, FileNamed {
    case population, funds, pollution, crime, traffic
}

/// What a count check counts.
public enum CountSubject: String, CaseIterable, Sendable, FileNamed {
    case road, bridge, residential, commercial, industrial, developed, coalPowerPlant, nuclearPowerPlant, powerLine
    case fireStation, policeStation, park, hospital, church, rail, stadium, seaport, airport
}

/// What a player has to meet by a scenario's end.
public struct ScenarioGoal: Hashable, Sendable {
    public var index: Int
    public var measure: GoalMeasure
    public var atLeast: Int?
    public var atMost: Int?
    public var text: String

    /// How a goal reads when its file gives no words of its own.
    public static func text(_ measure: GoalMeasure, atLeast: Int?, atMost: Int?) -> String {
        func number(_ value: Int) -> String { NewsDesk.grouped(value) }
        func money(_ value: Int) -> String { value < 0 ? "-$\(number(-value))" : "$\(number(value))" }
        let (what, show): (String, (Int) -> String) = switch measure {
        case .population: ("a population", number)
        case .funds: ("funds", money)
        case .pollution: ("average pollution", number)
        case .crime: ("average crime", number)
        case .traffic: ("a traffic average", number)
        }
        switch (atLeast, atMost) {
        case let (low?, high?): return "Have \(what) from \(show(low)) to \(show(high))"
        case let (low?, nil): return "Have \(what) of at least \(show(low))"
        default: return "Keep \(what) at most \(show(atMost ?? 0))"
        }
    }
}

/// A disaster that strikes on a date, and maybe again every so many weeks.
public struct ScenarioEvent: Hashable, Sendable {
    public var index: Int
    public var tick: Int
    public var disaster: Disaster
    public var repeatWeeks: Int?
    public var repeatTimes: Int?

    /// Each strike, the first included.
    public var strikes: [ScenarioEvent] {
        let every = (repeatWeeks ?? 0) * CityCalendar.stepsPerCount
        return (0..<(repeatTimes ?? 1)).map { ScenarioEvent(index: index, tick: tick + $0 * every, disaster: disaster) }
    }
}

/// A tool stroke a scenario makes, and the result it may expect.
public struct ScenarioAction: Hashable, Sendable {
    public var index: Int
    public var tick: Int
    public var tool: BuildAction
    public var fromX: Int
    public var fromY: Int
    public var toX: Int
    public var toY: Int
    public var expectStatus: BuildResult.Status?
    public var expectReason: BuildRefusal?

    public var stroke: BuildStroke {
        var stroke = BuildStroke(tool, fromX, fromY)
        stroke.drag(to: toX, toY)
        return stroke
    }

    public func isMet(by result: BuildResult) -> Bool? {
        guard let expectStatus else { return nil }
        return result.status == expectStatus && (expectReason == nil || result.reason == expectReason)
    }
}

/// What a check looks at, and the conditions it holds to.
public struct ScenarioCheck: Hashable, Sendable {
    public enum Subject: Hashable, Sendable {
        case tile(x: Int, y: Int)
        case funds
        case count(CountSubject)
    }

    public var index: Int
    public var tick: Int
    public var subject: Subject
    public var kind: TileKind?
    public var use: TileUse?
    public var development: Int?
    public var developmentAtLeast: Int?
    public var developmentAtMost: Int?
    public var served: Bool?
    public var powered: Bool?
    public var atLeast: Int?
    public var atMost: Int?
    public var equals: Int?
}

/// A city set up from a file: a seed and a script of strokes, or a saved city, disasters on set
/// dates, checks along the way, and goals to meet by an end date.
public struct Scenario: Sendable {
    public static let maxTicks = 100_000

    public var name: String
    public var description: String?
    public var seed: Int32
    public var mapWidth: Int
    public var mapHeight: Int
    public var funds: Int
    /// Steps from the start to the end.
    public var ticks: Int
    public var startYear = CityCalendar.startYear
    /// The saved city it starts from, instead of a seed.
    public var cityFile: URL?
    /// The step the city starts at, counted from January 1900.
    public var startStep = 0
    public var disasters = false
    public var actions: [ScenarioAction] = []
    public var checks: [ScenarioCheck] = []
    public var goals: [ScenarioGoal] = []
    public var events: [ScenarioEvent] = []

    public var hasGoals: Bool { !goals.isEmpty }
    public var isDraft: Bool { goals.isEmpty && checks.isEmpty }

    /// Every strike of every event, in order.
    public var strikes: [ScenarioEvent] {
        events.flatMap(\.strikes).sorted { ($0.tick, $0.index) < ($1.tick, $1.index) }
    }

    public func date(at tick: Int) -> ScenarioDate { .at(count: (startStep + tick) / CityCalendar.stepsPerCount) }

    /// The latest tick anything in the scenario happens at.
    public var latestTick: Int {
        (actions.map(\.tick) + checks.map(\.tick) + strikes.map(\.tick)).max() ?? 0
    }
}

/// Reads scenario files, format 3.0, checking everything before anything runs and refusing the
/// first problem with where it is.
public enum ScenarioLoader {
    public static let version = FormatVersion(3, 0)
    public static let maxFileBytes = 1024 * 1024
    public static let maxGoalTextLength = 120
    public static let maxEntries = 10_000
    public static let mapSides = 16...512
    public static let maxFunds = 1_000_000
    public static let coordinates = -512...1023
    static let maxNameLength = 80
    static let maxDescriptionLength = 500
    // The traffic average can't pass the most a block holds, 240, times the year's 2.4.
    static let mostTrafficAverage = 576

    /// Reads a scenario file. A scenario that starts from a saved city reads the city from beside it.
    public static func load(_ file: URL) throws(FormatError) -> Scenario {
        guard FileManager.default.fileExists(atPath: file.path) else { throw FormatError("There is no scenario file at '\(file.path)'.") }
        let text: String
        do throws(FormatError) {
            text = try SpecFolders.readBounded(file, limit: maxFileBytes)
        } catch {
            throw FormatError("The scenario file '\(file.path)': \(error.message)")
        }
        return try parse(text, folder: file.deletingLastPathComponent())
    }

    /// Reads a scenario from its text. Only one read from a file can start from a saved city.
    public static func parse(_ text: String, folder: URL? = nil) throws(FormatError) -> Scenario {
        guard text.utf8.count <= maxFileBytes else {
            throw FormatError("The scenario is \(text.utf8.count) bytes long; the most is \(maxFileBytes).")
        }
        var fields = try JSONFields(JSON.parse(text), at: "")
        guard let version = FormatVersion(try fields.required("format")) else { throw FormatError("format: needs a version such as \"3.0\".") }
        guard version.major == Self.version.major, version.minor <= Self.version.minor else {
            let reads = version > Self.version ? "up to \(Self.version)" : "\(Self.version.major).x"
            throw FormatError("format: this is format \(version); this version of CityLimits reads \(reads). A scenario from 0.4 can be opened in 0.5's editor and saved again there.")
        }
        let scenario = try read(&fields, folder: folder)
        try fields.finish(noun: "a scenario has")
        return scenario
    }

    private static func read(_ fields: inout JSONFields, folder: URL?) throws(FormatError) -> Scenario {
        let name = try fields.requiredString("name").trimmingWhitespace
        guard (1...maxNameLength).contains(name.count) else { throw FormatError("name: needs 1 to \(maxNameLength) characters, not \(name.count).") }
        let description = try fields.string("description")?.trimmingWhitespace
        if let description, description.count > maxDescriptionLength {
            throw FormatError("description: needs at most \(maxDescriptionLength) characters, not \(description.count).")
        }

        var scenario = Scenario(name: name, description: description, seed: 0, mapWidth: TileMap.defaultWidth, mapHeight: TileMap.defaultHeight, funds: 0, ticks: 0)
        scenario.disasters = try fields.bool("disasters") ?? false
        if let city = try fields.string("city") {
            for field in ["seed", "map", "funds", "start"] where fields.has(field) {
                throw FormatError("\(field): a scenario that starts from a saved city takes its seed, map, money, and date from the city, so it can't also have \(field).")
            }
            let file = try cityPath(city, folder)
            let saved: CitySimulation
            do throws(FormatError) {
                saved = try CityFiles.read(file).city
            } catch {
                throw FormatError("city: '\(city)' couldn't be opened: \(error.message)")
            }
            scenario.cityFile = file
            (scenario.seed, scenario.mapWidth, scenario.mapHeight) = (saved.seed, saved.map.width, saved.map.height)
            (scenario.funds, scenario.startYear, scenario.startStep) = (saved.treasury.funds, saved.date.year, saved.tickCount)
        } else {
            scenario.seed = Int32(try fields.requiredInt("seed", in: Int(Int32.min)...Int(Int32.max)))
            if var map = try fields.object("map") {
                scenario.mapWidth = try map.requiredInt("width", in: mapSides)
                scenario.mapHeight = try map.requiredInt("height", in: mapSides)
                try map.finish(noun: "a map has")
            }
            scenario.funds = try fields.int("funds", in: 0...maxFunds) ?? CityDefinitions.base.economy.startingFunds
            scenario.startYear = try fields.int("start", in: CityCalendar.startYear...CityCalendar.latestStartYear) ?? CityCalendar.startYear
            scenario.startStep = (scenario.startYear - CityCalendar.startYear) * CityCalendar.stepsPerYear
        }

        let start = ScenarioDate.at(count: (scenario.startStep + CityCalendar.stepsPerCount - 1) / CityCalendar.stepsPerCount)
        let ends = try date(try fields.requiredString("ends"), week: nil, at: "ends")
        let length = ends.step - scenario.startStep
        guard length >= 0 else { throw FormatError("ends: \(ends) is before the scenario starts, in \(start).") }
        guard length <= Scenario.maxTicks else {
            throw FormatError("ends: \(ends) is \(length) ticks from the start, in \(start); a scenario may run at most \(Scenario.maxTicks), about 130 years.")
        }
        scenario.ticks = length

        func tick(_ entry: inout JSONFields) throws(FormatError) -> Int {
            let date = try date(try entry.requiredString("at"), week: try entry.int("week"), at: entry.at("at"))
            let tick = date.step - scenario.startStep
            guard (0...length).contains(tick) else {
                throw FormatError("\(entry.at("at")): \(date) is outside the scenario, which runs from \(start) to \(ends).")
            }
            return tick
        }

        for (index, item) in try entries(&fields, "actions").enumerated() {
            var entry = try JSONFields(item, at: "actions[\(index)]")
            let at = try tick(&entry)
            scenario.actions.append(try stroke(&entry, index: index, tick: at))
            try entry.finish(noun: "an action has")
        }
        for (index, item) in try entries(&fields, "checks").enumerated() {
            var entry = try JSONFields(item, at: "checks[\(index)]")
            let at = try tick(&entry)
            scenario.checks.append(try check(&entry, index: index, tick: at, width: scenario.mapWidth, height: scenario.mapHeight))
            try entry.finish(noun: "a check has")
        }
        for (index, item) in try entries(&fields, "goals").enumerated() {
            var entry = try JSONFields(item, at: "goals[\(index)]")
            scenario.goals.append(try goal(&entry, index: index))
            try entry.finish(noun: "a goal has")
        }
        for (index, item) in try entries(&fields, "events").enumerated() {
            var entry = try JSONFields(item, at: "events[\(index)]")
            let disaster = try Disaster.named(try entry.requiredString("disaster"), at: entry.at("disaster"))
            let at = try tick(&entry)
            var event = ScenarioEvent(index: index, tick: at, disaster: disaster)
            if var repeating = try entry.object("repeat") {
                // Both are counted no further than a scenario can run, so the last strike can't overflow.
                let mostWeeks = Scenario.maxTicks / CityCalendar.stepsPerCount
                let weeks = try repeating.requiredInt("weeks", in: 1...mostWeeks)
                let times = try repeating.requiredInt("times", in: 1...mostWeeks)
                try repeating.finish(noun: "a repeat has")
                guard at + (times - 1) * weeks * CityCalendar.stepsPerCount <= length else {
                    throw FormatError("events[\(index)].repeat: striking \(times) times, \(weeks) weeks apart, runs past the scenario's end, \(ends).")
                }
                (event.repeatWeeks, event.repeatTimes) = (weeks, times)
            }
            scenario.events.append(event)
            try entry.finish(noun: "an event has")
        }
        return scenario
    }

    private static func entries(_ fields: inout JSONFields, _ list: String) throws(FormatError) -> [JSON] {
        let items = try fields.array(list) ?? []
        guard items.count <= maxEntries else { throw FormatError("\(list): holds \(items.count) entries; the most is \(maxEntries).") }
        for (index, item) in items.enumerated() where item == .null {
            throw FormatError("\(list)[\(index)]: needs an entry, not null.")
        }
        return items
    }

    // A month written YYYY-MM, from 1900 to 9999, and the week of it, 1 to 4, the first when none is given.
    private static func date(_ text: String, week: Int?, at: String) throws(FormatError) -> ScenarioDate {
        if let week, !(1...ScenarioDate.weeksPerMonth).contains(week) {
            let owner = at.split(separator: ".").dropLast().joined(separator: ".")
            throw FormatError("\(owner).week: needs a number from 1 to \(ScenarioDate.weeksPerMonth), not \(week).")
        }
        guard let date = ScenarioDate.parse(text, week: week ?? 1) else {
            throw FormatError("\(at): needs a month written YYYY-MM, from \(CityCalendar.startYear)-01 to \(CityCalendar.latestStartYear)-12, not '\(text)'.")
        }
        return date
    }

    // A saved city's file, named as a path inside the scenario's folder: no folder above it, so a
    // scenario can't read a file it shouldn't.
    private static func cityPath(_ city: String, _ folder: URL?) throws(FormatError) -> URL {
        guard let folder else {
            throw FormatError("city: only a scenario read from a file can start from a saved city, which is read from beside it.")
        }
        let file = URL(fileURLWithPath: city, relativeTo: folder)
        guard [CityFiles.flatExtension, CityFiles.binaryExtension].contains(file.pathExtension.lowercased()) else {
            throw FormatError("city: needs a .\(CityFiles.flatExtension) or a .\(CityFiles.binaryExtension) file, not '\(city)'.")
        }
        let root = folder.standardizedFileURL.path
        let full = file.standardizedFileURL.path
        guard !city.hasPrefix("/"), full.hasPrefix(root.hasSuffix("/") ? root : root + "/") else {
            throw FormatError("city: '\(city)' needs to be a file beside the scenario, or in a folder beside it.")
        }
        return file.standardizedFileURL
    }

    private static func point(_ fields: inout JSONFields, _ key: String) throws(FormatError) -> (x: Int, y: Int)? {
        guard var point = try fields.object(key) else { return nil }
        let x = try point.requiredInt("x", in: coordinates), y = try point.requiredInt("y", in: coordinates)
        try point.finish(noun: "a point has")
        return (x, y)
    }

    private static func stroke(_ entry: inout JSONFields, index: Int, tick: Int) throws(FormatError) -> ScenarioAction {
        let tool = try BuildAction.named(try entry.requiredString("tool"), at: entry.at("tool"))
        guard let from = try point(&entry, "from") else { throw FormatError("\(entry.at("from")): is required.") }
        let to = try point(&entry, "to") ?? from
        var action = ScenarioAction(index: index, tick: tick, tool: tool, fromX: from.x, fromY: from.y, toX: to.x, toY: to.y)
        if let text = try entry.string("expect") {
            // "Built", "Refused", or a status and a reason, as in "Refused:NotEnoughFunds".
            let at = entry.at("expect")
            let parts = text.split(separator: ":", omittingEmptySubsequences: false).map(String.init)
            guard parts.count <= 2 else {
                throw FormatError("\(at): needs a status, or a status and a reason such as Refused:NotEnoughFunds, not '\(text)'.")
            }
            action.expectStatus = try BuildResult.Status.named(parts[0], at: at)
            if parts.count == 2 { action.expectReason = try BuildRefusal.named(parts[1], at: at) }
            if action.expectStatus == .built, let reason = action.expectReason, reason != .none {
                throw FormatError("\(at): a stroke that was built has no reason, so '\(text)' can never be met.")
            }
        }
        return action
    }

    private static func check(_ entry: inout JSONFields, index: Int, tick: Int, width: Int, height: Int) throws(FormatError) -> ScenarioCheck {
        let at = entry.path
        let tileKeys = ["kind", "use", "development", "developmentAtLeast", "developmentAtMost", "served", "powered"]
        let fundsKeys = ["fundsAtLeast", "fundsAtMost", "fundsEquals"]
        let countKeys = ["equals", "atLeast", "atMost"]
        let present = Set(entry.keys)
        let has = { (keys: [String]) in keys.contains { present.contains($0) } }
        func only(_ own: [String], what: String, allowed: String) throws(FormatError) {
            let others = (tileKeys + fundsKeys + countKeys).filter { !own.contains($0) }
            if has(others) { throw FormatError("\(at): \(what) takes only \(allowed).") }
            if !has(own) { throw FormatError("\(at): \(what) needs at least one of \(allowed).") }
        }
        guard !(entry.keys.contains("tile") && entry.keys.contains("count")) else {
            throw FormatError("\(at): checks one subject, so it can't have both tile and count.")
        }

        var check = ScenarioCheck(index: index, tick: tick, subject: .funds)
        if let tile = try point(&entry, "tile") {
            try only(tileKeys, what: "a tile check", allowed: "kind, use, development, developmentAtLeast, developmentAtMost, served, or powered")
            guard (0..<width).contains(tile.x), (0..<height).contains(tile.y) else {
                throw FormatError("\(at).tile: (\(tile.x), \(tile.y)) is outside the \(width) by \(height) map.")
            }
            check.subject = .tile(x: tile.x, y: tile.y)
            check.kind = try entry.string("kind").map { text throws(FormatError) in try TileKind.named(text, at: "\(at).kind") }
            check.use = try entry.string("use").map { text throws(FormatError) in try TileUse.named(text, at: "\(at).use") }
            check.development = try entry.int("development", in: 0...TileState.maxDevelopment)
            check.developmentAtLeast = try entry.int("developmentAtLeast", in: 0...TileState.maxDevelopment)
            check.developmentAtMost = try entry.int("developmentAtMost", in: 0...TileState.maxDevelopment)
            check.served = try entry.bool("served")
            check.powered = try entry.bool("powered")
        } else if let count = try entry.string("count") {
            try only(countKeys, what: "a count check", allowed: "equals, atLeast, or atMost")
            check.subject = .count(try CountSubject.named(count, at: "\(at).count"))
            let most = width * height
            check.equals = try entry.int("equals", in: 0...most)
            check.atLeast = try entry.int("atLeast", in: 0...most)
            check.atMost = try entry.int("atMost", in: 0...most)
        } else {
            try only(fundsKeys, what: "a funds check", allowed: "fundsAtLeast, fundsAtMost, or fundsEquals; or give it a tile or a count")
            check.equals = try entry.int("fundsEquals", in: 0...Int(Int32.max))
            check.atLeast = try entry.int("fundsAtLeast", in: 0...Int(Int32.max))
            check.atMost = try entry.int("fundsAtMost", in: 0...Int(Int32.max))
        }
        return check
    }

    private static func goal(_ entry: inout JSONFields, index: Int) throws(FormatError) -> ScenarioGoal {
        let at = entry.path
        let measure = try GoalMeasure.named(try entry.requiredString("measure"), at: entry.at("measure"))
        let range: ClosedRange<Int> = switch measure {
        case .funds: -maxFunds * 1000...maxFunds * 1000
        case .pollution: 0...CityMaps.mostPollution
        case .crime: 0...CityMaps.mostCrime
        case .traffic: 0...mostTrafficAverage
        case .population: 0...Int(Int32.max)
        }
        let atLeast = try entry.int("atLeast", in: range)
        let atMost = try entry.int("atMost", in: range)
        guard atLeast != nil || atMost != nil else { throw FormatError("\(at): needs atLeast, atMost, or both.") }
        if let atLeast, let atMost, atLeast > atMost {
            throw FormatError("\(at): atLeast \(atLeast) is more than atMost \(atMost), so it can never be met.")
        }
        let text = try entry.string("text")?.trimmingWhitespace
        if let text, !(1...maxGoalTextLength).contains(text.count) {
            throw FormatError("\(at).text: needs 1 to \(maxGoalTextLength) characters, not \(text.count).")
        }
        return ScenarioGoal(index: index, measure: measure, atLeast: atLeast, atMost: atMost, text: text ?? ScenarioGoal.text(measure, atLeast: atLeast, atMost: atMost))
    }
}

/// The demo town: a road with a zone of each kind, powered by a coal plant up a line to the
/// north, whose industry is knocked down after a year. It lives in code, so it can't go missing.
public enum DemoTown {
    public static let id = "demo-town"

    public static let text = """
        {
          "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/scenario.v3.0.schema.json",
          "format": "3.0",
          "name": "Demo town",
          "description": "A road with a residential, a commercial, and an industrial zone, powered by a coal plant up a line to the north; the industry is knocked down after a year.",
          "seed": 2000,
          "funds": 20000,
          "ends": "1902-01",
          "actions": [
            { "at": "1900-01", "tool": "Road", "from": { "x": 34, "y": 53 }, "to": { "x": 42, "y": 53 }, "expect": "Built" },
            { "at": "1900-01", "tool": "ZoneResidential", "from": { "x": 35, "y": 51 }, "expect": "Built" },
            { "at": "1900-01", "tool": "ZoneCommercial", "from": { "x": 38, "y": 51 }, "expect": "Built" },
            { "at": "1900-01", "tool": "ZoneIndustrial", "from": { "x": 41, "y": 51 }, "expect": "Built" },
            { "at": "1900-01", "tool": "CoalPowerPlant", "from": { "x": 35, "y": 40 }, "expect": "Built" },
            { "at": "1900-01", "tool": "PowerLine", "from": { "x": 35, "y": 43 }, "to": { "x": 35, "y": 49 }, "expect": "Built" },
            { "at": "1901-01", "tool": "Bulldoze", "from": { "x": 41, "y": 51 }, "expect": "Built" }
          ],
          "checks": [
            { "at": "1900-01", "count": "Road", "equals": 9 },
            { "at": "1902-01", "tile": { "x": 35, "y": 51 }, "use": "Residential", "development": 0 },
            { "at": "1902-01", "tile": { "x": 38, "y": 51 }, "use": "Commercial", "development": 0 },
            { "at": "1902-01", "count": "Developed", "equals": 1 },
            { "at": "1902-01", "tile": { "x": 38, "y": 50 }, "served": true, "powered": true },
            { "at": "1902-01", "tile": { "x": 42, "y": 52 }, "kind": "Rubble", "use": "None" },
            { "at": "1902-01", "fundsEquals": 16572 },
            { "at": "1902-01", "count": "Industrial", "equals": 0 }
          ]
        }
        """

    public static var scenario: Scenario {
        // A test reads the demo town, so reading it can't fail in play.
        try! ScenarioLoader.parse(text)
    }
}

/// The scenarios in folders, by id: the file's name without `.json`, plus the demo town.
public struct ScenarioCatalog: Sendable {
    public let folders: [URL]

    public init(folders: [URL]) {
        self.folders = folders
    }

    /// The ids that can be run, the demo town first and then each folder's files in name order.
    public func ids() -> [String] {
        var ids = [DemoTown.id]
        for folder in folders {
            let files = (try? FileManager.default.contentsOfDirectory(atPath: folder.path)) ?? []
            ids += files.filter { $0.hasSuffix(".json") }.map { String($0.dropLast(5)) }
                .filter { SpecFormat.isValidId($0) && !ids.contains($0) }.sorted()
        }
        return ids
    }

    /// Reads a scenario by id. Ids are lowercase words and hyphens, so one can't name a path
    /// outside the folders.
    public func load(_ id: String) throws(FormatError) -> Scenario {
        guard SpecFormat.isValidId(id) else { throw FormatError("'\(id)' is not a scenario id: ids are lowercase letters, digits, and single hyphens.") }
        if id == DemoTown.id { return DemoTown.scenario }
        for folder in folders {
            let file = folder.appendingPathComponent("\(id).json")
            if FileManager.default.fileExists(atPath: file.path) { return try ScenarioLoader.load(file) }
        }
        throw FormatError("There is no scenario '\(id)'.")
    }

    /// The challenges a player can pick: scenarios with goals, and drafts to edit, with any that
    /// can't be read and why. Scenarios with only checks are tests, and aren't listed.
    public func challenges() -> [(file: URL, scenario: Scenario?, problem: String?)] {
        var listed: [(file: URL, scenario: Scenario?, problem: String?)] = []
        for folder in folders {
            let files = ((try? FileManager.default.contentsOfDirectory(atPath: folder.path)) ?? []).filter { $0.hasSuffix(".json") }.sorted().prefix(500)
            for name in files {
                let file = folder.appendingPathComponent(name)
                do throws(FormatError) {
                    let scenario = try ScenarioLoader.load(file)
                    if scenario.hasGoals || scenario.isDraft { listed.append((file, scenario, nil)) }
                } catch {
                    listed.append((file, nil, error.message))
                }
            }
        }
        return listed
    }
}
