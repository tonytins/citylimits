namespace CityLimits.CommandLine;

/// <summary>A subcommand that the CLI can execute.</summary>
public interface ISubcommand
{
    /// <summary>The name the subcommand is invoked by (e.g. "headless", "scenario").</summary>
    string Name { get; }

    /// <summary>A short description of what the subcommand does.</summary>
    string Description { get; }

    /// <summary>The usage lines and options shown by <c>citylimits &lt;name&gt; --help</c>.</summary>
    string Usage { get; }

    /// <summary>Executes the subcommand with the provided context.</summary>
    /// <returns>The exit code to return from the process; see <see cref="ExitCodes"/>.</returns>
    int Execute(SubcommandContext context);
}
