namespace CityLimits.UI;

/// <summary>
/// The loading screen, after Tony Bark's mock-up in <c>docs/design/</c>: the logo in the middle of
/// the blue screen, and its phrases scrolling by along the bottom as the news ticker does. The game
/// loads in well under a second, so the screen is for show: it stays a moment, or until the player
/// presses a key or clicks, and then the start menu takes over, gliding the logo up to its place.
/// </summary>
public sealed class LoadingScreen
{
    private const float DurationSeconds = 2.5f;
    private const int Margin = 24;
    private const int FontSize = 20;

    // The first phrase starts a third of the way across, so there is something to read at once.
    private const float StartShare = 1f / 3f;

    private readonly Texture2D? _logo;
    private readonly NewsTicker _phrases;
    private int _nextPhrase;
    private float _elapsed;
    private bool _skipped;

    /// <summary>Creates the loading screen with the logo, when it loaded.</summary>
    public LoadingScreen(GameAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _logo = assets.Logo;
        _phrases = new NewsTicker(NextPhrase, FontSize);
    }

    /// <summary>Whether the screen is done and the start menu may follow.</summary>
    public bool IsDone => _skipped || _elapsed >= DurationSeconds;

    /// <summary>Where the logo's top-left corner sits on the loading screen, for a logo of the given size.</summary>
    public static Vector2 LogoPosition(Vector2 logoSize) =>
        new((Raylib.GetScreenWidth() - logoSize.X) / 2f, (Raylib.GetScreenHeight() - logoSize.Y) / 2f);

    /// <summary>Advances the screen by a frame, and ends it on any key or click.</summary>
    public void Update()
    {
        var elapsed = Raylib.GetFrameTime();
        _elapsed += elapsed;
        _phrases.Update(elapsed, Raylib.GetScreenWidth(), StartShare);
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

        _phrases.Draw(Raylib.GetScreenHeight() - NewsTicker.Height - Margin, Raylib.GetScreenWidth(), background: false);

        Raylib.EndDrawing();
    }

    // The phrases in order, round again after the last.
    private NewsItem NextPhrase()
    {
        var phrase = LoadingPhrases.All[_nextPhrase];
        _nextPhrase = (_nextPhrase + 1) % LoadingPhrases.All.Length;
        return new NewsItem(phrase, NewsKind.Headline);
    }
}
