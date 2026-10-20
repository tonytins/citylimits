namespace CityLimits.CommandLine;

/// <summary>Parses CLI arguments into a subcommand context.</summary>
public static class SubcommandParser
{
    /// <summary>Parses the given arguments into a subcommand context.</summary>
    /// <remarks>
    /// The first argument names the subcommand unless it is an option, in which case every
    /// argument belongs to the default command. Help is requested by <c>--help</c> or <c>-h</c>
    /// anywhere on the line.
    /// </remarks>
    public static SubcommandContext Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var helpRequested = args.Any(a => a is "--help" or "-h");

        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            return new SubcommandContext
            {
                RawArgs = args,
                Args = args.Where(a => a is not ("--help" or "-h")).ToArray(),
                Subcommand = null,
                HelpRequested = helpRequested,
            };
        }

        return new SubcommandContext
        {
            RawArgs = args,
            Args = args.Skip(1).Where(a => a is not ("--help" or "-h")).ToArray(),
            Subcommand = args[0],
            HelpRequested = helpRequested,
        };
    }
}
