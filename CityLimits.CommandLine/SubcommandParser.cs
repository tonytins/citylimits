namespace CityLimits.CommandLine;

/// <summary>Parses CLI arguments into a subcommand context.</summary>
public static class SubcommandParser
{
    /// <summary>The global flag that runs a command with no window.</summary>
    public const string HeadlessFlag = "--headless";

    /// <summary>Parses the given arguments into a subcommand context.</summary>
    /// <remarks>
    /// The first argument names the subcommand unless it is an option, in which case every
    /// argument belongs to the default command. The global flags, <c>--help</c> (or <c>-h</c>)
    /// and <c>--headless</c>, may appear anywhere on the line, and are taken out of the
    /// arguments the command receives.
    /// </remarks>
    public static SubcommandContext Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var hasSubcommand = args.Length > 0 && !args[0].StartsWith('-');
        var rest = hasSubcommand ? args.Skip(1) : args;

        return new SubcommandContext
        {
            RawArgs = args,
            Args = rest.Where(a => !IsGlobalFlag(a)).ToArray(),
            Subcommand = hasSubcommand ? args[0] : null,
            HelpRequested = args.Any(a => a is "--help" or "-h"),
            Headless = args.Contains(HeadlessFlag),
        };
    }

    private static bool IsGlobalFlag(string arg) => arg is "--help" or "-h" or HeadlessFlag;
}
