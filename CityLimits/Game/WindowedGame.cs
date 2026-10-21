namespace CityLimits.Game;

using CityLimits.CommandLine;

/// <summary>The game in a window: it starts a new city and runs until the window is closed.</summary>
public static class WindowedGame
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Opens the window, runs the game until it is closed, and returns the exit code.</summary>
    public static int Play(int seed)
    {
        var folders = GameFolders.Detect();
        if (folders.Problem is not null)
        {
            Logger.Warn("{Problem}", folders.Problem);
        }

        Logger.Info(
            "Settings in {Settings}, mods in {Mods}, cities in {Cities}{Portable}",
            folders.SettingsFile,
            folders.ModsFolder,
            folders.CitiesFolder,
            folders.IsPortable ? ", all beside the game" : string.Empty);
        if (folders.SettingsToRead != folders.SettingsFile)
        {
            Logger.Info("Reading the settings an older version left at {Old}; they'll be saved to {New}", folders.SettingsToRead, folders.SettingsFile);
        }

        MakeModsFolder(folders.ModsFolder);
        var settings = SettingsStore.Load(folders.SettingsToRead, out var warning);
        if (warning is not null)
        {
            Logger.Warn("{Warning}", warning);
        }

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(settings.WindowWidth, settings.WindowHeight, "CityLimits");
        Raylib.SetTargetFPS(60);

        // Escape cancels a stroke in progress, so it can't also close the window; GameLoop quits on it otherwise.
        Raylib.SetExitKey(KeyboardKey.Null);

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
            using var game = new GameLoop(settings, folders, assets, seed);

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

    // The game makes the mods folder so a player can find where mods go; without it, the game
    // simply runs with none.
    private static void MakeModsFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn("The mods folder {Folder} couldn't be made: {Reason}", folder, ex.Message);
        }
    }
}
