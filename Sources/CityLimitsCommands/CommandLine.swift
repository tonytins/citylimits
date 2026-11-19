import ArgumentParser
import CityLimitsCore
import Foundation

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

/// The game's command line. The commands that open no window run here, outside the game, so
/// tests can run them as a script does. Parsing is swift-argument-parser's; running stays here,
/// writing through `output` and `error`, so a test can read what a script would.
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

    /// Runs the command line, without the program's name. Help goes to `output` with exit code
    /// 0; anything the parser or a check refuses goes to `error` with exit code 2.
    public func run(_ arguments: [String]) -> Invocation {
        // The parser keeps the last of an option given twice; the command line has always refused it.
        for name in Self.options {
            if arguments.filter({ $0 == name || $0.hasPrefix(name + "=") }).count > 1 { return refuse("\(name) is given twice.") }
        }
        var command: any ParsableCommand
        do {
            command = try RootCommand.parseAsRoot(arguments)
            switch command {
            case let scenario as ScenarioCommand:
                return self.scenario(scenario)
            case let root as RootCommand:
                return self.root(root)
            default:
                // The parser's own commands, such as help, say what they do by what they throw.
                try command.run()
                return .exit(ExitCode.success)
            }
        } catch {
            return finish(error)
        }
    }

    private static let options = ["--headless", "--seed", "--ticks", "--version", "--file", "--json", "--verbose"]

    // Help and the version go to the output with exit code 0; anything refused goes to the error
    // stream with exit code 2.
    private func finish(_ thrown: any Error) -> Invocation {
        let message = RootCommand.fullMessage(for: thrown)
        if RootCommand.exitCode(for: thrown) == .success {
            output(message)
            return .exit(ExitCode.success)
        }
        error(message)
        return .exit(ExitCode.usage)
    }

    // A refusal found once the command line was parsed, worded as the parser words its own.
    private func refuse(_ message: String) -> Invocation {
        error("Error: \(message)")
        error("See 'citylimits --help'.")
        return .exit(ExitCode.usage)
    }

    private func root(_ command: RootCommand) -> Invocation {
        if command.version {
            output("CityLimits \(version)")
            return .exit(ExitCode.success)
        }
        guard command.headless else {
            if command.ticks != nil { return refuse("--ticks only applies with --headless; the window runs until it is closed.") }
            return .window(seed: command.seed)
        }
        return .exit(headlessRun(seed: command.seed ?? Self.defaultSeed, ticks: command.ticks ?? Self.defaultTicks))
    }

    // MARK: The headless run

    /// A city grown from a seed, with a small demo town built in the middle, run with no window,
    /// logging a line each month and the year's budget.
    func headlessRun(seed: Int32, ticks: Int) -> Int32 {
        let log = RunLog(output)
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

    private func scenario(_ command: ScenarioCommand) -> Invocation {
        let catalog = ScenarioCatalog(folders: [builtInScenarios])
        if command.id == "list" {
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
        let (id, file, ticks) = (command.id, command.file, command.ticks)

        // The output stream belongs to the report, so the log moves out of its way.
        let json = command.json
        let log = RunLog(json ? error : output)
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
        runner.listsTiles = command.verbose
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
        if json { output(report.json().text().trimmingCharacters(in: .whitespacesAndNewlines)) }
        return .exit(report.passed ? ExitCode.success : ExitCode.checksFailed)
    }
}

/// The log a run with no window writes, to whichever stream the run gives it. It is the
/// command's own output, which scripts and tests read line by line, so it keeps its own plain
/// format rather than going through swift-log, which is for diagnostics.
struct RunLog {
    let write: (String) -> Void

    init(_ write: @escaping (String) -> Void) {
        self.write = write
    }

    func info(_ line: String) { write(line) }
    func warning(_ line: String) { write("warning: \(line)") }
    func error(_ line: String) { write("error: \(line)") }
}
