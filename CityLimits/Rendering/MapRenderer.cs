namespace CityLimits.Rendering;

/// <summary>Draws the map: from the chunk cache when it could be made, and tile by tile when it couldn't.</summary>
public sealed class MapRenderer : IDisposable
{
    /// <summary>The size of a tile in world units.</summary>
    public const int TileSize = 16;

    private readonly TileMap _map;
    private readonly Texture2D? _sheet;
    private readonly MapChunkCache? _chunks;

    /// <summary>Creates a renderer for a map, with the given assets. Call it after the window is open.</summary>
    public MapRenderer(TileMap map, GameAssets? assets)
    {
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
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

    /// <summary>Draws the map, and only the part the camera can see. Call it inside <c>BeginMode2D</c>.</summary>
    public void Draw(Camera camera)
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

        if (camera.Zoom >= 0.75f)
        {
            DrawGrid(minX, minY, maxX, maxY);
        }
    }

    /// <summary>Releases the chunk cache. Call it before the window closes.</summary>
    public void Dispose() => _chunks?.Dispose();

    /// <summary>
    /// Draws one tile with its top-left corner at the given point: from the sheet, or in flat
    /// colours when the sheet is missing. The chunk cache and the tile-by-tile path share it.
    /// </summary>
    internal static void DrawTile(TileMap map, Texture2D? sheet, int x, int y, int left, int top) =>
        DrawTileAs(sheet, map.StateAt(x, y), RoadNetwork.ConnectionsAt(map, x, y), TerrainEdges.SidesAt(map, x, y), map.ZoneStateAt(x, y), x, y, left, top);

    /// <summary>
    /// Draws a stroke's preview over the map, as Micropolis does: each tile it would change, as
    /// it would look, and the roads beside them that would join it. The outline shows what the
    /// stroke covers, in the warning colour when it would be refused.
    /// </summary>
    public void DrawPreview(BuildPlan plan, Camera camera)
    {
        foreach (var tile in plan.Tiles)
        {
            DrawPlanned(plan, tile.X, tile.Y);
            DrawJoiningRoad(plan, tile.X, tile.Y - 1);
            DrawJoiningRoad(plan, tile.X + 1, tile.Y);
            DrawJoiningRoad(plan, tile.X, tile.Y + 1);
            DrawJoiningRoad(plan, tile.X - 1, tile.Y);
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
        DrawTileAs(_sheet, plan.StateAt(x, y), plan.RoadConnectionsAt(x, y), TerrainSides.None, default, x, y, x * TileSize, y * TileSize);

    // An existing road beside the stroke changes shape when a new road joins it.
    private void DrawJoiningRoad(BuildPlan plan, int x, int y)
    {
        if (_map.Contains(x, y) && !plan.TryGetPlanned(x, y, out _) && _map.StateAt(x, y).IsRoad)
        {
            DrawPlanned(plan, x, y);
        }
    }

    private static void DrawTileAs(Texture2D? sheet, TileState state, RoadConnections roads, TerrainSides ground, TileState zoneCenter, int x, int y, int left, int top)
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
                TileAtlas.SourceFor(TileAtlas.IndexFor(state, roads, ground, zoneCenter, x, y)),
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
