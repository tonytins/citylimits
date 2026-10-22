namespace CityLimits.Game;

using CityLimits.CommandLine;

/// <summary>The game in a window: the loading screen, the start menu, and the cities started from it.</summary>
public static class WindowedGame
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Opens the window: the loading screen, then the start menu, and the cities the player starts
    /// from it, until they quit or close the window. A seed from the command line fills in New
    /// Game. Returns the exit code.
    /// </summary>
    public static int Play(int? seed)
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

        // Escape cancels a stroke or opens the menu, so it can't also close the window.
        Raylib.SetExitKey(KeyboardKey.Null);

        var assets = GameAssets.Load(Path.Combine(AppContext.BaseDirectory, GameAssets.FolderName), out var problems);
        foreach (var problem in problems)
        {
            Logger.Error("{Problem}", problem);
        }

        try
        {
            var loading = new LoadingScreen(assets);
            while (!loading.IsDone && !Raylib.WindowShouldClose())
            {
                loading.Update();
                loading.Draw();
            }

            var menu = new StartMenu(assets);
            menu.Show(fromLoading: true, seed);
            while (RunMenu(menu) == StartMenu.Choice.NewCity && menu.City is { } city)
            {
                var leaving = PlayCity(city, ref settings, folders, assets);
                if (leaving != GameLoop.Leave.ToMenu)
                {
                    break;
                }

                menu.Show(fromLoading: false, seed);
            }

            SaveSettings(folders, settings);
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

    // Runs the start menu until the player chooses, or closes the window, which counts as quitting.
    private static StartMenu.Choice RunMenu(StartMenu menu)
    {
        while (!Raylib.WindowShouldClose())
        {
            try
            {
                var choice = menu.Update();
                menu.Draw();
                if (choice != StartMenu.Choice.None)
                {
                    return choice;
                }
            }
            catch (Exception ex)
            {
                // As with the city's frames, a failure is logged and the menu carries on.
                Logger.Error(ex, "The menu's frame failed");
            }
        }

        return StartMenu.Choice.Quit;
    }

    // Plays a city until the player leaves it, keeping the settings it ends with.
    private static GameLoop.Leave PlayCity(NewCity city, ref GameSettings settings, GameFolders folders, GameAssets assets)
    {
        // Picking a seed is outside the simulation, so the shared generator may choose it; the log
        // keeps it, so the city can be grown again with --seed.
        var seed = city.Seed ?? Random.Shared.Next();
        Logger.Info("A new city, {Name}, from seed {Seed}", city.Name, seed);

        using var game = new GameLoop(settings, folders, assets, seed, city.Name);
        while (!Raylib.WindowShouldClose() && game.Leaving == GameLoop.Leave.None)
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

        settings = game.Settings;
        return Raylib.WindowShouldClose() ? GameLoop.Leave.Quit : game.Leaving;
    }

    private static void SaveSettings(GameFolders folders, GameSettings settings)
    {
        var width = Math.Clamp(Raylib.GetScreenWidth(), GameSettings.MinWindowWidth, GameSettings.MaxWindowWidth);
        var height = Math.Clamp(Raylib.GetScreenHeight(), GameSettings.MinWindowHeight, GameSettings.MaxWindowHeight);
        if (!SettingsStore.TrySave(folders.SettingsFile, settings with { WindowWidth = width, WindowHeight = height }, out var error))
        {
            Logger.Error("{Error}", error);
        }
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
