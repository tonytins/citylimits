namespace CityLimits.UI;

/// <summary>
/// The city as it is seen and worked on: the camera over the map, the map's drawing and the
/// overview, what moves on it (traffic, the train, fires, explosions, and an earthquake's shake),
/// and the stroke being dragged with its preview. Playing a city and editing a scenario share it,
/// so both look and build alike; what the player's input means, and the windows over the map,
/// stay with whoever owns the view. It only reads the city, except to apply a finished stroke
/// through the city's builder.
/// </summary>
public sealed class CityView : IDisposable
{
    private const float PanSpeed = 600f;

    // The fires' animation frame, and the explosions playing: where, and how many animation steps
    // each has run. An explosion moves on a frame every other step, as Micropolis plays its own,
    // and the few at once there could be share a buffer, so a frame never allocates.
    private const int MostExplosions = 8;
    private const int StepsPerExplosionFrame = 2;

    // An earthquake shakes the map's drawing from side to side for a moment, as the Java port does;
    // the city itself isn't moved.
    private const float ShakeSeconds = 1.5f;
    private const float ShakePixels = 4f;
    private const float ShakesPerSecond = 18f;

    private readonly CitySimulation _city;
    private readonly GameAssets _assets;
    private readonly MapRenderer _mapRenderer;
    private readonly TrafficAnimation _trafficAnimation = new();
    private readonly (int X, int Y, int Steps)[] _explosions = new (int, int, int)[MostExplosions];
    private int _explosionCount;
    private int _fireFrame;
    private float _shakeLeft;

    // The train runs at the traffic's pace, with a generator of its own, so it never changes the city.
    private readonly Train _train = new();
    private readonly Random _trainRandom = new();

    private BuildStroke? _stroke;

    /// <summary>Shows a city on a screen of the size given, centred on the middle of its map.</summary>
    public CityView(CitySimulation city, GameAssets assets, int screenWidth, int screenHeight)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(assets);
        _city = city;
        _assets = assets;
        _mapRenderer = new MapRenderer(city.Map, assets, city.Traffic, city.Fires, city.Floods, city.Radiation);
        Overview = new OverviewMap(city);
        city.Exploded += ShowExplosion;
        city.EarthquakeStruck += Shake;

        Camera.SetScreenSize(screenWidth, screenHeight);
        Camera.CenterOn(new Vector2(city.Map.Width * MapRenderer.TileSize / 2f, city.Map.Height * MapRenderer.TileSize / 2f));
    }

    /// <summary>Where the view looks, and how near.</summary>
    public Camera Camera { get; } = new();

    /// <summary>The small map of the whole city, in the corner.</summary>
    public OverviewMap Overview { get; }

    /// <summary>Tells whether the map is drawn from cached chunks, rather than tile by tile.</summary>
    public bool IsCached => _mapRenderer.IsCached;

    /// <summary>Tells whether a stroke is being dragged.</summary>
    public bool IsStroking => _stroke is not null;

    /// <summary>What the stroke being dragged would build, and cost, as of this frame.</summary>
    public BuildPlan Preview { get; } = new();

    /// <summary>
    /// Moves what moves on the map on by a frame: the traffic and the train at the speed given
    /// (paused, they stand still), and an earthquake's shake, which runs out whatever the speed.
    /// </summary>
    public void Animate(float elapsed, int speed)
    {
        _city.Roads.EnsureCurrent(_city.Map);
        var steps = _trafficAnimation.Advance(elapsed, speed);
        for (var i = 0; i < steps; i++)
        {
            _train.Step(_city.Map, _city.Demand.TaxablePopulation, _trainRandom);
        }

        _fireFrame = (_fireFrame + steps) % TileAtlas.FireFrames;
        AdvanceExplosions(steps);
        _shakeLeft = Math.Max(0f, _shakeLeft - elapsed);
    }

    /// <summary>Fits the camera to the window as it is this frame. Call it before the input is read.</summary>
    public void FitScreen() => Camera.SetScreenSize(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    /// <summary>
    /// Keeps the view in step with the city, once a frame after the input and the city's steps:
    /// the stroke's preview keeps up with the city growing under it, and the camera stays over the map.
    /// </summary>
    public void Update()
    {
        if (_stroke is not null)
        {
            _city.Builder.Plan(_stroke, Preview);
        }

        Camera.ClampTo(_city.Map.Width * MapRenderer.TileSize, _city.Map.Height * MapRenderer.TileSize);
    }

    /// <summary>Looks at a place in the world from the nearness given, as another view of the same map was.</summary>
    public void LookAt(Vector2 target, float zoom)
    {
        Camera.CenterOn(target);
        Camera.SetZoom(zoom);
    }

    /// <summary>Zooms in or out by the wheel's turn, keeping the point under the pointer still.</summary>
    public void ZoomAt(Vector2 screen, float wheel) => Camera.ZoomAt(screen, wheel);

    /// <summary>Pans the view for a frame in a direction, at the same speed on screen whatever the zoom.</summary>
    public void Pan(Vector2 direction, float elapsed) => Camera.Pan(direction * (PanSpeed * elapsed / Camera.Zoom));

    /// <summary>Finds the tile under a point on the screen; false when the point is off the map.</summary>
    public bool TryGetTileAt(Vector2 screen, out int tileX, out int tileY) =>
        MapRenderer.TryGetTileAt(_city.Map, Camera.ScreenToWorld(screen), out tileX, out tileY, out _);

    /// <summary>Starts a stroke of a tool on a tile, with its preview worked out at once.</summary>
    public void BeginStroke(BuildAction action, int tileX, int tileY)
    {
        _stroke = new BuildStroke(action, tileX, tileY);
        _city.Builder.Plan(_stroke, Preview);
    }

    /// <summary>Stretches the stroke being dragged to a tile; nothing happens with none.</summary>
    public void StretchStroke(int tileX, int tileY) => _stroke?.DragTo(tileX, tileY);

    /// <summary>Drops the stroke being dragged, building nothing.</summary>
    public void CancelStroke() => _stroke = null;

    /// <summary>
    /// Applies the stroke being dragged to the city, all or nothing, and says what it came to and
    /// which tool it was; false when no stroke was being dragged.
    /// </summary>
    public bool TryFinishStroke(out BuildAction action, out BuildResult result)
    {
        if (_stroke is null)
        {
            action = default;
            result = default;
            return false;
        }

        action = _stroke.Action;
        result = _city.Builder.Apply(_stroke);
        _stroke = null;
        return true;
    }

    /// <summary>
    /// Redraws the map's changed chunks and the overview into their textures. Call it before
    /// <see cref="Raylib.BeginDrawing"/>, since a texture can't be drawn into inside the frame.
    /// </summary>
    public void Refresh()
    {
        _mapRenderer.Refresh();
        Overview.Refresh();
    }

    /// <summary>
    /// Draws the map through the camera, with what moves on it, the power warnings, and, under the
    /// pointer, the stroke's preview or the hovered tile's outline.
    /// </summary>
    public void DrawMap(Vector2 mouse)
    {
        var view = Camera.ToRaylib();
        if (_shakeLeft > 0f)
        {
            var sway = MathF.Sin(_shakeLeft * ShakesPerSecond * MathF.Tau) * ShakePixels * (_shakeLeft / ShakeSeconds);
            view.Offset = view.Offset with { X = view.Offset.X + sway };
        }

        Raylib.BeginMode2D(view);
        _mapRenderer.Draw(Camera, _trafficAnimation.Frame, _fireFrame);
        _mapRenderer.DrawStadiumGames(Camera, _city.CityTime, _fireFrame);
        DrawTrain();
        DrawVehicles();
        for (var i = 0; i < _explosionCount; i++)
        {
            _mapRenderer.DrawExplosion(_explosions[i].X, _explosions[i].Y, _explosions[i].Steps / StepsPerExplosionFrame);
        }

        _mapRenderer.DrawUnpowered(Camera, Raylib.GetTime());
        if (_stroke is not null)
        {
            _mapRenderer.DrawPreview(Preview, Camera);
        }
        else
        {
            _mapRenderer.DrawHover(Camera, mouse);
        }

        Raylib.EndMode2D();
    }

    /// <summary>Draws the overview, with the part of the map in view outlined.</summary>
    public void DrawOverview() => Overview.Draw(Camera);

    /// <summary>Lets go of the city's events, and of what the view holds on the graphics card.</summary>
    public void Dispose()
    {
        _city.Exploded -= ShowExplosion;
        _city.EarthquakeStruck -= Shake;
        Overview.Dispose();
        _mapRenderer.Dispose();
    }

    private void Shake() => _shakeLeft = ShakeSeconds;

    // A new explosion takes the oldest one's place when the buffer is full.
    private void ShowExplosion(int x, int y)
    {
        if (_explosionCount == MostExplosions)
        {
            Array.Copy(_explosions, 1, _explosions, 0, MostExplosions - 1);
            _explosionCount--;
        }

        _explosions[_explosionCount++] = (x, y, 0);
    }

    private void AdvanceExplosions(int steps)
    {
        var kept = 0;
        for (var i = 0; i < _explosionCount; i++)
        {
            var explosion = _explosions[i] with { Steps = _explosions[i].Steps + steps };
            if (explosion.Steps < TileAtlas.ExplosionFrames * StepsPerExplosionFrame)
            {
                _explosions[kept++] = explosion;
            }
        }

        _explosionCount = kept;
    }

    // The ship, the plane, and the helicopter are drawn over the map, centred on where each is, the
    // aircraft above the ship; a missing drawing leaves that vehicle unseen, the rest drawn.
    private void DrawVehicles()
    {
        var vehicles = _city.Vehicles;
        if (vehicles.Ship is { } ship && _assets.ShipFrameFor(ship.Frame) is { } shipFrame)
        {
            Raylib.DrawTexture(shipFrame, ship.X - (shipFrame.Width / 2), ship.Y - (shipFrame.Height / 2), Color.White);
        }

        if (vehicles.Plane is { } plane && _assets.PlaneFrameFor(plane.Frame) is { } planeFrame)
        {
            Raylib.DrawTexture(planeFrame, plane.X - (planeFrame.Width / 2), plane.Y - (planeFrame.Height / 2), Color.White);
        }

        if (vehicles.Helicopter is { } copter && _assets.HelicopterFrameFor(copter.Frame) is { } copterFrame)
        {
            Raylib.DrawTexture(copterFrame, copter.X - (copterFrame.Width / 2), copter.Y - (copterFrame.Height / 2), Color.White);
        }
    }

    // The train is drawn over the map, centred on where it is, 32 pixels square as Micropolis' is.
    private void DrawTrain()
    {
        if (_train.IsRunning && _assets.TrainFrameFor(_train.Frame) is { } frame)
        {
            Raylib.DrawTexture(frame, _train.X - (frame.Width / 2), _train.Y - (frame.Height / 2), Color.White);
        }
    }
}
