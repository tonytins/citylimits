namespace CityLimits.CommandLine;

/// <summary>A subcommand that the CLI can execute.</summary>
public interface ISubcommand
{
    /// <summary>The name of the subcommand (e.g. "play", "headless", "scenario").</summary>
    string Name { get; }

    /// <summary>A short description of what the subcommand does.</summary>
    string Description { get; }

    /// <summary>Executes the subcommand with the provided context.</summary>
    /// <returns>The exit code to return from the process.</returns>
    int Execute(SubcommandContext context);
}
