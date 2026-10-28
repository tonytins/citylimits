namespace CityLimits.UI;

/// <summary>
/// The help window, opened from the green question mark at the end of the bar or with F1, after
/// Tony Bark's mock-up in <c>docs/design/</c>. Its toolbar has two pages: Keys, what the keys and the
/// mouse do, and Editing scenarios, how a scenario is made in the game. The game keeps running
/// while it is open, and the player moves it by its title bar; its close button, the question
/// mark, F1, or Escape closes it.
/// </summary>
public sealed class HelpPanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int Padding = 12;
    private const int Width = 520;
    private const int RowHeight = 20;
    private const int KeyColumn = 130;

    private static readonly string[] Pages = ["Keys", "Editing scenarios"];

    // What each key or press does, as the game handles it; a heading has no action.
    private static readonly (string Key, string? Action)[] Rows =
    [
        ("Keys", null),
        ("WASD, arrows", "Move around the map"),
        ("Space", "Pause, or play at the last speed"),
        (", and .", "Slow down and speed up"),
        ("B", "Open the budget"),
        ("Ctrl+S", "Save the city"),
        ("Ctrl+O", "Open a saved city"),
        ("F1", "Open or close this help"),
        ("Escape", "Cancel a stroke, close a panel, or open the menu"),
        ("Mouse", null),
        ("A tool's button", "Pick the tool up; again to put it down"),
        ("Drag on the map", "Build with the tool you hold"),
        ("Inspect tool", "Point at a tile to read what it is"),
        ("Wheel", "Zoom in and out"),
        ("Overview", "Click or drag to move the view there"),
        ("City name", "Rename the city"),
        ("Demand meter", "Open the demand chart"),
        ("Funds", "Open the budget"),
        ("Title bars", "Drag a window to move it"),
    ];

    // How a scenario is made in the game, as docs/scenarios.md tells it: a heading, then its lines.
    private static readonly (string Text, bool Heading)[] Editing =
    [
        ("Opening one", true),
        ("Scenarios on the start menu lists them: Edit opens one's city to edit, paused, and New starts one from a seed.", false),
        ("Building", true),
        ("Building is free while a scenario is edited, paused or playing, and its money stays as the scenario has it.", false),
        ("Its details", true),
        ("Click the city's name on the bar for the city panel, then Edit scenario under the goals: its name, dates, money, and description, its goals, and its disasters, each of which may repeat. Apply checks them and Cancel drops them.", false),
        ("Testing", true),
        ("Play to test it: the city is kept as it stands, and the scenario runs on it as a player would see it. The Escape menu's Back to before play undoes the test; Start over opens it afresh.", false),
        ("Saving", true),
        ("Ctrl+S, or the Escape menu's Save, saves it to Documents/CityLimits/Scenarios with its city beside it. With Save for 0.4 on in the settings, 0.4 can open it too.", false),
    ];

    // The tallest page sets the window's height; the Editing page is wrapped when it is first drawn.
    private static readonly int Height = WindowFrame.TitleHeight + 6 + (Rows.Length * RowHeight) + Padding;

    private readonly WindowFrame _frame = new("Help") { Toolbar = Pages, SelectedTool = 0 };
    private List<(string Line, bool Heading)>? _editingLines;

    /// <summary>Tells whether the player is dragging the window by its title bar.</summary>
    public bool IsDragging => _frame.IsDragging;

    /// <summary>Where the window is on the screen: in the middle until the player moves it.</summary>
    public Rectangle Bounds => _frame.Place(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Hud.BarTop() - Height) / 2));

    /// <summary>Tells whether a point on the screen is on the window's close button.</summary>
    public bool IsOnClose(Vector2 point) => WindowFrame.OnClose(point, Bounds);

    /// <summary>Turns to the page whose toolbar item a press landed on. Tells whether it did.</summary>
    public bool PickPage()
    {
        if (_frame.Update(Bounds) != WindowFrame.Action.Toolbar)
        {
            return false;
        }

        _frame.SelectedTool = _frame.ClickedTool;
        return true;
    }

    /// <summary>Starts moving the window when a press lands on its title bar. Tells whether it did.</summary>
    public bool BeginDrag(Vector2 point) => _frame.BeginDrag(point, Bounds);

    /// <summary>Moves the window with the pointer while the button is held.</summary>
    public void Drag(Vector2 point, bool held) => _frame.Drag(point, held, new Vector2(Width, Height));

    /// <summary>Draws the window where it is.</summary>
    public void Draw()
    {
        var bounds = Bounds;
        _frame.Draw(bounds);
        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + WindowFrame.TitleHeight + 6;
        if (_frame.SelectedTool == 1)
        {
            // Wrapped once, when first shown, since measuring needs the window.
            _editingLines ??= [.. Editing.SelectMany(part => part.Heading
                ? [(part.Text, true)]
                : TextWrap.Lines(part.Text, FontSize, Width - (Padding * 2)).Select(line => (line, false)))];
            foreach (var (line, heading) in _editingLines)
            {
                Raylib.DrawText(line, x, y + 2, FontSize, heading ? Theme.Accent : Theme.TextDim);
                y += heading ? RowHeight : FontSize + 4;
            }

            return;
        }

        foreach (var (key, action) in Rows)
        {
            if (action is null)
            {
                Raylib.DrawText(key, x, y + 2, FontSize, Theme.Accent);
            }
            else
            {
                Raylib.DrawText(key, x, y + 2, FontSize, Theme.Text);
                Raylib.DrawText(action, x + KeyColumn, y + 2, FontSize, Theme.TextDim);
            }

            y += RowHeight;
        }
    }
}
