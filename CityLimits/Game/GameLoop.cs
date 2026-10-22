namespace CityLimits.Game;

/// <summary>The game: it owns the clock, the camera, the tools, and the city being simulated.</summary>
public sealed class GameLoop : IDisposable
{
    /// <summary>The seed the starting city is built from.</summary>
    public const int DefaultSeed = 2000;

    private const float PanSpeed = 600f;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly GameClock _clock = new();
    private readonly CitySimulation _simulation;
    private readonly Camera _camera = new();
    private readonly ToolPalette _tools = new();
    private readonly MapRenderer _mapRenderer;
    private readonly Hud _hud = new();
    private readonly ToolPaletteView _toolPaletteView;
    private readonly OverviewMap _overview;
    private readonly BudgetPanel _budgetPanel = new();
    private readonly DemandPanel _demandPanel = new();
    private GameSettings _settings;

    // Whether the press that is held began on the overview, so dragging there moves the view.
    private bool _draggingOverview;

    // The demand panel, opened from the meter in the top bar; the game keeps running under it.
    private bool _demandOpen;
    private int _lastSpeed = 1;
    private string? _error;
    private string? _notice;
    private bool _noticeIsError;
    private readonly CityMessageFilter _messages = new();

    // The budget, the menu, and the rename panel each hold the game while open, taking every key
    // and click, and give the speed back when they close.
    private Modal _modal;
    private string? _budgetReason;
    private int _speedBeforeModal;
    private readonly CityPanel _cityPanel = new();
    private readonly LoadPanel _loadPanel = new();
    private readonly ConfirmPanel _confirm = new();
    private readonly GameFolders _folders;

    // The file the city was opened from or last saved to, and the save it was then; a city whose
    // save reads differently has changes a save would keep.
    private string? _cityFile;
    private string? _savedText;

    // What the question on screen is about, and the city to open once it is answered.
    private Pending _pending;
    private CitySimulation? _cityToOpen;
    private string? _fileToOpen;

    // The window the player is dragging by its title, if any; it follows the pointer until the
    // button is let go, and nothing else takes the pointer meanwhile.
    private IMovableWindow? _dragging;
    private readonly BuildPlan _preview = new();
    private BuildStroke? _stroke;

    /// <summary>
    /// Plays a city: a new one, or one opened from a file, which is then the file Ctrl+S saves it
    /// back to without asking. The mods skipped when a new city was made are shown, if any.
    /// </summary>
    public GameLoop(GameSettings settings, CitySimulation city, string? cityFile, GameFolders folders, GameAssets assets, IReadOnlyList<string>? modProblems = null)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(folders);
        _settings = settings;
        _lastSpeed = settings.Speed == 0 ? 1 : settings.Speed;
        _folders = folders;
        _simulation = city;
        _cityFile = cityFile;

        // An opened city starts saved; a new one has nothing saved yet.
        _savedText = cityFile is null ? null : FlatCityFormat.Write(city);
        var map = city.Map;
        var definitions = city.Definitions;
        modProblems ??= [];
        if (modProblems.Count > 0)
        {
            // The log holds every skipped mod; the screen says how many, so the player knows to look.
            _notice = modProblems.Count == 1 ? modProblems[0] : $"{modProblems.Count} mods were skipped; the log says why";
            _noticeIsError = true;
        }

        _simulation.MessageSent += ShowMessage;
        _simulation.YearSettled += OnYearSettled;

        _mapRenderer = new MapRenderer(map, assets);
        Logger.Info(_mapRenderer.IsCached ? "The map is drawn from cached chunks" : "The map is drawn tile by tile");
        _toolPaletteView = new ToolPaletteView(assets, definitions);
        _overview = new OverviewMap(_simulation);

        _camera.SetScreenSize(settings.WindowWidth, settings.WindowHeight);
        _camera.CenterOn(new Vector2(map.Width * MapRenderer.TileSize / 2f, map.Height * MapRenderer.TileSize / 2f));
    }

    /// <summary>The city being simulated.</summary>
    public CitySimulation Simulation => _simulation;

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

    // What holds the game while it is open.
    private enum Modal
    {
        None,
        Budget,
        Menu,
        City,
        Load,
        Confirm,
    }

    // What a question asks before it goes ahead.
    private enum Pending
    {
        None,
        Overwrite,
        LeaveToMenu,
        LeaveToQuit,
        OpenCity,
    }

    /// <summary>Whether, and where, the player asked to leave the city.</summary>
    public Leave Leaving { get; private set; }

    /// <summary>
    /// The settings as the game stands: the window's size, and the speed the city was running at,
    /// even while a panel holds it paused.
    /// </summary>
    public GameSettings Settings
    {
        get
        {
            var width = Math.Clamp(Raylib.GetScreenWidth(), GameSettings.MinWindowWidth, GameSettings.MaxWindowWidth);
            var height = Math.Clamp(Raylib.GetScreenHeight(), GameSettings.MinWindowHeight, GameSettings.MaxWindowHeight);
            var speed = _modal == Modal.None ? _settings.Speed : _speedBeforeModal;
            return _settings with { WindowWidth = width, WindowHeight = height, Speed = speed };
        }
    }

    /// <summary>Reads the input and advances the city.</summary>
    public void Update()
    {
        var elapsed = Raylib.GetFrameTime();
        _camera.SetScreenSize(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        _demandPanel.DefaultTop = DemandPanelTop();

        HandleInput(elapsed);

        if (_settings.Speed > 0)
        {
            var due = _clock.Advance(elapsed * GameSpeed.Multiplier(_settings.Speed));
            // A year that opens the budget stops the city at once, even partway through a frame's steps.
            for (var i = 0; i < due && _modal == Modal.None; i++)
            {
                _simulation.Tick();
            }
        }

        _simulation.Roads.EnsureCurrent(_simulation.Map);

        // The preview is worked out every frame, so it keeps up with the city growing under it.
        if (_stroke is not null)
        {
            _simulation.Builder.Plan(_stroke, _preview);
        }
        _camera.ClampTo(_simulation.Map.Width * MapRenderer.TileSize, _simulation.Map.Height * MapRenderer.TileSize);
    }

    /// <summary>Draws the game.</summary>
    public void Draw()
    {
        // Chunks and the overview are redrawn into their own textures, which can't happen inside the frame's camera.
        _mapRenderer.Refresh();
        _overview.Refresh();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color { R = 12, G = 14, B = 18, A = 255 });

        var mouse = Raylib.GetMousePosition();

        Raylib.BeginMode2D(_camera.ToRaylib());
        _mapRenderer.Draw(_camera);
        _mapRenderer.DrawUnpowered(_camera, Raylib.GetTime());
        if (_stroke is not null)
        {
            _mapRenderer.DrawPreview(_preview, _camera);
        }
        else
        {
            _mapRenderer.DrawHover(_camera, mouse);
        }

        Raylib.EndMode2D();

        _toolPaletteView.Draw(_tools, mouse);

        var world = _camera.ScreenToWorld(mouse);
        var onMap = MapRenderer.TryGetTileAt(_simulation.Map, world, out var tileX, out var tileY, out var state);

        _hud.DrawStatus(
            _simulation.Name,
            _simulation.Date,
            _simulation.TickCount,
            _settings.Speed,
            _lastSpeed,
            _simulation.Census.CityPopulation,
            _simulation.Demand,
            _simulation.Treasury.Funds,
            Raylib.GetFPS());
        _hud.DrawTileReadout(_simulation.Map, onMap ? state : null, tileX, tileY, _simulation.Definitions);
        _overview.Draw(_camera);
        _hud.DrawHints();
        _hud.DrawMessage(_error ?? _notice, _error is not null || _noticeIsError);

        // Windows go over the rest of the interface, wherever the player has moved them.
        if (_demandOpen)
        {
            _demandPanel.Draw(_simulation);
        }

        if (_stroke is not null)
        {
            _hud.DrawStrokeCost(mouse, _preview);
        }

        switch (_modal)
        {
            case Modal.Budget:
                _budgetPanel.Draw(_simulation, _budgetReason);
                break;
            case Modal.Menu:
                GameMenu.Draw();
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
        }

        Raylib.EndDrawing();
    }

    /// <summary>Releases what the game holds on the graphics card. Call it before the window closes.</summary>
    public void Dispose()
    {
        _overview.Dispose();
        _mapRenderer.Dispose();
    }

    /// <summary>Records a failure so the player can see it, and keeps the game running.</summary>
    public void ReportError(Exception exception)
    {
        _error = exception.Message;
        Logger.Error(exception, "The frame failed");
    }

    private void HandleInput(float elapsed)
    {
        var mouse = Raylib.GetMousePosition();
        if (Raylib.IsWindowResized())
        {
            _settings = _settings with { WindowWidth = Raylib.GetScreenWidth(), WindowHeight = Raylib.GetScreenHeight() };
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
        }

        // Ctrl+S and Ctrl+O save and open; held Control keeps the letters from panning the map.
        var control = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        if (control && Raylib.IsKeyPressed(KeyboardKey.S))
        {
            Save(confirmed: false);
            return;
        }

        if (control && Raylib.IsKeyPressed(KeyboardKey.O))
        {
            _loadPanel.Reset(_folders.CitiesFolder);
            OpenModal(Modal.Load);
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.B))
        {
            OpenModal(Modal.Budget);
            return;
        }

        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            _camera.ZoomAt(mouse, wheel);
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
            _camera.Pan(pan * (PanSpeed * elapsed / _camera.Zoom));
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
            if (_stroke is not null)
            {
                _stroke = null;
                _notice = null;
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

        foreach (var key in ToolPaletteView.ToolKeys)
        {
            if (!Raylib.IsKeyPressed(key))
            {
                continue;
            }

            var tool = ToolPaletteView.ToolForKey(key);
            if (tool is not null)
            {
                _tools.Select(tool.Value);
                _stroke = null;
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
        switch (GameMenu.Update())
        {
            case GameMenu.Choice.Resume:
                CloseModal();
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

    private void HandleLoadInput()
    {
        switch (_loadPanel.Update())
        {
            case LoadPanel.Outcome.Opened:
                CloseModal();
                (_cityToOpen, _fileToOpen) = (_loadPanel.City, _loadPanel.Path);
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
                Save(confirmed: true);
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
                (OpenedCity, OpenedFile) = (_cityToOpen, _fileToOpen);
                Leaving = Leave.Open;
                break;
        }
    }

    private void Ask(Pending pending, string title, string message, string yes, string no)
    {
        _pending = pending;
        _confirm.Ask(title, message, yes, no);
        OpenModal(Modal.Confirm);
    }

    private bool HasUnsavedChanges() => _savedText is null || FlatCityFormat.Write(_simulation) != _savedText;

    // A city is saved under its name, in the binary format unless it came from a flat file. Saving
    // over its own file is plain saving; replacing another city's file of the same name asks first.
    private void Save(bool confirmed)
    {
        var flat = _cityFile is not null && Path.GetExtension(_cityFile).Equals(FlatCityFormat.Extension, StringComparison.OrdinalIgnoreCase);
        var file = CityFiles.FileNameFor(_simulation.Name, flat ? FlatCityFormat.Extension : BinaryCityFormat.Extension);
        var path = Path.Combine(_folders.CitiesFolder, file);
        if (!confirmed && File.Exists(path) && !string.Equals(Path.GetFullPath(path), _cityFile is null ? null : Path.GetFullPath(_cityFile), StringComparison.Ordinal))
        {
            Ask(Pending.Overwrite, "Replace a saved city?", $"{file} already holds a saved city, and saving would replace it.", "Replace", "Cancel");
            return;
        }

        // The flat text is what later saves are compared with, whichever format the file is in.
        var text = FlatCityFormat.Write(_simulation);
        if (!CityFiles.TryWrite(_simulation, path, out var problem))
        {
            Logger.Error("{Problem}", problem);
            (_notice, _noticeIsError) = (problem, true);
            return;
        }

        (_cityFile, _savedText) = (path, text);
        Logger.Info("Saved {Name} to {Path}", _simulation.Name, path);
        (_notice, _noticeIsError) = ($"Saved {_simulation.Name} to {file}", false);
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
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            TryBeginDrag(_cityPanel, mouse);
        }
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
        _stroke = null;
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
        var onMap = MapRenderer.TryGetTileAt(_simulation.Map, _camera.ScreenToWorld(mouse), out var tileX, out var tileY, out _);

        if (_draggingOverview)
        {
            _draggingOverview = Raylib.IsMouseButtonDown(MouseButton.Left);
            if (_draggingOverview)
            {
                _overview.HandlePointer(mouse, _camera, pressed: false);
            }

            return;
        }

        if (_stroke is null)
        {
            if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                return;
            }

            // A press on the top bar works its buttons, and never reaches the map under it.
            if (mouse.Y < Hud.BarHeight)
            {
                PressBar(_hud.ButtonAt(mouse));
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
            if (_overview.Contains(mouse))
            {
                _draggingOverview = true;
                _overview.HandlePointer(mouse, _camera, pressed: true);
                return;
            }

            // A press on the palette picks a tool.
            if (_toolPaletteView.Contains(mouse))
            {
                var picked = _toolPaletteView.ToolAt(mouse);
                if (picked is not null)
                {
                    _tools.Select(picked.Value);
                }

                return;
            }

            var tool = ToolActions.ActionFor(_tools.Selected);
            if (tool is not null && onMap)
            {
                _stroke = new BuildStroke(tool.Value, tileX, tileY);
                _simulation.Builder.Plan(_stroke, _preview);
                _notice = null;
            }

            return;
        }

        if (Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            if (onMap)
            {
                _stroke.DragTo(tileX, tileY);
            }

            return;
        }

        var result = _simulation.Builder.Apply(_stroke);
        var applied = _stroke.Action;
        _stroke = null;
        _noticeIsError = result.Status == BuildStatus.Refused;
        _notice = Describe(applied, result);
    }

    private static string Describe(BuildAction action, BuildResult result) => result.Status switch
    {
        BuildStatus.Built when action == BuildAction.Bulldoze => $"Cleared {Count(result.TilesChanged, "tile")} for ${result.Cost}",
        BuildStatus.Built when action == BuildAction.Road => $"Built {Count(result.TilesChanged, "tile")} of road for ${result.Cost}",
        BuildStatus.Built when action == BuildAction.Park => $"Laid {Count(result.TilesChanged, "tile")} of park for ${result.Cost}",
        BuildStatus.Built when action == BuildAction.PowerLine => $"Strung {Count(result.TilesChanged, "tile")} of power line for ${result.Cost}",
        BuildStatus.Built when action == BuildAction.CoalPowerPlant => $"Built a coal power plant for ${result.Cost}",
        BuildStatus.Built when action == BuildAction.NuclearPowerPlant => $"Built a nuclear power plant for ${result.Cost}",
        BuildStatus.Built when action == BuildAction.FireStation => $"Built a fire station for ${result.Cost}",
        BuildStatus.Built when action == BuildAction.PoliceStation => $"Built a police station for ${result.Cost}",
        BuildStatus.Built => $"Zoned {Count(result.TilesChanged / ZoneLayout.Parts, "zone")} for ${result.Cost}",
        BuildStatus.Unchanged when result.Reason == BuildRefusal.AlreadyDone => "The land is already like that",
        BuildStatus.Refused when result.Reason == BuildRefusal.PartOfAZone => "Bulldoze its centre to clear it",
        BuildStatus.Unchanged => "There was nothing to clear",
        BuildStatus.Refused when result.Reason == BuildRefusal.NotEnoughFunds => $"Not enough money, it costs ${result.Cost}",
        BuildStatus.Refused when result.Reason == BuildRefusal.CannotBuildHere => "That ground cannot be built on",
        BuildStatus.Refused when result.Reason == BuildRefusal.OffTheMap => "That runs off the map",
        _ => "The work was refused",
    };

    // A message from the city takes the notice line, as Micropolis shows it, and goes to the log.
    private void ShowMessage(CityMessage message)
    {
        if (!_messages.ShouldPass(message, _simulation.CityTime))
        {
            return;
        }

        _notice = CityMessages.Describe(message);
        _noticeIsError = true;
        Logger.Warn(_notice);
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
            case Hud.BarButton.CityName:
                _cityPanel.Reset(_simulation);
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
