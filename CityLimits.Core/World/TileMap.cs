namespace CityLimits.Core.World;

/// <summary>A rectangular grid of tiles.</summary>
public sealed class TileMap
{
    private readonly TileKind[] _tiles;

    /// <summary>Creates a map from a tile array laid out row by row.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The width or the height is not positive.</exception>
    /// <exception cref="ArgumentException">The tile count does not match the size.</exception>
    public TileMap(int width, int height, TileKind[] tiles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(tiles);

        var expected = (long)width * height;
        if (tiles.LongLength != expected)
        {
            throw new ArgumentException($"A {width} by {height} map needs {expected} tiles, but {tiles.LongLength} were given.", nameof(tiles));
        }

        Width = width;
        Height = height;
        _tiles = (TileKind[])tiles.Clone();
    }

    /// <summary>The number of tiles across.</summary>
    public int Width { get; }

    /// <summary>The number of tiles down.</summary>
    public int Height { get; }

    /// <summary>Gets the tile at the given coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates are outside the map.</exception>
    public TileKind this[int x, int y]
    {
        get
        {
            if (!Contains(x, y))
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside a {Width} by {Height} map.");
            }

            return _tiles[(y * Width) + x];
        }
    }

    /// <summary>Tells whether the given coordinates are on the map.</summary>
    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>Gets the tile at the given coordinates, or <paramref name="fallback"/> when they are off the map.</summary>
    public TileKind TileAtOrDefault(int x, int y, TileKind fallback = TileKind.Grass) =>
        Contains(x, y) ? _tiles[(y * Width) + x] : fallback;
}
