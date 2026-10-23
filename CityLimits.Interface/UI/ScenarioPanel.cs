namespace CityLimits.UI;

/// <summary>
/// The challenges a player can pick from the start menu, laid out like Load Game's list: the game's
/// own, then the player's, each with its dates and how many goals it has. A click picks one, and
/// its briefing shows under the list: what it's about, and the goals to meet by its end. Play, or
/// Enter, starts it; Back or Escape leaves. A scenario that can't be read is listed with why, and
/// can't be played.
/// </summary>
public sealed class ScenarioPanel
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const int FontSize = 20;
    private const int RowSize = 14;
    private const int SmallSize = 10;
    private const int Padding = 16;
    private const int Width = 620;
    private const int Rows = 6;
    private const int RowHeight = 34;
    private const int BriefingLines = 7;
    private const int BriefingLineHeight = 16;
    private const int ButtonWidth = 100;
    private const int ButtonHeight = 30;
    private const int ListTop = Padding + FontSize + 16;
    private const int BriefingTop = ListTop + (Rows * RowHeight) + 12;
    private const int Height = BriefingTop + (BriefingLines * BriefingLineHeight) + 12 + ButtonHeight + Padding;

    private IReadOnlyList<ScenarioListing> _listings = [];

    // Each row's words, and the briefing, are laid out when they change, never in a frame.
    private readonly List<(string Name, string Details, bool Playable)> _rows = [];
    private readonly List<string> _briefing = [];
    private readonly List<string> _problemLines = [];
    private int _first;
    private int _selected = -1;
    private string? _problem;

    /// <summary>What a frame of the panel came to.</summary>
    public enum Outcome
    {
        /// <summary>The player is still choosing.</summary>
        Open,

        /// <summary>The player started the challenge, whose city is in <see cref="City"/>.</summary>
        Play,

        /// <summary>The player went back.</summary>
        Back,
    }

    /// <summary>The challenge's city, being played, once the outcome is <see cref="Outcome.Play"/>.</summary>
    public CitySimulation? City { get; private set; }

    /// <summary>Tells whether any challenge, readable or not, was found.</summary>
    public bool HasScenarios => _listings.Count > 0;

    /// <summary>Lists the challenges in the folders afresh: the game's own first, then the player's.</summary>
    public void Reset(IReadOnlyList<string> folders)
    {
        _listings = ChallengeShelf.List(folders, out var problem);
        if (problem is not null)
        {
            Logger.Warn("{Problem}", problem);
        }

        _rows.Clear();
        foreach (var listing in _listings)
        {
            var details = listing.Scenario is { } scenario
                ? $"{scenario.DateAt(0)} to {scenario.DateAt(scenario.Ticks)}   {Count(scenario.Goals.Count, "goal")}"
                : $"Can't be played: {listing.Problem}";
            _rows.Add((listing.Scenario?.Name ?? System.IO.Path.GetFileName(listing.Path), Shorten(details, Width - (Padding * 2) - 12), listing.Scenario is not null));
        }

        _first = 0;
        SetProblem(problem);
        City = null;
        Select(-1);
    }

    /// <summary>Takes this frame's keys, clicks, and scrolling.</summary>
    public Outcome Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Outcome.Back;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            return TryPlay();
        }

        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            _first = Math.Clamp(_first - Math.Sign(wheel), 0, Math.Max(0, _listings.Count - Rows));
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return Outcome.Open;
        }

        var mouse = Raylib.GetMousePosition();
        if (Raylib.CheckCollisionPointRec(mouse, ButtonBounds(1)))
        {
            return Outcome.Back;
        }

        if (Raylib.CheckCollisionPointRec(mouse, ButtonBounds(0)))
        {
            return TryPlay();
        }

        for (var row = 0; row < Rows && _first + row < _listings.Count; row++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, RowBounds(row)))
            {
                Select(_first + row);
            }
        }

        return Outcome.Open;
    }

    /// <summary>Draws the list in the middle of the screen, over a dimmed view.</summary>
    public void Draw()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds();
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        var x = (int)bounds.X + Padding;
        Raylib.DrawText("Scenarios", x, (int)bounds.Y + Padding, FontSize, Theme.Text);

        var mouse = Raylib.GetMousePosition();
        if (_listings.Count == 0)
        {
            Raylib.DrawText("No challenges were found.", x, (int)RowBounds(0).Y + 8, RowSize, Theme.TextDim);
        }

        for (var row = 0; row < Rows && _first + row < _listings.Count; row++)
        {
            var index = _first + row;
            var (name, details, playable) = _rows[index];
            var rowBounds = RowBounds(row);
            var hovered = Raylib.CheckCollisionPointRec(mouse, rowBounds);
            if (index == _selected || hovered)
            {
                Raylib.DrawRectangleRec(rowBounds, Theme.PanelBorder);
            }

            Raylib.DrawText(name, x + 6, (int)rowBounds.Y + 4, RowSize, index == _selected || hovered ? Theme.Accent : Theme.Text);
            Raylib.DrawText(details, x + 6, (int)rowBounds.Y + 4 + RowSize + 2, SmallSize, playable ? Theme.TextDim : Theme.Error);
        }

        if (_listings.Count > Rows)
        {
            Raylib.DrawText("Scroll for more", x, (int)bounds.Y + BriefingTop - 14, SmallSize, Theme.TextDim);
        }

        var y = (int)bounds.Y + BriefingTop;
        Raylib.DrawLine(x, y - 4, x + Width - (Padding * 2), y - 4, Theme.PanelBorder);
        var lines = _problemLines.Count > 0 ? _problemLines : _briefing;
        for (var i = 0; i < Math.Min(lines.Count, BriefingLines); i++)
        {
            Raylib.DrawText(lines[i], x, y + 2, SmallSize, _problemLines.Count > 0 ? Theme.Error : Theme.Text);
            y += BriefingLineHeight;
        }

        DrawButton(ButtonBounds(0), "Play", CanPlay, mouse);
        DrawButton(ButtonBounds(1), "Back", enabled: true, mouse);
    }

    private bool CanPlay => _selected >= 0 && _listings[_selected].Scenario is not null;

    private void SetProblem(string? problem)
    {
        _problem = problem;
        _problemLines.Clear();
        if (problem is not null)
        {
            _problemLines.AddRange(TextWrap.Lines(problem, SmallSize, Width - (Padding * 2)));
        }
    }

    // The briefing is laid out once, when a scenario is picked: its description, then its goals.
    private void Select(int index)
    {
        _selected = index;
        SetProblem(null);
        _briefing.Clear();
        if (index < 0)
        {
            _briefing.Add(_listings.Count > 0 ? "Pick a challenge to read what it asks of you." : string.Empty);
            return;
        }

        var listing = _listings[index];
        if (listing.Scenario is not { } scenario)
        {
            _briefing.AddRange(TextWrap.Lines($"This file can't be played: {listing.Problem}", SmallSize, Width - (Padding * 2)));
            return;
        }

        if (scenario.Description is { } description)
        {
            _briefing.AddRange(TextWrap.Lines(description, SmallSize, Width - (Padding * 2)));
        }

        _briefing.Add($"By {scenario.DateAt(scenario.Ticks)}:");
        _briefing.AddRange(scenario.Goals.Select(goal => $"   {goal.Text}"));
    }

    // Starting reads a saved city afresh, which may have gone since the list was made; the panel says so.
    private Outcome TryPlay()
    {
        if (!CanPlay)
        {
            return Outcome.Open;
        }

        var listing = _listings[_selected];
        try
        {
            City = ChallengeShelf.Begin(listing.Scenario!);
            Logger.Info("Playing the challenge '{Name}' from {File}", listing.Scenario!.Name, listing.Path);
            return Outcome.Play;
        }
        catch (InvalidDataException ex)
        {
            Logger.Warn("{File} couldn't be started: {Problem}", listing.Path, ex.Message);
            SetProblem(ex.Message);
            return Outcome.Open;
        }
    }

    private static void DrawButton(Rectangle bounds, string text, bool enabled, Vector2 mouse)
    {
        Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
        var width = Raylib.MeasureText(text, FontSize);
        var color = !enabled ? Theme.TextDim : Raylib.CheckCollisionPointRec(mouse, bounds) ? Theme.Accent : Theme.Text;
        Raylib.DrawText(text, (int)(bounds.X + ((bounds.Width - width) / 2)), (int)bounds.Y + 5, FontSize, color);
    }

    private static string Count(int count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";

    // A line too long for its row loses its end to an ellipsis.
    private static string Shorten(string text, int width)
    {
        if (Raylib.MeasureText(text, SmallSize) <= width)
        {
            return text;
        }

        var end = text.Length;
        while (end > 0 && Raylib.MeasureText(string.Concat(text.AsSpan(0, end), "..."), SmallSize) > width)
        {
            end--;
        }

        return string.Concat(text.AsSpan(0, end), "...");
    }

    private static Rectangle Bounds() => new()
    {
        X = (Raylib.GetScreenWidth() - Width) / 2,
        Y = (Raylib.GetScreenHeight() - Height) / 2,
        Width = Width,
        Height = Height,
    };

    private static Rectangle RowBounds(int row)
    {
        var bounds = Bounds();
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + ListTop + (row * RowHeight), Width = Width - (Padding * 2), Height = RowHeight - 2 };
    }

    // Play, then Back, along the bottom right.
    private static Rectangle ButtonBounds(int button)
    {
        var bounds = Bounds();
        var right = bounds.X + Width - Padding - ButtonWidth;
        return new Rectangle { X = right - ((1 - button) * (ButtonWidth + 8)), Y = bounds.Y + Height - Padding - ButtonHeight, Width = ButtonWidth, Height = ButtonHeight };
    }
}
