namespace CityLimits.CommandLine;

/// <summary>What runs when no subcommand is named, such as the game itself.</summary>
/// <param name="Description">A short description of what it does.</param>
/// <param name="Usage">The options it accepts, shown by <c>citylimits --help</c>.</param>
/// <param name="Execute">Runs it and returns the exit code.</param>
/// <param name="SupportsHeadless">Whether it can run with no window; the dispatcher refuses <c>--headless</c> when it cannot.</param>
public sealed record DefaultCommand(string Description, string Usage, Func<SubcommandContext, int> Execute, bool SupportsHeadless = false);
