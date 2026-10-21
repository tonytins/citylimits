namespace CityLimits.Core.Simulation;

/// <summary>
/// How well fire and police stations cover the city, worked out as Micropolis does. The scan adds
/// each station's strength to the 8 by 8 tile block it stands in: its funding's effect, halved
/// without power, and halved again without a road along its edge. Once a cycle each map is
/// smoothed three times, so a station's cover spreads to the blocks around it and fades with
/// distance.
/// </summary>
/// <remarks>
/// Coverage acts where Micropolis has it act, on crime and fires, which come with later
/// milestones; until then it is worked out and read, but nothing in the city answers to it.
/// </remarks>
public sealed class ServiceCoverage
{
    /// <summary>The width and height of a coverage block, in tiles.</summary>
    public const int BlockSize = 8;

    // A map is smoothed this many times before it is read.
    private const int Smoothings = 3;

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _fireRaw;
    private readonly int[] _policeRaw;
    private readonly int[] _fire;
    private readonly int[] _police;
    private readonly int[] _scratch;

    /// <summary>
    /// Creates the coverage of a map, in blocks of <see cref="BlockSize"/> tiles; a part block at
    /// the right or bottom edge counts as a block. The maps exist from the start, so the scan can
    /// add to them before the first cycle has begun.
    /// </summary>
    public ServiceCoverage(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _width = (map.Width + BlockSize - 1) / BlockSize;
        _height = (map.Height + BlockSize - 1) / BlockSize;
        var blocks = _width * _height;
        (_fireRaw, _policeRaw, _fire, _police, _scratch) = (new int[blocks], new int[blocks], new int[blocks], new int[blocks], new int[blocks]);
    }

    /// <summary>The fire stations the scan has passed this cycle.</summary>
    public int FireStations { get; private set; }

    /// <summary>The police stations the scan has passed this cycle.</summary>
    public int PoliceStations { get; private set; }

    /// <summary>How well fire stations cover a tile, as of the last spread; 0 off the map or before the first.</summary>
    public int FireCoverageAt(int x, int y) => At(_fire, x, y);

    /// <summary>How well police stations cover a tile, as of the last spread; 0 off the map or before the first.</summary>
    public int PoliceCoverageAt(int x, int y) => At(_police, x, y);

    /// <summary>Starts a new cycle's count of stations, clearing what the scan adds to.</summary>
    public void StartCount()
    {
        Array.Clear(_fireRaw);
        Array.Clear(_policeRaw);
        FireStations = 0;
        PoliceStations = 0;
    }

    /// <summary>
    /// Counts the station whose centre the scan has reached and adds its strength to its block:
    /// the funding's effect, halved without power, and halved again without a road along its edge.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The use isn't a station.</exception>
    public void AddStation(int x, int y, TileUse use, int fundingEffect, bool powered, bool served)
    {
        var strength = powered ? fundingEffect : fundingEffect / 2;
        if (!served)
        {
            strength /= 2;
        }

        var block = Block(x, y);
        switch (use)
        {
            case TileUse.FireStation:
                FireStations++;
                _fireRaw[block] += strength;
                break;
            case TileUse.PoliceStation:
                PoliceStations++;
                _policeRaw[block] += strength;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(use), $"{use} is not a station.");
        }
    }

    /// <summary>Spreads the police stations' strength into the coverage the city reads.</summary>
    public void SpreadPolice() => Spread(_policeRaw, _police);

    /// <summary>Spreads the fire stations' strength into the coverage the city reads.</summary>
    public void SpreadFire() => Spread(_fireRaw, _fire);

    // Each pass gives a block half of itself plus an eighth of each of its four neighbours, as
    // whole numbers, as Micropolis does; the map's edge adds nothing.
    private void Spread(int[] raw, int[] coverage)
    {
        Array.Copy(raw, coverage, raw.Length);
        for (var pass = 0; pass < Smoothings; pass++)
        {
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    var edge = 0;
                    edge += x > 0 ? coverage[(y * _width) + x - 1] : 0;
                    edge += x + 1 < _width ? coverage[(y * _width) + x + 1] : 0;
                    edge += y > 0 ? coverage[((y - 1) * _width) + x] : 0;
                    edge += y + 1 < _height ? coverage[((y + 1) * _width) + x] : 0;
                    _scratch[(y * _width) + x] = ((edge / 4) + coverage[(y * _width) + x]) / 2;
                }
            }

            Array.Copy(_scratch, coverage, coverage.Length);
        }
    }

    private int Block(int x, int y) => ((y / BlockSize) * _width) + (x / BlockSize);

    private int At(int[] map, int x, int y) =>
        x >= 0 && y >= 0 && x / BlockSize < _width && y / BlockSize < _height ? map[Block(x, y)] : 0;
}
