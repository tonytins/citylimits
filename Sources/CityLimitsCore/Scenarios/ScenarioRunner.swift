/// How a goal stood at the end.
public struct GoalReport: Hashable, Sendable {
    public var index: Int
    public var text: String
    public var met: Bool
    public var found: Int
}

/// How each part of a scenario run went, and the city at its end. Its JSON is a stable interface,
/// format 1: a field may be added within it, never renamed or removed.
public struct ScenarioReport: Sendable {
    public struct Action: Sendable {
        public var index: Int
        public var tick: Int
        public var date: String
        public var tool: BuildAction
        public var result: BuildResult
        public var skipped: [BuildRefusal: Int]
        public var met: Bool?
        /// Each tile it changed, when asked for.
        public var changes: [BuildPlan.Tile]?
    }

    public struct Check: Sendable {
        public var index: Int
        public var tick: Int
        public var date: String
        public var expected: String?
        public var actual: String?
        public var passed: Bool { expected == nil }
    }

    public struct Event: Sendable {
        public var index: Int
        public var tick: Int
        public var date: String
        public var disaster: Disaster
        public var happened: Bool
    }

    public var name: String
    public var source: String
    public var seed: Int32
    public var ticks: Int
    public var start: String
    public var ends: String
    public var actions: [Action] = []
    public var events: [Event] = []
    public var checks: [Check] = []
    public var goals: [GoalReport] = []
    public var hasGoals = false
    public var finalTick = 0
    public var finalCityTime = 0
    public var finalDate = ""
    public var finalFunds = 0
    public var counts: [CountSubject: Int] = [:]

    public var passed: Bool { checks.allSatisfy(\.passed) && actions.allSatisfy { $0.met != false } }
    public var won: Bool? { hasGoals ? goals.allSatisfy(\.met) : nil }

    /// The report as JSON, format 1.
    public func json() -> JSON {
        var fields: [(key: String, value: JSON)] = [
            ("format", .int(1)),
            ("scenario", .object([
                ("name", .string(name)), ("source", .string(source)), ("seed", .int(Int(seed))), ("ticks", .int(ticks)),
                ("start", .string(start)), ("ends", .string(ends)),
            ])),
            ("passed", .bool(passed)),
        ]
        if let won { fields.append(("won", .bool(won))) }
        fields.append(("final", .object([
            ("tick", .int(finalTick)), ("cityTime", .int(finalCityTime)), ("date", .string(finalDate)), ("funds", .int(finalFunds)),
            ("counts", .object(CountSubject.allCases.map { subject in
                (subject.fileName.prefix(1).lowercased() + subject.fileName.dropFirst(), .int(counts[subject] ?? 0))
            })),
        ])))
        fields.append(("actions", .array(actions.map { action in
            var entry: [(key: String, value: JSON)] = [
                ("index", .int(action.index)), ("tick", .int(action.tick)), ("tool", .string(action.tool.fileName)),
                ("status", .string(action.result.status.fileName)),
            ]
            if !action.result.changed { entry.append(("reason", .string(action.result.reason.fileName))) }
            entry += [
                ("tiles", .int(action.result.tilesChanged)),
                ("skipped", .object(BuildRefusal.allCases.compactMap { reason in action.skipped[reason].map { (reason.fileName, .int($0)) } })),
                ("cost", .int(action.result.changed ? action.result.cost : 0)),
            ]
            if let met = action.met { entry.append(("met", .bool(met))) }
            entry.append(("date", .string(action.date)))
            if let changes = action.changes {
                entry.append(("changes", .array(changes.map { tile in
                    .object([("x", .int(tile.x)), ("y", .int(tile.y)), ("ground", .string(tile.state.kind.fileName)), ("use", .string(tile.state.use.fileName))])
                })))
            }
            return .object(entry)
        })))
        fields.append(("events", .array(events.map { event in
            .object([
                ("index", .int(event.index)), ("tick", .int(event.tick)), ("date", .string(event.date)),
                ("disaster", .string(event.disaster.fileName)), ("happened", .bool(event.happened)),
            ])
        })))
        fields.append(("checks", .array(checks.map { check in
            var entry: [(key: String, value: JSON)] = [("index", .int(check.index)), ("tick", .int(check.tick)), ("passed", .bool(check.passed))]
            if let expected = check.expected { entry.append(("expected", .string(expected))) }
            if let actual = check.actual { entry.append(("actual", .string(actual))) }
            entry.append(("date", .string(check.date)))
            return .object(entry)
        })))
        fields.append(("goals", .array(goals.map { goal in
            .object([("index", .int(goal.index)), ("text", .string(goal.text)), ("met", .bool(goal.met)), ("found", .int(goal.found))])
        })))
        return .object(fields)
    }
}

/// Runs a scenario from its start to its end, or to a step asked for, and reports each action,
/// event, check, and goal. A run never writes a file.
public struct ScenarioRunner {
    /// Called as each part of the run happens, for logging.
    public var onAction: ((ScenarioReport.Action) -> Void)?
    public var onEvent: ((ScenarioReport.Event) -> Void)?
    public var onCheck: ((ScenarioReport.Check) -> Void)?
    /// Whether each action's report lists every tile it changed.
    public var listsTiles = false

    public init() {}

    /// The city a scenario starts with: its saved city, or a map grown from its seed.
    public static func start(_ scenario: Scenario) throws(FormatError) -> CitySimulation {
        if let file = scenario.cityFile {
            do throws(FormatError) {
                return try CityFiles.read(file).city
            } catch {
                throw FormatError("The scenario's city '\(file.lastPathComponent)' couldn't be opened: \(error.message)")
            }
        }
        let map = TerrainGenerator.generate(seed: scenario.seed, width: scenario.mapWidth, height: scenario.mapHeight)
        return CitySimulation(map: map, seed: scenario.seed, funds: scenario.funds, startYear: scenario.startYear)
    }

    /// Runs the scenario. `ticks`, when given, must reach everything the scenario does.
    public func run(_ scenario: Scenario, source: String, ticks: Int? = nil) throws(FormatError) -> ScenarioReport {
        let end = ticks ?? scenario.ticks
        guard (scenario.latestTick...Scenario.maxTicks).contains(end) else {
            throw FormatError("ticks: needs a number from \(scenario.latestTick), the scenario's last action, event, or check, to \(Scenario.maxTicks), not \(end).")
        }
        let city = try Self.start(scenario)
        city.randomFires = scenario.disasters
        city.randomFloods = scenario.disasters
        city.randomEarthquakes = scenario.disasters
        city.randomMeltdowns = scenario.disasters
        city.randomAirCrashes = scenario.disasters
        city.randomShipwrecks = scenario.disasters
        city.randomTornadoes = scenario.disasters
        city.randomMonsters = scenario.disasters

        var report = ScenarioReport(
            name: scenario.name, source: source, seed: scenario.seed, ticks: end,
            start: scenario.date(at: 0).description, ends: scenario.date(at: end).description)
        report.hasGoals = scenario.hasGoals
        let plan = BuildPlan()
        let actions = scenario.actions.sorted { ($0.tick, $0.index) < ($1.tick, $1.index) }
        let strikes = scenario.strikes
        let checks = scenario.checks.sorted { ($0.tick, $0.index) < ($1.tick, $1.index) }
        var (nextAction, nextEvent, nextCheck) = (0, 0, 0)

        for tick in 0...end {
            let date = scenario.date(at: tick).description
            while nextAction < actions.count && actions[nextAction].tick == tick {
                let action = actions[nextAction]
                nextAction += 1
                city.builder.plan(action.stroke, into: plan)
                let result = city.builder.apply(action.stroke)
                var skipped: [BuildRefusal: Int] = [:]
                for reason in BuildRefusal.allCases where plan.skipped(because: reason) > 0 { skipped[reason] = plan.skipped(because: reason) }
                let made = ScenarioReport.Action(
                    index: action.index, tick: tick, date: date, tool: action.tool, result: result, skipped: skipped,
                    met: action.isMet(by: result), changes: listsTiles ? (result.changed ? plan.tiles : []) : nil)
                report.actions.append(made)
                onAction?(made)
            }
            while nextEvent < strikes.count && strikes[nextEvent].tick == tick {
                let strike = strikes[nextEvent]
                nextEvent += 1
                let event = ScenarioReport.Event(index: strike.index, tick: tick, date: date, disaster: strike.disaster, happened: city.strike(strike.disaster))
                report.events.append(event)
                onEvent?(event)
            }
            while nextCheck < checks.count && checks[nextCheck].tick == tick {
                let check = checks[nextCheck]
                nextCheck += 1
                let failure = Self.failure(of: check, in: city)
                let made = ScenarioReport.Check(index: check.index, tick: tick, date: date, expected: failure?.expected, actual: failure?.actual)
                report.checks.append(made)
                onCheck?(made)
            }
            if tick == end {
                report.goals = scenario.goals.map { Self.judge($0, in: city) }
                break
            }
            city.tick()
        }

        report.finalTick = city.tickCount
        report.finalCityTime = city.cityTime
        report.finalDate = city.date.description
        report.finalFunds = city.treasury.funds
        report.counts = Self.counts(city)
        return report
    }

    public static func measure(_ measure: GoalMeasure, in city: CitySimulation) -> Int {
        switch measure {
        case .population: city.census.cityPopulation
        case .funds: city.treasury.funds
        case .pollution: city.maps.pollutionAverage
        case .crime: city.maps.crimeAverage
        case .traffic: city.traffic.average
        }
    }

    public static func judge(_ goal: ScenarioGoal, in city: CitySimulation) -> GoalReport {
        let found = measure(goal.measure, in: city)
        let met = (goal.atLeast.map { found >= $0 } ?? true) && (goal.atMost.map { found <= $0 } ?? true)
        return GoalReport(index: goal.index, text: goal.text, met: met, found: found)
    }

    /// What the city has of each thing a check can count.
    public static func counts(_ city: CitySimulation) -> [CountSubject: Int] {
        var counts: [CountSubject: Int] = [:]
        let map = city.map
        for y in 0..<map.height {
            for x in 0..<map.width {
                let state = map[x, y]
                if state.hasRoad {
                    counts[.road, default: 0] += 1
                    if state.isBridge { counts[.bridge, default: 0] += 1 }
                }
                if state.isRail { counts[.rail, default: 0] += 1 }
                if state.hasPowerLine { counts[.powerLine, default: 0] += 1 }
                if state.use == .park { counts[.park, default: 0] += 1 }
                if state.isBuildingCenter {
                    let subject: CountSubject? = switch state.use {
                    case .coalPowerPlant: .coalPowerPlant
                    case .nuclearPowerPlant: .nuclearPowerPlant
                    case .fireStation: .fireStation
                    case .policeStation: .policeStation
                    case .hospital: .hospital
                    case .church: .church
                    case .stadium: .stadium
                    case .seaport: .seaport
                    case .airport: .airport
                    case .residential: .residential
                    case .commercial: .commercial
                    case .industrial: .industrial
                    default: nil
                    }
                    if let subject { counts[subject, default: 0] += 1 }
                }
                if state.isZoneCenter && city.definitions.zone(state.use).population(on: map, centerX: x, centerY: y) > 0 {
                    counts[.developed, default: 0] += 1
                }
            }
        }
        return counts
    }

    // The first condition of a check that doesn't hold, or nil when they all do.
    static func failure(of check: ScenarioCheck, in city: CitySimulation) -> (expected: String, actual: String)? {
        switch check.subject {
        case .funds:
            let funds = city.treasury.funds
            if let exactly = check.equals, funds != exactly { return ("funds equal \(exactly)", "\(funds)") }
            if let least = check.atLeast, funds < least { return ("funds at least \(least)", "\(funds)") }
            if let most = check.atMost, funds > most { return ("funds at most \(most)", "\(funds)") }
            return nil
        case .count(let subject):
            let count = counts(city)[subject] ?? 0
            let what = "\(subject.fileName) count"
            if let exactly = check.equals, count != exactly { return ("\(what) equals \(exactly)", "\(count)") }
            if let least = check.atLeast, count < least { return ("\(what) at least \(least)", "\(count)") }
            if let most = check.atMost, count > most { return ("\(what) at most \(most)", "\(count)") }
            return nil
        case .tile(let x, let y):
            let map = city.map
            let state = map[x, y]
            let development = map.centerState(x, y).development
            let tile = "tile (\(x), \(y))"
            if let kind = check.kind, state.kind != kind { return ("\(tile) kind is \(kind.fileName)", state.kind.fileName) }
            if let use = check.use, state.use != use { return ("\(tile) use is \(use.fileName)", state.use.fileName) }
            if let exactly = check.development, development != exactly { return ("\(tile) development is \(exactly)", "\(development)") }
            if let least = check.developmentAtLeast, development < least { return ("\(tile) development at least \(least)", "\(development)") }
            if let most = check.developmentAtMost, development > most { return ("\(tile) development at most \(most)", "\(development)") }
            if let served = check.served {
                guard state.isZoned else { return ("\(tile) served is \(served)", "the tile has no zone") }
                city.roads.ensureCurrent(map)
                let center = map.centerOf(x, y)
                let isServed = city.roads.isZoneServed(center.x, center.y)
                if isServed != served { return ("\(tile) served is \(served)", "\(isServed)") }
            }
            if let powered = check.powered {
                guard state.hasFootprint else { return ("\(tile) powered is \(powered)", "the tile has no zone or plant") }
                let isPowered = map.centerState(x, y).powered
                if isPowered != powered { return ("\(tile) powered is \(powered)", "\(isPowered)") }
            }
            return nil
        }
    }
}

/// A scenario being played as a challenge: its actions made and its events struck on their
/// dates, and its goals judged at its end date. The city plays on after, won or lost.
public final class Challenge {
    public let name: String
    public let description: String?
    public let goals: [ScenarioGoal]
    /// The step the goals are judged at, counted from January 1900.
    public let endStep: Int
    public internal(set) var won: Bool?
    private(set) var actions: [(step: Int, action: ScenarioAction)]
    private(set) var events: [(step: Int, disaster: Disaster)]

    init(name: String, description: String?, goals: [ScenarioGoal], endStep: Int, actions: [(Int, ScenarioAction)], events: [(Int, Disaster)], won: Bool?) {
        self.name = name
        self.description = description
        self.goals = goals
        self.endStep = endStep
        self.actions = actions.map { (step: $0.0, action: $0.1) }
        self.events = events.map { (step: $0.0, disaster: $0.1) }
        self.won = won
    }

    public var ends: ScenarioDate { .at(count: endStep / CityCalendar.stepsPerCount) }

    /// The disasters still to come, by date.
    public var upcomingEvents: [(date: ScenarioDate, disaster: Disaster)] {
        events.map { (.at(count: $0.step / CityCalendar.stepsPerCount), $0.disaster) }
    }

    /// Starts playing a scenario on its city.
    public static func begin(_ scenario: Scenario, on city: CitySimulation) -> Challenge {
        let start = city.tickCount
        return Challenge(
            name: scenario.name, description: scenario.description, goals: scenario.goals, endStep: start + scenario.ticks,
            actions: scenario.actions.sorted { ($0.tick, $0.index) < ($1.tick, $1.index) }.map { (start + $0.tick, $0) },
            events: scenario.strikes.map { (start + $0.tick, $0.disaster) }, won: nil)
    }

    /// What falls due before the city's next step; judges the goals once the end comes.
    func beforeStep(_ city: CitySimulation) -> [GoalReport]? {
        while let next = actions.first, next.step <= city.tickCount {
            actions.removeFirst()
            city.builder.apply(next.action.stroke)
        }
        while let next = events.first, next.step <= city.tickCount {
            events.removeFirst()
            city.strike(next.disaster)
        }
        guard won == nil, city.tickCount >= endStep else { return nil }
        let reports = goals.map { ScenarioRunner.judge($0, in: city) }
        won = reports.allSatisfy(\.met)
        return reports
    }

    // MARK: Saving

    func capture() -> JSON {
        .object([
            ("name", .string(name)), ("description", description.map(JSON.string) ?? .null), ("endStep", .int(endStep)),
            ("won", won.map(JSON.bool) ?? .null),
            ("goals", .array(goals.map { goal in
                .object([
                    ("measure", .string(goal.measure.fileName)), ("atLeast", goal.atLeast.map(JSON.int) ?? .null),
                    ("atMost", goal.atMost.map(JSON.int) ?? .null), ("text", .string(goal.text)),
                ])
            })),
            ("actions", .array(actions.map { entry in
                .object([
                    ("step", .int(entry.step)), ("tool", .string(entry.action.tool.fileName)), ("fromX", .int(entry.action.fromX)),
                    ("fromY", .int(entry.action.fromY)), ("toX", .int(entry.action.toX)), ("toY", .int(entry.action.toY)),
                ])
            })),
            ("events", .array(events.map { .object([("step", .int($0.step)), ("disaster", .string($0.disaster.fileName))]) })),
        ])
    }

    static func restore(_ fields: inout JSONFields) throws(FormatError) -> Challenge {
        let name = (try fields.string("name") ?? "").trimmingWhitespace
        guard (1...80).contains(name.count) else { throw FormatError("challenge.name: needs 1 to 80 characters.") }
        let description = try fields.string("description")?.trimmingWhitespace
        let endStep = try fields.requiredInt("endStep", in: 0...Int.max)
        let won = try fields.bool("won")
        var goals: [ScenarioGoal] = []
        for (index, item) in (try fields.array("goals") ?? []).enumerated() {
            var goal = try JSONFields(item, at: "challenge.goals[\(index)]")
            let measure = try GoalMeasure.named(try goal.requiredString("measure"), at: goal.at("measure"))
            let atLeast = try goal.int("atLeast"), atMost = try goal.int("atMost")
            let text = try goal.string("text") ?? ""
            guard (1...ScenarioLoader.maxGoalTextLength).contains(text.count), atLeast != nil || atMost != nil else {
                throw FormatError("\(goal.path): needs words of 1 to \(ScenarioLoader.maxGoalTextLength) characters, and atLeast, atMost, or both.")
            }
            try goal.finish(noun: "a goal has")
            goals.append(ScenarioGoal(index: index, measure: measure, atLeast: atLeast, atMost: atMost, text: text))
        }
        var actions: [(Int, ScenarioAction)] = []
        for (index, item) in (try fields.array("actions") ?? []).enumerated() {
            var action = try JSONFields(item, at: "challenge.actions[\(index)]")
            let tool = try BuildAction.named(try action.requiredString("tool"), at: action.at("tool"))
            let step = try action.requiredInt("step", in: 0...Int.max)
            let coordinates = ScenarioLoader.coordinates
            let made = ScenarioAction(
                index: index, tick: 0, tool: tool, fromX: try action.requiredInt("fromX", in: coordinates),
                fromY: try action.requiredInt("fromY", in: coordinates), toX: try action.requiredInt("toX", in: coordinates),
                toY: try action.requiredInt("toY", in: coordinates))
            try action.finish(noun: "an action has")
            actions.append((step, made))
        }
        var events: [(Int, Disaster)] = []
        for (index, item) in (try fields.array("events") ?? []).enumerated() {
            var event = try JSONFields(item, at: "challenge.events[\(index)]")
            let disaster = try Disaster.named(try event.requiredString("disaster"), at: event.at("disaster"))
            events.append((try event.requiredInt("step", in: 0...Int.max), disaster))
            try event.finish(noun: "an event has")
        }
        return Challenge(
            name: name, description: description?.isEmpty == false ? description : nil, goals: goals, endStep: endStep,
            actions: actions.sorted { $0.0 < $1.0 }, events: events.sorted { $0.0 < $1.0 }, won: won)
    }
}

extension CitySimulation {
    /// Starts a scenario as a challenge: its city, named for it when made from a seed.
    public static func begin(_ scenario: Scenario) throws(FormatError) -> CitySimulation {
        let city = try ScenarioRunner.start(scenario)
        if scenario.cityFile == nil {
            city.rename(String(scenario.name.prefix(maxNameLength)))
        }
        city.challenge = Challenge.begin(scenario, on: city)
        return city
    }
}
