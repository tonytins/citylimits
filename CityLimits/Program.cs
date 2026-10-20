namespace CityLimits;

using CityLimits.CommandLine;
using CityLimits.Game.Commands;

internal static class Program
{
    // STAThread is required if you deploy using NativeAOT on Windows
    // See https://github.com/raylib-cs/raylib-cs/issues/301
    [System.STAThread]
    public static int Main(string[] args)
    {
        GameLog.Setup();

        try
        {
            var dispatcher = new SubcommandDispatcher(
                [new HeadlessSubcommand(), new ScenarioSubcommand()],
                WindowedGame.Command);

            return dispatcher.Dispatch(args);
        }
        finally
        {
            LogManager.Shutdown();
        }
    }
}
