namespace CityLimits.UI;

/// <summary>
/// The settings window, opened from the start menu or the menu Escape opens in a city. Its tabs
/// group the settings: Graphics, for fullscreen, and Gameplay, for autosave. A change applies at
/// once; the window's owner writes the settings when it closes. The player moves it by its title,
/// and its x or Escape closes it.
/// </summary>
public sealed class SettingsPanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int SmallSize = 10;
    private const int TitleSize = 20;
    private const int Padding = 12;
    private const int Width = 420;
    private const int CloseSize = 20;
    private const int TitleStrip = Padding + TitleSize + 6;
    private const int TabHeight = 26;
    private const int TabWidth = 100;
    private const int RowHeight = 52;
    private const int LabelColumn = 110;
    private const int OptionWidth = 64;
    private const int OptionHeight = 24;
    private const int OptionGap = 6;
    private const int Rows = 1;
    private const int Height = TitleStrip + TabHeight + Padding + (Rows * RowHeight) + Padding;

    private static readonly string[] Tabs = ["Graphics", "Gameplay"];

    // Each tab's settings: a label, the choices, which one the settings hold, what choosing one
    // changes, and a line saying what it does.
    private static readonly Setting[][] Settings =
    [
        [
            new(
                "Fullscreen",
                ["Off", "On"],
                settings => settings.Fullscreen ? 1 : 0,
                (settings, choice) => settings with { Fullscreen = choice == 1 },
                "Fill the screen with the game, as a window without borders."),
        ],
        [
            new(
                "Autosave",
                ["Off", "One", "Two"],
                settings => settings.AutosaveSlots,
                (settings, choice) => settings with { AutosaveSlots = choice },
                "Every 15 minutes of play, beside the city's own file."),
        ],
    ];

    private readonly WindowPlacement _placement = new(TitleStrip);
    private int _tab;

    /// <summary>What the player did with the window this frame.</summary>
    public enum Outcome
    {
        /// <summary>The window is still open, and nothing changed.</summary>
        Open,

        /// <summary>A setting changed; <see cref="Current"/> holds it.</summary>
        Changed,

        /// <summary>The player closed the window.</summary>
        Closed,
    }

    /// <summary>The settings as the player has set them.</summary>
    public GameSettings Current { get; private set; } = GameSettings.Default;

    /// <summary>Tells whether the player is dragging the window by its title.</summary>
    public bool IsDragging => _placement.IsDragging;

    /// <summary>Where the window is on the screen: in the middle until the player moves it.</summary>
    public Rectangle Bounds => _placement.Bounds(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Hud.BarTop() - Height) / 2));

    /// <summary>Opens the window on the settings as they are, at the tab it was last left on.</summary>
    public void Open(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings;
    }

    /// <summary>Starts moving the window when a press lands on its title, but not on its x. Tells whether it did.</summary>
    public bool BeginDrag(Vector2 point) =>
        !Raylib.CheckCollisionPointRec(point, CloseBounds(Bounds)) && _placement.BeginDrag(point, Bounds);

    /// <summary>Moves the window with the pointer while the button is held.</summary>
    public void Drag(Vector2 point, bool held) => _placement.Drag(point, held, new Vector2(Width, Height));

    /// <summary>Takes this frame's input: Escape or the x closes it, a tab shows its settings, and a choice changes one.</summary>
    public Outcome Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return Outcome.Closed;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return Outcome.Open;
        }

        var mouse = Raylib.GetMousePosition();
        var bounds = Bounds;
        if (Raylib.CheckCollisionPointRec(mouse, CloseBounds(bounds)))
        {
            return Outcome.Closed;
        }

        for (var tab = 0; tab < Tabs.Length; tab++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, TabBounds(bounds, tab)))
            {
                _tab = tab;
                return Outcome.Open;
            }
        }

        var settings = Settings[_tab];
        for (var row = 0; row < settings.Length; row++)
        {
            var setting = settings[row];
            for (var choice = 0; choice < setting.Choices.Length; choice++)
            {
                if (Raylib.CheckCollisionPointRec(mouse, OptionBounds(bounds, row, choice)) && choice != setting.Chosen(Current))
                {
                    Current = setting.Choose(Current, choice);
                    return Outcome.Changed;
                }
            }
        }

        return Outcome.Open;
    }

    /// <summary>Draws the window where it is.</summary>
    public void Draw()
    {
        var bounds = Bounds;
        Raylib.DrawRectangleRec(bounds, Theme.Panel);
        Raylib.DrawRectangleLinesEx(bounds, 1f, Theme.PanelBorder);

        var x = (int)bounds.X + Padding;
        Raylib.DrawText("Settings", x, (int)bounds.Y + Padding, TitleSize, Theme.Text);
        var close = CloseBounds(bounds);
        Raylib.DrawText("x", (int)close.X + 6, (int)close.Y + 2, FontSize, Theme.TextDim);

        var mouse = Raylib.GetMousePosition();
        for (var tab = 0; tab < Tabs.Length; tab++)
        {
            var tabBounds = TabBounds(bounds, tab);
            var hovered = Raylib.CheckCollisionPointRec(mouse, tabBounds);
            Raylib.DrawRectangleRec(tabBounds, tab == _tab ? Theme.PanelBorder : Theme.Panel);
            Raylib.DrawRectangleLinesEx(tabBounds, 1f, Theme.PanelBorder);
            var color = tab == _tab ? Theme.Accent : hovered ? Theme.Text : Theme.TextDim;
            Raylib.DrawText(Tabs[tab], (int)tabBounds.X + ((TabWidth - Raylib.MeasureText(Tabs[tab], FontSize)) / 2), (int)tabBounds.Y + 6, FontSize, color);
        }

        // The line under the tabs runs to the window's edge, broken under the tab that is showing.
        var line = (int)bounds.Y + TitleStrip + TabHeight;
        Raylib.DrawLine((int)bounds.X, line, (int)(bounds.X + bounds.Width), line, Theme.PanelBorder);

        var settings = Settings[_tab];
        for (var row = 0; row < settings.Length; row++)
        {
            var setting = settings[row];
            var top = RowTop(bounds, row);
            Raylib.DrawText(setting.Label, x, top + 5, FontSize, Theme.Text);
            var chosen = setting.Chosen(Current);
            for (var choice = 0; choice < setting.Choices.Length; choice++)
            {
                var option = OptionBounds(bounds, row, choice);
                var hovered = Raylib.CheckCollisionPointRec(mouse, option);
                Raylib.DrawRectangleRec(option, choice == chosen ? Theme.PanelBorder : Theme.Panel);
                Raylib.DrawRectangleLinesEx(option, 1f, choice == chosen ? Theme.Accent : Theme.PanelBorder);
                var color = choice == chosen ? Theme.Accent : hovered ? Theme.Text : Theme.TextDim;
                var text = setting.Choices[choice];
                Raylib.DrawText(text, (int)option.X + ((OptionWidth - Raylib.MeasureText(text, FontSize)) / 2), (int)option.Y + 5, FontSize, color);
            }

            Raylib.DrawText(setting.Note, x, top + OptionHeight + 8, SmallSize, Theme.TextDim);
        }
    }

    private static Rectangle CloseBounds(Rectangle bounds) => new() { X = bounds.X + Width - CloseSize - 6, Y = bounds.Y + 6, Width = CloseSize, Height = CloseSize };

    private static Rectangle TabBounds(Rectangle bounds, int tab) => new()
    {
        X = bounds.X + Padding + (tab * (TabWidth + 4)),
        Y = bounds.Y + TitleStrip,
        Width = TabWidth,
        Height = TabHeight,
    };

    private static int RowTop(Rectangle bounds, int row) => (int)bounds.Y + TitleStrip + TabHeight + Padding + (row * RowHeight);

    private static Rectangle OptionBounds(Rectangle bounds, int row, int choice) => new()
    {
        X = bounds.X + Padding + LabelColumn + (choice * (OptionWidth + OptionGap)),
        Y = RowTop(bounds, row),
        Width = OptionWidth,
        Height = OptionHeight,
    };

    private sealed record Setting(string Label, string[] Choices, Func<GameSettings, int> Chosen, Func<GameSettings, int, GameSettings> Choose, string Note);
}
