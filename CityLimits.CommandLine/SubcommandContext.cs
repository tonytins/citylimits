namespace CityLimits.CommandLine;

/// <summary>Context passed to a subcommand during execution.</summary>
public sealed record SubcommandContext
{
    /// <summary>The raw arguments passed to the process.</summary>
    public string[] RawArgs { get; init; } = Array.Empty<string>();

    /// <summary>The arguments for this subcommand (everything after the subcommand name).</summary>
    public string[] Args { get; init; } = Array.Empty<string>();

    /// <summary>Whether help was requested.</summary>
    public bool HelpRequested { get; init; }

    /// <summary>Whether the command runs with no window, from the global <c>--headless</c> flag.</summary>
    public bool Headless { get; init; }

    /// <summary>The subcommand that was invoked, or null when none was named.</summary>
    public string? Subcommand { get; init; }

    /// <summary>Where the command writes what it was asked for.</summary>
    public TextWriter Output { get; init; } = Console.Out;

    /// <summary>Where the command reports a command line it could not use.</summary>
    public TextWriter Error { get; init; } = Console.Error;
}
