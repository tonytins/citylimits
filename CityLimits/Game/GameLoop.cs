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
            for (var i = 0; i < due; i++)
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
        if (_stroke is not null)
        {
            _mapRenderer.DrawPreview(_preview, _camera);
        }
        else
        {
            _mapRenderer.DrawHover(_camera, mouse);
        }

        Raylib.EndMode2D();

        _toolPaletteView.Draw(_tools);

        var world = _camera.ScreenToWorld(mouse);
        var onMap = MapRenderer.TryGetTileAt(_simulation.Map, world, out var tileX, out var tileY, out var state);

        _hud.DrawStatus(
            _simulation.Date,
            _simulation.TickCount,
            _settings.Speed,
            _tools.Selected,
            _simulation.Treasury.Funds,
            CostOfSelectedTool(),
            world,
            Raylib.GetFPS());
        _hud.DrawTileReadout(_simulation.Map, onMap ? state : null, tileX, tileY, _simulation.Definitions);
        _hud.DrawHints();
        _hud.DrawMessage(_error ?? _notice, _error is not null || _noticeIsError);

        if (_stroke is not null)
        {
            _hud.DrawStrokeCost(mouse, _preview);
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

        if (!SettingsStore.TrySave(_settingsPath, _settings, out var error))
        {
            Logger.Error("{Error}", error);
            return false;
        }

        return true;
    }

    private void HandleInput(float elapsed)
    {
        var mouse = Raylib.GetMousePosition();
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

        if (Raylib.IsWindowResized())
        {
            _settings = _settings with { WindowWidth = Raylib.GetScreenWidth(), WindowHeight = Raylib.GetScreenHeight() };
        }
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

            // A press on the palette picks a tool; it never reaches the map underneath.
            if (_toolPaletteView.Contains(_tools, mouse))
            {
                var picked = _toolPaletteView.ToolAt(_tools, mouse);
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
        BuildStatus.Built => $"Zoned {Count(result.TilesChanged / ZoneLayout.Parts, "zone")} for ${result.Cost}",
        BuildStatus.Unchanged when result.Reason == BuildRefusal.AlreadyDone => "The land is already like that",
        BuildStatus.Refused when result.Reason == BuildRefusal.PartOfAZone => "Bulldoze a zone's centre to clear it",
        BuildStatus.Unchanged => "There was nothing to clear",
        BuildStatus.Refused when result.Reason == BuildRefusal.NotEnoughFunds => $"Not enough money, it costs ${result.Cost}",
        BuildStatus.Refused when result.Reason == BuildRefusal.CannotBuildHere => "That ground cannot be built on",
        BuildStatus.Refused when result.Reason == BuildRefusal.OffTheMap => "That runs off the map",
        _ => "The work was refused",
    };

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
