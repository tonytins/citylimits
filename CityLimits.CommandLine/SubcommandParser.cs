namespace CityLimits.CommandLine;

/// <summary>Parses CLI arguments into a subcommand context.</summary>
public static class SubcommandParser
{
    /// <summary>Parses the given arguments into a subcommand context.</summary>
    public static ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return ParseResult.Ok(new SubcommandContext
            {
                RawArgs = Array.Empty<string>(),
                Args = Array.Empty<string>(),
                Subcommand = null,
                HelpRequested = false,
            });
        }

        var rawArgs = args;
        var subcommand = args[0];
        var rest = args.Skip(1).ToArray();

        if (subcommand is "--help" or "-h")
        {
            return ParseResult.Ok(new SubcommandContext
            {
                RawArgs = rawArgs,
                Args = Array.Empty<string>(),
                Subcommand = null,
                HelpRequested = true,
            });
        }

        if (subcommand.StartsWith("--") || subcommand.StartsWith("-"))
        {
            // Old-style flag without subcommand
            return ParseResult.Ok(new SubcommandContext
            {
                RawArgs = rawArgs,
                Args = rawArgs,
                Subcommand = null,
                HelpRequested = false,
            });
        }

        var helpRequested = rest.Any(a => a is "--help" or "-h");
        return ParseResult.Ok(new SubcommandContext
        {
            RawArgs = rawArgs,
            Args = rest,
            Subcommand = subcommand,
            HelpRequested = helpRequested,
        });
    }
}
