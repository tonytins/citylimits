namespace CityLimits.Rendering;

/// <summary>Draws the map.</summary>
public sealed class MapRenderer
{
    /// <summary>The size of a tile in world units.</summary>
    public const int TileSize = 16;

    private readonly GameAssets? _assets;

    /// <summary>Creates a renderer that draws the map with the given assets.</summary>
    public MapRenderer(GameAssets? assets) => _assets = assets;

    /// <summary>Finds the tile under a point in the world, when the point is on the map.</summary>
    public static bool TryGetTileAt(TileMap map, Vector2 world, out int tileX, out int tileY, out TileKind kind)
    {
        tileX = (int)Math.Floor(world.X / TileSize);
        tileY = (int)Math.Floor(world.Y / TileSize);

        if (!map.Contains(tileX, tileY))
        {
            kind = TileKind.Grass;
            return false;
        }

        kind = map[tileX, tileY];
        return true;
    }

    /// <summary>Draws the map, and only the tiles the camera can see.</summary>
    public void Draw(TileMap map, Camera camera)
    {
        var (minX, minY, maxX, maxY) = VisibleRange(map, camera);
        var sheet = _assets?.TileSheet;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var kind = map[x, y];
                var destination = new Rectangle
                {
                    X = x * TileSize,
                    Y = y * TileSize,
                    Width = TileSize,
                    Height = TileSize,
                };

                if (sheet is null)
                {
                    Raylib.DrawRectangleRec(destination, Theme.For(kind));
                    continue;
                }

                Raylib.DrawTexturePro(
                    sheet.Value,
                    TileAtlas.SourceFor(kind, x, y),
                    destination,
                    Vector2.Zero,
                    0f,
                    Color.White);
            }
        }

        if (camera.Zoom >= 0.75f)
        {
            DrawGrid(minX, minY, maxX, maxY);
        }
    }

    /// <summary>Draws the outline around the tile under the mouse.</summary>
    public void DrawHover(TileMap map, Camera camera, Vector2 mousePosition)
    {
        var world = camera.ScreenToWorld(mousePosition);
        if (!TryGetTileAt(map, world, out var tileX, out var tileY, out _))
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
