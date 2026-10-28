namespace CityLimits.UI;

/// <summary>
/// A small pop-up window with a title, a short message, and an Okay button in the middle at the
/// bottom, which Enter, Escape, its close button, or a click on Okay closes. It moves by its title bar like the other
/// windows, and opens in the middle of the space above the bar. Its owner draws it over whatever
/// opened it, and gives it the input while it is open.
/// </summary>
public sealed class NoticePanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int Padding = 12;
    private const int Width = 340;
    private const int LineHeight = 18;
    private const int Top = WindowFrame.TitleHeight + 8;
    private const int ButtonWidth = 90;
    private const int ButtonHeight = 28;
    private const string ButtonText = "Okay";

    private readonly WindowFrame _frame = new(string.Empty);
    private readonly List<string> _lines = [];

    /// <summary>Tells whether the pop-up is showing.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Tells whether the player is dragging the pop-up by its title bar.</summary>
    public bool IsDragging => _frame.IsDragging;

    /// <summary>Where the pop-up is on the screen: in the middle until the player moves it.</summary>
    public Rectangle Bounds
    {
        get
        {
            var size = new Vector2(Width, Height());
            return _frame.Place(size, new Vector2((Raylib.GetScreenWidth() - size.X) / 2, (Hud.BarTop() - size.Y) / 2));
        }
    }

    /// <summary>Opens the pop-up with a title and a message, which is wrapped to fit it.</summary>
    /// <exception cref="ArgumentException">The title or the message is empty.</exception>
    public void Show(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        _frame.Title = title;
        _lines.Clear();
        _lines.AddRange(TextWrap.Lines(message, FontSize, Width - (Padding * 2)));
        IsOpen = true;
    }

    /// <summary>Starts moving the pop-up when a press lands on its title bar. Tells whether it did.</summary>
    public bool BeginDrag(Vector2 point) => IsOpen && _frame.BeginDrag(point, Bounds);

    /// <summary>Moves the pop-up with the pointer while the button is held.</summary>
    public void Drag(Vector2 point, bool held) => _frame.Drag(point, held, new Vector2(Width, Height()));

    /// <summary>Takes this frame's input: Enter, Escape, its close button, or a click on Okay closes it. Tells whether it closed.</summary>
    public bool Update()
    {
        if (!IsOpen)
        {
            return false;
        }

        var closed = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsKeyPressed(KeyboardKey.Escape) ||
            (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ButtonBounds(Bounds))) ||
            _frame.Update(Bounds) == WindowFrame.Action.Close;
        IsOpen = !closed;
        return closed;
    }

    /// <summary>Draws the pop-up where it is, when it is open.</summary>
    public void Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        var bounds = Bounds;
        _frame.Draw(bounds);
        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + Top;
        foreach (var line in _lines)
        {
            Raylib.DrawText(line, x, y, FontSize, Theme.TextDim);
            y += LineHeight;
        }

        var button = ButtonBounds(bounds);
        var hovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), button);
        Raylib.DrawRectangleRec(button, hovered ? Theme.PanelBorder : Theme.Panel);
        Raylib.DrawRectangleLinesEx(button, 1f, Theme.Accent);
        var textX = (int)(button.X + ((ButtonWidth - Raylib.MeasureText(ButtonText, FontSize)) / 2));
        Raylib.DrawText(ButtonText, textX, (int)button.Y + ((ButtonHeight - FontSize) / 2), FontSize, hovered ? Theme.Accent : Theme.Text);
    }

    private int Height() => Top + (Math.Max(_lines.Count, 1) * LineHeight) + Padding + ButtonHeight + Padding;

    private static Rectangle ButtonBounds(Rectangle bounds) => new()
    {
        X = bounds.X + ((bounds.Width - ButtonWidth) / 2),
        Y = bounds.Y + bounds.Height - Padding - ButtonHeight,
        Width = ButtonWidth,
        Height = ButtonHeight,
    };
}
