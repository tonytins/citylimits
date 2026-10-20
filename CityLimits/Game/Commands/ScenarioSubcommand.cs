namespace CityLimits.Game.Commands;

using CityLimits.CommandLine;
using NLog;

/// <summary>Scenario subcommand - load and run a scenario.</summary>
public sealed class ScenarioSubcommand : ISubcommand
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public string Name => "scenario";

    public string Description => "Load and run a scenario for debugging";

    public int Execute(SubcommandContext context)
    {
        var args = context.Args;
        if (args.Length == 0 || args.Any(a => a is "--help" or "-h"))
        {
            PrintHelp();
            return args.Length == 0 ? 2 : 0;
        }

        // Simple initial implementation - just list for now
        if (args[0] == "list" || args.Contains("--list"))
        {
            Console.WriteLine("Available scenarios: (none yet - coming soon)");
            return 0;
        }

        // Try to get scenario id (positional)
        string? scenarioId = null;
        string? scenarioFile = null;
        int ticks = 0;
        bool json = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--file" && i + 1 < args.Length)
            {
                scenarioFile = args[i + 1];
                continue;
            }
            if (args[i] == "--ticks" && i + 1 < args.Length && int.TryParse(args[i + 1], out var t))
            {
                ticks = t;
                continue;
            }
            if (args[i] == "--json")
            {
                json = true;
                continue;
            }
            if (!args[i].StartsWith("--") && scenarioId == null && scenarioFile == null && args[i] != "list")
            {
                scenarioId = args[i];
            }
        }

        Logger.Info("Scenario mode requested (placeholder): id={Id}, file={File}, ticks={Ticks}, json={Json}",
            scenarioId, scenarioFile, ticks, json);
        Console.WriteLine("Scenario mode is not yet implemented. This is the scaffolding.");
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: citylimits scenario <id> [options]");
        Console.WriteLine("       citylimits scenario --file <path> [options]");
        Console.WriteLine("       citylimits scenario list");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file <path>   Load a scenario from a JSON file");
        Console.WriteLine("  --ticks <n>     Advance N ticks then exit (default 0)");
        Console.WriteLine("  --json          Emit machine-readable JSON to stdout");
    }
}
