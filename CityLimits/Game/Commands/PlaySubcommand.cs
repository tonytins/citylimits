namespace CityLimits.Game.Commands;

using CityLimits.CommandLine;
using CityLimits.Core.Settings;
using CityLimits.Rendering;
using CityLimits.UI;
using NLog;
using Raylib_cs;

/// <summary>Default subcommand - starts a new city in a window.</summary>
public sealed class PlaySubcommand : ISubcommand
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public string Name => "play";

    public string Description => "Start a new city in a window (default)";

    public int Execute(SubcommandContext context)
    {
        int seed = GameLoop.DefaultSeed;
        var args = context.Args;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedSeed))
            {
                seed = parsedSeed;
                break;
            }
        }

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
