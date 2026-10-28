namespace CityLimits.Core.Simulation;

/// <summary>
/// What the stadiums, seaports, and airports do to the land around them, as their specs'
/// influences say (<see cref="InfluenceSpec"/>), worked out from the map each time a map that
/// answers to them is: each powered one adds its strength to the 8 by 8 tile block it stands in,
/// which spreads, as a station's coverage does, once for each step of its reach. Since it comes
/// from the map alone, it holds nothing a save needs.
/// </summary>
public sealed class FacilityInfluences
{
    /// <summary>The width and height of a block, in tiles: a station's coverage block.</summary>
    public const int BlockSize = ServiceCoverage.BlockSize;

    private static readonly InfluenceEffect[] Effects = Enum.GetValues<InfluenceEffect>();

    private readonly TileMap _map;
    private readonly FacilitySpec[] _facilities;
    private readonly int _width;
    private readonly int _height;

    // For each effect, what every facility together does, as of the last time it was worked out.
    private readonly int[][] _totals;

    // For each kind of facility, its blocks' strength before it spreads, and a pass's scratch.
    private readonly int[][] _raw;
    private readonly int[] _scratch;

    /// <summary>Creates the influences on a map, from the facilities the definitions give.</summary>
    public FacilityInfluences(TileMap map, CityDefinitions? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _facilities = [.. (definitions ?? CityDefinitions.Base).Facilities];
        _width = (map.Width + BlockSize - 1) / BlockSize;
        _height = (map.Height + BlockSize - 1) / BlockSize;
        var blocks = _width * _height;
        _totals = Effects.Select(_ => new int[blocks]).ToArray();
        _raw = _facilities.Select(_ => new int[blocks]).ToArray();
        _scratch = new int[blocks];
    }

    /// <summary>
    /// How much the facilities change a map at a tile, as of the last <see cref="Update"/> of that
    /// effect; 0 off the map, and before the first.
    /// </summary>
    public int At(InfluenceEffect effect, int x, int y) =>
        x >= 0 && y >= 0 && x / BlockSize < _width && y / BlockSize < _height
            ? _totals[(int)effect][((y / BlockSize) * _width) + (x / BlockSize)]
            : 0;

    /// <summary>
    /// Works out what the facilities do to one of the maps, from the powered ones on the map now.
    /// When no facility has that influence, the map isn't scanned.
    /// </summary>
    public void Update(InfluenceEffect effect)
    {
        var total = _totals[(int)effect];
        Array.Clear(total);
        if (!_facilities.Any(facility => facility.InfluenceOn(effect) is not null))
        {
            return;
        }

        foreach (var raw in _raw)
        {
            Array.Clear(raw);
        }

        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (!state.IsBuildingCenter || !state.Powered || !TileRules.IsFacility(state.Use))
                {
                    continue;
                }

                var kind = Array.FindIndex(_facilities, facility => facility.Use == state.Use);
                if (kind >= 0 && _facilities[kind].InfluenceOn(effect) is { } influence)
                {
                    _raw[kind][((y / BlockSize) * _width) + (x / BlockSize)] += influence.Strength;
                }
            }
        }

        // Each kind spreads as far as its own reach, then they add up.
        for (var kind = 0; kind < _facilities.Length; kind++)
        {
            if (_facilities[kind].InfluenceOn(effect) is not { } influence)
            {
                continue;
            }

            ServiceCoverage.Smooth(_raw[kind], _scratch, _width, _height, influence.Reach);
            for (var block = 0; block < total.Length; block++)
            {
                total[block] += _raw[kind][block];
            }
        }
    }
}
