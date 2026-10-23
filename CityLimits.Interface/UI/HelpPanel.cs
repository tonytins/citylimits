namespace CityLimits.UI;

/// <summary>
/// The help window, opened from the green question mark at the end of the bar or with F1, after
/// Tony Bark's mock-up in <c>docs/design/</c>. It lists what the keys and the mouse do. The game
/// keeps running while it is open, and the player moves it by its title; its x, the question mark,
/// F1, or Escape closes it.
/// </summary>
public sealed class HelpPanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int TitleSize = 20;
    private const int Padding = 12;
    private const int Width = 480;
    private const int RowHeight = 20;
    private const int KeyColumn = 130;
    private const int CloseSize = 20;
    private const int TitleStrip = Padding + TitleSize + 6;

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
        ("Title strips", "Drag a window to move it"),
    ];

    private static readonly int Height = TitleStrip + 4 + (Rows.Length * RowHeight) + Padding;

    private readonly WindowPlacement _placement = new(TitleStrip);

    /// <summary>Tells whether the player is dragging the window by its title.</summary>
    public bool IsDragging => _placement.IsDragging;

    /// <summary>Where the window is on the screen: in the middle until the player moves it.</summary>
    public Rectangle Bounds => _placement.Bounds(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Hud.BarTop() - Height) / 2));

    /// <summary>Tells whether a point on the screen is on the window's close button.</summary>
    public bool IsOnClose(Vector2 point) => Raylib.CheckCollisionPointRec(point, CloseBounds(Bounds));

    /// <summary>Starts moving the window when a press lands on its title. Tells whether it did.</summary>
    public bool BeginDrag(Vector2 point) => _placement.BeginDrag(point, Bounds);

    /// <summary>Moves the window with the pointer while the button is held.</summary>
    public void Drag(Vector2 point, bool held) => _placement.Drag(point, held, new Vector2(Width, Height));

    /// <summary>Draws the window where it is.</summary>
    public void Draw()
    {
        var bounds = Bounds;
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + Padding;
        Raylib.DrawText("Help", x, y, TitleSize, Theme.Text);
        var close = CloseBounds(bounds);
        Raylib.DrawText("x", (int)close.X + 6, (int)close.Y + 2, FontSize, Theme.TextDim);

        y = (int)bounds.Y + TitleStrip + 4;
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

    private static Rectangle CloseBounds(Rectangle bounds) => new() { X = bounds.X + Width - CloseSize - 6, Y = bounds.Y + 6, Width = CloseSize, Height = CloseSize };
}
