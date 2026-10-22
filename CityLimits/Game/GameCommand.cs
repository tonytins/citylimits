namespace CityLimits.Game;

using CityLimits.CommandLine;

/// <summary>What runs when no subcommand is named: the game in a window, or with <c>--headless</c>, the city with no window.</summary>
public static class GameCommand
{
    /// <summary>The options the game accepts, as the usage text shows them.</summary>
    public static readonly string Usage = $"""
          --seed <seed>     the seed a new city is built from: with --headless, the city's (default {GameLoop.DefaultSeed});
                            in the window, it fills in New Game
          --ticks <count>   with --headless: how many ticks the run takes, 0 to {HeadlessRun.MaxTicks} (default {HeadlessRun.DefaultTicks})
        """;

    private static readonly OptionSpec Options = new(["--seed", "--ticks"], []);

    /// <summary>The game as the dispatcher runs it by default.</summary>
    public static DefaultCommand Command { get; } = new("Start a new city in a window", Usage, Run, SupportsHeadless: true);

    /// <summary>Reads the options, then runs the city headless or opens the window.</summary>
    public static int Run(SubcommandContext context)
    {
        if (!CommandOptions.TryParse(context.Args, Options, out var options, out var error)
            || !options.TryGetInt("--seed", GameLoop.DefaultSeed, int.MinValue, int.MaxValue, out var seed, out error)
            || !options.TryGetInt("--ticks", HeadlessRun.DefaultTicks, 0, HeadlessRun.MaxTicks, out var ticks, out error))
        {
            return Refuse(context, error);
        }

        if (!context.Headless && options.ValueOf("--ticks") is not null)
        {
            return Refuse(context, "--ticks only applies with --headless; the window runs until it is closed.");
        }

        // The headless branch must never reach WindowedGame, or it would open a window.
        return context.Headless
            ? new HeadlessRun(seed, ticks).Run()
            : WindowedGame.Play(options.ValueOf("--seed") is null ? null : seed);
    }

    private static int Refuse(SubcommandContext context, string error)
    {
        context.Error.WriteLine(error);
        context.Error.WriteLine("Run 'citylimits --help' for the options.");
        return ExitCodes.Usage;
    }
}
