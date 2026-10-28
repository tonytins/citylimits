namespace CityLimits.UI;

/// <summary>
/// The city panel, opened by a click on the city's name in the bar. Its title is the city's name,
/// which the player edits in place, the only window that turns that on: a click on it starts an
/// edit, Enter renames the city, and Escape or a click elsewhere drops the edit. Under it, the
/// city at a glance: its date and age, population, demand, and money. A city played as a challenge
/// has a second column beside it, with the challenge's name, its end, how it stands, and the
/// disasters still to come, and its goals under both, each with how the city measures up; a
/// scenario being edited shows its draft there, which nothing acts on until it is tested, with an
/// Edit under its goals that opens the scenario's details. At the
/// bottom, the seed its map grew from, which a click copies. Its toolbar's second page, Ordinances,
/// lists the city's ordinances, after SimCity 3000's, each a button that enacts or repeals it, with
/// what it earns or costs a year for every 1,000 people and, under the pointer, what it does. Its
/// third, Neighbours, has the cities beyond the map's edges, what connects to each, and the deal
/// made with each, which can be broken there; and the offer standing, which a click opens. The
/// game is paused while it is open, and the player moves it by its title bar.
/// </summary>
public sealed class CityPanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int SmallSize = 10;
    private const int Padding = 14;

    // A free city's panel holds the city's column; a challenge's is wide enough for a second.
    private const int NarrowWidth = 420;
    private const int WideWidth = 580;
    private const int BaseHeight = 290;

    // A challenge's section: its heading, a line a goal, and a gap above the seed.
    private const int ChallengeHeading = 22;
    private const int ChallengeGap = 10;
    private const int LineHeight = 20;
    private const int ColumnGap = 24;
    private const int LeftColumn = 330;

    // How many of the disasters to come the challenge's column lists before saying how many more.
    private const int EventRoom = 3;
    private const double NoteSeconds = 2.0;

    // The ordinances' page: a row each, and the line under them.
    private const int OrdinanceRow = 21;
    private const int CityPage = 0;
    private const int OrdinancesPage = 1;
    private const int NeighboursPage = 2;

    // The neighbours' page: two lines for each, and a button to break a deal.
    private const int NeighbourRow = 50;
    private const int BreakWidth = 60;

    private static readonly string[] Pages = ["City", "Ordinances", "Neighbours"];
    private static readonly CityNeighbours.Side[] Sides = Enum.GetValues<CityNeighbours.Side>();

    private readonly WindowFrame _frame = new(CitySimulation.DefaultName, CitySimulation.MaxNameLength)
    {
        Toolbar = Pages,
        SelectedTool = CityPage,
        TitleEditable = true,
        CheckTitle = name => CitySimulation.IsValidName(name) ? null : $"A city's name needs 1 to {CitySimulation.MaxNameLength} characters",
    };

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

    // The ordinances as the page shows them, laid out when the panel opens or one changes.
    private string[] _ordinanceIds = [];
    private string[] _ordinanceNames = [];
    private string[] _ordinanceMoney = [];
    private string[] _ordinanceEffects = [];
    private bool[] _enacted = [];
    private string _ordinanceTotal = string.Empty;

    // The neighbours as the page shows them, laid out when the panel opens or a deal changes.
    private readonly string[] _neighbourNames = new string[4];
    private readonly string[] _neighbourLinks = new string[4];
    private readonly bool[] _hasDeal = new bool[4];
    private string _offerLine = string.Empty;
    private bool _hasOffer;

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

        /// <summary>The player clicked an ordinance, in <see cref="Ordinance"/>, to enact or repeal it.</summary>
        ToggleOrdinance,

        /// <summary>The player asked to see the offer a neighbour has made.</summary>
        ShowOffer,

        /// <summary>The player asked to break the deal with the neighbour in <see cref="DealSide"/>.</summary>
        BreakDeal,
    }

    /// <summary>The neighbour whose deal the player asked to break, once the outcome is <see cref="Outcome.BreakDeal"/>.</summary>
    public CityNeighbours.Side DealSide { get; private set; }

    /// <summary>The ordinance the player clicked, once the outcome is <see cref="Outcome.ToggleOrdinance"/>.</summary>
    public string? Ordinance { get; private set; }

    /// <summary>The name the player entered, once the outcome is <see cref="Outcome.Renamed"/>, trimmed.</summary>
    public string Name { get; private set; } = CitySimulation.DefaultName;

    /// <summary>Where the panel is: in the middle of the screen until the player moves it.</summary>
    public Rectangle Bounds => _frame.Place(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Raylib.GetScreenHeight() - Height) / 2));

    /// <inheritdoc />
    public bool IsDragging => _frame.IsDragging;

    /// <inheritdoc />
    public bool BeginDrag(Vector2 point) => _frame.BeginDrag(point, Bounds);

    /// <inheritdoc />
    public void Drag(Vector2 point, bool held) => _frame.Drag(point, held, new Vector2(Width, Height));

    /// <summary>
    /// Opens the panel on the city as it is, with its challenge, or, when it has none, the draft of
    /// the scenario being edited, if any, which nothing acts on yet; on its Ordinances page when
    /// <paramref name="ordinances"/> asks, as the ticker's ordinance news does.
    /// </summary>
    public void Reset(CitySimulation city, Challenge? draft = null, bool ordinances = false, bool neighbours = false)
    {
        ArgumentNullException.ThrowIfNull(city);
        _frame.CancelEdit();
        _frame.Title = city.Name;
        _frame.SelectedTool = neighbours ? NeighboursPage : ordinances ? OrdinancesPage : CityPage;
        RefreshOrdinances(city);
        RefreshNeighbours(city);
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

        _renamedAt = double.NegativeInfinity;
        _copiedAt = double.NegativeInfinity;
    }

    /// <summary>Lays the neighbours' page out again, as the city stands with them now; call it when a deal changes.</summary>
    public void RefreshNeighbours(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        var neighbours = city.Neighbours;
        var population = city.Census.CityPopulation;
        foreach (var side in Sides)
        {
            var i = (int)side;
            var neighbour = neighbours.Of(side);
            _neighbourNames[i] = $"{neighbour.Name}, to the {CityNeighbours.SideName(side)}: {Hud.Count(neighbour.PopulationBeside(population))} people";
            var connections = neighbours.ConnectionsTo(side);
            var links = connections == Connections.None
                ? "Not connected"
                : string.Join(", ", new[] { (Connections.Road, "road"), (Connections.Rail, "rail"), (Connections.Power, "power") }
                    .Where(link => connections.HasFlag(link.Item1)).Select(link => link.Item2));
            links = char.ToUpperInvariant(links[0]) + links[1..];
            var deal = neighbours.DealWith(side);
            _hasDeal[i] = deal is not null;
            _neighbourLinks[i] = deal is null
                ? links
                : $"{links}. {(deal.Kind == DealKind.CityBuys ? "Sells you" : "Buys")} {Hud.Count(deal.Power)} power for {Hud.Money(deal.Money)} a year to {CityCalendar.DateAt(deal.EndStep / CityCalendar.StepsPerCount)}";
        }

        _hasOffer = neighbours.Offer is not null;
        _offerLine = neighbours.Offer is { } offer
            ? $"{neighbours.Of(offer.Side).Name} offers a deal until {CityCalendar.DateAt(offer.ExpiresStep / CityCalendar.StepsPerCount)}: click here to answer"
            : "Build a road, rail, or power line off an edge to connect";
    }

    /// <summary>Lays the ordinances' page out again, as the city has them now; call it when one changes.</summary>
    public void RefreshOrdinances(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        var all = city.Ordinances.All;
        var population = city.Census.CityPopulation;
        _ordinanceIds = [.. all.Select(ordinance => ordinance.Id)];
        _ordinanceNames = [.. all.Select(ordinance => ordinance.Name)];
        // A row gives the rate, which a new city's size would make $0; the line under them, what it comes to now.
        _ordinanceMoney = [.. all.Select(ordinance => $"{SignedMoney(ordinance.Money)} per 1,000")];
        _ordinanceEffects = [.. all.Select(EffectsOf)];
        _enacted = [.. all.Select(ordinance => city.Ordinances.IsEnacted(ordinance.Id))];
        var (income, cost) = city.Ordinances.YearlyMoney(population);
        _ordinanceTotal = _enacted.Any(on => on)
            ? $"Those enacted earn {Hud.Money(income)} and cost {Hud.Money(cost)} a year at {Hud.Count(population)} people"
            : "Each earns or costs that a year for every 1,000 people. Click one to enact it";
    }

    /// <summary>Takes this frame's typing and clicks, other than a drag by the title bar.</summary>
    public Outcome Update(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);

        var mouse = Raylib.GetMousePosition();
        var pressed = Raylib.IsMouseButtonPressed(MouseButton.Left);

        // While the name is being edited, the keyboard is the title's: Escape drops the edit, not the panel.
        var editing = _frame.IsEditingTitle;
        switch (_frame.Update(Bounds))
        {
            case WindowFrame.Action.Close:
                return Outcome.Closed;
            case WindowFrame.Action.Toolbar:
                _frame.SelectedTool = _frame.ClickedTool;
                return Outcome.Open;
            case WindowFrame.Action.Renamed:
                Name = _frame.Title;
                _renamedAt = Raylib.GetTime();
                return Outcome.Renamed;
        }

        if (editing || _frame.IsEditingTitle)
        {
            return Outcome.Open;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Outcome.Closed;
        }

        if (_frame.SelectedTool == NeighboursPage)
        {
            if (!pressed)
            {
                return Outcome.Open;
            }

            if (_hasOffer && Raylib.CheckCollisionPointRec(mouse, OfferBounds()))
            {
                return Outcome.ShowOffer;
            }

            foreach (var side in Sides)
            {
                if (_hasDeal[(int)side] && Raylib.CheckCollisionPointRec(mouse, BreakBounds((int)side)))
                {
                    DealSide = side;
                    return Outcome.BreakDeal;
                }
            }

            return Outcome.Open;
        }

        if (_frame.SelectedTool == OrdinancesPage)
        {
            var row = pressed ? OrdinanceAt(mouse) : -1;
            if (row < 0)
            {
                return Outcome.Open;
            }

            Ordinance = _ordinanceIds[row];
            return Outcome.ToggleOrdinance;
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

        return Outcome.Open;
    }

    /// <summary>Draws the panel over a dimmed city.</summary>
    public void Draw(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds;
        _frame.Draw(bounds);
        if (_frame.SelectedTool == OrdinancesPage)
        {
            DrawOrdinances(bounds);
            return;
        }

        if (_frame.SelectedTool == NeighboursPage)
        {
            DrawNeighbours(bounds);
            return;
        }

        // The frame shows a problem with the name under the title bar while the name is edited.
        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + WindowFrame.TitleHeight + 6;
        var now = Raylib.GetTime();
        if (_frame.TitleProblem is null)
        {
            var (note, noteColor) = now - _renamedAt < NoteSeconds
                ? ("Renamed", Theme.Accent)
                : _frame.IsEditingTitle ? ("Press Enter to rename the city, or Escape to leave it", Theme.TextDim) : ("Click the name above to rename the city", Theme.TextDim);
            Raylib.DrawText(note, x, y, SmallSize, noteColor);
        }

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

    // Each ordinance a row: lit when enacted, with what it earns or costs; under them, what the one
    // under the pointer does, or what those enacted come to.
    private void DrawOrdinances(Rectangle bounds)
    {
        var mouse = Raylib.GetMousePosition();
        var hovered = OrdinanceAt(mouse);
        for (var i = 0; i < _ordinanceIds.Length; i++)
        {
            var row = OrdinanceBounds(i);
            if (_enacted[i])
            {
                Raylib.DrawRectangleRec(row, Theme.ToolbarSelected);
            }

            Raylib.DrawRectangleLinesEx(row, 1f, _enacted[i] ? Theme.Accent : i == hovered ? Theme.TextDim : Theme.PanelBorder);
            var textY = (int)(row.Y + ((row.Height - FontSize) / 2));
            Raylib.DrawText(_ordinanceNames[i], (int)row.X + 8, textY, FontSize, _enacted[i] ? Theme.Accent : Theme.Text);
            var money = _ordinanceMoney[i];
            Raylib.DrawText(money, (int)(row.X + row.Width - 8 - Raylib.MeasureText(money, SmallSize + 2)), textY + 2, SmallSize + 2, Theme.TextDim);
        }

        var note = hovered >= 0 ? _ordinanceEffects[hovered] : _ordinanceTotal;
        Raylib.DrawText(note, (int)bounds.X + Padding, (int)(bounds.Y + bounds.Height - Padding - SmallSize), SmallSize, hovered >= 0 ? Theme.Text : Theme.TextDim);
    }

    // Each neighbour two lines, its deal with a button to break it; under them, the offer standing.
    private void DrawNeighbours(Rectangle bounds)
    {
        var mouse = Raylib.GetMousePosition();
        var x = (int)bounds.X + Padding;
        foreach (var side in Sides)
        {
            var i = (int)side;
            var top = NeighbourTop(i);
            Raylib.DrawText(_neighbourNames[i], x, top, FontSize, Theme.Text);
            Raylib.DrawText(_neighbourLinks[i], x, top + FontSize + 6, SmallSize + 2, _hasDeal[i] ? Theme.Accent : Theme.TextDim);
            if (_hasDeal[i])
            {
                var button = BreakBounds(i);
                Raylib.DrawRectangleRec(button, Theme.PanelBorder);
                const string Label = "Break";
                var color = Raylib.CheckCollisionPointRec(mouse, button) ? Theme.Error : Theme.Text;
                Raylib.DrawText(Label, (int)(button.X + ((button.Width - Raylib.MeasureText(Label, FontSize)) / 2)), (int)button.Y + 3, FontSize, color);
            }

            Raylib.DrawLine(x, top + NeighbourRow - 8, (int)(bounds.X + bounds.Width) - Padding, top + NeighbourRow - 8, Theme.PanelBorder);
        }

        var offer = OfferBounds();
        var offerColor = !_hasOffer ? Theme.TextDim : Raylib.CheckCollisionPointRec(mouse, offer) ? Theme.Accent : Theme.NewsOrdinance;
        Raylib.DrawText(_offerLine, (int)offer.X, (int)offer.Y + 2, SmallSize + 2, offerColor);
    }

    private int NeighbourTop(int row) => (int)Bounds.Y + WindowFrame.TitleHeight + 10 + (row * NeighbourRow);

    private Rectangle BreakBounds(int row) => new()
    {
        X = Bounds.X + Width - Padding - BreakWidth,
        Y = NeighbourTop(row) - 2,
        Width = BreakWidth,
        Height = 20,
    };

    private Rectangle OfferBounds()
    {
        var bounds = Bounds;
        return new Rectangle { X = bounds.X + Padding, Y = bounds.Y + bounds.Height - Padding - SmallSize - 6, Width = Width - (Padding * 2), Height = SmallSize + 6 };
    }

    private int OrdinanceAt(Vector2 point)
    {
        for (var i = 0; i < _ordinanceIds.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, OrdinanceBounds(i)))
            {
                return i;
            }
        }

        return -1;
    }

    private Rectangle OrdinanceBounds(int row)
    {
        var bounds = Bounds;
        return new Rectangle
        {
            X = bounds.X + Padding,
            Y = bounds.Y + WindowFrame.TitleHeight + 8 + (row * OrdinanceRow),
            Width = Width - (Padding * 2),
            Height = OrdinanceRow - 3,
        };
    }

    // What an ordinance does, in words, as the tuning guide words it.
    private static string EffectsOf(OrdinanceSpec ordinance) =>
        ordinance.Effects.Count == 0
            ? $"{ordinance.Name}: nothing but its money"
            : $"{ordinance.Name}: {string.Join(", ", ordinance.Effects.Select(effect => effect.Effect switch
            {
                OrdinanceEffect.ResidentialDemand => $"residential demand {Signed(effect.Amount)}",
                OrdinanceEffect.CommercialDemand => $"commercial demand {Signed(effect.Amount)}",
                OrdinanceEffect.IndustrialDemand => $"industrial demand {Signed(effect.Amount)}",
                OrdinanceEffect.Crime => $"crime {Signed(effect.Amount)}",
                OrdinanceEffect.IndustrialPollution => $"industry's pollution {Signed(effect.Amount)}%",
                OrdinanceEffect.Fires => $"fires breaking out {Signed(effect.Amount)}%",
                OrdinanceEffect.PowerCapacity => $"power plants' capacity {Signed(effect.Amount)}%",
                _ => $"traffic {Signed(effect.Amount)}%",
            }))}";

    private static string Signed(int amount) => amount > 0 ? $"+{amount}" : amount.ToString(CultureInfo.InvariantCulture);

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
