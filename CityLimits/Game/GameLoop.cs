namespace CityLimits.Game;

/// <summary>The game: it owns the clock, the camera, the tools, and the city being simulated.</summary>
public sealed class GameLoop
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
    private int _lastTileX = -1;
    private int _lastTileY = -1;
    private bool _painting;

    /// <summary>Creates the game with the settings it was started with.</summary>
    public GameLoop(GameSettings settings, string settingsPath, GameAssets assets, int seed = DefaultSeed)
    {
        _settings = settings;
        _lastSpeed = settings.Speed == 0 ? 1 : settings.Speed;
        _settingsPath = settingsPath;

        var map = TileMapGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
        _simulation = new CitySimulation(map, seed);

        _mapRenderer = new MapRenderer(assets);
        _toolPaletteView = new ToolPaletteView(assets);

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
            var due = _clock.Advance(elapsed * _settings.Speed);
            for (var i = 0; i < due; i++)
            {
                _simulation.Tick();
            }
        }

        _simulation.Roads.EnsureCurrent(_simulation.Map);
        _camera.ClampTo(MapSize.DefaultWidth * MapRenderer.TileSize, MapSize.DefaultHeight * MapRenderer.TileSize);
    }

    /// <summary>Draws the game.</summary>
    public void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color { R = 12, G = 14, B = 18, A = 255 });

        var mouse = Raylib.GetMousePosition();

        Raylib.BeginMode2D(_camera.ToRaylib());
        _mapRenderer.Draw(_simulation.Map, _camera);
        _mapRenderer.DrawHover(_simulation.Map, _camera, mouse);
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
        _hud.DrawTileReadout(onMap ? state : null, tileX, tileY);
        _hud.DrawHints();
        _hud.DrawMessage(_error ?? _notice, _error is not null || _noticeIsError);

        Raylib.EndDrawing();
    }

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
            ShouldClose = true;
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
                _painting = false;
            }
        }

        HandlePainting(mouse);

        if (Raylib.IsWindowResized())
        {
            _settings = _settings with { WindowWidth = Raylib.GetScreenWidth(), WindowHeight = Raylib.GetScreenHeight() };
        }
    }

    private void HandlePainting(Vector2 mouse)
    {
        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            _painting = false;
            _lastTileX = -1;
            _lastTileY = -1;
            return;
        }

        var world = _camera.ScreenToWorld(mouse);
        if (!MapRenderer.TryGetTileAt(_simulation.Map, world, out var tileX, out var tileY, out _))
        {
            return;
        }

        if (_painting && tileX == _lastTileX && tileY == _lastTileY)
        {
            return;
        }

        _painting = true;
        _lastTileX = tileX;
        _lastTileY = tileY;
        ApplyTool(tileX, tileY);
    }

    private void ApplyTool(int tileX, int tileY)
    {
        var action = ToolActions.ActionFor(_tools.Selected);
        if (action is null)
        {
            _notice = null;
            return;
        }

        var result = _simulation.Builder.Apply(action.Value, tileX, tileY);
        _noticeIsError = result.Status == BuildStatus.Refused;
        _notice = Describe(result);
    }

    private static string Describe(BuildResult result) => result.Status switch
    {
        BuildStatus.Built when result.Refund > 0 => $"Cleared the tile, ${result.Refund} back",
        BuildStatus.Built => "Done",
        BuildStatus.Unchanged when result.Reason == BuildRefusal.AlreadyDone => "The tile is already like that",
        BuildStatus.Unchanged => "There was nothing to clear",
        BuildStatus.Refused when result.Reason == BuildRefusal.NotEnoughFunds => $"Not enough money, it costs ${result.Cost}",
        BuildStatus.Refused when result.Reason == BuildRefusal.CannotBuildHere => "That ground cannot be built on",
        BuildStatus.Refused when result.Reason == BuildRefusal.OffTheMap => "That tile is off the map",
        _ => "The work was refused",
    };

    private int CostOfSelectedTool()
    {
        var action = ToolActions.ActionFor(_tools.Selected);
        return action is null ? 0 : TileCosts.CostFor(action.Value);
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
