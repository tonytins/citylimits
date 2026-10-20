namespace CityLimits;

internal static class Program
{
    private const string WindowTitle = "CityLimits";
    private const int TargetFramesPerSecond = 60;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // STAThread is required if you deploy using NativeAOT on Windows
    // See https://github.com/raylib-cs/raylib-cs/issues/301
    [System.STAThread]
    public static int Main(string[] args)
    {
        GameLog.Setup();

        try
        {
            if (!CommandLineOptions.TryParse(args, out var options, out var error))
            {
                Logger.Error("{Error}", error);
                Console.Error.WriteLine(CommandLineOptions.Usage);
                return 2;
            }

            if (options.ShowHelp)
            {
                Console.WriteLine(CommandLineOptions.Usage);
                return 0;
            }

            return options.Headless
                ? new HeadlessRun(options.Seed, options.Ticks).Run()
                : RunGame(options);
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    private static int RunGame(CommandLineOptions options)
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        var settings = SettingsStore.Load(settingsPath, out var warning);
        if (warning is not null)
        {
            Logger.Warn("{Warning}", warning);
        }

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(settings.WindowWidth, settings.WindowHeight, WindowTitle);
        Raylib.SetTargetFPS(TargetFramesPerSecond);

        // Textures need an OpenGL context, so they are loaded once the window is open.
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
            var game = new GameLoop(settings, settingsPath, assets, options.Seed);

            while (!Raylib.WindowShouldClose())
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
            return 1;
        }
        finally
        {
            assets.Dispose();
            Raylib.CloseWindow();
        }

        return 0;
    }
}
