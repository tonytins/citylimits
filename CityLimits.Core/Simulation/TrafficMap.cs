namespace CityLimits.Core.Simulation;

/// <summary>
/// How busy the roads are, as in Micropolis: on blocks of 2 by 2 tiles, as land value is kept. A
/// zone's trip that arrives leaves <see cref="PerTrip"/> on each road tile it remembered passing
/// (every other step), up to <see cref="Most"/> a block, and once a cycle, at phase 10, every block
/// fades a little, so traffic stays where trips keep going.
/// </summary>
/// <remarks>
/// The numbers are Micropolis', which Tony Bark approved for Radial. Micropolis caps a block at 240
/// only one time in six, by chance; CityLimits always caps it, as Tony Bark chose.
/// </remarks>
public sealed class TrafficMap
{
    /// <summary>What one trip adds to a road tile's block.</summary>
    public const int PerTrip = 50;

    /// <summary>The busiest a block gets.</summary>
    public const int Most = 240;

    // A busy block fades faster: by 34 above this, by 24 above the next, and to nothing below it.
    private const int Busy = 200;
    private const int BusyFade = 34;
    private const int Quiet = 24;
    private const int QuietFade = 24;

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _density;

    /// <summary>Creates an empty traffic map for a city's map.</summary>
    public TrafficMap(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        (_width, _height) = (BlocksAcross(map.Width), BlocksAcross(map.Height));
        _density = new int[_width * _height];
    }

    /// <summary>How many blocks a map of the given size has, for a save that has none yet.</summary>
    public static int BlocksFor(int mapWidth, int mapHeight) => BlocksAcross(mapWidth) * BlocksAcross(mapHeight);

    /// <summary>How busy the roads are around a tile, from 0 to <see cref="Most"/>; 0 off the map.</summary>
    public int DensityAt(int x, int y) =>
        x >= 0 && y >= 0 && x / 2 < _width && y / 2 < _height ? _density[((y / 2) * _width) + (x / 2)] : 0;

    /// <summary>The busiest block's traffic, from 0 to <see cref="Most"/>.</summary>
    public int Busiest
    {
        get
        {
            var busiest = 0;
            foreach (var density in _density)
            {
                busiest = Math.Max(busiest, density);
            }

            return busiest;
        }
    }

    /// <summary>Lays a trip's traffic along its route: each road tile it remembered adds to its block.</summary>
    public void AddRoute(TileMap map, IReadOnlyList<(int X, int Y)> route)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(route);
        foreach (var (x, y) in route)
        {
            // Micropolis lays traffic on roads, bridges, and crossings, and nothing else a route passes.
            if (!map.Contains(x, y) || !map.StateAt(x, y).IsRoad)
            {
                continue;
            }

            var block = ((y / 2) * _width) + (x / 2);
            _density[block] = Math.Min(_density[block] + PerTrip, Most);
        }
    }

    /// <summary>Fades every block a little, once a cycle.</summary>
    public void Fade()
    {
        for (var i = 0; i < _density.Length; i++)
        {
            var density = _density[i];
            _density[i] = density > Busy ? density - BusyFade
                : density > Quiet ? density - QuietFade
                : 0;
        }
    }

    internal TrafficSave Capture() => new() { Density = [.. _density] };

    internal void Restore(TrafficSave save)
    {
        SaveChecks.CopyInto(save.Density, _density, "traffic.density");
        for (var i = 0; i < _density.Length; i++)
        {
            SaveChecks.InRange(_density[i], 0, Most, "traffic.density");
        }
    }

    private static int BlocksAcross(int tiles) => (tiles + 1) / 2;
}
