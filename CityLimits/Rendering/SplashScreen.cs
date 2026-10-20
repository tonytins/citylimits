namespace CityLimits.Rendering;

/// <summary>A skippable splash screen shown before the game starts.</summary>
public sealed class SplashScreen
{
    /// <summary>How long the splash screen shows for, unless the player skips it.</summary>
    private const float DurationSeconds = 3f;

    private const int FontSize = 14;
    private const int Padding = 10;

    private static readonly string Hint = "Press any key or click to skip";

    private readonly Texture2D? _logo;
    private readonly float _screenWidth;
    private readonly float _screenHeight;

    private float _elapsed;
    private bool _skipped;

    /// <summary>Whether the splash screen is done showing and the game may start.</summary>
    public bool IsDone => _skipped || _elapsed >= DurationSeconds;

    /// <summary>Creates the splash screen for the assets that were just loaded.</summary>
    public SplashScreen(GameAssets assets)
    {
        _logo = assets.Logo;
        _screenWidth = Raylib.GetScreenWidth();
        _screenHeight = Raylib.GetScreenHeight();
    }

    /// <summary>Advances the splash screen by one frame and watches for skip input.</summary>
    public void Update()
    {
        if (_skipped)
        {
            return;
        }

        _elapsed += Raylib.GetFrameTime();

        if (Raylib.GetKeyPressed() != 0)
        {
            _skipped = true;
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) ||
            Raylib.IsMouseButtonPressed(MouseButton.Right) ||
            Raylib.IsMouseButtonPressed(MouseButton.Middle))
        {
            _skipped = true;
        }
    }

    /// <summary>Draws the splash screen on top of whatever is already on the window.</summary>
    public void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Theme.SplashBackground);

        if (_logo is { } logo && Raylib.IsTextureValid(logo))
        {
            var x = (_screenWidth - logo.Width) / 2f;
            var y = (_screenHeight - logo.Height) / 2f;
            Raylib.DrawTexture(logo, (int)x, (int)y, Color.White);

            var version = VersionInfo.DisplayVersion;
            var versionWidth = Raylib.MeasureText(version, FontSize);
            var versionX = (int)((_screenWidth - versionWidth) / 2f);
            var versionY = (int)(y + logo.Height + Padding);
            Raylib.DrawText(version, versionX, versionY, FontSize, Theme.Text);
        }

        var hintWidth = Raylib.MeasureText(Hint, FontSize);
        var hintX = (int)((_screenWidth - hintWidth) / 2f);
        var hintY = (int)(_screenHeight - FontSize - Padding);
        Raylib.DrawText(Hint, hintX, hintY, FontSize, Theme.Text);

        Raylib.EndDrawing();
    }
}
