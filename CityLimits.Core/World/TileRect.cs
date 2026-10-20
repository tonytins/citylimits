namespace CityLimits.Core.World;

/// <summary>A rectangle of tiles: its top-left tile and how many tiles it spans each way.</summary>
public readonly record struct TileRect(int X, int Y, int Width, int Height)
{
    /// <summary>The number of tiles the rectangle covers.</summary>
    public long Area => (long)Width * Height;

    /// <summary>Tells whether every tile of the rectangle is on the map.</summary>
    public bool IsInside(TileMap map) =>
        X >= 0 && Y >= 0 && (long)X + Width <= map.Width && (long)Y + Height <= map.Height;

    /// <summary>The smallest rectangle that holds both corners, whichever way round they are given.</summary>
    public static TileRect Spanning(int x1, int y1, int x2, int y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1) + 1, Math.Abs(y2 - y1) + 1);
}
