namespace CityLimits.UI;

/// <summary>
/// The loading screen, after the owner's mock-up in <c>docs/design/</c>: the logo in the middle of
/// the blue screen and a line of text in the bottom right. The game loads in well under a second,
/// so the screen is for show: it stays a moment, or until the player presses a key or clicks,
/// and then the start menu takes over, gliding the logo up to its place.
/// </summary>
public sealed class LoadingScreen
{
    // The lines the screen shows, one at a time. More can join; the first is a nod to SimCity 2000.
    private static readonly string[] Lines = ["Reticulating splines"];

    private const float DurationSeconds = 2.5f;
    private const int FontSize = 20;
    private const int Margin = 24;

    private readonly Texture2D? _logo;
    private float _elapsed;
    private bool _skipped;

    /// <summary>Creates the loading screen with the logo, when it loaded.</summary>
    public LoadingScreen(GameAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _logo = assets.Logo;
    }

    /// <summary>Whether the screen is done and the start menu may follow.</summary>
    public bool IsDone => _skipped || _elapsed >= DurationSeconds;

    /// <summary>Where the logo's top-left corner sits on the loading screen, for a logo of the given size.</summary>
    public static Vector2 LogoPosition(Vector2 logoSize) =>
        new((Raylib.GetScreenWidth() - logoSize.X) / 2f, (Raylib.GetScreenHeight() - logoSize.Y) / 2f);

    /// <summary>Advances the screen by a frame, and ends it on any key or click.</summary>
    public void Update()
    {
        _elapsed += Raylib.GetFrameTime();
        if (Raylib.GetKeyPressed() != 0
            || Raylib.IsMouseButtonPressed(MouseButton.Left)
            || Raylib.IsMouseButtonPressed(MouseButton.Right)
            || Raylib.IsMouseButtonPressed(MouseButton.Middle))
        {
            _skipped = true;
        }
    }

    /// <summary>Draws the screen as a whole frame.</summary>
    public void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Theme.SplashBackground);

        if (_logo is { } logo && Raylib.IsTextureValid(logo))
        {
            Raylib.DrawTextureV(logo, LogoPosition(new Vector2(logo.Width, logo.Height)), Color.White);
        }

        // A new line every second, should there be more than one.
        var line = Lines[(int)_elapsed % Lines.Length];
        var width = Raylib.MeasureText(line, FontSize);
        Raylib.DrawText(line, Raylib.GetScreenWidth() - width - Margin, Raylib.GetScreenHeight() - FontSize - Margin, FontSize, Theme.Text);

        Raylib.EndDrawing();
    }
}
