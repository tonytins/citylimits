namespace CityLimits.UI;

/// <summary>
/// The start menu, after the owner's mock-up in <c>docs/design/</c>: the logo near the top of the
/// blue screen, New Game, Load Game, Settings, and Quit under it, and the version in the bottom right.
/// Coming from the loading screen, the logo glides up from the middle. New Game opens the new-city
/// panel, Load Game the list of saved cities, dimmed while there are none, and Settings the settings
/// window.
/// </summary>
public sealed class StartMenu
{
    private const int FontSize = 20;
    private const int VersionSize = 10;
    private const int ItemSpacing = 36;
    private const int Margin = 16;
    private const float GlideSeconds = 0.3f;

    private static readonly string[] Items = ["New Game", "Load Game", "Settings", "Quit"];

    private readonly Texture2D? _logo;
    private readonly NewCityPanel _newCity = new();
    private readonly LoadPanel _load = new();
    private readonly SettingsPanel _settings = new();
    private bool _loadOpen;
    private bool _settingsOpen;
    private string _citiesFolder = string.Empty;
    private float _glide = GlideSeconds;
    private bool _newCityOpen;
    private int? _commandLineSeed;

    /// <summary>What the player chose on the menu.</summary>
    public enum Choice
    {
        /// <summary>Nothing yet.</summary>
        None,

        /// <summary>Start the city in <see cref="City"/>.</summary>
        NewCity,

        /// <summary>Play the saved city in <see cref="LoadedCity"/>.</summary>
        LoadCity,

        /// <summary>The settings window closed; <see cref="Settings"/> holds what the player set.</summary>
        SettingsClosed,

        /// <summary>Leave the game.</summary>
        Quit,
    }

    /// <summary>Creates the menu with the logo, when it loaded.</summary>
    public StartMenu(GameAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _logo = assets.Logo;
    }

    /// <summary>The city the player asked for, once the choice is <see cref="Choice.NewCity"/>.</summary>
    public NewCity? City => _newCity.City;

    /// <summary>The saved city the player opened, once the choice is <see cref="Choice.LoadCity"/>.</summary>
    public CitySimulation? LoadedCity => _load.City;

    /// <summary>The file the opened city came from.</summary>
    public string? LoadedFile => _load.Path;

    /// <summary>What importing the opened city replaced, when it came from a Micropolis city.</summary>
    public IReadOnlyList<string> LoadedNotes => _load.Notes;

    /// <summary>The settings as the player has left them, changed in the settings window or not.</summary>
    public GameSettings Settings { get; private set; } = GameSettings.Default;

    /// <summary>
    /// Shows the menu afresh. Coming from the loading screen, the logo glides up into place; the
    /// seed given on the command line, if any, fills in the new-city panel, and the saved cities
    /// are listed afresh from their folder.
    /// </summary>
    public void Show(bool fromLoading, int? commandLineSeed, string citiesFolder, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _glide = fromLoading ? 0f : GlideSeconds;
        _newCityOpen = false;
        _loadOpen = false;
        _settingsOpen = false;
        Settings = settings;
        _commandLineSeed = commandLineSeed;
        _citiesFolder = citiesFolder;
        _load.Reset(citiesFolder);
    }

    /// <summary>Takes this frame's input.</summary>
    public Choice Update()
    {
        _glide = Math.Min(_glide + Raylib.GetFrameTime(), GlideSeconds);
        if (_settingsOpen)
        {
            return UpdateSettings();
        }

        if (_loadOpen)
        {
            switch (_load.Update())
            {
                case LoadPanel.Outcome.Opened:
                    return Choice.LoadCity;
                case LoadPanel.Outcome.Back:
                    _loadOpen = false;
                    break;
            }

            return Choice.None;
        }

        if (_newCityOpen)
        {
            switch (_newCity.Update())
            {
                case NewCityPanel.Outcome.Start:
                    return Choice.NewCity;
                case NewCityPanel.Outcome.Back:
                    _newCityOpen = false;
                    break;
            }

            return Choice.None;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return Choice.None;
        }

        return ItemAt(Raylib.GetMousePosition()) switch
        {
            0 => OpenNewCity(),
            1 => OpenLoad(),
            2 => OpenSettings(),
            3 => Choice.Quit,
            _ => Choice.None,
        };
    }

    /// <summary>Draws the menu as a whole frame.</summary>
    public void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Theme.SplashBackground);

        if (_logo is { } logo && Raylib.IsTextureValid(logo))
        {
            Raylib.DrawTextureV(logo, LogoPosition(), Color.White);
        }

        var mouse = Raylib.GetMousePosition();
        var hovered = _newCityOpen || _loadOpen || _settingsOpen ? -1 : ItemAt(mouse);
        for (var i = 0; i < Items.Length; i++)
        {
            var bounds = ItemBounds(i);
            var color = !IsEnabled(i) ? Theme.TextDim : i == hovered ? Theme.Accent : Theme.Text;
            Raylib.DrawText(Items[i], (int)bounds.X, (int)bounds.Y, FontSize, color);
        }

        var version = VersionInfo.DisplayVersion;
        Raylib.DrawText(version, Raylib.GetScreenWidth() - Raylib.MeasureText(version, VersionSize) - Margin, Raylib.GetScreenHeight() - VersionSize - Margin, VersionSize, Theme.Text);

        if (_newCityOpen)
        {
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
            _newCity.Draw();
        }
        else if (_loadOpen)
        {
            _load.Draw();
        }
        else if (_settingsOpen)
        {
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
            _settings.Draw();
        }

        Raylib.EndDrawing();
    }

    // A change applies at once, fullscreen included; the game writes the settings once the window closes.
    private Choice UpdateSettings()
    {
        var mouse = Raylib.GetMousePosition();
        if (_settings.IsDragging)
        {
            _settings.Drag(mouse, Raylib.IsMouseButtonDown(MouseButton.Left));
            return Choice.None;
        }

        switch (_settings.Update())
        {
            case SettingsPanel.Outcome.Changed:
                Settings = _settings.Current;
                Display.Apply(Settings.Fullscreen);
                return Choice.None;
            case SettingsPanel.Outcome.Closed:
                _settingsOpen = false;
                return Choice.SettingsClosed;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _settings.BeginDrag(mouse);
        }

        return Choice.None;
    }

    private Choice OpenSettings()
    {
        _settings.Open(Settings);
        _settingsOpen = true;
        return Choice.None;
    }

    private Choice OpenNewCity()
    {
        _newCity.Reset(_commandLineSeed);
        _newCityOpen = true;
        return Choice.None;
    }

    private Choice OpenLoad()
    {
        _load.Reset(_citiesFolder);
        _loadOpen = true;
        return Choice.None;
    }

    // Load Game is dimmed while there's no saved city to load.
    private bool IsEnabled(int item) => item != 1 || _load.HasCities;

    private int ItemAt(Vector2 point)
    {
        for (var i = 0; i < Items.Length; i++)
        {
            if (IsEnabled(i) && Raylib.CheckCollisionPointRec(point, ItemBounds(i)))
            {
                return i;
            }
        }

        return -1;
    }

    private Vector2 LogoSize() => _logo is { } logo ? new Vector2(logo.Width, logo.Height) : Vector2.Zero;

    // A sixth of the way down, as the mock-up has it; on the way there, eased out from the middle.
    private Vector2 LogoPosition()
    {
        var size = LogoSize();
        var target = new Vector2((Raylib.GetScreenWidth() - size.X) / 2f, Raylib.GetScreenHeight() / 6f);
        var t = _glide / GlideSeconds;
        var eased = 1f - ((1f - t) * (1f - t) * (1f - t));
        return Vector2.Lerp(LoadingScreen.LogoPosition(size), target, eased);
    }

    // The items sit under the logo's final place, so they don't move while it glides.
    private Rectangle ItemBounds(int item)
    {
        var top = (Raylib.GetScreenHeight() / 6f) + LogoSize().Y + 24 + (item * ItemSpacing);
        var width = Raylib.MeasureText(Items[item], FontSize);
        return new Rectangle { X = (Raylib.GetScreenWidth() - width) / 2f, Y = top, Width = width, Height = FontSize };
    }
}
