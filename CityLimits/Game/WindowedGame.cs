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

        MakeFolder(folders.ModsFolder, "mods");
        MakeFolder(folders.ScenariosFolder, "scenarios");
        var settings = SettingsStore.Load(folders.SettingsToRead, out var warning);
        if (warning is not null)
        {
            Logger.Warn("{Warning}", warning);
        }

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(settings.WindowWidth, settings.WindowHeight, "CityLimits");
        Raylib.SetTargetFPS(60);
        Display.Apply(settings.Fullscreen);

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

            using var menu = new StartMenu(assets, VersionInfo.DisplayVersion);
            // The game's own challenges, then the player's.
            IReadOnlyList<string> scenarioFolders = [Commands.ScenarioSubcommand.ScenarioFolder, folders.ScenariosFolder];
            menu.Show(fromLoading: true, seed, folders.CitiesFolder, settings, scenarioFolders, GameMods.Load(folders, out _));
            while (true)
            {
                var choice = RunMenu(menu, folders);
                settings = menu.Settings;
                IReadOnlyList<string> modProblems = [];
                CitySimulation? city = choice switch
                {
                    StartMenu.Choice.NewCity when menu.City is { } wanted => CreateCity(wanted, folders, out modProblems),
                    StartMenu.Choice.LoadCity => menu.LoadedCity,
                    StartMenu.Choice.PlayScenario or StartMenu.Choice.EditScenario => menu.ChallengeCity,
                    _ => null,
                };
                if (city is null)
                {
                    break;
                }

                var file = choice == StartMenu.Choice.LoadCity ? menu.LoadedFile : null;
                var notes = choice == StartMenu.Choice.LoadCity ? menu.LoadedNotes : [];
                var editing = choice == StartMenu.Choice.EditScenario ? menu.Editing : null;
                if (PlayCities(city, file, modProblems, notes, editing, ref settings, folders, assets) != GameLoop.Leave.ToMenu)
                {
                    break;
                }

                menu.Show(fromLoading: false, seed, folders.CitiesFolder, settings, scenarioFolders, GameMods.Load(folders, out _));
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
    // Closing the settings window writes the settings, and the menu carries on.
    private static StartMenu.Choice RunMenu(StartMenu menu, GameFolders folders)
    {
        while (!Raylib.WindowShouldClose())
        {
            try
            {
                var choice = menu.Update();
                menu.Draw();
                if (choice == StartMenu.Choice.SettingsClosed)
                {
                    SaveSettings(folders, menu.Settings);
                }
                else if (choice != StartMenu.Choice.None)
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
        var city = new CitySimulation(map, seed, definitions: GameMods.Load(folders, out modProblems));
        city.Rename(wanted.Name);
        return city;
    }

    // Plays a city until the player leaves it, and then any city they open from it, keeping the
    // settings each ends with. Returns how the last one was left. A scenario being edited carries on
    // in the city it opens, its test gone back on or its city afresh, looked at from where it was
    // with the same tool in hand.
    private static GameLoop.Leave PlayCities(CitySimulation city, string? file, IReadOnlyList<string> modProblems, IReadOnlyList<string> notes, ScenarioEdit? editing, ref GameSettings settings, GameFolders folders, GameAssets assets)
    {
        (Vector2 Target, float Zoom)? lookAt = null;
        Tool? tool = null;
        while (true)
        {
            Logger.Info("{Doing} {Name}{From}", editing is null ? "Playing" : "Editing", city.Name, file is null ? string.Empty : $" from {file}");
            GameLoop.Leave leaving;
            using (var game = new GameLoop(settings, city, file, folders, assets, modProblems, notes, editing, lookAt, tool))
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
                    editing = game.Editing;
                    lookAt = editing is null ? null : game.LookingAt;
                    tool = editing is null ? null : game.SelectedTool;
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
        if (!SettingsStore.TrySave(folders.SettingsFile, Display.WithWindowSize(settings), out var error))
        {
            Logger.Error("{Error}", error);
        }
    }

    // The game makes the mods and scenarios folders so a player can find where their own go;
    // without them, the game simply runs with none.
    private static void MakeFolder(string folder, string what)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn("The {What} folder {Folder} couldn't be made: {Reason}", what, folder, ex.Message);
        }
    }
}
