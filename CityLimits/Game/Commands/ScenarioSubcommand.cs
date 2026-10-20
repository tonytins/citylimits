namespace CityLimits.Game.Commands;

using CityLimits.CommandLine;

/// <summary>Scenario subcommand - load and run a scenario. Only its command line exists so far.</summary>
public sealed class ScenarioSubcommand : ISubcommand
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly OptionSpec Options = new(["--file", "--ticks"], ["--json"], MaxPositionals: 1);

    public string Name => "scenario";

    public string Description => "Load and run a scenario for debugging (not implemented yet)";

    public string Usage => $"""
        Usage: citylimits scenario <id> [options]
               citylimits scenario --file <path> [options]
               citylimits scenario list

        Options:
          --file <path>     load a scenario from a JSON file
          --ticks <count>   advance this many ticks, then exit, 0 to {HeadlessSubcommand.MaxTicks} (default 0)
          --json            write machine-readable JSON to stdout
        """;

    public int Execute(SubcommandContext context)
    {
        if (!CommandOptions.TryParse(context.Args, Options, out var options, out var error)
            || !options.TryGetInt("--ticks", 0, 0, HeadlessSubcommand.MaxTicks, out var ticks, out error))
        {
            return Refuse(context, error);
        }

        var scenarioId = options.Positionals.FirstOrDefault();
        var scenarioFile = options.ValueOf("--file");

        if (scenarioId == "list")
        {
            return options.Has("--json") || scenarioFile is not null || ticks != 0
                ? Refuse(context, "'scenario list' takes no options.")
                : ListScenarios(context);
        }

        if (scenarioId is null == scenarioFile is null)
        {
            return Refuse(context, "Name a scenario or give --file, but not both.");
        }

        Logger.Info(
            "Scenario requested: id={Id}, file={File}, ticks={Ticks}, json={Json}",
            scenarioId,
            scenarioFile,
            ticks,
            options.Has("--json"));
        context.Error.WriteLine("Scenarios are not implemented yet, so nothing was run.");
        return ExitCodes.Failure;
    }

    private static int ListScenarios(SubcommandContext context)
    {
        context.Output.WriteLine("Available scenarios: none yet.");
        return ExitCodes.Success;
    }

    private static int Refuse(SubcommandContext context, string error)
    {
        context.Error.WriteLine(error);
        context.Error.WriteLine("Run 'citylimits scenario --help' for the options.");
        return ExitCodes.Usage;
    }
}
