namespace CityLimits.Game;

/// <summary>Turns on the log the game writes to.</summary>
public static class GameLog
{
    /// <summary>Sends the log to the console, and to nowhere else.</summary>
    public static void Setup() => Configure(standardError: false);

    /// <summary>
    /// Moves the log to the console's error stream, so a command can write a report on the
    /// output stream that a script can parse with no log lines mixed in.
    /// </summary>
    public static void UseStandardError() => Configure(standardError: true);

    private static void Configure(bool standardError)
    {
        var configuration = new LoggingConfiguration();

        var console = new ConsoleTarget("console")
        {
            Layout = "${time:HH:mm:ss} ${level:uppercase=true:padding=-5} ${message} ${exception:format=tostring}",
            StdErr = standardError,
        };

        configuration.AddRule(LogLevel.Info, LogLevel.Fatal, console);
        LogManager.Setup().LoadConfiguration(configuration);
    }
}
