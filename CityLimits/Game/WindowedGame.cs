namespace CityLimits.Game;

using CityLimits.CommandLine;

/// <summary>The game in a window: it starts a new city and runs until the window is closed.</summary>
public static class WindowedGame
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Opens the window, runs the game until it is closed, and returns the exit code.</summary>
    public static int Play(int seed)
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        var settings = SettingsStore.Load(settingsPath, out var warning);
        if (warning is not null)
        {
            Logger.Warn("{Warning}", warning);
        }

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(settings.WindowWidth, settings.WindowHeight, "CityLimits");
        Raylib.SetTargetFPS(60);

        var assets = GameAssets.Load(Path.Combine(AppContext.BaseDirectory, GameAssets.FolderName), out var problems);
        foreach (var problem in problems)
        {
            Logger.Error("{Problem}", problem);
        }

        var splash = new SplashScreen(assets);
        while (!splash.IsDone && !Raylib.WindowShouldClose())
        {
            splash.Update();
            splash.Draw();
        }

        try
        {
            using var game = new GameLoop(settings, settingsPath, assets, seed);

            while (!Raylib.WindowShouldClose() && !game.ShouldClose)
            {
                try
                {
                    game.Update();
                    game.Draw();
                }
                catch (Exception ex)
                {
                    game.ReportError(ex);
                }
            }

            game.SaveSettings();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "CityLimits stopped");
            return ExitCodes.Failure;
        }
        finally
        {
            assets.Dispose();
            Raylib.CloseWindow();
        }

        return ExitCodes.Success;
    }
}
