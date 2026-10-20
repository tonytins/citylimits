namespace CityLimits.Game.Commands;

using CityLimits.CommandLine;
using NLog;

/// <summary>Headless subcommand - runs a city with no window.</summary>
public sealed class HeadlessSubcommand : ISubcommand
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public string Name => "headless";

    public string Description => "Run a generated city with no window";

    public int Execute(SubcommandContext context)
    {
        int seed = GameLoop.DefaultSeed;
        int ticks = 100;
        var args = context.Args;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedSeed))
            {
                seed = parsedSeed;
                continue;
            }
            if (args[i] == "--ticks" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedTicks))
            {
                if (parsedTicks >= 0)
                {
                    ticks = parsedTicks;
                }
                continue;
            }
        }

        return new HeadlessRun(seed, ticks).Run();
    }
}
