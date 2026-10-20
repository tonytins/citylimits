namespace CityLimits.Game;

using CityLimits.CommandLine;

/// <summary>The game itself: what runs when no subcommand is named. It starts a new city in a window.</summary>
public static class WindowedGame
{
    /// <summary>The options the game accepts, as the usage text shows them.</summary>
    public const string Usage = """
          --seed <seed>   the seed the city is built from (default 2000)
          --help          print this text
        """;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly OptionSpec Options = new(["--seed"], []);

    /// <summary>The game as the dispatcher runs it by default.</summary>
    public static DefaultCommand Command { get; } = new("Start a new city in a window", Usage, Run);

    /// <summary>Reads the options, then opens the window and runs the game until it is closed.</summary>
    public static int Run(SubcommandContext context)
    {
        if (context.Args.Contains("--headless"))
        {
            context.Error.WriteLine("--headless is now a command: run 'citylimits headless'.");
            return ExitCodes.Usage;
        }

        if (!CommandOptions.TryParse(context.Args, Options, out var options, out var error)
            || !options.TryGetInt("--seed", GameLoop.DefaultSeed, int.MinValue, int.MaxValue, out var seed, out error))
        {
            context.Error.WriteLine(error);
            context.Error.WriteLine("Run 'citylimits --help' for the options.");
            return ExitCodes.Usage;
        }

        return Play(seed);
    }

    private static int Play(int seed)
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
            var game = new GameLoop(settings, settingsPath, assets, seed);

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
