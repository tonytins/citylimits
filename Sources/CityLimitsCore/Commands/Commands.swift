import Foundation

/// Where the game keeps its files: settings and the base game's `common/` folder in the config
/// folder, and cities, mods, and scenarios in `Documents/CityLimits`. A portable copy, one with a
/// `settings.json` beside the program, keeps them all beside it.
public struct GameFolders: Sendable {
    public static let folderName = "CityLimits"
    public static let settingsFileName = "settings.json"

    public var settingsFile: URL
    public var commonFolder: URL
    public var modsFolder: URL
    public var citiesFolder: URL
    public var scenariosFolder: URL
    public var isPortable: Bool
    /// Why the folders aren't where they'd usually be, if they aren't.
    public var problem: String?

    /// Every folder beside the program, as a portable copy keeps them.
    public static func beside(_ folder: URL, problem: String? = nil) -> GameFolders {
        GameFolders(
            settingsFile: folder.appendingPathComponent(settingsFileName), commonFolder: folder.appendingPathComponent("common"),
            modsFolder: folder.appendingPathComponent("Mods"), citiesFolder: folder.appendingPathComponent("Cities"),
            scenariosFolder: folder.appendingPathComponent("Scenarios"), isPortable: true, problem: problem)
    }

    /// The folders for this machine: portable when there's a settings file beside the program,
    /// otherwise the XDG config folder and the Documents folder.
    public static func detect(programFolder: URL, environment: [String: String] = ProcessInfo.processInfo.environment) -> GameFolders {
        if FileManager.default.fileExists(atPath: programFolder.appendingPathComponent(settingsFileName).path) {
            return beside(programFolder)
        }
        guard let home = environment["HOME"], home.hasPrefix("/") else {
            return beside(programFolder, problem: "The home folder couldn't be found, so CityLimits keeps its files beside the game")
        }
        let homeURL = URL(fileURLWithPath: home)
        let config = environment["XDG_CONFIG_HOME"].flatMap { $0.hasPrefix("/") ? URL(fileURLWithPath: $0) : nil }
            ?? homeURL.appendingPathComponent(".config")
        let documents = (environment["XDG_DOCUMENTS_DIR"] ?? userDirectory("XDG_DOCUMENTS_DIR", config: config, home: home))
            .map { URL(fileURLWithPath: $0) } ?? homeURL.appendingPathComponent("Documents")
        let game = documents.appendingPathComponent(folderName)
        return GameFolders(
            settingsFile: config.appendingPathComponent(folderName).appendingPathComponent(settingsFileName),
            commonFolder: config.appendingPathComponent(folderName).appendingPathComponent("common"),
            modsFolder: game.appendingPathComponent("Mods"), citiesFolder: game.appendingPathComponent("Cities"),
            scenariosFolder: game.appendingPathComponent("Scenarios"), isPortable: false)
    }

    // The desktop's own name for a folder, from user-dirs.dirs, as `xdg-user-dir` reads it.
    private static func userDirectory(_ key: String, config: URL, home: String) -> String? {
        guard let text = try? String(contentsOf: config.appendingPathComponent("user-dirs.dirs"), encoding: .utf8) else { return nil }
        for line in text.split(separator: "\n") where line.hasPrefix("\(key)=") {
            let value = line.dropFirst(key.count + 1).trimmingCharacters(in: CharacterSet(charactersIn: "\""))
            let path = value.replacingOccurrences(of: "$HOME", with: home)
            return path.hasPrefix("/") && path != home ? path : nil
        }
        return nil
    }

    /// The base game with `common/` written for this build and read back, then the mods applied.
    public func definitions(build: String) -> (definitions: CityDefinitions, problems: [String]) {
        var problems: [String] = []
        do throws(FormatError) {
            try SpecFolders.refreshBase(commonFolder, build: build)
        } catch {
            problems.append(error.message)
        }
        let base = SpecFolders.loadBase(commonFolder)
        if let problem = base.problem { problems.append(problem) }
        let mods = SpecFolders.loadMods(modsFolder, onto: base.definitions)
        return (mods.definitions, problems + mods.problems)
    }
}

extension CityMessage {
    /// What the player reads.
    public var text: String {
        switch self {
        case .needPower: "The city needs a power plant"
        case .blackouts: "Blackouts: many zones have no power"
        case .brownouts: "Brownouts: the power plants can't keep up, so build another"
        case .highTaxes: "Taxes are too high: people and businesses are leaving"
        case .roadsNeedFunding: "The roads need funding, or they will crumble"
        case .needFireStation: "The city needs a fire station"
        case .needPoliceStation: "The city needs a police station"
        case .fireNeedsFunding: "The fire department needs funding"
        case .policeNeedsFunding: "The police department needs funding"
        case .trafficJams: "Frequent traffic jams reported"
        case .needRail: "Inadequate rail system"
        case .highPollution: "Pollution very high"
        case .highCrime: "Crime very high"
        case .fireReported: "Fire reported!"
        case .explosionReported: "Explosion reported!"
        case .floodReported: "Flooding reported!"
        case .earthquakeReported: "Major earthquake reported!"
        case .needStadium: "Residents demand a stadium"
        case .needSeaport: "Industry requires a seaport"
        case .needAirport: "Commerce requires an airport"
        case .meltdownReported: "A nuclear meltdown has occurred at your power plant!"
        case .planeCrashReported: "A plane has crashed!"
        case .helicopterCrashReported: "A helicopter crashed!"
        case .shipwreckReported: "Shipwreck reported!"
        case .heavyTrafficReported: "Heavy traffic reported"
        case .tornadoReported: "Tornado reported!"
        case .monsterReported: "A monster has been sighted!"
        }
    }

    /// Whether it reports a disaster, which the news ticker puts first.
    public var isDisaster: Bool {
        switch self {
        case .fireReported, .explosionReported, .floodReported, .earthquakeReported, .meltdownReported, .planeCrashReported,
            .helicopterCrashReported, .shipwreckReported, .tornadoReported, .monsterReported:
            true
        default:
            false
        }
    }
}

/// Holds back a message the city repeats within a year, so the log and the ticker don't fill
/// with the same complaint.
public struct CityMessageFilter: Sendable {
    private var last: CityMessage?
    private var lastTime = 0

    public init() {}

    public mutating func shouldPass(_ message: CityMessage, cityTime: Int) -> Bool {
        if message == last && cityTime - lastTime < CityCalendar.countsPerYear { return false }
        (last, lastTime) = (message, cityTime)
        return true
    }
}

/// The game's exit codes, which scripts read.
public enum ExitCode {
    public static let success: Int32 = 0
    /// What was asked for couldn't run: a file missing, too large, or invalid.
    public static let failure: Int32 = 1
    /// The command line was wrong.
    public static let usage: Int32 = 2
    /// A scenario ran, and a check or an expectation failed.
    public static let checksFailed: Int32 = 3
}

/// What the command line asked for: something done without a window, finished with an exit
/// code, or the window. `--headless` is global, so any command may run without a window.
public enum Invocation: Equatable, Sendable {
    case exit(Int32)
    case window(seed: Int32?)
}

/// The game's command line. The commands that open no window run here, in the core library, so
/// tests can run them as a script does.
public struct CommandLineRunner {
    public static let defaultSeed: Int32 = 2000
    public static let defaultTicks = CityCalendar.stepsPerYear

    public var version: String
    public var folders: GameFolders
    /// The game's own scenarios.
    public var builtInScenarios: URL
    public var output: (String) -> Void
    public var error: (String) -> Void

    public init(version: String, folders: GameFolders, builtInScenarios: URL, output: @escaping (String) -> Void, error: @escaping (String) -> Void) {
        self.version = version
        self.folders = folders
        self.builtInScenarios = builtInScenarios
        self.output = output
        self.error = error
    }

    public static let usage = """
        Usage:
          citylimits [--seed <seed>] [--headless [--ticks <count>]]
          citylimits scenario <id> --headless [--ticks <count>] [--json] [--verbose]
          citylimits scenario --file <path> --headless [--ticks <count>] [--json] [--verbose]
          citylimits scenario list

        Options:
          --headless        run with no window
          --seed <seed>     the seed a new city is built from: with --headless, the city's
                            (default \(defaultSeed)); in the window, it fills in New Game
          --ticks <count>   with --headless: how many ticks the run takes, 0 to \(Scenario.maxTicks)
                            (default \(defaultTicks), a year); for a scenario, instead of its own
          --file <path>     run a scenario from a JSON file instead of a built-in one
          --json            write a scenario's report as JSON to stdout, and the log to stderr
          --verbose         list every tile each of a scenario's actions changed
          --version         print the version
          --help            print this

        Exit codes: 0 done, or every check passed; 1 what was asked for couldn't run; 2 the
        command line was wrong; 3 a scenario's check or expectation failed. A lost challenge is 0.
        """

    // MARK: Parsing

    private struct Options {
        var flags = Set<String>()
        var values: [String: String] = [:]
        var positionals: [String] = []
    }

    private static let flags: Set<String> = ["--headless", "--json", "--verbose", "--help", "-h", "--version"]
    private static let valued: Set<String> = ["--seed", "--ticks", "--file"]

    private func parse(_ arguments: [String]) -> Result<Options, ParseError> {
        var options = Options()
        var index = 0
        while index < arguments.count {
            let argument = arguments[index]
            index += 1
            if Self.flags.contains(argument) {
                guard options.flags.insert(argument).inserted else { return .failure(ParseError("\(argument) is given twice.")) }
            } else if Self.valued.contains(argument) {
                guard index < arguments.count, !arguments[index].hasPrefix("--") else { return .failure(ParseError("\(argument) needs a value.")) }
                guard options.values.updateValue(arguments[index], forKey: argument) == nil else {
                    return .failure(ParseError("\(argument) is given twice."))
                }
                index += 1
            } else if argument.hasPrefix("-") {
                return .failure(ParseError("unknown option '\(argument)'."))
            } else {
                options.positionals.append(argument)
            }
        }
        return .success(options)
    }

    private struct ParseError: Error {
        let message: String
        init(_ message: String) { self.message = message }
    }

    private func number(_ options: Options, _ name: String, in range: ClosedRange<Int>) -> Result<Int?, ParseError> {
        guard let text = options.values[name] else { return .success(nil) }
        guard let value = Int(text), range.contains(value) else {
            return .failure(ParseError("\(name) needs a whole number from \(range.lowerBound) to \(range.upperBound), not '\(text)'."))
        }
        return .success(value)
    }

    private func refuse(_ message: String) -> Invocation {
        error("citylimits: \(message)")
        error("Run 'citylimits --help' for the options.")
        return .exit(ExitCode.usage)
    }

    // MARK: Running

    /// Runs the command line, without the program's name.
    public func run(_ arguments: [String]) -> Invocation {
        let options: Options
        switch parse(arguments) {
        case .success(let parsed): options = parsed
        case .failure(let problem): return refuse(problem.message)
        }
        if options.flags.contains("--help") || options.flags.contains("-h") {
            output(Self.usage)
            return .exit(ExitCode.success)
        }
        if options.flags.contains("--version") {
            output("CityLimits \(version)")
            return .exit(ExitCode.success)
        }
        let ticks: Int?
        switch number(options, "--ticks", in: 0...Scenario.maxTicks) {
        case .success(let value): ticks = value
        case .failure(let problem): return refuse(problem.message)
        }
        let headless = options.flags.contains("--headless")

        if options.positionals.first == "scenario" {
            return scenario(options, ticks: ticks, headless: headless)
        }
        if let stray = options.positionals.first { return refuse("unknown command '\(stray)'.") }
        for option in ["--file", "--json", "--verbose"] where options.flags.contains(option) || options.values[option] != nil {
            return refuse("\(option) is for 'citylimits scenario'.")
        }
        let seed: Int?
        switch number(options, "--seed", in: Int(Int32.min)...Int(Int32.max)) {
        case .success(let value): seed = value
        case .failure(let problem): return refuse(problem.message)
        }
        guard headless else {
            if ticks != nil { return refuse("--ticks only applies with --headless; the window runs until it is closed.") }
            return .window(seed: seed.map { Int32($0) })
        }
        return .exit(headlessRun(seed: seed.map { Int32($0) } ?? Self.defaultSeed, ticks: ticks ?? Self.defaultTicks))
    }

    // MARK: The headless run

    /// A city grown from a seed, with a small demo town built in the middle, run with no window,
    /// logging a line each month and the year's budget.
    func headlessRun(seed: Int32, ticks: Int) -> Int32 {
        let log = Log(output)
        if let problem = folders.problem { log.warning(problem) }
        let loaded = folders.definitions(build: version)
        for problem in loaded.problems { log.warning(problem) }
        if !loaded.definitions.mods.isEmpty { log.info("Mods applied, in order: \(loaded.definitions.mods.map(\.id).joined(separator: ", "))") }

        let city = CitySimulation(map: TerrainGenerator.generate(seed: seed), seed: seed, definitions: loaded.definitions)
        log.info("Headless run: seed \(seed), map \(city.map.width) by \(city.map.height), \(ticks) ticks")
        log.info("The city starts with $\(city.treasury.funds)")
        if let spot = Self.buildDemoTown(city) {
            log.info("Built a coal plant, a power line, a road, and three zones at \(spot.x), \(spot.y)")
        } else {
            log.warning("No room for a demo town on this map")
        }

        var messages = CityMessageFilter()
        city.onEvent = { event in
            switch event {
            case .message(let message) where messages.shouldPass(message, cityTime: city.cityTime):
                log.warning("tick \(city.tickCount): \(message.text)")
            case .yearSettled(let year):
                log.info("tick \(city.tickCount): the year's budget: taxes $\(year.taxIncome) at \(year.taxRate)%, roads $\(year.roadFunded), fire $\(year.fireFunded), police $\(year.policeFunded), cash flow $\(year.cashFlow)\(year.cut ? ", funding cut" : ""); traffic average \(city.traffic.average)")
            default:
                break
            }
        }
        // A month is 64 steps, so the log writes a line when the month changes, not every step.
        var month = city.date
        for _ in 0..<ticks {
            city.tick()
            guard city.date != month else { continue }
            month = city.date
            log.info("tick \(city.tickCount): \(city.date)   $\(city.treasury.funds)   population \(city.census.cityPopulation)   demand R \(city.demand.residential) C \(city.demand.commercial) I \(city.demand.industrial)   power \(city.power.poweredTileCount)/\(city.power.capacity)   busiest road \(city.traffic.busiest)   pollution \(city.maps.pollutionAverage)   crime \(city.maps.crimeAverage)   fires \(city.hazards.fires.count)   floods \(city.hazards.floods.count)")
        }
        let counts = ScenarioRunner.counts(city)
        let zones = (counts[.residential] ?? 0) + (counts[.commercial] ?? 0) + (counts[.industrial] ?? 0)
        log.info("Headless run finished: \(city.tickCount) ticks, \(city.date), \((0..<city.map.height).reduce(0) { total, y in total + (0..<city.map.width).filter { city.map[$0, y].isRoad }.count }) road tiles, \(zones) zones, \(counts[.developed] ?? 0) developed, $\(city.treasury.funds)")
        return ExitCode.success
    }

    // A road nine tiles long with a residential, a commercial, and an industrial zone along its
    // north side, and a coal plant to the north that powers them down a line, built near the
    // middle of the map with strokes as a player would build it.
    static func buildDemoTown(_ city: CitySimulation) -> (x: Int, y: Int)? {
        let (townWidth, plantSize, lineLength) = (9, 4, 6)
        let townHeight = plantSize + lineLength + ZoneLayout.size + 1
        let map = city.map
        var best: (x: Int, y: Int)?
        var bestDistance = Int.max
        let (middleX, middleY) = (map.width / 2, map.height / 2)
        for y in 0...max(0, map.height - townHeight) where y + townHeight <= map.height {
            for x in 0...max(0, map.width - townWidth) where x + townWidth <= map.width {
                let distance = (x - middleX) * (x - middleX) + (y - middleY) * (y - middleY)
                guard distance < bestDistance else { continue }
                let clear = (y..<(y + townHeight)).allSatisfy { row in (x..<(x + townWidth)).allSatisfy { map[$0, row].kind.canBuildOn } }
                if clear { (best, bestDistance) = ((x, y), distance) }
            }
        }
        guard let (left, top) = best else { return nil }
        let zonesY = top + plantSize + lineLength + 1
        let roadY = top + plantSize + lineLength + ZoneLayout.size
        city.builder.apply(.coalPowerPlant, left + 1, top + 1)
        var line = BuildStroke(.powerLine, left + 1, top + plantSize)
        line.drag(to: left + 1, top + plantSize + lineLength - 1)
        city.builder.apply(line)
        var road = BuildStroke(.road, left, roadY)
        road.drag(to: left + townWidth - 1, roadY)
        city.builder.apply(road)
        city.builder.apply(.zoneResidential, left + 1, zonesY)
        city.builder.apply(.zoneCommercial, left + 4, zonesY)
        city.builder.apply(.zoneIndustrial, left + 7, zonesY)
        return (left, top)
    }

    // MARK: Scenarios

    private func scenario(_ options: Options, ticks: Int?, headless: Bool) -> Invocation {
        guard options.values["--seed"] == nil else { return refuse("--seed is for a new city; a scenario has its own.") }
        let id = options.positionals.dropFirst().first
        guard options.positionals.count <= 2 else { return refuse("'scenario' takes one id.") }
        let catalog = ScenarioCatalog(folders: [builtInScenarios])
        if id == "list" {
            guard options.flags.subtracting(["--headless"]).isEmpty, options.values.isEmpty else {
                return refuse("'scenario list' takes no options.")
            }
            for id in catalog.ids() {
                do throws(FormatError) {
                    let scenario = try catalog.load(id)
                    output(id.padding(toLength: 24, withPad: " ", startingAt: 0) + " " + scenario.name + (scenario.hasGoals ? " (a challenge)" : ""))
                } catch {
                    output(id.padding(toLength: 24, withPad: " ", startingAt: 0) + " (can't be read: \(error.message))")
                }
            }
            return .exit(ExitCode.success)
        }
        let file = options.values["--file"]
        guard (id == nil) != (file == nil) else { return refuse("name a scenario or give --file, but not both.") }
        if let id, !SpecFormat.isValidId(id) {
            return refuse("'\(id)' is not a scenario id: an id is lowercase letters, digits, and single hyphens.")
        }
        // A scenario will open a window once it can; requiring the flag now means a script
        // written today never starts opening windows later.
        guard headless else { return refuse("scenarios run only with --headless for now.") }

        // The output stream belongs to the report, so the log moves out of its way.
        let json = options.flags.contains("--json")
        let log = Log(json ? error : output)
        let source = id ?? file!
        let scenario: Scenario
        do throws(FormatError) {
            if let id {
                scenario = try catalog.load(id)
            } else {
                scenario = try ScenarioLoader.load(URL(fileURLWithPath: file!))
            }
        } catch {
            log.error("The scenario '\(source)' could not be loaded: \(error.message)")
            return .exit(ExitCode.failure)
        }
        if let ticks, ticks < scenario.latestTick {
            return refuse("--ticks \(ticks) ends before tick \(scenario.latestTick), when the scenario's last action, disaster, or check is due.")
        }

        let length = ticks ?? scenario.ticks
        let from = scenario.cityFile.map { "the city \($0.lastPathComponent)" } ?? "seed \(scenario.seed)"
        log.info("Scenario '\(scenario.name)' from \(source): \(from), map \(scenario.mapWidth) by \(scenario.mapHeight), \(scenario.date(at: 0)) to \(scenario.date(at: length)), \(length) ticks, $\(scenario.funds)")
        var runner = ScenarioRunner()
        runner.listsTiles = options.flags.contains("--verbose")
        runner.onAction = { action in
            let reason = action.result.changed ? "" : " (\(action.result.reason.fileName))"
            let expectation = switch action.met {
            case true?: ", as expected"
            case false?: ", NOT as expected"
            case nil: ""
            }
            let line = "action \(action.index) at \(action.date), tick \(action.tick): \(action.tool.fileName) \(action.result.status.fileName)\(reason), \(action.result.tilesChanged) tiles, $\(action.result.changed ? action.result.cost : 0)\(expectation)"
            if action.met == false { log.warning(line) } else { log.info(line) }
            for tile in action.changes ?? [] { log.info("  (\(tile.x), \(tile.y)): \(tile.state.use.fileName) on \(tile.state.kind.fileName)") }
            if action.changes != nil {
                for reason in BuildRefusal.allCases { if let count = action.skipped[reason] { log.info("  skipped \(count): \(reason.fileName)") } }
            }
        }
        runner.onEvent = { event in
            log.info("event \(event.index) at \(event.date), tick \(event.tick): \(event.disaster.fileName) \(event.happened ? "struck" : "found nowhere to strike")")
        }
        runner.onCheck = { check in
            if check.passed {
                log.info("check \(check.index) at \(check.date), tick \(check.tick): passed")
            } else {
                log.warning("check \(check.index) at \(check.date), tick \(check.tick): FAILED, expected \(check.expected ?? ""), found \(check.actual ?? "")")
            }
        }
        let report: ScenarioReport
        do throws(FormatError) {
            report = try runner.run(scenario, source: source, ticks: ticks)
        } catch {
            log.error("The scenario '\(source)' stopped: \(error.message)")
            return .exit(ExitCode.failure)
        }
        for goal in report.goals {
            if goal.met {
                log.info("goal \(goal.index): \(goal.text): met, with \(goal.found)")
            } else {
                log.warning("goal \(goal.index): \(goal.text): NOT met, with \(goal.found)")
            }
        }
        if let won = report.won { log.info("The challenge was \(won ? "won" : "lost")") }
        let counts = report.counts
        log.info("Scenario \(report.passed ? "passed" : "FAILED"): \(report.finalTick) ticks, \(report.finalDate), $\(report.finalFunds), \(counts[.road] ?? 0) road tiles (\(counts[.bridge] ?? 0) bridges), \(counts[.residential] ?? 0) residential, \(counts[.commercial] ?? 0) commercial, \(counts[.industrial] ?? 0) industrial, \(counts[.developed] ?? 0) developed")
        if json { output(report.json().text().trimmingWhitespace) }
        return .exit(report.passed ? ExitCode.success : ExitCode.checksFailed)
    }
}

/// Log lines for a run with no window, to whichever stream the run gives it.
struct Log {
    let write: (String) -> Void

    init(_ write: @escaping (String) -> Void) {
        self.write = write
    }

    func info(_ line: String) { write(line) }
    func warning(_ line: String) { write("warning: \(line)") }
    func error(_ line: String) { write("error: \(line)") }
}
