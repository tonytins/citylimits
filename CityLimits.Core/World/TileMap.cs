namespace CityLimits.Core.World;

/// <summary>A rectangular grid of tiles.</summary>
public sealed class TileMap
{
    private readonly TileState[] _tiles;

    /// <summary>Creates a map from a state array laid out row by row.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The width or the height is not positive.</exception>
    /// <exception cref="ArgumentException">The tile count does not match the size.</exception>
    public TileMap(int width, int height, TileState[] tiles)
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
        _tiles = (TileState[])tiles.Clone();
    }

    /// <summary>Creates a map of bare ground, with nothing built on it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The width or the height is not positive.</exception>
    /// <exception cref="ArgumentException">The tile count does not match the size.</exception>
    public static TileMap FromTerrain(int width, int height, TileKind[] tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        var states = new TileState[tiles.LongLength];
        for (var i = 0; i < tiles.Length; i++)
        {
            states[i] = new TileState(tiles[i]);
        }

        return new TileMap(width, height, states);
    }

    /// <summary>The number of tiles across.</summary>
    public int Width { get; }

    /// <summary>The number of tiles down.</summary>
    public int Height { get; }

    /// <summary>Gets the ground of the tile at the given coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates are outside the map.</exception>
    public TileKind this[int x, int y] => StateAt(x, y).Kind;

    /// <summary>Gets the whole state of the tile at the given coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates are outside the map.</exception>
    public TileState StateAt(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside a {Width} by {Height} map.");
        }

        return _tiles[(y * Width) + x];
    }

    /// <summary>
    /// Raised with a tile's coordinates after its state changes, so a view of the map can redraw
    /// only what changed. It is not raised when a tile is set to the state it already had.
    /// </summary>
    /// <remarks>
    /// The arguments are plain coordinates rather than an <see cref="EventArgs"/>, because the
    /// simulation can change hundreds of tiles in one tick and must not allocate for each.
    /// A handler runs inside the simulation's work, so it must be quick and must not throw.
    /// </remarks>
    public event Action<int, int>? TileChanged;

    /// <summary>Replaces the whole state of the tile at the given coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates are outside the map.</exception>
    public void SetState(int x, int y, TileState state)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside a {Width} by {Height} map.");
        }

        var index = (y * Width) + x;
        if (_tiles[index] == state)
        {
            return;
        }

        _tiles[index] = state;
        TileChanged?.Invoke(x, y);
    }

    /// <summary>Tells whether the given coordinates are on the map.</summary>
    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>Gets the tile at the given coordinates, or <paramref name="fallback"/> when they are off the map.</summary>
    public TileKind TileAtOrDefault(int x, int y, TileKind fallback = TileKind.Grass) =>
        Contains(x, y) ? _tiles[(y * Width) + x].Kind : fallback;

    /// <summary>Gets the state at the given coordinates, or <paramref name="fallback"/> when they are off the map.</summary>
    public TileState StateAtOrDefault(int x, int y, TileState fallback = default) =>
        Contains(x, y) ? _tiles[(y * Width) + x] : fallback;
}
