namespace CityLimits.UI;

/// <summary>
/// The city panel, opened by a click on the city's name in the bar. At the top, the name, ready
/// to edit: Enter renames the city, and closing drops an edit that wasn't entered. Under it, the
/// city at a glance: its date and age, population, demand, and money. A city played as a challenge
/// has a second column beside it, with the challenge's name, its end, how it stands, and the
/// disasters still to come, and its goals under both, each with how the city measures up; a
/// scenario being edited shows its draft there, which nothing acts on until it is tested, with an
/// Edit under its goals that opens the scenario's details. At the
/// bottom, the seed its map grew from, which a click copies. The game is paused while it is open,
/// and the player moves it by its title.
/// </summary>
public sealed class CityPanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int SmallSize = 10;
    private const int TitleSize = 20;
    private const int Padding = 14;

    // A free city's panel holds the city's column; a challenge's is wide enough for a second.
    private const int NarrowWidth = 360;
    private const int WideWidth = 580;
    private const int BaseHeight = 340;

    // A challenge's section: its heading, a line a goal, and a gap above the seed.
    private const int ChallengeHeading = 22;
    private const int ChallengeGap = 10;
    private const int LineHeight = 20;
    private const int CloseSize = 20;
    private const int TitleStrip = Padding + TitleSize + 6;
    private const int ColumnGap = 24;
    private const int LeftColumn = 330;

    // How many of the disasters to come the challenge's column lists before saying how many more.
    private const int EventRoom = 3;
    private const double NoteSeconds = 2.0;

    private readonly WindowPlacement _placement = new(TitleStrip);
    private readonly TextField _name = new(CitySimulation.DefaultName, CitySimulation.MaxNameLength, _ => true);
    private string? _problem;
    private double _renamedAt = double.NegativeInfinity;
    private double _copiedAt = double.NegativeInfinity;

    // How many goals the city's challenge has, which sets the panel's height; none for a free city.
    private int _goalRows;
    private bool _isChallenge;

    // The challenge the panel shows: the city's own, or, while its scenario is edited, the draft.
    private Challenge? _challenge;
    private bool _isDraft;

    // The disasters to come, laid out when the panel opens, since the city is paused while it is.
    private readonly List<string> _events = [];
    private int _moreEvents;

    private int Width => _isChallenge ? WideWidth : NarrowWidth;

    // The Edit button's row, under a draft's goals.
    private const int EditRow = 34;
    private const int EditWidth = 140;

    private int Height => BaseHeight + (_isChallenge ? ChallengeHeading + (_goalRows * LineHeight) + ChallengeGap : 0) + (_isDraft ? EditRow : 0);

    /// <summary>What a frame of the panel came to.</summary>
    public enum Outcome
    {
        /// <summary>The panel stays open.</summary>
        Open,

        /// <summary>The player entered a new name, in <see cref="Name"/>; the panel stays open.</summary>
        Renamed,

        /// <summary>The player closed the panel.</summary>
        Closed,

        /// <summary>The player asked to edit the details of the scenario being edited.</summary>
        EditScenario,
    }

    /// <summary>The name the player entered, once the outcome is <see cref="Outcome.Renamed"/>, trimmed.</summary>
    public string Name { get; private set; } = CitySimulation.DefaultName;

    /// <summary>Where the panel is: in the middle of the screen until the player moves it.</summary>
    public Rectangle Bounds => _placement.Bounds(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Raylib.GetScreenHeight() - Height) / 2));

    /// <inheritdoc />
    public bool IsDragging => _placement.IsDragging;

    /// <inheritdoc />
    public bool BeginDrag(Vector2 point) => _placement.BeginDrag(point, Bounds);

    /// <inheritdoc />
    public void Drag(Vector2 point, bool held) => _placement.Drag(point, held, new Vector2(Width, Height));

    /// <summary>
    /// Opens the panel on the city as it is, with its challenge, or, when it has none, the draft of
    /// the scenario being edited, if any, which nothing acts on yet.
    /// </summary>
    public void Reset(CitySimulation city, Challenge? draft = null)
    {
        ArgumentNullException.ThrowIfNull(city);
        _name.Text = city.Name;
        _challenge = city.Challenge ?? draft;
        _isDraft = city.Challenge is null && draft is not null;
        _isChallenge = _challenge is not null;
        _goalRows = _challenge?.Goals.Count ?? 0;
        _events.Clear();
        _moreEvents = 0;
        foreach (var (date, disaster) in _challenge?.UpcomingEvents ?? [])
        {
            if (_events.Count < EventRoom)
            {
                _events.Add($"{disaster}, {date}");
            }
            else
            {
                _moreEvents++;
            }
        }

        _problem = null;
        _renamedAt = double.NegativeInfinity;
        _copiedAt = double.NegativeInfinity;
    }

    /// <summary>Takes this frame's typing and clicks, other than a drag by the title.</summary>
    public Outcome Update(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);

        var mouse = Raylib.GetMousePosition();
        var pressed = Raylib.IsMouseButtonPressed(MouseButton.Left);
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || (pressed && Raylib.CheckCollisionPointRec(mouse, CloseBounds())))
        {
            return Outcome.Closed;
        }

        if (pressed && _isDraft && Raylib.CheckCollisionPointRec(mouse, EditBounds()))
        {
            return Outcome.EditScenario;
        }

        if (pressed && Raylib.CheckCollisionPointRec(mouse, SeedBounds()))
        {
            Raylib.SetClipboardText(city.Seed.ToString(CultureInfo.InvariantCulture));
            _copiedAt = Raylib.GetTime();
        }

        // A problem stays until the player changes the name it was about.
        if (_name.Update())
        {
            _problem = null;
        }

        if (!Raylib.IsKeyPressed(KeyboardKey.Enter) && !Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            return Outcome.Open;
        }

        if (!CitySimulation.IsValidName(_name.Text))
        {
            _problem = $"A city's name needs 1 to {CitySimulation.MaxNameLength} characters";
            return Outcome.Open;
        }

        Name = _name.Text.Trim();
        _renamedAt = Raylib.GetTime();
        return Outcome.Renamed;
    }

    /// <summary>Draws the panel over a dimmed city.</summary>
    public void Draw(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds;
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + Padding;
        Raylib.DrawText(city.Name, x, y, TitleSize, Theme.Text);
        var close = CloseBounds();
        Raylib.DrawText("x", (int)close.X + 6, (int)close.Y + 2, FontSize, Theme.TextDim);
        y = (int)bounds.Y + TitleStrip + 4;

        var field = new Rectangle { X = x, Y = y, Width = Width - (Padding * 2), Height = TextField.Height };
        _name.Draw(field, focused: true);
        y += TextField.Height + 4;
        var now = Raylib.GetTime();
        var (note, noteColor) = _problem is not null
            ? (_problem, Theme.Error)
            : now - _renamedAt < NoteSeconds ? ("Renamed", Theme.Accent) : ("Type a new name and press Enter to rename the city", Theme.TextDim);
        Raylib.DrawText(note, x, y, SmallSize, noteColor);
        y += SmallSize + 12;

        DrawCity(city, x, y);
        if (_challenge is { } challenge)
        {
            DrawChallenge(challenge, x + LeftColumn + ColumnGap, y);
            DrawGoals(city, challenge, x, (int)bounds.Y + BaseHeight - Padding - LineHeight - 8);
        }

        if (_isDraft)
        {
            var edit = EditBounds();
            Raylib.DrawRectangleRec(edit, Theme.PanelBorder);
            const string Label = "Edit scenario";
            var color = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), edit) ? Theme.Accent : Theme.Text;
            Raylib.DrawText(Label, (int)(edit.X + ((edit.Width - Raylib.MeasureText(Label, FontSize)) / 2)), (int)edit.Y + 7, FontSize, color);
        }

        var seed = SeedBounds();
        Raylib.DrawLine((int)bounds.X + Padding, (int)seed.Y - 6, (int)(bounds.X + Width) - Padding, (int)seed.Y - 6, Theme.PanelBorder);
        var seedText = $"Seed {city.Seed.ToString(CultureInfo.InvariantCulture)}";
        Raylib.DrawText(seedText, (int)seed.X, (int)seed.Y + 4, FontSize, Theme.Text);
        var copied = now - _copiedAt < NoteSeconds;
        Raylib.DrawText(copied ? "copied" : "click to copy", (int)seed.X + Raylib.MeasureText(seedText, FontSize) + 12, (int)seed.Y + 7, SmallSize, copied ? Theme.Accent : Theme.TextDim);
    }

    private static void DrawCity(CitySimulation city, int x, int y)
    {
        Raylib.DrawText("City", x, y, SmallSize, Theme.TextDim);
        y += SmallSize + 6;

        var date = city.Date;
        var age = date.Year - city.FoundedYear;
        var ageText = age switch
        {
            0 => "founded this year",
            1 => "1 year old",
            _ => $"{age} years old",
        };
        Raylib.DrawText($"{date.Month:00}/{date.Year:0000}, {ageText}", x, y, FontSize, Theme.Text);
        y += LineHeight;

        var census = city.Census;
        Raylib.DrawText($"Population {Hud.Count(census.CityPopulation)}", x, y, FontSize, Theme.Text);
        y += LineHeight - 4;
        Raylib.DrawText(
            $"Residents {Hud.Count(census.ResidentPopulation)}   Commerce {Hud.Count(census.CommercialPopulation)}   Industry {Hud.Count(census.IndustrialPopulation)}",
            x,
            y,
            SmallSize,
            Theme.TextDim);
        y += LineHeight;

        var demand = city.Demand;
        Raylib.DrawText("Demand", x, y, FontSize, Theme.Text);
        var levelX = x + Raylib.MeasureText("Demand", FontSize) + 10;
        levelX = DrawLevel("R", demand.Residential, Theme.Residential, levelX, y);
        levelX = DrawLevel("C", demand.Commercial, Theme.Commercial, levelX, y);
        DrawLevel("I", demand.Industrial, Theme.Industrial, levelX, y);
        y += LineHeight;

        var funds = city.Treasury.Funds;
        Raylib.DrawText($"Funds {Hud.Money(funds)}, tax {city.Budget.TaxRate}%", x, y, FontSize, funds < 0 ? Theme.Error : Theme.Text);
        y += LineHeight;

        var estimate = city.EstimateBudget();
        Raylib.DrawText($"This year, as it stands: {SignedMoney(estimate.CashFlow)}", x, y, FontSize, estimate.CashFlow < 0 ? Theme.Error : Theme.Text);
        y += LineHeight;

        var last = city.Budget.LastYear;
        var lastText = last is null ? "Last year: not settled yet" : $"Last year: {SignedMoney(last.CashFlow)}";
        Raylib.DrawText(lastText, x, y, FontSize, last is { CashFlow: < 0 } ? Theme.Error : Theme.Text);
    }

    // The challenge the city is being played as: its name, its end, how it stands, and the
    // disasters still to come, with their dates.
    private void DrawChallenge(Challenge challenge, int x, int y)
    {
        Raylib.DrawText(_isDraft ? "Scenario" : "Challenge", x, y, SmallSize, Theme.TextDim);
        y += SmallSize + 6;
        Raylib.DrawText(challenge.Name, x, y, FontSize, Theme.Text);
        y += LineHeight;
        Raylib.DrawText($"Ends {challenge.Ends}", x, y, FontSize, Theme.Text);
        y += LineHeight;
        var (state, stateColor) = challenge.Won switch
        {
            _ when _isDraft => ("Being edited", Theme.TextDim),
            true => ("Won", Theme.Accent),
            false => ("Lost", Theme.Error),
            null => ("Under way", Theme.TextDim),
        };
        Raylib.DrawText(state, x, y, FontSize, stateColor);
        y += LineHeight + 6;

        Raylib.DrawText("Disasters to come", x, y, SmallSize, Theme.TextDim);
        y += SmallSize + 6;
        if (_events.Count == 0)
        {
            Raylib.DrawText("None", x, y, FontSize, Theme.TextDim);
            return;
        }

        foreach (var line in _events)
        {
            Raylib.DrawText(line, x, y, FontSize, Theme.Text);
            y += LineHeight;
        }

        if (_moreEvents > 0)
        {
            Raylib.DrawText($"and {_moreEvents} more", x, y, FontSize, Theme.TextDim);
        }
    }

    // The challenge's goals across the panel, each with what the city measures now, met or not
    // yet, or, once judged, met or missed.
    private void DrawGoals(CitySimulation city, Challenge challenge, int x, int y)
    {
        var right = x + Width - (Padding * 2);
        Raylib.DrawLine(x, y - 4, right, y - 4, Theme.PanelBorder);
        Raylib.DrawText($"Goals by {challenge.Ends}", x, y + 2, FontSize, Theme.Text);
        y += ChallengeHeading;

        foreach (var goal in challenge.Goals)
        {
            var found = ScenarioRunner.Measure(city, goal.Measure);
            var met = ScenarioRunner.Judge(city, goal).Met;
            var shown = goal.Measure == GoalMeasure.Funds ? Hud.Money(found) : Hud.Count(found);
            var verdict = challenge.Won is null ? (met ? "met" : "not yet") : (met ? "met" : "missed");
            var status = $"{shown}, {verdict}";
            Raylib.DrawText(goal.Text, x + 8, y + 2, SmallSize + 2, Theme.Text);
            Raylib.DrawText(status, right - Raylib.MeasureText(status, SmallSize + 2), y + 2, SmallSize + 2, met ? Theme.Accent : Theme.TextDim);
            y += LineHeight;
        }
    }

    // A demand's letter and level in its colour. Returns where the next one starts.
    private static int DrawLevel(string letter, int value, Color color, int x, int y)
    {
        var text = $"{letter} {(value > 0 ? "+" : string.Empty)}{Hud.Count(value)}";
        Raylib.DrawText(text, x, y, FontSize, color);
        return x + Raylib.MeasureText(text, FontSize) + 12;
    }

    private static string SignedMoney(long amount) => amount > 0 ? $"+{Hud.Money(amount)}" : Hud.Money(amount);

    private Rectangle CloseBounds()
    {
        var bounds = Bounds;
        return new Rectangle { X = bounds.X + Width - CloseSize - 6, Y = bounds.Y + 6, Width = CloseSize, Height = CloseSize };
    }

    // Under the goals, at the right, just above the seed.
    private Rectangle EditBounds()
    {
        var seed = SeedBounds();
        return new Rectangle { X = Bounds.X + Width - Padding - EditWidth, Y = seed.Y - 10 - EditRow + 4, Width = EditWidth, Height = EditRow - 8 };
    }

    private Rectangle SeedBounds()
    {
        var bounds = Bounds;
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + Height - Padding - LineHeight - 2, Width = Width - (Padding * 2), Height = LineHeight + 4 };
    }
}
