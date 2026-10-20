namespace CityLimits.Core.World;

/// <summary>The roads on the map, and the tiles they serve.</summary>
public sealed class RoadNetwork
{
    private bool[] _roads = [];
    private int _width;
    private int _height;
    private bool _dirty = true;

    /// <summary>The number of road tiles the network holds.</summary>
    public int RoadCount { get; private set; }

    /// <summary>Tells whether the network is out of date with the map.</summary>
    public bool IsDirty => _dirty;

    /// <summary>Marks the network as out of date, so it is rebuilt before it is used.</summary>
    public void MarkDirty() => _dirty = true;

    /// <summary>Rebuilds the network from the map, when it is out of date.</summary>
    public void EnsureCurrent(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (_dirty)
        {
            Rebuild(map);
        }
    }

    /// <summary>Rebuilds the network from the map.</summary>
    public void Rebuild(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (_roads.Length != map.Width * map.Height)
        {
            _roads = new bool[map.Width * map.Height];
        }
        else
        {
            Array.Clear(_roads);
        }

        _width = map.Width;
        _height = map.Height;
        RoadCount = 0;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.StateAt(x, y).IsRoad)
                {
                    _roads[(y * map.Width) + x] = true;
                    RoadCount++;
                }
            }
        }

        _dirty = false;
    }

    /// <summary>Tells whether there is a road on the given tile.</summary>
    public bool HasRoad(int x, int y) =>
        (uint)x < (uint)_width && (uint)y < (uint)_height && _roads[(y * _width) + x];

    /// <summary>
    /// Tells whether a tile reaches the road network. A tile is served when it
    /// carries a road itself, or when it touches one.
    /// </summary>
    public bool IsServed(int x, int y) =>
        HasRoad(x, y) ||
        HasRoad(x, y - 1) ||
        HasRoad(x, y + 1) ||
        HasRoad(x - 1, y) ||
        HasRoad(x + 1, y);
}
