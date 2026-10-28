namespace CityLimits.UI;

/// <summary>
/// The settings window, opened from the start menu or the menu Escape opens in a city. Its tabs
/// group the settings: Graphics, for fullscreen, which takes effect when the game next starts;
/// Gameplay, for autosave, whether disasters happen at all, and saving for 0.4; and Disasters, for each kind: fires,
/// floods, earthquakes, meltdowns, air crashes, and shipwrecks. A
/// change applies at once otherwise; the window's owner writes the settings when it closes. The
/// player moves it by its title bar, whose toolbar holds the tabs, and its close button or Escape closes it. Choosing a fullscreen setting the
/// game isn't running in opens a pop-up saying it waits for a restart, which takes the input,
/// Escape and dragging included, until its Okay closes it.
/// </summary>
public sealed class SettingsPanel : IMovableWindow
{
    private const int FontSize = 14;
    private const int SmallSize = 10;
    private const int Padding = 12;
    private const int Width = 440;
    private const int RowHeight = 52;
    private const int LabelColumn = 110;
    private const int OptionWidth = 64;
    private const int OptionHeight = 24;
    private const int OptionGap = 6;
    private const int Rows = 6;
    private const int Height = WindowFrame.TitleHeight + Padding + (Rows * RowHeight) + Padding;

    private static readonly string[] Tabs = ["Graphics", "Gameplay", "Disasters"];

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
                "Fill the screen, without borders, from the next time the game starts."),
        ],
        [
            new(
                "Autosave",
                ["Off", "One", "Two"],
                settings => settings.AutosaveSlots,
                (settings, choice) => settings with { AutosaveSlots = choice },
                "Every 15 minutes of play, beside the city's own file."),
            new(
                "Disasters",
                ["Off", "On"],
                settings => settings.Disasters ? 1 : 0,
                (settings, choice) => settings with { Disasters = choice == 1 },
                "Whether disasters happen on their own; the Disasters tab picks which."),
            new(
                "Save for 0.4",
                ["Off", "On"],
                settings => settings.SaveOlderFormats ? 1 : 0,
                (settings, choice) => settings with { SaveOlderFormats = choice == 1 },
                "Save cities so 0.4 can open them; what 0.4 can't hold is left out."),
        ],
        [
            new(
                "Fires",
                ["Off", "On"],
                settings => settings.Fires ? 1 : 0,
                (settings, choice) => settings with { Fires = choice == 1 },
                "Fires break out now and then; a fire already burning spreads either way."),
            new(
                "Floods",
                ["Off", "On"],
                settings => settings.Floods ? 1 : 0,
                (settings, choice) => settings with { Floods = choice == 1 },
                "Rivers and lakes now and then rise over the land beside them."),
            new(
                "Earthquakes",
                ["Off", "On"],
                settings => settings.Earthquakes ? 1 : 0,
                (settings, choice) => settings with { Earthquakes = choice == 1 },
                "The ground shakes, wrecking buildings and starting fires of its own."),
            new(
                "Meltdowns",
                ["Off", "On"],
                settings => settings.Meltdowns ? 1 : 0,
                (settings, choice) => settings with { Meltdowns = choice == 1 },
                "A nuclear plant can melt down, leaving radioactive ground for decades."),
            new(
                "Air crashes",
                ["Off", "On"],
                settings => settings.AirCrashes ? 1 : 0,
                (settings, choice) => settings with { AirCrashes = choice == 1 },
                "The plane and the helicopter can collide, starting fires where they fall."),
            new(
                "Shipwrecks",
                ["Off", "On"],
                settings => settings.Shipwrecks ? 1 : 0,
                (settings, choice) => settings with { Shipwrecks = choice == 1 },
                "A ship can run aground, wrecking what it hits."),
        ],
    ];

    private readonly WindowFrame _frame = new("Settings") { Toolbar = Tabs, SelectedTool = 0 };
    private readonly NoticePanel _notice = new();

    // The press that closes the pop-up mustn't also start dragging the window behind it.
    private bool _noticeClosedThisFrame;

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

    /// <summary>Tells whether the player is dragging the window, or its pop-up, by its title bar.</summary>
    public bool IsDragging => _frame.IsDragging || _notice.IsDragging;

    /// <summary>Where the window is on the screen: in the middle until the player moves it.</summary>
    public Rectangle Bounds => _frame.Place(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Hud.BarTop() - Height) / 2));

    /// <summary>Opens the window on the settings as they are, at the tab it was last left on.</summary>
    public void Open(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings;
    }

    /// <summary>
    /// Starts moving the window when a press lands on its title bar, but not on its close button or a
    /// tab; while the pop-up is
    /// open, only the pop-up moves. Tells whether it did.
    /// </summary>
    public bool BeginDrag(Vector2 point)
    {
        if (_notice.IsOpen || _noticeClosedThisFrame)
        {
            return _notice.BeginDrag(point);
        }

        return _frame.BeginDrag(point, Bounds);
    }

    /// <summary>Moves the window, or its pop-up, with the pointer while the button is held.</summary>
    public void Drag(Vector2 point, bool held)
    {
        if (_notice.IsDragging)
        {
            _notice.Drag(point, held);
            return;
        }

        _frame.Drag(point, held, new Vector2(Width, Height));
    }

    /// <summary>Takes this frame's input: Escape or the close button closes it, a tab shows its settings, and a choice changes one.</summary>
    public Outcome Update()
    {
        _noticeClosedThisFrame = false;
        if (_notice.IsOpen)
        {
            _noticeClosedThisFrame = _notice.Update();
            return Outcome.Open;
        }

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
        switch (_frame.Update(bounds))
        {
            case WindowFrame.Action.Close:
                return Outcome.Closed;
            case WindowFrame.Action.Toolbar:
                _frame.SelectedTool = _frame.ClickedTool;
                return Outcome.Open;
        }

        var settings = Settings[_frame.SelectedTool];
        for (var row = 0; row < settings.Length; row++)
        {
            var setting = settings[row];
            for (var choice = 0; choice < setting.Choices.Length; choice++)
            {
                if (Raylib.CheckCollisionPointRec(mouse, OptionBounds(bounds, row, choice)) && choice != setting.Chosen(Current))
                {
                    var before = Current;
                    Current = setting.Choose(Current, choice);
                    WarnOfRestart(before);
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
        _frame.Draw(bounds);
        var x = (int)bounds.X + Padding;
        var mouse = Raylib.GetMousePosition();
        var settings = Settings[_frame.SelectedTool];
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

        _notice.Draw();
    }

    // Fullscreen changes when the game next starts, so choosing what it isn't running in says so;
    // choosing what it already is, such as turning it back, needs no restart and says nothing.
    private void WarnOfRestart(GameSettings before)
    {
        if (Current.Fullscreen != before.Fullscreen && Current.Fullscreen != Display.IsFullscreen)
        {
            var change = Current.Fullscreen ? "fill the screen" : "go back to a window";
            _notice.Show("Restart needed", $"The game will {change} the next time you start CityLimits.");
        }
    }

    private static int RowTop(Rectangle bounds, int row) => (int)bounds.Y + WindowFrame.TitleHeight + Padding + (row * RowHeight);

    private static Rectangle OptionBounds(Rectangle bounds, int row, int choice) => new()
    {
        X = bounds.X + Padding + LabelColumn + (choice * (OptionWidth + OptionGap)),
        Y = RowTop(bounds, row),
        Width = OptionWidth,
        Height = OptionHeight,
    };

    private sealed record Setting(string Label, string[] Choices, Func<GameSettings, int> Chosen, Func<GameSettings, int, GameSettings> Choose, string Note);
}
