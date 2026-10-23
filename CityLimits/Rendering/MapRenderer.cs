namespace CityLimits.Rendering;

/// <summary>Draws the map: from the chunk cache when it could be made, and tile by tile when it couldn't.</summary>
public sealed class MapRenderer : IDisposable
{
    /// <summary>The size of a tile in world units.</summary>
    public const int TileSize = 16;

    private readonly TileMap _map;
    private readonly TrafficMap? _traffic;
    private readonly Fires? _fires;
    private readonly Texture2D? _sheet;
    private readonly MapChunkCache? _chunks;

    /// <summary>
    /// Creates a renderer for a map, with the given assets and, when the map is to show them, the
    /// city's traffic and fires. Call it after the window is open.
    /// </summary>
    public MapRenderer(TileMap map, GameAssets? assets, TrafficMap? traffic = null, Fires? fires = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
        _traffic = traffic;
        _fires = fires;
        _sheet = assets?.TileSheet;
        _chunks = MapChunkCache.TryCreate(map, _sheet);
    }

    /// <summary>Finds the tile under a point in the world, when the point is on the map.</summary>
    public static bool TryGetTileAt(TileMap map, Vector2 world, out int tileX, out int tileY, out TileState state)
    {
        tileX = (int)Math.Floor(world.X / TileSize);
        tileY = (int)Math.Floor(world.Y / TileSize);

        if (!map.Contains(tileX, tileY))
        {
            state = default;
            return false;
        }

        state = map.StateAt(tileX, tileY);
        return true;
    }

    /// <summary>Tells whether the map is drawn from the chunk cache, rather than tile by tile.</summary>
    public bool IsCached => _chunks is not null;

    /// <summary>Redraws whatever changed in the map since the last frame. Call it before <c>BeginDrawing</c>.</summary>
    public void Refresh() => _chunks?.Refresh();

    /// <summary>
    /// Draws the map, and only the part the camera can see, with the roads' traffic and the fires in
    /// the given animation frames. Call it inside <c>BeginMode2D</c>.
    /// </summary>
    public void Draw(Camera camera, int trafficFrame = 0, int fireFrame = 0)
    {
        var (minX, minY, maxX, maxY) = VisibleRange(_map, camera);

        if (_chunks is not null)
        {
            _chunks.Draw(minX, minY, maxX, maxY);
        }
        else
        {
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    DrawTile(_map, _sheet, x, y, x * TileSize, y * TileSize);
                }
            }
        }

        DrawTraffic(minX, minY, maxX, maxY, trafficFrame);
        DrawFires(minX, minY, maxX, maxY, fireFrame);

        if (camera.Zoom >= 0.75f)
        {
            DrawGrid(minX, minY, maxX, maxY);
        }
    }

    /// <summary>
    /// Blinks a lightning bolt over the centre of each zone or station the scan found without
    /// power, as Micropolis does: on for half a second, off for half a second. It changes every frame, so
    /// it is drawn over the cached map, not into it. Call it inside <c>BeginMode2D</c>.
    /// </summary>
    public void DrawUnpowered(Camera camera, double seconds)
    {
        if ((long)(seconds * 2) % 2 == 1)
        {
            return;
        }

        var (minX, minY, maxX, maxY) = VisibleRange(_map, camera);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var state = _map.StateAt(x, y);
                if (!state.IsBuildingCenter || state.Powered)
                {
                    continue;
                }

                var destination = new Rectangle { X = x * TileSize, Y = y * TileSize, Width = TileSize, Height = TileSize };
                if (_sheet is null)
                {
                    Raylib.DrawRectangleRec(destination, Theme.Unpowered);
                }
                else
                {
                    Raylib.DrawTexturePro(_sheet.Value, TileAtlas.SourceFor(TileAtlas.LightningBoltTile), destination, Vector2.Zero, 0f, Color.White);
                }
            }
        }
    }

    // The traffic moves every frame, so it is drawn over the cached map, not into it: each road
    // showing traffic is drawn again, cars and all, from its frame of the sheet's animation. With
    // no sheet the roads stay their flat colour.
    private void DrawTraffic(int minX, int minY, int maxX, int maxY, int frame)
    {
        if (_traffic is null || _sheet is null)
        {
            return;
        }

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var level = _traffic.LevelAt(x, y);
                if (level == TrafficLevel.None)
                {
                    continue;
                }

                var state = _map.StateAt(x, y);
                var road = TileAtlas.IndexFor(state, RoadNetwork.ConnectionsAt(_map, x, y), PowerLines.ConnectionsAt(_map, x, y), RoadConnections.None, TerrainSides.None, state, x, y);
                var destination = new Rectangle { X = x * TileSize, Y = y * TileSize, Width = TileSize, Height = TileSize };
                Raylib.DrawTexturePro(_sheet.Value, TileAtlas.SourceFor(TileAtlas.TrafficIndexFor(road, level, frame)), destination, Vector2.Zero, 0f, Color.White);
            }
        }
    }

    /// <summary>
    /// Draws an explosion at a tile, in a frame of the sheet's drawings of a building blowing apart.
    /// It is the game's alone: the fires it set are already burning. Call it inside <c>BeginMode2D</c>.
    /// </summary>
    public void DrawExplosion(int x, int y, int frame)
    {
        var destination = new Rectangle { X = x * TileSize, Y = y * TileSize, Width = TileSize, Height = TileSize };
        if (_sheet is null)
        {
            Raylib.DrawRectangleRec(destination, Theme.Fire);
            return;
        }

        var index = TileAtlas.ExplosionTile + Math.Clamp(frame, 0, TileAtlas.ExplosionFrames - 1);
        Raylib.DrawTexturePro(_sheet.Value, TileAtlas.SourceFor(index), destination, Vector2.Zero, 0f, Color.White);
    }

    // Fires flicker every frame, so they are drawn over the cached map, which holds the rubble
    // beneath them; with no sheet, a burning tile is a flat colour.
    private void DrawFires(int minX, int minY, int maxX, int maxY, int frame)
    {
        if (_fires is null || _fires.Count == 0)
        {
            return;
        }

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!_fires.IsBurning(x, y))
                {
                    continue;
                }

                var destination = new Rectangle { X = x * TileSize, Y = y * TileSize, Width = TileSize, Height = TileSize };
                if (_sheet is null)
                {
                    Raylib.DrawRectangleRec(destination, Theme.Fire);
                }
                else
                {
                    Raylib.DrawTexturePro(_sheet.Value, TileAtlas.SourceFor(TileAtlas.FireIndexFor(x, y, frame)), destination, Vector2.Zero, 0f, Color.White);
                }
            }
        }
    }

    /// <summary>Releases the chunk cache. Call it before the window closes.</summary>
    public void Dispose() => _chunks?.Dispose();

    /// <summary>
    /// Draws one tile with its top-left corner at the given point: from the sheet, or in flat
    /// colours when the sheet is missing. The chunk cache and the tile-by-tile path share it.
    /// </summary>
    internal static void DrawTile(TileMap map, Texture2D? sheet, int x, int y, int left, int top) =>
        DrawTileAs(sheet, map.StateAt(x, y), RoadNetwork.ConnectionsAt(map, x, y), PowerLines.ConnectionsAt(map, x, y), Rails.ConnectionsAt(map, x, y), TerrainEdges.SidesAt(map, x, y), map.ZoneStateAt(x, y), x, y, left, top);

    /// <summary>
    /// Draws a stroke's preview over the map, as Micropolis does: each tile it would change, as
    /// it would look, and the roads and power lines beside them that would join it. The outline shows what the
    /// stroke covers, in the warning colour when it would be refused.
    /// </summary>
    public void DrawPreview(BuildPlan plan, Camera camera)
    {
        foreach (var tile in plan.Tiles)
        {
            DrawPlanned(plan, tile.X, tile.Y);
            DrawJoining(plan, tile.X, tile.Y - 1);
            DrawJoining(plan, tile.X + 1, tile.Y);
            DrawJoining(plan, tile.X, tile.Y + 1);
            DrawJoining(plan, tile.X - 1, tile.Y);
        }

        var bounds = plan.Bounds;
        Raylib.DrawRectangleLinesEx(
            new Rectangle
            {
                X = bounds.X * TileSize,
                Y = bounds.Y * TileSize,
                Width = bounds.Width * TileSize,
                Height = bounds.Height * TileSize,
            },
            2f / camera.Zoom,
            plan.Refusal == BuildRefusal.None ? Theme.Hover : Theme.Error);
    }

    // A planned zone is new, so it shows undeveloped; a planned tile that isn't a zone has no
    // development to show. A planned tile is built on or cleared, so it has no shore or wood edge.
    private void DrawPlanned(BuildPlan plan, int x, int y) =>
        DrawTileAs(_sheet, plan.StateAt(x, y), plan.RoadConnectionsAt(x, y), plan.PowerConnectionsAt(x, y), plan.RailConnectionsAt(x, y), TerrainSides.None, default, x, y, x * TileSize, y * TileSize);

    // An existing road, rail, or power line beside the stroke changes shape when a new one joins it.
    private void DrawJoining(BuildPlan plan, int x, int y)
    {
        if (_map.Contains(x, y) && !plan.TryGetPlanned(x, y, out _) && (_map.StateAt(x, y).HasRoad || _map.StateAt(x, y).IsRail || _map.StateAt(x, y).HasPowerLine))
        {
            DrawPlanned(plan, x, y);
        }
    }

    private static void DrawTileAs(Texture2D? sheet, TileState state, RoadConnections roads, RoadConnections lines, RoadConnections rails, TerrainSides ground, TileState zoneCenter, int x, int y, int left, int top)
    {
        var destination = new Rectangle { X = left, Y = top, Width = TileSize, Height = TileSize };

        if (sheet is null)
        {
            Raylib.DrawRectangleRec(destination, state.Use == TileUse.None ? Theme.For(state.Kind) : Theme.For(state.Use));
        }
        else
        {
            Raylib.DrawTexturePro(
                sheet.Value,
                TileAtlas.SourceFor(TileAtlas.IndexFor(state, roads, lines, rails, ground, zoneCenter, x, y)),
                destination,
                Vector2.Zero,
                0f,
                Color.White);
        }
    }

    /// <summary>Draws the outline around the tile under the mouse.</summary>
    public void DrawHover(Camera camera, Vector2 mousePosition)
    {
        var world = camera.ScreenToWorld(mousePosition);
        if (!TryGetTileAt(_map, world, out var tileX, out var tileY, out _))
        {
            return;
        }

        Raylib.DrawRectangleLinesEx(
            new Rectangle
            {
                X = tileX * TileSize,
                Y = tileY * TileSize,
                Width = TileSize,
                Height = TileSize,
            },
            1f / camera.Zoom,
            Theme.Hover);
    }

    private static void DrawGrid(int minX, int minY, int maxX, int maxY)
    {
        for (var x = minX; x <= maxX + 1; x++)
        {
            var lineX = x * TileSize;
            Raylib.DrawLineEx(new Vector2(lineX, minY * TileSize), new Vector2(lineX, (maxY + 1) * TileSize), 0.5f, Theme.Grid);
        }

        for (var y = minY; y <= maxY + 1; y++)
        {
            var lineY = y * TileSize;
            Raylib.DrawLineEx(new Vector2(minX * TileSize, lineY), new Vector2((maxX + 1) * TileSize, lineY), 0.5f, Theme.Grid);
        }
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) VisibleRange(TileMap map, Camera camera)
    {
        var topLeft = camera.ScreenToWorld(Vector2.Zero);
        var bottomRight = camera.ScreenToWorld(camera.ScreenSize);

        var minX = Math.Max(0, (int)Math.Floor(topLeft.X / TileSize));
        var minY = Math.Max(0, (int)Math.Floor(topLeft.Y / TileSize));
        var maxX = Math.Min(map.Width - 1, (int)Math.Ceiling(bottomRight.X / TileSize));
        var maxY = Math.Min(map.Height - 1, (int)Math.Ceiling(bottomRight.Y / TileSize));
        return (minX, minY, maxX, maxY);
    }
}
