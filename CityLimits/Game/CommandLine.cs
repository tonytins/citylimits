namespace CityLimits.Game;

/// <summary>The options the game was started with.</summary>
public sealed record CommandLineOptions
{
    /// <summary>The text the game prints when it is asked how to use it.</summary>
    public const string Usage = """
        CityLimits

          --headless        run the city with no window
          --ticks <count>   how many ticks a headless run takes (default 100)
          --seed <seed>     the seed the city is built from (default 2000)
          --help            print this text
        """;

    /// <summary>Whether the game runs with no window.</summary>
    public bool Headless { get; init; }

    /// <summary>How many ticks a headless run takes.</summary>
    public int Ticks { get; init; } = 100;

    /// <summary>The seed the city is built from.</summary>
    public int Seed { get; init; } = GameLoop.DefaultSeed;

    /// <summary>Whether the player asked for the usage text.</summary>
    public bool ShowHelp { get; init; }

    /// <summary>Reads the options, and reports the first one that makes no sense.</summary>
    public static bool TryParse(string[] args, out CommandLineOptions options, out string error)
    {
        var parsed = new CommandLineOptions();
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--headless":
                    parsed = parsed with { Headless = true };
                    break;

                case "--help":
                case "-h":
                    parsed = parsed with { ShowHelp = true };
                    break;

                case "--ticks":
                    if (!TryReadNumber(args, ref i, out var ticks))
                    {
                        error = "--ticks needs a count of ticks.";
                        options = parsed;
                        return false;
                    }

                    if (ticks < 0)
                    {
                        error = $"--ticks needs a count of zero or more, not {ticks}.";
                        options = parsed;
                        return false;
                    }

                    parsed = parsed with { Ticks = ticks };
                    break;

                case "--seed":
                    if (!TryReadNumber(args, ref i, out var seed))
                    {
                        error = "--seed needs a number.";
                        options = parsed;
                        return false;
                    }

                    parsed = parsed with { Seed = seed };
                    break;

                default:
                    error = $"Unknown option '{args[i]}'.";
                    options = parsed;
                    return false;
            }
        }

        options = parsed;
        return true;
    }

    private static bool TryReadNumber(string[] args, ref int index, out int value)
    {
        value = 0;
        if (index + 1 >= args.Length)
        {
            return false;
        }

        index++;
        return int.TryParse(args[index], out value);
    }
}
