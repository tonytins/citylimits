namespace CityLimits.Game.Commands;

using CityLimits.CommandLine;

/// <summary>Headless subcommand - runs a city with no window.</summary>
public sealed class HeadlessSubcommand : ISubcommand
{
    /// <summary>The most ticks a headless run takes, so a mistyped count cannot run for hours.</summary>
    public const int MaxTicks = 100_000;

    private static readonly OptionSpec Options = new(["--seed", "--ticks"], []);

    public string Name => "headless";

    public string Description => "Run a generated city with no window";

    public string Usage => $"""
        Usage: citylimits headless [options]

        Options:
          --seed <seed>     the seed the city is built from (default {GameLoop.DefaultSeed})
          --ticks <count>   how many ticks the run takes, 0 to {MaxTicks} (default 100)
        """;

    public int Execute(SubcommandContext context)
    {
        if (!CommandOptions.TryParse(context.Args, Options, out var options, out var error)
            || !options.TryGetInt("--seed", GameLoop.DefaultSeed, int.MinValue, int.MaxValue, out var seed, out error)
            || !options.TryGetInt("--ticks", 100, 0, MaxTicks, out var ticks, out error))
        {
            context.Error.WriteLine(error);
            context.Error.WriteLine("Run 'citylimits headless --help' for the options.");
            return ExitCodes.Usage;
        }

        return new HeadlessRun(seed, ticks).Run();
    }
}
