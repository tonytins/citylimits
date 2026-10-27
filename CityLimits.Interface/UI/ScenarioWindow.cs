using System.Globalization;
using System.Text.RegularExpressions;

namespace CityLimits.UI;

/// <summary>
/// A scenario's details, opened from the city panel's Edit while the scenario is edited, in three
/// columns: its name, start, end, money, and description; its goals; and its disasters, each of
/// which may repeat. A goal or a disaster is added with its column's Add and taken out with its x;
/// a click on a goal's measure or a disaster's kind moves it to the next one. Nothing changes until
/// Apply, or Enter, and Cancel, or Escape, drops every edit. What can't be read, or what the
/// scenario can't hold, is said under the columns, and the window stays open. A scenario whose saved
/// city has run since its founding takes its start and its money from the city, so those only show.
/// Tab moves to the next field. The player moves the window by its title.
/// </summary>
public sealed partial class ScenarioWindow : IMovableWindow
{
    private const int TitleSize = 20;
    private const int FieldSize = 14;
    private const int LabelSize = 10;
    private const int Padding = 14;
    private const int TitleStrip = Padding + TitleSize + 6;
    private const int FirstColumn = 260;
    private const int SecondColumn = 300;
    private const int ThirdColumn = 300;
    private const int ColumnGap = 20;
    private const int Width = Padding + FirstColumn + ColumnGap + SecondColumn + ColumnGap + ThirdColumn + Padding;
    private const int FieldHeight = FieldSize + 12;
    private const int LabelGap = 4;
    private const int Row = LabelSize + LabelGap + FieldHeight + 8;
    private const int BlockHeight = (FieldHeight * 2) + 4 + 10;
    private const int HeaderHeight = 26;
    private const int ContentHeight = HeaderHeight + (MostEntries * BlockHeight);
    private const int ProblemHeight = 32;
    private const int Height = TitleStrip + ContentHeight + ProblemHeight + ButtonRow.RowHeight + Padding;
    private const int SmallButton = 20;

    /// <summary>The most goals, and the most disasters, the window holds.</summary>
    public const int MostEntries = 5;

    // What a full column's Add button says, in step with MostEntries.
    private const string FullLabel = "5 at most";

    private static readonly string[] Buttons = ["Cancel", "Apply"];
    private static readonly GoalMeasure[] Measures = Enum.GetValues<GoalMeasure>();
    private static readonly Disaster[] Disasters = Enum.GetValues<Disaster>();

    private readonly WindowPlacement _placement = new(TitleStrip);
    private readonly TextField _name = new(string.Empty, 80, _ => true, FieldSize);
    private readonly TextField _start = new(string.Empty, 4, char.IsAsciiDigit, FieldSize);
    private readonly TextField _ends = new(string.Empty, 7, c => char.IsAsciiDigit(c) || c == '-', FieldSize);
    private readonly TextField _funds = new(string.Empty, 7, char.IsAsciiDigit, FieldSize);
    private readonly TextArea _description = new(string.Empty, 500, FieldSize - 2, FirstColumn);
    private readonly List<GoalRow> _goals = [];
    private readonly List<EventRow> _events = [];

    // Every field in Tab's order, laid out again only when a goal or a disaster is added or taken out.
    private readonly List<object> _order = [];
    private object? _focus;
    private bool _startFromCity;
    private string? _cityStart;
    private string? _problem;

    /// <summary>What a frame of the window came to.</summary>
    public enum Outcome
    {
        /// <summary>The window stays open.</summary>
        Open,

        /// <summary>The player applied the details in <see cref="Details"/>; the window stays open until its owner closes it.</summary>
        Apply,

        /// <summary>The player dropped the edits.</summary>
        Cancel,
    }

    /// <summary>The details as the player set them, once the outcome is <see cref="Outcome.Apply"/>.</summary>
    public ScenarioDetails? Details { get; private set; }

    /// <summary>Where the window is: in the middle of the screen until the player moves it.</summary>
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
    /// Opens the window on a scenario's details as they stand; its start and money can be changed
    /// unless <paramref name="canMoveStart"/> says its city keeps its own.
    /// </summary>
    public void Open(Scenario scenario, bool canMoveStart)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var details = ScenarioDetails.Of(scenario);
        _startFromCity = !canMoveStart;
        _cityStart = _startFromCity ? $"{scenario.DateAt(0)}, set by its city" : null;
        _name.Text = details.Name;
        _start.Text = details.StartYear.ToString(CultureInfo.InvariantCulture);
        _ends.Text = $"{details.Ends.Year:0000}-{details.Ends.Month:00}";
        _funds.Text = details.Funds.ToString(CultureInfo.InvariantCulture);
        _description.Text = details.Description ?? string.Empty;
        _goals.Clear();
        _goals.AddRange(details.Goals.Take(MostEntries).Select(GoalRow.Of));
        _events.Clear();
        _events.AddRange(details.Events.Take(MostEntries).Select(EventRow.Of));
        _problem = details.Goals.Count > MostEntries || details.Events.Count > MostEntries
            ? $"Only the first {MostEntries} goals and disasters can be edited here; Apply would drop the rest."
            : null;
        Details = null;
        Order();
        _focus = _name;
    }

    /// <summary>Says what is wrong with the details applied, under the columns, and keeps the window open.</summary>
    public void ShowProblem(string problem) => _problem = Readable(problem);

    /// <summary>Takes this frame's keys, typing, and clicks, other than a drag by the title.</summary>
    public Outcome Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Outcome.Cancel;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            return TryApply();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab) && _order.Count > 0)
        {
            var at = _focus is null ? -1 : _order.IndexOf(_focus);
            _focus = _order[(at + 1) % _order.Count];
        }

        switch (_focus)
        {
            case TextField field:
                field.Update();
                break;
            case TextArea area:
                area.Update();
                break;
        }

        return Raylib.IsMouseButtonPressed(MouseButton.Left) ? Click(Raylib.GetMousePosition()) : Outcome.Open;
    }

    /// <summary>Draws the window over a dimmed view.</summary>
    public void Draw()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds;
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);
        Raylib.DrawText("Scenario", (int)bounds.X + Padding, (int)bounds.Y + Padding, TitleSize, Theme.Text);
        var mouse = Raylib.GetMousePosition();

        // The first column: the scenario as a whole.
        DrawLabelled("Name", _name, FirstField(0));
        if (_startFromCity)
        {
            var start = FirstField(1);
            Raylib.DrawText("Starts", (int)start.X, (int)start.Y - LabelSize - LabelGap, LabelSize, Theme.TextDim);
            Raylib.DrawText(_cityStart!, (int)start.X, (int)start.Y + 6, FieldSize, Theme.TextDim);
        }
        else
        {
            DrawLabelled("Starts in the year", _start, FirstField(1));
        }

        DrawLabelled("Ends, judged in the month", _ends, FirstField(2));
        if (_startFromCity)
        {
            var funds = FirstField(3);
            Raylib.DrawText("Funds", (int)funds.X, (int)funds.Y - LabelSize - LabelGap, LabelSize, Theme.TextDim);
            Raylib.DrawText("Set by its city", (int)funds.X, (int)funds.Y + 6, FieldSize, Theme.TextDim);
        }
        else
        {
            DrawLabelled("Funds to start with", _funds, FirstField(3));
        }

        var description = DescriptionBounds();
        Raylib.DrawText("Description, the briefing", (int)description.X, (int)description.Y - LabelSize - LabelGap, LabelSize, Theme.TextDim);
        _description.Draw(description, _focus == _description);

        // The goals.
        DrawHeader("Goals", SecondLeft(), _goals.Count < MostEntries, mouse);
        for (var i = 0; i < _goals.Count; i++)
        {
            var goal = _goals[i];
            var (measure, least, most, remove, text) = GoalBounds(i);
            DrawChoice(measure, goal.Measure.ToString(), mouse);
            DrawSmallLabel("at least", least);
            goal.Least.Draw(least, _focus == goal.Least);
            DrawSmallLabel("at most", most);
            goal.Most.Draw(most, _focus == goal.Most);
            DrawRemove(remove, mouse);
            goal.Text.Draw(text, _focus == goal.Text);
            if (goal.Text.Length == 0 && _focus != goal.Text)
            {
                Raylib.DrawText("Its words: blank to word it from the measure", (int)text.X + 6, (int)text.Y + 7, LabelSize, Theme.TextDim);
            }
        }

        // The disasters.
        DrawHeader("Disasters", ThirdLeft(), _events.Count < MostEntries, mouse);
        for (var i = 0; i < _events.Count; i++)
        {
            var happening = _events[i];
            var (kind, at, week, remove, every, times) = EventBounds(i);
            DrawChoice(kind, happening.Disaster.ToString(), mouse);
            DrawSmallLabel("in", at);
            happening.At.Draw(at, _focus == happening.At);
            DrawSmallLabel("week", week);
            happening.Week.Draw(week, _focus == happening.Week);
            DrawRemove(remove, mouse);
            DrawSmallLabel("repeat every", every);
            happening.Every.Draw(every, _focus == happening.Every);
            DrawSmallLabel("weeks,", times);
            happening.Times.Draw(times, _focus == happening.Times);
            Raylib.DrawText("times", (int)(times.X + times.Width + 6), (int)times.Y + 8, LabelSize, Theme.TextDim);
        }

        var bottom = (int)bounds.Y + TitleStrip + ContentHeight;
        Raylib.DrawLine((int)bounds.X + Padding, bottom + 4, (int)(bounds.X + Width) - Padding, bottom + 4, Theme.PanelBorder);
        Raylib.DrawText(
            _problem ?? "Blank repeat fields strike once; a blank week is the month's first. Nothing changes until Apply.",
            (int)bounds.X + Padding,
            bottom + 12,
            LabelSize,
            _problem is null ? Theme.TextDim : Theme.Error);
        ButtonRow.Draw(Corner(), Buttons, mouse);
    }

    private Outcome Click(Vector2 mouse)
    {
        switch (ButtonRow.ButtonAt(mouse, Corner(), Buttons))
        {
            case 0:
                return Outcome.Cancel;
            case 1:
                return TryApply();
        }

        if (Raylib.CheckCollisionPointRec(mouse, AddBounds(SecondLeft())) && _goals.Count < MostEntries)
        {
            _goals.Add(GoalRow.Of(new GoalDetails(GoalMeasure.Population, 1000, null, null)));
            Order();
            return Outcome.Open;
        }

        if (Raylib.CheckCollisionPointRec(mouse, AddBounds(ThirdLeft())) && _events.Count < MostEntries)
        {
            var ends = ScenarioDate.TryParse(_ends.Text, 1, out var date) ? date.Value : new ScenarioDate(CityCalendar.StartYear, 1);
            _events.Add(EventRow.Of(new EventDetails(ends, Disaster.Fire, null)));
            Order();
            return Outcome.Open;
        }

        for (var i = 0; i < _goals.Count; i++)
        {
            var (measure, _, _, remove, _) = GoalBounds(i);
            if (Raylib.CheckCollisionPointRec(mouse, measure))
            {
                _goals[i].Measure = Measures[(Array.IndexOf(Measures, _goals[i].Measure) + 1) % Measures.Length];
                return Outcome.Open;
            }

            if (Raylib.CheckCollisionPointRec(mouse, remove))
            {
                _goals.RemoveAt(i);
                Order();
                return Outcome.Open;
            }
        }

        for (var i = 0; i < _events.Count; i++)
        {
            var (kind, _, _, remove, _, _) = EventBounds(i);
            if (Raylib.CheckCollisionPointRec(mouse, kind))
            {
                _events[i].Disaster = Disasters[(Array.IndexOf(Disasters, _events[i].Disaster) + 1) % Disasters.Length];
                return Outcome.Open;
            }

            if (Raylib.CheckCollisionPointRec(mouse, remove))
            {
                _events.RemoveAt(i);
                Order();
                return Outcome.Open;
            }
        }

        // A click on a field gives it the focus.
        foreach (var field in _order)
        {
            if (Raylib.CheckCollisionPointRec(mouse, BoundsOf(field)))
            {
                _focus = field;
                break;
            }
        }

        return Outcome.Open;
    }

    // What the fields say, read as details; a field that can't be read is said, and nothing is applied.
    private Outcome TryApply()
    {
        _problem = null;
        if (!int.TryParse(_start.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var start) && !_startFromCity)
        {
            _problem = "Starts: needs a year, such as 1900.";
            return Outcome.Open;
        }

        if (!ScenarioDate.TryParse(_ends.Text, 1, out var ends))
        {
            _problem = "Ends: needs a month, written as 1910-01.";
            return Outcome.Open;
        }

        if (!int.TryParse(_funds.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var funds) && !_startFromCity)
        {
            _problem = "Funds: needs a sum of money, such as 20000.";
            return Outcome.Open;
        }

        var goals = new List<GoalDetails>(_goals.Count);
        for (var i = 0; i < _goals.Count; i++)
        {
            var goal = _goals[i];
            if (!TryNumber(goal.Least.Text, out var least) || !TryNumber(goal.Most.Text, out var most))
            {
                _problem = $"Goal {i + 1}: at least and at most need whole numbers, or nothing.";
                return Outcome.Open;
            }

            goals.Add(new GoalDetails(goal.Measure, least, most, goal.Text.Text.Trim() is { Length: > 0 } words ? words : null));
        }

        var events = new List<EventDetails>(_events.Count);
        for (var i = 0; i < _events.Count; i++)
        {
            var happening = _events[i];
            var week = 1;
            if (happening.Week.Text.Length > 0 && !int.TryParse(happening.Week.Text, NumberStyles.None, CultureInfo.InvariantCulture, out week))
            {
                week = 0;
            }

            if (!ScenarioDate.TryParse(happening.At.Text, week, out var at))
            {
                _problem = $"Disaster {i + 1}: needs a month, written as 1906-07, and a week from 1 to 4, or nothing for the first.";
                return Outcome.Open;
            }

            var (every, times) = (happening.Every.Text, happening.Times.Text);
            EventRepeat? repeat = null;
            if (every.Length > 0 || times.Length > 0)
            {
                if (!int.TryParse(every, NumberStyles.None, CultureInfo.InvariantCulture, out var weeks) ||
                    !int.TryParse(times, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                {
                    _problem = $"Disaster {i + 1}: a repeat needs both its weeks and its times, or neither, to strike once.";
                    return Outcome.Open;
                }

                repeat = new EventRepeat(weeks, count);
            }

            events.Add(new EventDetails(at.Value, happening.Disaster, repeat));
        }

        Details = new ScenarioDetails(_name.Text.Trim(), _description.Text.Trim(), start, ends.Value, funds, goals, events);
        return Outcome.Apply;
    }

    private static bool TryNumber(string text, out int? number)
    {
        number = null;
        if (text.Length == 0)
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        number = value;
        return true;
    }

    // The loader names the file's fields; the window names what the player sees.
    // as "Disaster 2, repeat: ..." for the file's "events[1].repeat: ...".
    private static string Readable(string problem) =>
        EntryPattern().Replace(problem, m =>
            $"{(m.Groups[1].Value == "goals" ? "Goal" : "Disaster")} {int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + 1}{(m.Groups[3].Success ? ", " : string.Empty)}");

    [GeneratedRegex(@"(goals|events)\[(\d+)\](\.)?")]
    private static partial Regex EntryPattern();

    private void Order()
    {
        _order.Clear();
        _order.Add(_name);
        if (!_startFromCity)
        {
            _order.Add(_start);
        }

        _order.Add(_ends);
        if (!_startFromCity)
        {
            _order.Add(_funds);
        }

        _order.Add(_description);
        foreach (var goal in _goals)
        {
            _order.AddRange([goal.Least, goal.Most, goal.Text]);
        }

        foreach (var happening in _events)
        {
            _order.AddRange([happening.At, happening.Week, happening.Every, happening.Times]);
        }

        if (_focus is not null && !_order.Contains(_focus))
        {
            _focus = null;
        }
    }

    private Rectangle BoundsOf(object field)
    {
        if (field == _name)
        {
            return FirstField(0);
        }

        if (field == _start)
        {
            return FirstField(1);
        }

        if (field == _ends)
        {
            return FirstField(2);
        }

        if (field == _funds)
        {
            return FirstField(3);
        }

        if (field == _description)
        {
            return DescriptionBounds();
        }

        for (var i = 0; i < _goals.Count; i++)
        {
            var (_, least, most, _, text) = GoalBounds(i);
            if (field == _goals[i].Least)
            {
                return least;
            }

            if (field == _goals[i].Most)
            {
                return most;
            }

            if (field == _goals[i].Text)
            {
                return text;
            }
        }

        for (var i = 0; i < _events.Count; i++)
        {
            var (_, at, week, _, every, times) = EventBounds(i);
            if (field == _events[i].At)
            {
                return at;
            }

            if (field == _events[i].Week)
            {
                return week;
            }

            if (field == _events[i].Every)
            {
                return every;
            }

            if (field == _events[i].Times)
            {
                return times;
            }
        }

        return default;
    }

    private float Top => Bounds.Y + TitleStrip;

    private Vector2 Corner() => new(Bounds.X + Width - Padding, Bounds.Y + Height - Padding);

    private float FirstLeft() => Bounds.X + Padding;

    private float SecondLeft() => FirstLeft() + FirstColumn + ColumnGap;

    private float ThirdLeft() => SecondLeft() + SecondColumn + ColumnGap;

    private Rectangle FirstField(int row) =>
        new() { X = FirstLeft(), Y = Top + LabelSize + LabelGap + (row * Row), Width = FirstColumn, Height = FieldHeight };

    private Rectangle DescriptionBounds()
    {
        var y = Top + LabelSize + LabelGap + (4 * Row);
        return new Rectangle { X = FirstLeft(), Y = y, Width = FirstColumn, Height = Top + ContentHeight - y };
    }

    private Rectangle AddBounds(float left) => new() { X = left + 200, Y = Top, Width = 100, Height = SmallButton };

    private (Rectangle Measure, Rectangle Least, Rectangle Most, Rectangle Remove, Rectangle Text) GoalBounds(int i)
    {
        var left = SecondLeft();
        var y = Top + HeaderHeight + (i * BlockHeight);
        return (
            new Rectangle { X = left, Y = y, Width = 92, Height = FieldHeight },
            new Rectangle { X = left + 140, Y = y, Width = 50, Height = FieldHeight },
            new Rectangle { X = left + 236, Y = y, Width = 40, Height = FieldHeight },
            new Rectangle { X = left + SecondColumn - SmallButton, Y = y + 3, Width = SmallButton, Height = SmallButton },
            new Rectangle { X = left, Y = y + FieldHeight + 4, Width = SecondColumn, Height = FieldHeight });
    }

    private (Rectangle Kind, Rectangle At, Rectangle Week, Rectangle Remove, Rectangle Every, Rectangle Times) EventBounds(int i)
    {
        var left = ThirdLeft();
        var y = Top + HeaderHeight + (i * BlockHeight);
        var second = y + FieldHeight + 4;
        return (
            new Rectangle { X = left, Y = y, Width = 92, Height = FieldHeight },
            new Rectangle { X = left + 110, Y = y, Width = 76, Height = FieldHeight },
            new Rectangle { X = left + 222, Y = y, Width = 30, Height = FieldHeight },
            new Rectangle { X = left + ThirdColumn - SmallButton, Y = y + 3, Width = SmallButton, Height = SmallButton },
            new Rectangle { X = left + 70, Y = second, Width = 44, Height = FieldHeight },
            new Rectangle { X = left + 154, Y = second, Width = 44, Height = FieldHeight });
    }

    private void DrawHeader(string title, float left, bool canAdd, Vector2 mouse)
    {
        Raylib.DrawText(title, (int)left, (int)Top + 4, FieldSize, Theme.Text);
        var add = AddBounds(left);
        Raylib.DrawRectangleRec(add, Theme.PanelBorder);
        var label = canAdd ? "+ Add" : FullLabel;
        var color = !canAdd ? Theme.TextDim : Raylib.CheckCollisionPointRec(mouse, add) ? Theme.Accent : Theme.Text;
        Raylib.DrawText(label, (int)(add.X + ((add.Width - Raylib.MeasureText(label, LabelSize + 2)) / 2)), (int)add.Y + 4, LabelSize + 2, color);
    }

    private static void DrawLabelled(string label, TextField field, Rectangle bounds, object? focus = null)
    {
        Raylib.DrawText(label, (int)bounds.X, (int)bounds.Y - LabelSize - LabelGap, LabelSize, Theme.TextDim);
        field.Draw(bounds, focus == field);
    }

    private void DrawLabelled(string label, TextField field, Rectangle bounds) => DrawLabelled(label, field, bounds, _focus);

    private static void DrawSmallLabel(string label, Rectangle field) =>
        Raylib.DrawText(label, (int)field.X - Raylib.MeasureText(label, LabelSize) - 6, (int)field.Y + 8, LabelSize, Theme.TextDim);

    private static void DrawChoice(Rectangle bounds, string text, Vector2 mouse)
    {
        Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
        Raylib.DrawText(text, (int)bounds.X + 6, (int)bounds.Y + 7, LabelSize + 2, Raylib.CheckCollisionPointRec(mouse, bounds) ? Theme.Accent : Theme.Text);
    }

    private static void DrawRemove(Rectangle bounds, Vector2 mouse) =>
        Raylib.DrawText("x", (int)bounds.X + 6, (int)bounds.Y + 2, FieldSize, Raylib.CheckCollisionPointRec(mouse, bounds) ? Theme.Error : Theme.TextDim);

    // A goal as the window holds it: the measure picked, and its fields.
    private sealed class GoalRow
    {
        public GoalMeasure Measure { get; set; }

        public required TextField Least { get; init; }

        public required TextField Most { get; init; }

        public required TextField Text { get; init; }

        public static GoalRow Of(GoalDetails goal) => new()
        {
            Measure = goal.Measure,
            Least = new TextField(goal.AtLeast?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, 10, IsNumberCharacter, FieldSize),
            Most = new TextField(goal.AtMost?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, 10, IsNumberCharacter, FieldSize),
            Text = new TextField(goal.Text ?? string.Empty, ScenarioLoader.MaxGoalTextLength, _ => true, FieldSize),
        };

        private static bool IsNumberCharacter(char c) => char.IsAsciiDigit(c) || c == '-';
    }

    // A disaster as the window holds it: the kind picked, and its fields.
    private sealed class EventRow
    {
        public Disaster Disaster { get; set; }

        public required TextField At { get; init; }

        public required TextField Week { get; init; }

        public required TextField Every { get; init; }

        public required TextField Times { get; init; }

        public static EventRow Of(EventDetails happening) => new()
        {
            Disaster = happening.Disaster,
            At = new TextField($"{happening.At.Year:0000}-{happening.At.Month:00}", 7, c => char.IsAsciiDigit(c) || c == '-', FieldSize),
            Week = new TextField(happening.At.Week == 1 ? string.Empty : happening.At.Week.ToString(CultureInfo.InvariantCulture), 1, char.IsAsciiDigit, FieldSize),
            Every = new TextField(happening.Repeat?.Weeks.ToString(CultureInfo.InvariantCulture) ?? string.Empty, 4, char.IsAsciiDigit, FieldSize),
            Times = new TextField(happening.Repeat?.Times.ToString(CultureInfo.InvariantCulture) ?? string.Empty, 4, char.IsAsciiDigit, FieldSize),
        };
    }
}
