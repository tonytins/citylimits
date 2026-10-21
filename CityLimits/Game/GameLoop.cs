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
    private readonly string _settingsPath;
    private GameSettings _settings;
    private int _lastSpeed = 1;
    private string? _error;
    private string? _notice;
    private bool _noticeIsError;
    private readonly CityMessageFilter _messages = new();

    // The budget panel holds the game while it is open, and gives the speed back when it closes.
    private bool _budgetOpen;
    private string? _budgetReason;
    private int _speedBeforeBudget;
    private readonly BuildPlan _preview = new();
    private BuildStroke? _stroke;

    /// <summary>Creates the game with the settings it was started with.</summary>
    public GameLoop(GameSettings settings, string settingsPath, GameAssets assets, int seed = DefaultSeed)
    {
        _settings = settings;
        _lastSpeed = settings.Speed == 0 ? 1 : settings.Speed;
        _settingsPath = settingsPath;

        var map = TileMapGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
        var definitions = GameMods.Load(out var modProblems);
        _simulation = new CitySimulation(map, seed, definitions: definitions);
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

        _camera.SetScreenSize(settings.WindowWidth, settings.WindowHeight);
        _camera.CenterOn(new Vector2(map.Width * MapRenderer.TileSize / 2f, map.Height * MapRenderer.TileSize / 2f));
    }

    /// <summary>The city being simulated.</summary>
    public CitySimulation Simulation => _simulation;

    /// <summary>The speed the game is running at, where zero is paused.</summary>
    public int Speed => _settings.Speed;

    /// <summary>Tells whether the player asked to leave the game.</summary>
    public bool ShouldClose { get; private set; }

    /// <summary>Reads the input and advances the city.</summary>
    public void Update()
    {
        var elapsed = Raylib.GetFrameTime();
        _camera.SetScreenSize(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

        HandleInput(elapsed);

        if (_settings.Speed > 0)
        {
            var due = _clock.Advance(elapsed * GameSpeed.Multiplier(_settings.Speed));
            // A year that opens the budget stops the city at once, even partway through a frame's steps.
            for (var i = 0; i < due && !_budgetOpen; i++)
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
        _camera.ClampTo(MapSize.DefaultWidth * MapRenderer.TileSize, MapSize.DefaultHeight * MapRenderer.TileSize);
    }

    /// <summary>Draws the game.</summary>
    public void Draw()
    {
        // Chunks are redrawn into their own textures, which can't happen inside the frame's camera.
        _mapRenderer.Refresh();

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
            _simulation.Date,
            _simulation.TickCount,
            _settings.Speed,
            _simulation.Census.CityPopulation,
            _tools.Selected,
            _simulation.Treasury.Funds,
            CostOfSelectedTool(),
            world,
            Raylib.GetFPS());
        _hud.DrawTileReadout(_simulation.Map, onMap ? state : null, tileX, tileY, _simulation.Definitions);
        _hud.DrawDemand(_simulation.Demand, _toolPaletteView.Bottom() + 8);
        _hud.DrawHints();
        _hud.DrawMessage(_error ?? _notice, _error is not null || _noticeIsError);

        if (_stroke is not null)
        {
            _hud.DrawStrokeCost(mouse, _preview);
        }

        if (_budgetOpen)
        {
            BudgetPanel.Draw(_simulation, _budgetReason);
        }

        Raylib.EndDrawing();
    }

    /// <summary>Releases what the game holds on the graphics card. Call it before the window closes.</summary>
    public void Dispose() => _mapRenderer.Dispose();

    /// <summary>Records a failure so the player can see it, and keeps the game running.</summary>
    public void ReportError(Exception exception)
    {
        _error = exception.Message;
        Logger.Error(exception, "The frame failed");
    }

    /// <summary>Writes the settings the game is ending with.</summary>
    public bool SaveSettings()
    {
        var width = Math.Clamp(Raylib.GetScreenWidth(), GameSettings.MinWindowWidth, GameSettings.MaxWindowWidth);
        var height = Math.Clamp(Raylib.GetScreenHeight(), GameSettings.MinWindowHeight, GameSettings.MaxWindowHeight);
        _settings = _settings with { WindowWidth = width, WindowHeight = height };
        var saved = _budgetOpen ? _settings with { Speed = _speedBeforeBudget } : _settings;

        if (!SettingsStore.TrySave(_settingsPath, saved, out var error))
        {
            Logger.Error("{Error}", error);
            return false;
        }

        return true;
    }

    private void HandleInput(float elapsed)
    {
        var mouse = Raylib.GetMousePosition();
        if (Raylib.IsWindowResized())
        {
            _settings = _settings with { WindowWidth = Raylib.GetScreenWidth(), WindowHeight = Raylib.GetScreenHeight() };
        }

        // While the budget is open, it takes every key and click, and the map takes none.
        if (_budgetOpen)
        {
            HandleBudgetInput(mouse);
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.B))
        {
            OpenBudget(null);
            return;
        }

        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            _camera.ZoomAt(mouse, wheel);
        }

        var pan = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left))
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
            else
            {
                ShouldClose = true;
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
            CloseBudget();
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && BudgetPanel.HandleClick(mouse, _simulation.Budget) == BudgetPanel.Click.Close)
        {
            CloseBudget();
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
        OpenBudget(reason);
    }

    private void OpenBudget(string? reason)
    {
        if (_budgetOpen)
        {
            return;
        }

        _budgetOpen = true;
        _budgetReason = reason;
        _speedBeforeBudget = _settings.Speed;
        _settings = _settings with { Speed = 0 };
        _stroke = null;
    }

    private void CloseBudget()
    {
        _budgetOpen = false;
        _budgetReason = null;
        _settings = _settings with { Speed = _speedBeforeBudget };
    }

    // A stroke starts where the button goes down, stretches while it is held, and is applied
    // when it is let go, as in Micropolis. The pointer leaving the map stops the stretch there.
    private void HandleStroke(Vector2 mouse)
    {
        var onMap = MapRenderer.TryGetTileAt(_simulation.Map, _camera.ScreenToWorld(mouse), out var tileX, out var tileY, out _);

        if (_stroke is null)
        {
            if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                return;
            }

            // A press on the funds opens the budget.
            if (Raylib.CheckCollisionPointRec(mouse, _hud.FundsBounds))
            {
                OpenBudget(null);
                return;
            }

            // A press on a panel never reaches the map underneath.
            if (Raylib.CheckCollisionPointRec(mouse, Hud.DemandBounds(_toolPaletteView.Bottom() + 8)))
            {
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

    private int CostOfSelectedTool()
    {
        var action = ToolActions.ActionFor(_tools.Selected);
        return action is null ? 0 : _simulation.Definitions.CostOf(action.Value);
    }

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
