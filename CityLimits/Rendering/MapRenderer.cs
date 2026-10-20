namespace CityLimits.Rendering;

/// <summary>Draws the map.</summary>
public sealed class MapRenderer
{
    /// <summary>The size of a tile in world units.</summary>
    public const int TileSize = 16;

    /// <summary>The width of the edge drawn where a road does not join another.</summary>
    private const int RoadEdge = 3;

    /// <summary>The length of one dash of the line down a road.</summary>
    private const int DashLength = 4;

    /// <summary>The gap between two dashes of the line down a road.</summary>
    private const int DashGap = 4;

    private readonly GameAssets? _assets;

    /// <summary>Creates a renderer that draws the map with the given assets.</summary>
    public MapRenderer(GameAssets? assets) => _assets = assets;

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

    /// <summary>Draws the map, and only the tiles the camera can see.</summary>
    public void Draw(TileMap map, Camera camera)
    {
        var (minX, minY, maxX, maxY) = VisibleRange(map, camera);
        var sheet = _assets?.TileSheet;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var state = map.StateAt(x, y);
                var destination = new Rectangle
                {
                    X = x * TileSize,
                    Y = y * TileSize,
                    Width = TileSize,
                    Height = TileSize,
                };

                if (sheet is null)
                {
                    Raylib.DrawRectangleRec(destination, Theme.For(state.Kind));
                }
                else
                {
                    Raylib.DrawTexturePro(
                        sheet.Value,
                        TileAtlas.SourceFor(state.Kind, x, y),
                        destination,
                        Vector2.Zero,
                        0f,
                        Color.White);
                }

                DrawUse(map, x, y, state);
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

    private static void DrawUse(TileMap map, int x, int y, TileState state)
    {
        switch (state.Use)
        {
            case TileUse.Road:
                DrawRoad(map, x, y);
                break;
            case TileUse.Residential:
            case TileUse.Commercial:
            case TileUse.Industrial:
                DrawZone(x, y, state);
                break;
        }
    }

    private static void DrawRoad(TileMap map, int x, int y)
    {
        var left = x * TileSize;
        var top = y * TileSize;

        Raylib.DrawRectangle(left, top, TileSize, TileSize, Theme.Road);

        var north = HasRoad(map, x, y - 1);
        var south = HasRoad(map, x, y + 1);
        var west = HasRoad(map, x - 1, y);
        var east = HasRoad(map, x + 1, y);

        if (!north)
        {
            Raylib.DrawRectangle(left, top, TileSize, RoadEdge, Theme.RoadEdge);
        }

        if (!south)
        {
            Raylib.DrawRectangle(left, top + TileSize - RoadEdge, TileSize, RoadEdge, Theme.RoadEdge);
        }

        if (!west)
        {
            Raylib.DrawRectangle(left, top, RoadEdge, TileSize, Theme.RoadEdge);
        }

        if (!east)
        {
            Raylib.DrawRectangle(left + TileSize - RoadEdge, top, RoadEdge, TileSize, Theme.RoadEdge);
        }

        var horizontal = east || west;
        var vertical = north || south;

        if (horizontal && vertical)
        {
            Raylib.DrawRectangle(left + (TileSize / 2) - 2, top + (TileSize / 2) - 2, 4, 4, Theme.RoadLine);
            return;
        }

        if (horizontal)
        {
            if (west)
            {
                DrawDashes(left, top + (TileSize / 2), -1, 0, TileSize / 2);
            }

            if (east)
            {
                DrawDashes(left + (TileSize / 2), top + (TileSize / 2), 1, 0, TileSize / 2);
            }

            return;
        }

        if (vertical)
        {
            if (north)
            {
                DrawDashes(left + (TileSize / 2), top, 0, -1, TileSize / 2);
            }

            if (south)
            {
                DrawDashes(left + (TileSize / 2), top + (TileSize / 2), 0, 1, TileSize / 2);
            }
        }
    }

    private static void DrawDashes(int x, int y, int stepX, int stepY, int length)
    {
        for (var offset = 0; offset < length; offset += DashLength + DashGap)
        {
            var run = Math.Min(DashLength, length - offset);
            var width = stepX != 0 ? run : 2;
            var height = stepY != 0 ? run : 2;
            Raylib.DrawRectangle(x + (stepX * offset), y + (stepY * offset), width, height, Theme.RoadLine);
        }
    }

    private static void DrawZone(int x, int y, TileState state)
    {
        var left = x * TileSize;
        var top = y * TileSize;
        var color = Theme.For(state.Use);

        if (state.Development == 0)
        {
            Raylib.DrawRectangle(left + 5, top + 5, 6, 6, color);
            return;
        }

        var size = 6 + (state.Development * 3);
        var inset = (TileSize - size) / 2;
        var building = new Rectangle
        {
            X = left + inset,
            Y = top + inset,
            Width = size,
            Height = size,
        };

        Raylib.DrawRectangleRec(building, color);
        Raylib.DrawRectangleLinesEx(building, 1f, Theme.BuildingOutline);
    }

    private static bool HasRoad(TileMap map, int x, int y) =>
        map.Contains(x, y) && map.StateAt(x, y).IsRoad;

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
