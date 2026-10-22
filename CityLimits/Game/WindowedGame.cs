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
            menu.Show(fromLoading: true, seed, folders.CitiesFolder);
            while (true)
            {
                var choice = RunMenu(menu);
                IReadOnlyList<string> modProblems = [];
                CitySimulation? city = choice switch
                {
                    StartMenu.Choice.NewCity when menu.City is { } wanted => CreateCity(wanted, folders, out modProblems),
                    StartMenu.Choice.LoadCity => menu.LoadedCity,
                    _ => null,
                };
                if (city is null)
                {
                    break;
                }

                var file = choice == StartMenu.Choice.LoadCity ? menu.LoadedFile : null;
                var notes = choice == StartMenu.Choice.LoadCity ? menu.LoadedNotes : [];
                if (PlayCities(city, file, modProblems, notes, ref settings, folders, assets) != GameLoop.Leave.ToMenu)
                {
                    break;
                }

                menu.Show(fromLoading: false, seed, folders.CitiesFolder);
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

    // Makes the city New Game asked for: a map from its seed, and the mods installed now.
    private static CitySimulation CreateCity(NewCity wanted, GameFolders folders, out IReadOnlyList<string> modProblems)
    {
        // Picking a seed is outside the simulation, so the shared generator may choose it; the log
        // keeps it, so the city can be grown again with --seed.
        var seed = wanted.Seed ?? Random.Shared.Next();
        Logger.Info("A new city, {Name}, from seed {Seed}", wanted.Name, seed);

        var map = TileMapGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
        var city = new CitySimulation(map, seed, definitions: GameMods.Load(folders.ModsFolder, out modProblems));
        city.Rename(wanted.Name);
        return city;
    }

    // Plays a city until the player leaves it, and then any city they open from it, keeping the
    // settings each ends with. Returns how the last one was left.
    private static GameLoop.Leave PlayCities(CitySimulation city, string? file, IReadOnlyList<string> modProblems, IReadOnlyList<string> notes, ref GameSettings settings, GameFolders folders, GameAssets assets)
    {
        while (true)
        {
            Logger.Info("Playing {Name}{From}", city.Name, file is null ? string.Empty : $" from {file}");
            GameLoop.Leave leaving;
            using (var game = new GameLoop(settings, city, file, folders, assets, modProblems, notes))
            {
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
                leaving = Raylib.WindowShouldClose() ? GameLoop.Leave.Quit : game.Leaving;
                if (leaving == GameLoop.Leave.Open && game.OpenedCity is { } opened)
                {
                    (city, file, modProblems, notes) = (opened, game.OpenedFile, [], game.OpenedNotes);
                }
            }

            if (leaving != GameLoop.Leave.Open)
            {
                return leaving;
            }
        }
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
