namespace CityLimits.UI;

/// <summary>
/// The news ticker, after Tony Bark's mock-up in <c>docs/design/</c>: a strip of news that scrolls
/// from right to left and never stops. Lines posted to it wait their turn in a
/// <see cref="NewsQueue"/>, disasters and advisories ahead of headlines, and when nothing is
/// waiting a filler line keeps it running. Each line is drawn in its kind's colour; an
/// ordinance's news, in blue, can be clicked (<see cref="OrdinanceAt"/>). The loading screen uses
/// one too, for its phrases.
/// </summary>
public sealed class NewsTicker
{
    /// <summary>The height of the strip, in pixels.</summary>
    public const int Height = 24;

    /// <summary>The size of the ticker's text in the game.</summary>
    public const int GameFontSize = 14;

    // Pixels a second: slow enough to read a line as it passes.
    private const float Speed = 90f;

    // The space between one line and the next, with a dot in the middle of it.
    private const int Spacing = 48;
    private const float DotRadius = 2f;

    // More lines than can ever fit across a screen at once, so the list never grows.
    private const int MostShowing = 32;

    private readonly NewsQueue _queue = new();
    private readonly Func<NewsItem> _filler;
    private readonly List<Showing> _showing = new(MostShowing);
    private readonly int _fontSize;

    /// <summary>Creates a ticker that asks the filler for a line whenever nothing is waiting, with text of the given size.</summary>
    public NewsTicker(Func<NewsItem> filler, int fontSize = GameFontSize)
    {
        ArgumentNullException.ThrowIfNull(filler);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        _filler = filler;
        _fontSize = fontSize;
    }

    /// <summary>Queues a line to scroll by when its turn comes.</summary>
    public void Post(NewsItem item) => _queue.Post(item);

    /// <summary>Queues a line from the game, such as a save or a warning, in yellow.</summary>
    public void Advise(string text) => _queue.Post(new NewsItem(text, NewsKind.Advisory));

    /// <summary>
    /// Moves the lines on by the time a frame took, across a strip of the given width, and brings
    /// on the next line once the last one is clear of the right edge. A ticker that has nothing
    /// on it yet starts its first line at the given share of the width, so the screen isn't empty.
    /// </summary>
    public void Update(float elapsed, int width, float startShare = 1f)
    {
        if (float.IsNaN(elapsed) || elapsed < 0)
        {
            elapsed = 0;
        }

        // A long frame moves the lines on at most a screen, so a stall doesn't skip the news.
        var step = Math.Min(Speed * elapsed, width);
        for (var i = _showing.Count - 1; i >= 0; i--)
        {
            var line = _showing[i] with { X = _showing[i].X - step };
            if (line.X + line.Width < 0)
            {
                _showing.RemoveAt(i);
                continue;
            }

            _showing[i] = line;
        }

        while (_showing.Count < MostShowing)
        {
            float x;
            if (_showing.Count == 0)
            {
                x = width * Math.Clamp(startShare, 0f, 1f);
            }
            else
            {
                var last = _showing[^1];
                x = last.X + last.Width + Spacing;
                if (x > width)
                {
                    break;
                }
            }

            var item = _queue.TryNext(out var next) ? next : _filler();
            _showing.Add(new Showing(item, x, Raylib.MeasureText(item.Text, _fontSize)));
        }
    }

    /// <summary>Draws the strip with its top edge at the given height, on black or, for the loading screen, on nothing.</summary>
    public void Draw(int top, int width, bool background = true)
    {
        if (background)
        {
            Raylib.DrawRectangle(0, top, width, Height, Theme.Bar);
        }

        var textY = top + ((Height - _fontSize) / 2);
        foreach (var line in _showing)
        {
            Raylib.DrawText(line.Item.Text, (int)line.X, textY, _fontSize, ColorOf(line.Item.Kind));
            Raylib.DrawCircleV(new Vector2(line.X + line.Width + (Spacing / 2f), top + (Height / 2f)), DotRadius, Theme.TextDim);
        }
    }

    /// <summary>
    /// The ordinance whose news is under a point, on a strip with its top edge at the given height,
    /// or null when the point isn't on one.
    /// </summary>
    public string? OrdinanceAt(Vector2 point, int top)
    {
        if (point.Y < top || point.Y >= top + Height)
        {
            return null;
        }

        foreach (var line in _showing)
        {
            if (line.Item.Ordinance is { } id && point.X >= line.X && point.X < line.X + line.Width)
            {
                return id;
            }
        }

        return null;
    }

    private static Color ColorOf(NewsKind kind) => kind switch
    {
        NewsKind.Disaster => Theme.NewsDisaster,
        NewsKind.Ordinance => Theme.NewsOrdinance,
        NewsKind.Advisory => Theme.NewsAdvisory,
        _ => Theme.NewsHeadline,
    };

    private readonly record struct Showing(NewsItem Item, float X, int Width);
}
