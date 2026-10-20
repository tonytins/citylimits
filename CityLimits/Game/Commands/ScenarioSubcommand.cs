namespace CityLimits.Game.Commands;

using CityLimits.CommandLine;
using CityLimits.Core.Scenarios;

/// <summary>
/// Scenario subcommand - runs a scenario with no window and reports what came of it, as
/// docs/scenarios.md specifies. A scenario that can't be loaded falls back to the demo town,
/// which is always reported and never mistaken for success.
/// </summary>
public sealed class ScenarioSubcommand : ISubcommand
{
    /// <summary>The folder the built-in scenario files are read from, beside the executable.</summary>
    public static readonly string ScenarioFolder = Path.Combine(AppContext.BaseDirectory, GameAssets.FolderName, "Scenarios");

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly OptionSpec Options = new(["--file", "--ticks"], ["--json"], MaxPositionals: 1);

    public string Name => "scenario";

    public string Description => "Run a scenario with no window: a seeded city, scripted strokes, and checks";

    public string Usage => $"""
        Usage: citylimits scenario <id> --headless [options]
               citylimits scenario --file <path> --headless [options]
               citylimits scenario list

        Scenarios run only with --headless for now; a window comes later.

        Options:
          --file <path>     run a scenario from a JSON file instead of a built-in one
          --ticks <count>   run this many ticks instead of the scenario's own, 0 to {Scenario.MaxTicks},
                            but not fewer than its last action or check needs
          --json            write the report as JSON to stdout, and the log to stderr

        Exit codes: 0 every check passed, 1 the scenario could not run (the demo town ran
        instead), 2 the command line was wrong, 3 a check or an expectation failed.
        """;

    public bool SupportsHeadless => true;

    public int Execute(SubcommandContext context)
    {
        if (!CommandOptions.TryParse(context.Args, Options, out var options, out var error))
        {
            return Refuse(context, error);
        }

        int? ticks = null;
        if (options.ValueOf("--ticks") is not null)
        {
            if (!options.TryGetInt("--ticks", 0, 0, Scenario.MaxTicks, out var given, out error))
            {
                return Refuse(context, error);
            }

            ticks = given;
        }

        var scenarioId = options.Positionals.FirstOrDefault();
        var scenarioFile = options.ValueOf("--file");
        var catalog = new ScenarioCatalog(ScenarioFolder);

        if (scenarioId == "list")
        {
            return options.Has("--json") || scenarioFile is not null || ticks is not null
                ? Refuse(context, "'scenario list' takes no options.")
                : List(context, catalog);
        }

        if (scenarioId is null == scenarioFile is null)
        {
            return Refuse(context, "Name a scenario or give --file, but not both.");
        }

        if (scenarioId is not null && !ScenarioCatalog.IsValidId(scenarioId))
        {
            return Refuse(context, $"'{scenarioId}' is not a scenario id: an id is lowercase letters, digits, and single hyphens.");
        }

        // Without the flag, a scenario will open a window once it can; requiring the flag
        // now means a script written today never starts opening windows later.
        if (!context.Headless)
        {
            return Refuse(context, "Scenarios run only with --headless for now.");
        }

        // The output stream belongs to the report, so the log moves out of its way.
        var json = options.Has("--json");
        if (json)
        {
            GameLog.UseStandardError();
        }

        var requested = scenarioId ?? scenarioFile!;
        var loaded = scenarioId is not null
            ? catalog.TryLoad(scenarioId, out var scenario, out error)
            : ScenarioLoader.TryLoadFile(scenarioFile!, out scenario, out error);

        ScenarioFallback? fallback = null;
        var source = requested;
        if (!loaded)
        {
            // The demo town runs in its place, so the run still shows something, but the failure
            // is logged, recorded in the report, and given its own exit code.
            Logger.Error("The scenario '{Requested}' could not be loaded, so the demo town runs instead: {Error}", requested, error);
            fallback = new ScenarioFallback(requested, error);
            scenario = DemoTown.Scenario;
            source = DemoTown.Id;
        }

        if (ticks is { } length && length < ScenarioRunner.LatestTick(scenario!))
        {
            return Refuse(context, $"--ticks {length} ends before tick {ScenarioRunner.LatestTick(scenario!)}, when the scenario's last action or check is due.");
        }

        ScenarioReport report;
        try
        {
            report = Run(scenario!, source, ticks, fallback);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "The scenario '{Source}' stopped", source);
            return ExitCodes.Failure;
        }

        if (json)
        {
            context.Output.WriteLine(ScenarioReportJson.Serialize(report));
        }

        if (fallback is not null)
        {
            return ExitCodes.Failure;
        }

        return report.Passed ? ExitCodes.Success : ExitCodes.ChecksFailed;
    }

    private static ScenarioReport Run(Scenario scenario, string source, int? ticks, ScenarioFallback? fallback)
    {
        Logger.Info(
            "Scenario '{Name}' from {Source}: seed {Seed}, map {Width} by {Height}, {Ticks} ticks, ${Funds}",
            scenario.Name,
            source,
            scenario.Seed,
            scenario.MapWidth,
            scenario.MapHeight,
            ticks ?? scenario.Ticks,
            scenario.Funds);

        var runner = new ScenarioRunner();
        runner.ActionMade += LogAction;
        runner.CheckMade += LogCheck;
        var report = runner.Run(scenario, source, ticks, fallback);

        var counts = report.Final.Counts;
        Logger.Info(
            "Scenario {Outcome}: {Ticks} ticks, {Date}, ${Funds}, {Roads} road tiles ({Bridges} bridges), {Residential} residential, {Commercial} commercial, {Industrial} industrial, {Developed} developed",
            report.Passed ? "passed" : "FAILED",
            report.Final.Tick,
            report.Final.Date,
            report.Final.Funds,
            counts.Road,
            counts.Bridge,
            counts.Residential,
            counts.Commercial,
            counts.Industrial,
            counts.Developed);
        return report;
    }

    private static void LogAction(ActionReport action)
    {
        var reason = action.Reason is null ? string.Empty : $" ({action.Reason})";
        var expectation = action.Met switch
        {
            true => ", as expected",
            false => ", NOT as expected",
            null => string.Empty,
        };

        if (action.Met == false)
        {
            Logger.Warn("action {Index} at tick {Tick}: {Tool} {Status}{Reason}, {Tiles} tiles, ${Cost}{Expectation}", action.Index, action.Tick, action.Tool, action.Status, reason, action.Tiles, action.Cost, expectation);
            return;
        }

        Logger.Info("action {Index} at tick {Tick}: {Tool} {Status}{Reason}, {Tiles} tiles, ${Cost}{Expectation}", action.Index, action.Tick, action.Tool, action.Status, reason, action.Tiles, action.Cost, expectation);
    }

    private static void LogCheck(CheckReport check)
    {
        if (check.Passed)
        {
            Logger.Info("check {Index} at tick {Tick}: passed", check.Index, check.Tick);
            return;
        }

        Logger.Warn("check {Index} at tick {Tick}: FAILED, expected {Expected}, found {Actual}", check.Index, check.Tick, check.Expected, check.Actual);
    }

    private static int List(SubcommandContext context, ScenarioCatalog catalog)
    {
        var ids = catalog.ListIds(out var problem);
        if (problem is not null)
        {
            Logger.Warn("{Problem}", problem);
        }

        foreach (var id in ids)
        {
            var line = catalog.TryLoad(id, out var scenario, out var error)
                ? $"{id,-24} {scenario.Name}"
                : $"{id,-24} (can't be read: {error})";
            context.Output.WriteLine(line);
        }

        return ExitCodes.Success;
    }

    private static int Refuse(SubcommandContext context, string error)
    {
        context.Error.WriteLine(error);
        context.Error.WriteLine("Run 'citylimits scenario --help' for the options.");
        return ExitCodes.Usage;
    }
}
