import ArgumentParser
import CityLimitsCore

/// How the exit codes read in every command's help.
private let exitCodes = """
    Exit codes: 0 done, or every check passed; 1 what was asked for couldn't run; 2 the command \
    line was wrong; 3 a scenario's check or expectation failed. A lost challenge is 0.
    """

/// `citylimits`: with no command, the game's window; with `--headless`, a city grown from a seed
/// and run with no window. `CommandLineRunner` runs what it parses.
struct RootCommand: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "citylimits",
        abstract: "A SimCity-style city builder. With no command, it opens the game's window.",
        usage: """
            citylimits [--seed <seed>] [--headless [--ticks <count>]]
            citylimits scenario <id> --headless [--ticks <count>] [--json] [--verbose]
            citylimits scenario --file <path> --headless [--ticks <count>] [--json] [--verbose]
            citylimits scenario list
            """,
        discussion: exitCodes,
        subcommands: [ScenarioCommand.self])

    @Flag(help: "Run with no window: grow a city from a seed and log it month by month, or run a scenario.")
    var headless = false

    @Option(
        parsing: .unconditional,
        help: ArgumentHelp(
            "The seed a new city is built from: with --headless, the city's (default \(CommandLineRunner.defaultSeed)); in the window, it fills in New Game.",
            valueName: "seed"))
    var seed: Int32?

    @Option(
        help: ArgumentHelp(
            "With --headless: how many ticks the run takes, 0 to \(Scenario.maxTicks) (default \(CommandLineRunner.defaultTicks), a year); for a scenario, instead of its own.",
            valueName: "count"))
    var ticks: Int?

    @Flag(help: "Print the version.")
    var version = false

    func validate() throws {
        try checkTicks(ticks)
    }
}

/// `citylimits scenario`: run a built-in scenario or one from a file, with no window, or list the
/// built-in ones. `--headless` and `--ticks` are the root command's, given anywhere on the line,
/// as `--headless` is a global flag.
struct ScenarioCommand: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "scenario",
        abstract: "Run a scenario with no window, or list the built-in ones.",
        usage: """
            citylimits scenario <id> --headless [--ticks <count>] [--json] [--verbose]
            citylimits scenario --file <path> --headless [--ticks <count>] [--json] [--verbose]
            citylimits scenario list
            """,
        discussion: exitCodes)

    @Argument(help: "A built-in scenario's id, or 'list' to list them.")
    var id: String?

    @Option(help: ArgumentHelp("Run a scenario from a file instead of a built-in one.", valueName: "path"))
    var file: String?

    @ParentCommand var root: RootCommand

    @Flag(help: "Write the report as JSON to stdout, and the log to stderr.")
    var json = false

    @Flag(help: "List every tile each of the scenario's actions changed.")
    var verbose = false

    var headless: Bool { root.headless }
    var ticks: Int? { root.ticks }

    func validate() throws {
        guard root.seed == nil else { throw ValidationError("--seed is for a new city; a scenario has its own.") }
        guard !root.version else { throw ValidationError("--version takes no command.") }
        if id == "list" {
            guard file == nil, ticks == nil, !json, !verbose else { throw ValidationError("'scenario list' takes no options.") }
            return
        }
        guard (id == nil) != (file == nil) else { throw ValidationError("name a scenario or give --file, but not both.") }
        if let id, !SpecFormat.isValidId(id) {
            throw ValidationError("'\(id)' is not a scenario id: an id is lowercase letters, digits, and single hyphens.")
        }
        // A scenario will open a window once it can; requiring the flag now means a script
        // written today never starts opening windows later.
        guard headless else { throw ValidationError("scenarios run only with --headless for now.") }
    }
}

private func checkTicks(_ ticks: Int?) throws {
    if let ticks, !(0...Scenario.maxTicks).contains(ticks) {
        throw ValidationError("--ticks needs a whole number from 0 to \(Scenario.maxTicks), not \(ticks).")
    }
}
