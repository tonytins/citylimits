namespace CityLimits.Core.Simulation;

/// <summary>How much traffic a road shows: none, light, or heavy, as Micropolis draws its roads.</summary>
public enum TrafficLevel
{
    /// <summary>A quiet road: its block has less than 64 traffic.</summary>
    None,

    /// <summary>Light traffic: 64 to 191.</summary>
    Light,

    /// <summary>Heavy traffic: 192 or more. A road showing it counts twice toward the roads' upkeep.</summary>
    Heavy,
}

/// <summary>
/// How busy the roads are, as in Micropolis: on blocks of 2 by 2 tiles, as land value is kept. A
/// zone's trip that arrives leaves <see cref="PerTrip"/> on each road tile it remembered passing
/// (every other step), up to <see cref="Most"/> a block, and once a cycle, at phase 10, every block
/// fades a little, so traffic stays where trips keep going. Each road tile shows a
/// <see cref="TrafficLevel"/>, set from its block when the scan passes it, as Micropolis keeps it
/// in the road's own drawing, and once a year the city works out its <see cref="Average"/>.
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

    // A road shows light traffic from this much on its block, and heavy from the next.
    private const int LightFrom = 64;
    private const int HeavyFrom = 192;

    // Micropolis scales the average traffic on the city's built-up land by this, so it reads as a
    // problem from about 60.
    private const double AverageScale = 2.4;

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _density;
    private readonly TileMap _map;
    private readonly byte[] _levels;

    /// <summary>Creates an empty traffic map for a city's map, which it follows as tiles change.</summary>
    public TrafficMap(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        (_width, _height) = (BlocksAcross(map.Width), BlocksAcross(map.Height));
        _density = new int[_width * _height];
        _map = map;
        _levels = new byte[map.Width * map.Height];

        // As in Micropolis, where the level is part of a road's drawing, a tile that changes, such
        // as a road bulldozed or crossed by a line, starts again quiet.
        map.TileChanged += OnTileChanged;
    }

    /// <summary>
    /// The city's traffic, as Micropolis' evaluation works it out once a year: the average on every
    /// tile with land value, times 2.4. Above 60 the city reports frequent traffic jams.
    /// </summary>
    public int Average { get; private set; }

    /// <summary>The traffic a road tile showed when the scan last passed it; none off the map or off a road.</summary>
    public TrafficLevel LevelAt(int x, int y) => _map.Contains(x, y) ? (TrafficLevel)_levels[(y * _map.Width) + x] : TrafficLevel.None;

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

    /// <summary>
    /// The scan passing a road tile: shows the level its block's traffic calls for now, and tells
    /// what it showed before, which is what the roads' upkeep counts, as in Micropolis.
    /// </summary>
    internal TrafficLevel Show(int x, int y)
    {
        var index = (y * _map.Width) + x;
        var shown = (TrafficLevel)_levels[index];
        var density = DensityAt(x, y);
        _levels[index] = (byte)(density < LightFrom ? TrafficLevel.None : density < HeavyFrom ? TrafficLevel.Light : TrafficLevel.Heavy);
        return shown;
    }

    /// <summary>Works out the year's <see cref="Average"/>, over the tiles that have land value.</summary>
    internal void UpdateAverage(CityMaps maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        // Micropolis starts the count at one, so a city with no land value averages nothing.
        long total = 0;
        var count = 1;
        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                if (maps.LandValueAt(x, y) != 0)
                {
                    total += DensityAt(x, y);
                    count++;
                }
            }
        }

        Average = (int)Math.Round((double)total / count * AverageScale, MidpointRounding.AwayFromZero);
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

    // Each row of the map, a character a tile: 0 for none, 1 for light, 2 for heavy.
    internal RoadTrafficSave CaptureRoads()
    {
        var rows = new string[_map.Height];
        var row = new char[_map.Width];
        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                row[x] = (char)('0' + _levels[(y * _map.Width) + x]);
            }

            rows[y] = new string(row);
        }

        return new RoadTrafficSave { Levels = rows, Average = Average };
    }

    internal void Restore(TrafficSave save)
    {
        SaveChecks.CopyInto(save.Density, _density, "traffic.density");
        for (var i = 0; i < _density.Length; i++)
        {
            SaveChecks.InRange(_density[i], 0, Most, "traffic.density");
        }
    }

    // A level on a tile that isn't a road, as an older game that kept this part while changing the
    // city could leave, is dropped rather than refused: the road it belonged to has gone.
    internal void RestoreRoads(RoadTrafficSave save)
    {
        if (save.Levels is not { } rows || rows.Length != _map.Height)
        {
            throw new InvalidSave($"roadTraffic.levels: needs {_map.Height} rows.");
        }

        for (var y = 0; y < _map.Height; y++)
        {
            if (rows[y] is not { } row || row.Length != _map.Width)
            {
                throw new InvalidSave($"roadTraffic.levels[{y}]: needs {_map.Width} characters.");
            }

            for (var x = 0; x < _map.Width; x++)
            {
                var level = row[x] - '0';
                if (level is < (int)TrafficLevel.None or > (int)TrafficLevel.Heavy)
                {
                    throw new InvalidSave($"roadTraffic.levels[{y}]: needs 0, 1, or 2 for each tile, not '{row[x]}' at {x}.");
                }

                _levels[(y * _map.Width) + x] = _map.StateAt(x, y).IsRoad ? (byte)level : (byte)0;
            }
        }

        Average = SaveChecks.InRange(save.Average, 0, (int)Math.Ceiling(Most * AverageScale), "roadTraffic.average");
    }

    private void OnTileChanged(int x, int y) => _levels[(y * _map.Width) + x] = 0;

    private static int BlocksAcross(int tiles) => (tiles + 1) / 2;
}
