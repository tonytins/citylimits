namespace CityLimits.Game;

/// <summary>The game: it owns the clock, the tools, the windows, and the city being simulated, which it shows through a <see cref="CityView"/>.</summary>
public sealed class GameLoop : IDisposable
{
    /// <summary>The seed the starting city is built from.</summary>
    public const int DefaultSeed = 2000;

    // How long what a stroke came to stays beside the pointer once it is let go.
    private const float HintSeconds = 3f;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly GameClock _clock = new();
    private readonly CitySimulation _simulation;
    private readonly CityView _view;
    private readonly ToolPalette _tools = new();
    private readonly Hud _hud = new();
    private readonly ToolPaletteView _toolPaletteView;
    private readonly BudgetPanel _budgetPanel = new();
    private readonly DemandPanel _demandPanel = new();
    private readonly HelpPanel _helpPanel = new();
    private readonly NewsTicker _ticker;
    private GameSettings _settings;

    // Whether the press that is held began on the overview, so dragging there moves the view.
    private bool _draggingOverview;

    // The demand panel, opened from the meter in the bar, and the help window, opened from its
    // question mark; the game keeps running under both.
    private bool _demandOpen;
    private bool _helpOpen;
    private int _lastSpeed = 1;
    private readonly CityMessageFilter _messages = new();

    // What the last stroke came to, beside the pointer for a moment; and the last failure the
    // ticker was told of, so a frame that keeps failing doesn't fill it.
    private string? _hint;
    private float _hintSeconds;
    private string? _lastError;

    // The budget, the menu, and the rename panel each hold the game while open, taking every key
    // and click, and give the speed back when they close.
    private Modal _modal;
    private string? _budgetReason;
    private int _speedBeforeModal;
    private readonly CityPanel _cityPanel = new();
    private readonly LoadPanel _loadPanel = new();
    private readonly ConfirmPanel _confirm = new();
    private readonly SettingsPanel _settingsPanel = new();
    private readonly NoticePanel _notice = new();
    private readonly ScenarioWindow _scenarioWindow = new();
    private readonly GameFolders _folders;

    // The file the city was opened from or last saved to, and the save it was then; a city whose
    // save reads differently has changes a save would keep.
    private string? _cityFile;
    private string? _savedText;

    // Fifteen minutes of play between autosaves; the last one written is kept to skip an unchanged city.
    private readonly Autosave _autosave = new();
    private string? _autosavedText;

    // What the question on screen is about, and the city to open once it is answered.
    private Pending _pending;
    private CitySimulation? _cityToOpen;
    private string? _fileToOpen;
    private IReadOnlyList<string> _notesToOpen = [];

    // The window the player is dragging by its title, if any; it follows the pointer until the
    // button is let go, and nothing else takes the pointer meanwhile.
    private IMovableWindow? _dragging;

    // The scenario being edited, if any: its city is played with building free and the clock
    // stopped, and playing tests it. While it isn't being tested, the city panel shows its draft.
    private readonly ScenarioEdit? _editing;
    private Challenge? _draft;

    // The bar's name, made again only when the city's name or the test changes, so a frame doesn't allocate it.
    private string _barName = string.Empty;
    private string? _barNameOf;
    private bool _barNameTesting;

    /// <summary>
    /// Plays a city: a new one, or one opened from a file, which is then the file Ctrl+S saves it
    /// back to without asking; a Micropolis city it was imported from is never written. The mods
    /// skipped when a new city was made are shown, if any, or what an import replaced. A scenario's
    /// city opened to edit starts paused, with building free; another view of the same map, as when
    /// a test goes back, is looked at from where it was, with the tool that was in hand.
    /// </summary>
    public GameLoop(GameSettings settings, CitySimulation city, string? cityFile, GameFolders folders, GameAssets assets, IReadOnlyList<string>? modProblems = null, IReadOnlyList<string>? openNotes = null, ScenarioEdit? editing = null, (Vector2 Target, float Zoom)? lookAt = null, Tool? tool = null)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(folders);
        _settings = settings;
        _lastSpeed = settings.Speed == 0 ? 1 : settings.Speed;
        _folders = folders;
        _simulation = city;
        _cityFile = cityFile;

        // An opened city starts saved; a new or an imported one has nothing saved yet.
        _savedText = cityFile is null || CityFiles.IsImport(cityFile) ? null : FlatCityFormat.Write(city);

        // The news runs on headlines from the city whenever the game has nothing to say. They are
        // picked with a generator of their own, so they never change what the city does.
        var desk = new NewsDesk(city, new Random());
        _ticker = new NewsTicker(desk.Next);

        modProblems ??= [];
        if (modProblems.Count > 0)
        {
            // The log holds every skipped mod; the ticker says how many, so the player knows to look.
            _ticker.Advise(modProblems.Count == 1 ? modProblems[0] : $"{modProblems.Count} mods were skipped; the log says why");
        }

        // What opening the city came to, such as what an import replaced or a newer save's parts,
        // each in turn on the ticker; the load list wrote them all to the log.
        foreach (var note in openNotes ?? [])
        {
            _ticker.Advise(note);
        }

        _simulation.MessageSent += ShowMessage;

        // A challenge says what it asks when it starts, and its result when its goals are judged.
        if (city.Challenge is { } challenge)
        {
            challenge.Finished += ShowChallengeResult;
            if (challenge.Won is null)
            {
                _ticker.Advise($"{challenge.Name}: meet its goals by {challenge.Ends}; the city panel lists them");
            }
        }
        ApplyDisasters(settings);
        _simulation.YearSettled += OnYearSettled;

        _view = new CityView(city, assets, settings.WindowWidth, settings.WindowHeight);
        if (lookAt is { } look)
        {
            _view.LookAt(look.Target, look.Zoom);
        }

        if (tool is { } held)
        {
            _tools.Toggle(held);
        }

        _editing = editing;
        if (editing is not null)
        {
            _settings = _settings with { Speed = 0 };
            _draft = Challenge.Begin(editing.Scenario, city);
            _ticker.Advise($"Editing {editing.Scenario.Name}: building is free, and playing tests it; Escape's menu goes back");
        }
        Logger.Info(_view.IsCached ? "The map is drawn from cached chunks" : "The map is drawn tile by tile");
        _toolPaletteView = new ToolPaletteView(assets, city.Definitions);
    }

    /// <summary>The city being simulated.</summary>
    public CitySimulation Simulation => _simulation;

    /// <summary>The scenario being edited, if any, which a city opened from this one carries on editing.</summary>
    public ScenarioEdit? Editing => _editing;

    /// <summary>Where the view looks, and how near, for another view of the same map to carry on from.</summary>
    public (Vector2 Target, float Zoom) LookingAt => (_view.Camera.Target, _view.Camera.Zoom);

    /// <summary>The tool in hand, if any, for another view of the same map to carry on with.</summary>
    public Tool? SelectedTool => _tools.Selected;

    /// <summary>The speed the game is running at, where zero is paused.</summary>
    public int Speed => _settings.Speed;

    /// <summary>What leaving the city was for, once the player has asked to.</summary>
    public enum Leave
    {
        /// <summary>The city carries on.</summary>
        None,

        /// <summary>Back to the start menu.</summary>
        ToMenu,

        /// <summary>Out of the game.</summary>
        Quit,

        /// <summary>To another city, in <see cref="OpenedCity"/>.</summary>
        Open,
    }

    /// <summary>The city the player opened, once leaving is <see cref="Leave.Open"/>.</summary>
    public CitySimulation? OpenedCity { get; private set; }

    /// <summary>The file the opened city came from.</summary>
    public string? OpenedFile { get; private set; }

    /// <summary>What importing the opened city replaced, when it came from a Micropolis city.</summary>
    public IReadOnlyList<string> OpenedNotes { get; private set; } = [];

    // What holds the game while it is open.
    private enum Modal
    {
        None,
        Budget,
        Menu,
        City,
        Load,
        Confirm,
        Settings,
        Notice,
        Scenario,
    }

    // What a question asks before it goes ahead.
    private enum Pending
    {
        None,
        Overwrite,
        LeaveToMenu,
        LeaveToQuit,
        OpenCity,
        BackToBeforePlay,
        StartOver,
    }

    /// <summary>Whether, and where, the player asked to leave the city.</summary>
    public Leave Leaving { get; private set; }

    /// <summary>
    /// The settings as the game stands: the window's size, and the speed the city was running at,
    /// even while a panel holds it paused, or, editing a scenario, the speed it was last played at.
    /// </summary>
    public GameSettings Settings => Display.WithWindowSize(_settings with { Speed = _editing is not null ? _lastSpeed : _modal == Modal.None ? _settings.Speed : _speedBeforeModal });

    /// <summary>Reads the input and advances the city.</summary>
    public void Update()
    {
        var elapsed = Raylib.GetFrameTime();
        _view.FitScreen();
        _demandPanel.DefaultTop = DemandPanelTop();

        HandleInput(elapsed);

        // Once the player has asked to leave, the city stands still: another may open in its place,
        // and a scenario's test mustn't begin again on the way out.
        if (Leaving != Leave.None)
        {
            return;
        }

        // Playing a scenario being edited tests it, from a checkpoint taken before its first step.
        if (_settings.Speed > 0 && _modal == Modal.None && _editing is { IsTesting: false })
        {
            BeginTest();
        }

        if (_settings.Speed > 0)
        {
            var due = _clock.Advance(elapsed * GameSpeed.Multiplier(_settings.Speed));
            // A year that opens the budget stops the city at once, even partway through a frame's steps.
            for (var i = 0; i < due && _modal == Modal.None; i++)
            {
                _simulation.Tick();
            }
        }

        _view.Animate(elapsed, _modal == Modal.None ? _settings.Speed : GameSpeed.Paused);

        // The news stands still while the city does, a panel holding it included. Its first line
        // starts a third of the way across, so a city opened paused still shows the news.
        _ticker.Update(_settings.Speed > 0 && _modal == Modal.None ? elapsed : 0f, Raylib.GetScreenWidth(), 1f / 3f);
        _hintSeconds = Math.Max(0f, _hintSeconds - elapsed);

        // With autosave off, the time to the next one doesn't run.
        if (_editing is null && _settings.AutosaveSlots > 0 && _autosave.Advance(elapsed, _settings.Speed > 0 && _modal == Modal.None))
        {
            AutosaveNow();
        }

        _view.Update();
    }

    /// <summary>Draws the game.</summary>
    public void Draw()
    {
        // Chunks and the overview are redrawn into their own textures, which can't happen inside the frame's camera.
        _view.Refresh();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color { R = 12, G = 14, B = 18, A = 255 });

        var mouse = Raylib.GetMousePosition();

        _view.DrawMap(mouse);

        _toolPaletteView.Draw(_tools, mouse);

        var onMap = _view.TryGetTileAt(mouse, out var tileX, out var tileY);

        _hud.DrawStatus(
            BarName(),
            _simulation.Date,
            // _simulation.TickCount,
            _settings.Speed,
            _lastSpeed,
            _simulation.Census.CityPopulation,
            _simulation.Demand,
            _simulation.Treasury.Funds,
            Raylib.GetFPS());
        _view.DrawOverview();
        _ticker.Draw(Raylib.GetScreenHeight() - NewsTicker.Height, Raylib.GetScreenWidth());

        // Windows go over the rest of the interface, wherever the player has moved them.
        if (_demandOpen)
        {
            _demandPanel.Draw(_simulation);
        }

        if (_helpOpen)
        {
            _helpPanel.Draw();
        }

        // Beside the pointer: what the stroke being dragged would cost, what the last one came to,
        // or, with the Inspect tool, what the tile under the pointer is, when it isn't over the interface.
        if (_view.IsStroking)
        {
            Hud.DrawPointerHint(mouse, Hud.StrokeHint(_view.Preview));
        }
        else if (_hintSeconds > 0 && _hint is not null)
        {
            Hud.DrawPointerHint(mouse, _hint);
        }
        else if (_tools.Selected == Tool.Inspect && onMap && _modal == Modal.None && !IsOverInterface(mouse))
        {
            Hud.DrawPointerHint(mouse, $"Tile {tileX}, {tileY}", TileInfo.Describe(_simulation.Map, tileX, tileY, _simulation.Definitions, _simulation.Hazards));
        }

        switch (_modal)
        {
            case Modal.Budget:
                _budgetPanel.Draw(_simulation, _budgetReason);
                break;
            case Modal.Menu:
                GameMenu.Draw(_editing is not null, _editing?.IsTesting == true);
                break;
            case Modal.City:
                _cityPanel.Draw(_simulation);
                break;
            case Modal.Load:
                _loadPanel.Draw();
                break;
            case Modal.Confirm:
                _confirm.Draw();
                break;
            case Modal.Settings:
                Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
                _settingsPanel.Draw();
                break;
            case Modal.Notice:
                Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
                _notice.Draw();
                break;
            case Modal.Scenario:
                _scenarioWindow.Draw();
                break;
        }

        Raylib.EndDrawing();
    }

    /// <summary>Releases what the game holds on the graphics card. Call it before the window closes.</summary>
    public void Dispose()
    {
        _view.Dispose();
        _loadPanel.Dispose();
    }

    /// <summary>Records a failure, tells the player on the ticker, and keeps the game running.</summary>
    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Logger.Error(exception, "The frame failed");
        if (exception.Message != _lastError)
        {
            _lastError = exception.Message;
            _ticker.Advise($"Something went wrong: {exception.Message}");
        }
    }

    private void HandleInput(float elapsed)
    {
        var mouse = Raylib.GetMousePosition();
        // The size kept is the window's own; filling the screen keeps the one it goes back to.
        if (Raylib.IsWindowResized())
        {
            _settings = Display.WithWindowSize(_settings);
        }

        if (_dragging is not null)
        {
            _dragging.Drag(mouse, Raylib.IsMouseButtonDown(MouseButton.Left));
            if (!_dragging.IsDragging)
            {
                _dragging = null;
            }

            return;
        }

        // While a panel holds the game, it takes every key and click, and the map takes none.
        switch (_modal)
        {
            case Modal.Budget:
                HandleBudgetInput(mouse);
                return;
            case Modal.Menu:
                HandleMenuInput();
                return;
            case Modal.City:
                HandleCityInput(mouse);
                return;
            case Modal.Load:
                HandleLoadInput();
                return;
            case Modal.Confirm:
                HandleConfirmInput();
                return;
            case Modal.Settings:
                HandleSettingsInput(mouse);
                return;
            case Modal.Notice:
                if (_notice.Update())
                {
                    CloseModal();
                }
                else if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    TryBeginDrag(_notice, mouse);
                }

                return;
            case Modal.Scenario:
                HandleScenarioInput(mouse);
                return;
        }

        // Ctrl+S and Ctrl+O save and open; held Control keeps the letters from panning the map.
        var control = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        // A scenario being edited saves as a scenario, and no other city opens while it is edited.
        if (control && _editing is not null && Raylib.IsKeyPressed(KeyboardKey.O))
        {
            _ticker.Advise("No other city opens while a scenario is edited");
            return;
        }

        if (control && Raylib.IsKeyPressed(KeyboardKey.S))
        {
            SaveOrSaveScenario(confirmed: false);
            return;
        }

        if (control && Raylib.IsKeyPressed(KeyboardKey.O))
        {
            _loadPanel.Reset(_folders.CitiesFolder, InstalledDefinitions());
            OpenModal(Modal.Load);
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.B))
        {
            OpenModal(Modal.Budget);
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F1))
        {
            _helpOpen = !_helpOpen;
        }

        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            _view.ZoomAt(mouse, wheel);
        }

        var pan = Vector2.Zero;
        if (control)
        {
            // No panning while Control is held.
        }
        else if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left))
        {
            pan.X -= 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right))
        {
            pan.X += 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up))
        {
            pan.Y -= 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down))
        {
            pan.Y += 1f;
        }

        if (pan != Vector2.Zero)
        {
            _view.Pan(pan, elapsed);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            TogglePause();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Comma))
        {
            ChangeSpeed(-1);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Period))
        {
            ChangeSpeed(1);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_view.IsStroking)
            {
                _view.CancelStroke();
            }
            else if (_helpOpen)
            {
                _helpOpen = false;
            }
            else if (_demandOpen)
            {
                _demandOpen = false;
            }
            else
            {
                OpenModal(Modal.Menu);
            }
        }

        HandleStroke(mouse);
    }

    private void HandleBudgetInput(Vector2 mouse)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.B))
        {
            CloseModal();
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left) || TryBeginDrag(_budgetPanel, mouse))
        {
            return;
        }

        if (_budgetPanel.HandleClick(mouse, _simulation.Budget) == BudgetPanel.Click.Close)
        {
            CloseModal();
        }
    }

    // A year that needs attention opens the budget by itself: upkeep came to more than the
    // taxes, or the money ran out and funding was cut.
    private void OnYearSettled(BudgetYear year)
    {
        if (!year.NeedsAttention)
        {
            return;
        }

        var reason = year.Cut
            ? "The money ran out, so funding was cut"
            : $"Upkeep came to ${year.Expenses}, more than the ${year.TaxIncome} in taxes";
        Logger.Warn("The budget needs attention: {Reason}", reason);
        OpenModal(Modal.Budget, reason);
    }

    private void HandleMenuInput()
    {
        switch (GameMenu.Update(_editing is not null, _editing?.IsTesting == true))
        {
            case GameMenu.Choice.BackToBeforePlay:
                CloseModal();
                Ask(Pending.BackToBeforePlay, "Go back to before play?", "The city goes back to how it was when you pressed play: the test, and everything built during it, are undone.", "Go back", "Stay");
                break;
            case GameMenu.Choice.StartOver:
                CloseModal();
                Ask(Pending.StartOver, $"Start {_editing!.Scenario.Name} over?", "Its city opens again as the scenario has it, and every change since is undone.", "Start over", "Stay");
                break;
            case GameMenu.Choice.Resume:
                CloseModal();
                break;
            case GameMenu.Choice.Save:
                CloseModal();
                SaveOrSaveScenario(confirmed: false);
                break;
            case GameMenu.Choice.Load:
                CloseModal();
                _loadPanel.Reset(_folders.CitiesFolder, InstalledDefinitions());
                OpenModal(Modal.Load);
                break;
            case GameMenu.Choice.Settings:
                // The city stays paused from the menu to the settings and back.
                _settingsPanel.Open(Settings);
                _modal = Modal.Settings;
                break;
            case GameMenu.Choice.MainMenu:
                CloseModal();
                LeaveOrAsk(Pending.LeaveToMenu);
                break;
            case GameMenu.Choice.Quit:
                CloseModal();
                LeaveOrAsk(Pending.LeaveToQuit);
                break;
        }
    }

    // A change applies at once; closing writes the settings, so a crash later can't lose them, and
    // goes back to the menu the settings came from.
    private void HandleSettingsInput(Vector2 mouse)
    {
        switch (_settingsPanel.Update())
        {
            case SettingsPanel.Outcome.Changed:
                // Fullscreen is kept for the next start; the rest apply now.
                var chosen = _settingsPanel.Current;
                _settings = _settings with
                {
                    Fullscreen = chosen.Fullscreen,
                    AutosaveSlots = chosen.AutosaveSlots,
                    Disasters = chosen.Disasters,
                    Fires = chosen.Fires,
                    Floods = chosen.Floods,
                    Earthquakes = chosen.Earthquakes,
                    SaveOlderFormats = chosen.SaveOlderFormats,
                };
                ApplyDisasters(_settings);
                Logger.Info(
                    "Settings changed: fullscreen {Fullscreen} from the next start, {Slots} autosaves, disasters {Disasters}: fires {Fires}, floods {Floods}, earthquakes {Earthquakes}; saving for 0.4 {Older}",
                    chosen.Fullscreen,
                    chosen.AutosaveSlots,
                    chosen.Disasters,
                    chosen.Fires,
                    chosen.Floods,
                    chosen.Earthquakes,
                    chosen.SaveOlderFormats);
                return;
            case SettingsPanel.Outcome.Closed:
                if (!SettingsStore.TrySave(_folders.SettingsFile, Settings, out var error))
                {
                    Logger.Error("{Error}", error);
                    _ticker.Advise(error!);
                }

                _modal = Modal.Menu;
                return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            TryBeginDrag(_settingsPanel, mouse);
        }
    }

    private void HandleLoadInput()
    {
        switch (_loadPanel.Update())
        {
            case LoadPanel.Outcome.Opened:
                CloseModal();
                (_cityToOpen, _fileToOpen, _notesToOpen) = (_loadPanel.City, _loadPanel.Path, _loadPanel.Notes);
                LeaveOrAsk(Pending.OpenCity);
                break;
            case LoadPanel.Outcome.Back:
                CloseModal();
                break;
        }
    }

    private void HandleConfirmInput()
    {
        var answer = _confirm.Update();
        if (answer == ConfirmPanel.Answer.None)
        {
            return;
        }

        var pending = _pending;
        _pending = Pending.None;
        CloseModal();
        if (answer == ConfirmPanel.Answer.No)
        {
            _cityToOpen = null;
            return;
        }

        switch (pending)
        {
            case Pending.Overwrite:
                SaveOrSaveScenario(confirmed: true);
                break;
            default:
                Go(pending);
                break;
        }
    }

    // Leaving a city with changes since its last save asks first, so a city isn't lost by a click.
    private void LeaveOrAsk(Pending leaving)
    {
        if (!HasUnsavedChanges())
        {
            Go(leaving);
            return;
        }

        var where = leaving switch
        {
            Pending.LeaveToMenu => "for the menu",
            Pending.LeaveToQuit => "and quit",
            _ => $"for {_cityToOpen?.Name}",
        };
        Ask(leaving, $"Leave {_simulation.Name} {where}?", "It has changes since it was last saved. Ctrl+S saves it first.", "Leave", "Stay");
    }

    private void Go(Pending leaving)
    {
        switch (leaving)
        {
            case Pending.LeaveToMenu:
                Leaving = Leave.ToMenu;
                break;
            case Pending.LeaveToQuit:
                Leaving = Leave.Quit;
                break;
            case Pending.OpenCity when _cityToOpen is not null:
                (OpenedCity, OpenedFile, OpenedNotes) = (_cityToOpen, _fileToOpen, _notesToOpen);
                Leaving = Leave.Open;
                break;
            case Pending.BackToBeforePlay when _editing is not null:
                Reopen(_editing.EndTest, "went back to before play");
                break;
            case Pending.StartOver when _editing is not null:
                Reopen(_editing.OpenCity, "started over");
                break;
        }
    }

    // The scenario being edited goes on in a city of its own: the one from before its test, or its
    // own afresh. The game opens it as it would any other city, looking where it looked.
    private void Reopen(Func<CitySimulation> open, string what)
    {
        try
        {
            (OpenedCity, OpenedFile, OpenedNotes) = (open(), null, []);
            Leaving = Leave.Open;
            Logger.Info("Editing {Name}: {What}", _editing!.Scenario.Name, what);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            Logger.Error("Editing {Name}: couldn't have {What}: {Problem}", _editing!.Scenario.Name, what, ex.Message);
            _ticker.Advise(ex.Message);
        }
    }

    // The test starts here: the city is kept as it is to go back to, and the scenario runs on it.
    private void BeginTest()
    {
        _view.CancelStroke();
        _editing!.BeginTest(_simulation);
        _simulation.Challenge!.Finished += ShowChallengeResult;
        Logger.Info("Testing {Name}", _editing.Scenario.Name);
        _ticker.Advise($"Testing {_editing.Scenario.Name}: Escape's menu goes back to before play");
    }

    // The city's name in the bar, or, editing a scenario, what is being done to it.
    private string BarName()
    {
        var testing = _editing?.IsTesting == true;
        if (!ReferenceEquals(_barNameOf, _simulation.Name) || _barNameTesting != testing)
        {
            (_barNameOf, _barNameTesting) = (_simulation.Name, testing);
            _barName = _editing is null ? _simulation.Name : testing ? $"Testing {_simulation.Name}" : $"Editing {_simulation.Name}";
        }

        return _barName;
    }

    private void Ask(Pending pending, string title, string message, string yes, string no)
    {
        _pending = pending;
        _confirm.Ask(title, message, yes, no);
        OpenModal(Modal.Confirm);
    }

    // An autosave is a binary save beside the city's own, written only when the city has changed
    // since it was last saved either way. It doesn't count as the player saving, so leaving still asks.
    private void AutosaveNow()
    {
        var text = FlatCityFormat.Write(_simulation);
        if (text == _savedText || text == _autosavedText)
        {
            Logger.Debug("Autosave skipped: {Name} hasn't changed since it was last saved", _simulation.Name);
            return;
        }

        // An autosave follows the city's own format, so a flat city's autosave keeps a newer save's parts too.
        var path = Autosave.NextPath(_folders.CitiesFolder, _simulation.Name, _settings.AutosaveSlots, SaveExtension());
        if (!CityFiles.TryWrite(_simulation, path, out var problem, _settings.SaveOlderFormats))
        {
            Logger.Error("Autosave failed: {Problem}", problem);
            _ticker.Advise($"Autosave failed: {problem}");
            return;
        }

        _autosavedText = text;
        Logger.Info("Autosaved {Name} to {Path}", _simulation.Name, path);
        _ticker.Advise($"Autosaved {_simulation.Name}");
    }

    // A city saves in its own format: flat when it came from a flat file, and otherwise binary, a
    // Micropolis city included.
    private string SaveExtension() =>
        _cityFile is not null && Path.GetExtension(_cityFile).Equals(FlatCityFormat.Extension, StringComparison.OrdinalIgnoreCase)
            ? FlatCityFormat.Extension
            : BinaryCityFormat.Extension;

    // A scenario being edited has changes when it or its city reads differently from its last save.
    private bool HasUnsavedChanges() => _editing is not null
        ? _editing.HasChanges(_simulation)
        : _savedText is null || FlatCityFormat.Write(_simulation) != _savedText;

    private void SaveOrSaveScenario(bool confirmed)
    {
        if (_editing is null)
        {
            Save(confirmed);
        }
        else
        {
            SaveScenario(confirmed);
        }
    }

    // A scenario saves to the player's own scenarios folder under its name, its city beside it.
    // Saving over its own file is plain saving; replacing another scenario of the same name asks first.
    private void SaveScenario(bool confirmed)
    {
        var editing = _editing!;
        string path;
        try
        {
            path = editing.SavePath();
        }
        catch (ArgumentException ex)
        {
            _ticker.Advise(ex.Message);
            return;
        }

        if (!confirmed && File.Exists(path) && !string.Equals(Path.GetFullPath(path), Path.GetFullPath(editing.Path), StringComparison.Ordinal))
        {
            Ask(Pending.Overwrite, "Replace a saved scenario?", $"{Path.GetFileName(path)} already holds a scenario, and saving would replace it and its city.", "Replace", "Cancel");
            return;
        }

        if (!editing.TrySave(_simulation, out var problem, _settings.SaveOlderFormats))
        {
            Logger.Error("{Problem}", problem);
            _ticker.Advise(problem);
            return;
        }

        _draft = Challenge.Begin(editing.Scenario, _simulation);
        Logger.Info("Saved the scenario {Name} to {Path}", editing.Scenario.Name, editing.Path);
        _ticker.Advise($"Saved {editing.Scenario.Name} to {Path.GetFileName(editing.Path)}");
    }

    // A city is saved under its name, in the binary format unless it came from a flat file. Saving
    // over its own file is plain saving, an autosave's city counting as its own; replacing another
    // city's file of the same name asks first.
    private void Save(bool confirmed)
    {
        // A city imported from Micropolis is saved as CityLimits' own, never back to its file.
        var file = CityFiles.FileNameFor(_simulation.Name, SaveExtension());
        var path = Path.Combine(_folders.CitiesFolder, file);
        var ownFile = _cityFile is null || CityFiles.IsImport(_cityFile) ? null
            : Autosave.IsAutosave(_cityFile) ? Autosave.CityFileFor(_cityFile)
            : _cityFile;
        if (!confirmed && File.Exists(path) && !string.Equals(Path.GetFullPath(path), ownFile is null ? null : Path.GetFullPath(ownFile), StringComparison.Ordinal))
        {
            Ask(Pending.Overwrite, "Replace a saved city?", $"{file} already holds a saved city, and saving would replace it.", "Replace", "Cancel");
            return;
        }

        // The flat text is what later saves are compared with, whichever format the file is in.
        var text = FlatCityFormat.Write(_simulation);
        if (!CityFiles.TryWrite(_simulation, path, out var problem, _settings.SaveOlderFormats))
        {
            Logger.Error("{Problem}", problem);
            _ticker.Advise(problem);
            return;
        }

        (_cityFile, _savedText, _autosavedText) = (path, text, text);
        Logger.Info("Saved {Name} to {Path}", _simulation.Name, path);
        _ticker.Advise($"Saved {_simulation.Name} to {file}");
    }

    // The panel's x closes it before its title can start a drag, since the x sits in the title.
    private void HandleCityInput(Vector2 mouse)
    {
        switch (_cityPanel.Update(_simulation))
        {
            case CityPanel.Outcome.Renamed:
                _simulation.Rename(_cityPanel.Name);
                Logger.Info("The city is now called {Name}", _simulation.Name);
                return;
            case CityPanel.Outcome.Closed:
                CloseModal();
                return;
            case CityPanel.Outcome.EditScenario when _editing is { IsTesting: false }:
                // The city stays paused from the city panel to the details and back.
                _scenarioWindow.Open(_editing.Scenario, _editing.CanMoveStart(_simulation));
                _modal = Modal.Scenario;
                return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            TryBeginDrag(_cityPanel, mouse);
        }
    }

    // The details are taken whole when they read as a scenario, a moved start year moving the city's
    // founding with everything on it kept; then, or on Cancel, the city panel the window came from
    // opens again. A problem keeps the window open, saying why.
    private void HandleScenarioInput(Vector2 mouse)
    {
        switch (_scenarioWindow.Update())
        {
            case ScenarioWindow.Outcome.Cancel:
                BackToCityPanel();
                return;
            case ScenarioWindow.Outcome.Apply:
                var editing = _editing!;
                if (!editing.TryChange(_scenarioWindow.Details!, _simulation, out var changed, out var problem))
                {
                    _scenarioWindow.ShowProblem(problem);
                    return;
                }

                try
                {
                    editing.Accept(changed, _simulation);
                }
                catch (InvalidOperationException ex)
                {
                    _scenarioWindow.ShowProblem(ex.Message);
                    return;
                }

                _draft = Challenge.Begin(changed, _simulation);
                Logger.Info("Editing {Name}: its details changed", changed.Name);
                _ticker.Advise($"{changed.Name}: its details are applied");
                BackToCityPanel();
                return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            TryBeginDrag(_scenarioWindow, mouse);
        }
    }

    private void BackToCityPanel()
    {
        _cityPanel.Reset(_simulation, _editing is { IsTesting: false } ? _draft : null);
        _modal = Modal.City;
    }

    private bool TryBeginDrag(IMovableWindow window, Vector2 mouse)
    {
        if (!window.BeginDrag(mouse))
        {
            return false;
        }

        _dragging = window;
        return true;
    }

    // The reason is why the budget opened by itself, or null when the player opened it.
    private void OpenModal(Modal modal, string? budgetReason = null)
    {
        if (_modal != Modal.None)
        {
            return;
        }

        _modal = modal;
        _budgetReason = budgetReason;
        _speedBeforeModal = _settings.Speed;
        _settings = _settings with { Speed = 0 };
        _view.CancelStroke();
    }

    private void CloseModal()
    {
        _modal = Modal.None;
        _budgetReason = null;
        _settings = _settings with { Speed = _speedBeforeModal };
    }

    // A stroke starts where the button goes down, stretches while it is held, and is applied
    // when it is let go, as in Micropolis. The pointer leaving the map stops the stretch there.
    private void HandleStroke(Vector2 mouse)
    {
        var onMap = _view.TryGetTileAt(mouse, out var tileX, out var tileY);

        if (_draggingOverview)
        {
            _draggingOverview = Raylib.IsMouseButtonDown(MouseButton.Left);
            if (_draggingOverview)
            {
                _view.Overview.HandlePointer(mouse, _view.Camera, pressed: false);
            }

            return;
        }

        if (!_view.IsStroking)
        {
            if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                return;
            }

            // A press on the bar works its buttons, and neither it nor the ticker under it reaches the map.
            if (Hud.IsOnBottom(mouse))
            {
                PressBar(_hud.ButtonAt(mouse));
                return;
            }

            // A press on the help window, which sits over everything else, never reaches what is
            // under it: its x closes it, and its title moves it.
            if (_helpOpen && Raylib.CheckCollisionPointRec(mouse, _helpPanel.Bounds))
            {
                if (_helpPanel.IsOnClose(mouse))
                {
                    _helpOpen = false;
                }
                else
                {
                    TryBeginDrag(_helpPanel, mouse);
                }

                return;
            }

            // A press on the demand panel, which sits over everything else, never reaches what is
            // under it: its x closes it, and its title moves it.
            if (_demandOpen && Raylib.CheckCollisionPointRec(mouse, _demandPanel.Bounds))
            {
                if (_demandPanel.IsOnClose(mouse))
                {
                    _demandOpen = false;
                }
                else
                {
                    TryBeginDrag(_demandPanel, mouse);
                }

                return;
            }

            // A press on the overview picks a mode or moves the view, and never reaches the map.
            if (_view.Overview.Contains(mouse))
            {
                _draggingOverview = true;
                _view.Overview.HandlePointer(mouse, _view.Camera, pressed: true);
                return;
            }

            // A press on the palette picks a tool.
            if (_toolPaletteView.Contains(mouse))
            {
                var picked = _toolPaletteView.ToolAt(mouse);
                if (picked is not null)
                {
                    _tools.Toggle(picked.Value);
                    _view.CancelStroke();
                }

                return;
            }

            var tool = _tools.Selected is { } held ? ToolActions.ActionFor(held) : null;
            if (tool is not null && onMap)
            {
                _view.BeginStroke(tool.Value, tileX, tileY);
                _hintSeconds = 0;
            }

            return;
        }

        if (Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            if (onMap)
            {
                _view.StretchStroke(tileX, tileY);
            }

            return;
        }

        if (_view.TryFinishStroke(out var applied, out var result))
        {
            _hint = Describe(applied, result);
            _hintSeconds = HintSeconds;
        }
    }

    // What a stroke came to, beside the pointer; a scenario's editor builds for free, and says so.
    private string Describe(BuildAction action, BuildResult result) => result.Status switch
    {
        BuildStatus.Built when action == BuildAction.Bulldoze => $"Cleared {Count(result.TilesChanged, "tile")} {Price(result)}",
        BuildStatus.Built when action == BuildAction.Road => $"Built {Count(result.TilesChanged, "tile")} of road {Price(result)}",
        BuildStatus.Built when action == BuildAction.Rail => $"Laid {Count(result.TilesChanged, "tile")} of rail {Price(result)}",
        BuildStatus.Built when action == BuildAction.Park => $"Laid {Count(result.TilesChanged, "tile")} of park {Price(result)}",
        BuildStatus.Built when action == BuildAction.PowerLine => $"Strung {Count(result.TilesChanged, "tile")} of power line {Price(result)}",
        BuildStatus.Built when action == BuildAction.CoalPowerPlant => $"Built a coal power plant {Price(result)}",
        BuildStatus.Built when action == BuildAction.NuclearPowerPlant => $"Built a nuclear power plant {Price(result)}",
        BuildStatus.Built when action == BuildAction.FireStation => $"Built a fire station {Price(result)}",
        BuildStatus.Built when action == BuildAction.PoliceStation => $"Built a police station {Price(result)}",
        BuildStatus.Built when action == BuildAction.Stadium => $"Built a stadium {Price(result)}",
        BuildStatus.Built when action == BuildAction.Seaport => $"Built a seaport {Price(result)}",
        BuildStatus.Built when action == BuildAction.Airport => $"Built an airport {Price(result)}",
        BuildStatus.Built => $"Zoned {Count(result.TilesChanged / ZoneLayout.Parts, "zone")} {Price(result)}",
        BuildStatus.Unchanged when result.Reason == BuildRefusal.AlreadyDone => "The land is already like that",
        BuildStatus.Refused when result.Reason == BuildRefusal.PartOfAZone => "Bulldoze its centre to clear it",
        BuildStatus.Refused when result.Reason == BuildRefusal.OnFire => "It's on fire: wait for it to burn out",
        BuildStatus.Refused when result.Reason == BuildRefusal.Flooded => "It's under water: wait for it to drain",
        BuildStatus.Unchanged => "There was nothing to clear",
        BuildStatus.Refused when result.Reason == BuildRefusal.NotEnoughFunds => $"Not enough money, it costs ${result.Cost}",
        BuildStatus.Refused when result.Reason == BuildRefusal.CannotBuildHere => "That ground cannot be built on",
        BuildStatus.Refused when result.Reason == BuildRefusal.OffTheMap => "That runs off the map",
        _ => "The work was refused",
    };

    private string Price(BuildResult result) => _simulation.Builder.BuildsForFree ? "for free" : $"for ${result.Cost}";

    // A message from the city goes on the ticker as an advisory, and to the log. A disaster goes in
    // red the moment it starts, every time, since each one is news.
    private void ShowMessage(CityMessage message)
    {
        var text = CityMessages.Describe(message);
        if (CityMessages.IsDisaster(message))
        {
            _ticker.Post(new NewsItem(text, NewsKind.Disaster));
            Logger.Warn(text);
            return;
        }

        if (!_messages.ShouldPass(message, _simulation.CityTime))
        {
            return;
        }

        _ticker.Advise(text);
        Logger.Warn(text);
    }

    private static string Count(int count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";

    private void PressBar(Hud.BarButton button)
    {
        switch (button)
        {
            case Hud.BarButton.PausePlay:
                TogglePause();
                break;
            case Hud.BarButton.Demand:
                _demandOpen = !_demandOpen;
                break;
            case Hud.BarButton.Funds:
                OpenModal(Modal.Budget);
                break;
            case Hud.BarButton.Help:
                _helpOpen = !_helpOpen;
                break;
            case Hud.BarButton.CityName:
                _cityPanel.Reset(_simulation, _editing is { IsTesting: false } ? _draft : null);
                OpenModal(Modal.City);
                break;
            default:
                // A speed button sets that speed, and plays the game if it was paused.
                if (Hud.SpeedOf(button) is { } speed)
                {
                    _lastSpeed = speed;
                    _settings = _settings with { Speed = speed };
                }

                break;
        }
    }

    private int DemandPanelTop() => _toolPaletteView.Bottom() + 8;

    // The goals are judged on the challenge's last step; the pop-up says how it went, and pauses the
    // city until the player has read it. The city plays on after, won or lost.
    private void ShowChallengeResult(IReadOnlyList<GoalReport> goals)
    {
        var challenge = _simulation.Challenge!;
        var missed = goals.Where(goal => !goal.Met).Select(goal => goal.Text).ToList();
        var (title, message) = challenge.Won == true
            ? ("Challenge won!", $"{challenge.Name}: every goal was met by {challenge.Ends}. The city is yours to play on.")
            : ("Challenge lost", $"{challenge.Name}: by {challenge.Ends}, {missed.Count} of {goals.Count} goals weren't met: {string.Join("; ", missed)}. The city is yours to play on.");
        Logger.Info("{Title} {Message}", title, message);
        _ticker.Advise(title);
        _notice.Show(title, message);
        OpenModal(Modal.Notice);
    }

    // A city opened from here runs on the base game with the mods installed now, read afresh each
    // time, so a mod added since the game started counts; skipped mods are logged as they're read.
    private CityDefinitions InstalledDefinitions() => GameMods.Load(_folders.ModsFolder, out _);

    // The player's disaster switches decide which disasters happen on their own in the city.
    private void ApplyDisasters(GameSettings settings)
    {
        _simulation.RandomFires = settings.RandomFires;
        _simulation.RandomFloods = settings.RandomFloods;
        _simulation.RandomEarthquakes = settings.RandomEarthquakes;
    }

    // Whether the pointer is over the palette, the overview, the bar, the ticker, or an open window, rather than the map.
    private bool IsOverInterface(Vector2 mouse) =>
        Hud.IsOnBottom(mouse)
        || _toolPaletteView.Contains(mouse)
        || _view.Overview.Contains(mouse)
        || (_demandOpen && Raylib.CheckCollisionPointRec(mouse, _demandPanel.Bounds))
        || (_helpOpen && Raylib.CheckCollisionPointRec(mouse, _helpPanel.Bounds));

    private void TogglePause()
    {
        _settings = _settings.Speed == 0
            ? _settings with { Speed = _lastSpeed }
            : _settings with { Speed = 0 };
    }

    private void ChangeSpeed(int delta)
    {
        var speed = Math.Clamp(_settings.Speed + delta, GameSettings.MinSpeed, GameSettings.MaxSpeed);
        if (speed > 0)
        {
            _lastSpeed = speed;
        }

        _settings = _settings with { Speed = speed };
    }
}
