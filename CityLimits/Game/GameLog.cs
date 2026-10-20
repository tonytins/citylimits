namespace CityLimits.Game;

/// <summary>Turns on the log the game writes to.</summary>
public static class GameLog
{
    /// <summary>Sends the log to the console, and to nowhere else.</summary>
    public static void Setup()
    {
        var configuration = new LoggingConfiguration();

        var console = new ConsoleTarget("console")
        {
            Layout = "${time:HH:mm:ss} ${level:uppercase=true:padding=-5} ${message} ${exception:format=tostring}",
        };

        configuration.AddRule(LogLevel.Info, LogLevel.Fatal, console);
        LogManager.Setup().LoadConfiguration(configuration);
    }
}
