namespace CityLimits;

using CityLimits.CommandLine;
using CityLimits.Game;
using CityLimits.Game.Commands;
using NLog;

internal static class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // STAThread is required if you deploy using NativeAOT on Windows
    // See https://github.com/raylib-cs/raylib-cs/issues/301
    [System.STAThread]
    public static int Main(string[] args)
    {
        GameLog.Setup();

        try
        {
            var dispatcher = new SubcommandDispatcher(
                new ISubcommand[]
                {
                    new HeadlessSubcommand(),
                    new ScenarioSubcommand(),
                },
                new PlaySubcommand());

            return dispatcher.Dispatch(args);
        }
        finally
        {
            LogManager.Shutdown();
        }
    }
}
