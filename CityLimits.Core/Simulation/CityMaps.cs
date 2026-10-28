namespace CityLimits.Core.Simulation;

/// <summary>
/// The city's overlay maps, as Micropolis keeps them: land value, pollution, crime, and population
/// density on a grid of 2 by 2 tile blocks, the terrain's worth on 4 by 4 blocks, and the
/// commercial rate on 8 by 8 blocks, with the city's centre of mass that land value and the
/// commercial rate measure from. Land value and pollution are worked out in phase 12, crime in
/// phase 13, and population density in phase 14. Every grid is made once, with the map, and reused.
/// </summary>
public sealed class CityMaps
{
    /// <summary>The most land value a block can have.</summary>
    public const int MaxLandValue = 250;

    /// <summary>The most pollution a block can have once smoothed.</summary>
    public const int MostPollution = 255;

    /// <summary>The most crime a block can have.</summary>
    public const int MostCrime = 250;

    // Land value starts from 34 blocks out from the centre, times four, and stops falling at 32.
    private const int LandValueReach = 34;
    private const int FarthestDistance = 32;
    private const int TerrainPerTile = 15;

    // What each tile adds to its block's pollution, as Micropolis' core has it: every tile of an
    // industrial building, of a coal plant, of a seaport, and of an airport, not only the centre the
    // Java port counts, as Tony Bark chose, and a road by the traffic it shows. A block holds at most 255.
    private const int IndustryPollution = 50;
    private const int CoalPlantPollution = 100;
    private const int LightTrafficPollution = 50;
    private const int HeavyTrafficPollution = 75;
    private const int FirePollution = 90;
    private const int MostBlockPollution = 255;

    // Micropolis' crime: 128 less land value, plus population density, no more than 300, less
    // police coverage, from 0 to 250. Land value loses 20 where crime is above 190.
    private const int CrimeBase = 128;
    private const int MostCrimeBeforePolice = 300;
    private const int HighCrime = 190;
    private const int HighCrimeLandValue = 20;

    private readonly CityDefinitions _definitions;
    private readonly TileMap _map;
    private readonly int _halfWidth;
    private readonly int _halfHeight;
    private readonly int[] _landValue;
    private readonly int[] _pollution;
    private readonly int[] _pollutionRaw;
    private readonly int[] _pollutionScratch;
    private readonly int[] _crime;
    private readonly int[] _density;
    private readonly int[] _densityScratch;
    private readonly int _quarterWidth;
    private readonly int _quarterHeight;
    private readonly int[] _terrain;
    private readonly int[] _terrainScratch;
    private readonly int _eighthWidth;
    private readonly int[] _commercialRate;

    /// <summary>Makes the maps for a city's map; nothing is worked out until the phases run.</summary>
    public CityMaps(TileMap map, CityDefinitions? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _definitions = definitions ?? CityDefinitions.Base;
        _halfWidth = (map.Width + 1) / 2;
        _halfHeight = (map.Height + 1) / 2;
        _landValue = new int[_halfWidth * _halfHeight];
        _pollution = new int[_halfWidth * _halfHeight];
        _pollutionRaw = new int[_halfWidth * _halfHeight];
        _pollutionScratch = new int[_halfWidth * _halfHeight];
        _crime = new int[_halfWidth * _halfHeight];
        _density = new int[_halfWidth * _halfHeight];
        _densityScratch = new int[_halfWidth * _halfHeight];
        _quarterWidth = (map.Width + 3) / 4;
        _quarterHeight = (map.Height + 3) / 4;
        _terrain = new int[_quarterWidth * _quarterHeight];
        _terrainScratch = new int[_quarterWidth * _quarterHeight];
        _eighthWidth = (map.Width + 7) / 8;
        _commercialRate = new int[_eighthWidth * ((map.Height + 7) / 8)];
        CenterX = map.Width / 2;
        CenterY = map.Height / 2;
    }

    /// <summary>The number of 2 by 2 blocks the maps hold for a map of a size.</summary>
    public static int BlocksFor(int mapWidth, int mapHeight) => ((mapWidth + 1) / 2) * ((mapHeight + 1) / 2);

    /// <summary>The column of the city's centre of mass: the average of its zones' centres.</summary>
    public int CenterX { get; private set; }

    /// <summary>The row of the city's centre of mass.</summary>
    public int CenterY { get; private set; }

    /// <summary>The average land value of the blocks with something built on them, as of the last time it was worked out; 0 when nothing is built.</summary>
    public int LandValueAverage { get; private set; }

    /// <summary>The average pollution of the blocks with any, as of the last time it was worked out; 0 when none has any.</summary>
    public int PollutionAverage { get; private set; }

    /// <summary>The average crime of the blocks with land value, as of the last time it was worked out; 0 when none has any.</summary>
    public int CrimeAverage { get; private set; }

    /// <summary>The land value under a tile, from 0 for a block with nothing built on it up to <see cref="MaxLandValue"/>.</summary>
    public int LandValueAt(int x, int y) => _map.Contains(x, y) ? _landValue[((y / 2) * _halfWidth) + (x / 2)] : 0;

    /// <summary>The pollution around a tile, from 0 to <see cref="MostPollution"/>.</summary>
    public int PollutionAt(int x, int y) => _map.Contains(x, y) ? _pollution[((y / 2) * _halfWidth) + (x / 2)] : 0;

    /// <summary>The crime around a tile, from 0 to <see cref="MostCrime"/>; 0 where there is no land value.</summary>
    public int CrimeAt(int x, int y) => _map.Contains(x, y) ? _crime[((y / 2) * _halfWidth) + (x / 2)] : 0;

    /// <summary>The population density around a tile.</summary>
    public int DensityAt(int x, int y) => _map.Contains(x, y) ? _density[((y / 2) * _halfWidth) + (x / 2)] : 0;

    /// <summary>How much commerce wants to be at a tile: higher nearer the city's centre.</summary>
    public int CommercialRateAt(int x, int y) => _map.Contains(x, y) ? _commercialRate[((y / 8) * _eighthWidth) + (x / 8)] : 0;

    /// <summary>
    /// Works land value and pollution out again, as Micropolis does in one pass. For each 2 by 2
    /// block with something built on it, land value is closeness to the city's centre plus the worth
    /// of the terrain around it, less the pollution there and 20 more where crime is high, from 1 to
    /// <see cref="MaxLandValue"/>; a block with nothing built on it has none. The terrain's worth,
    /// the pollution, and the crime it reads are as of the last time each was worked out; a wooded
    /// park counts as trees, not as something built, and a fountain counts as built. Pollution is
    /// what the block's industry, coal plants, busy roads, and fires give off, smoothed twice; a
    /// burning tile is rubble, not something built.
    /// </summary>
    /// <param name="traffic">The traffic each road shows, which pollutes.</param>
    /// <param name="fires">The fires burning, which pollute.</param>
    public void UpdateLandValue(TrafficMap traffic, Fires fires)
    {
        ArgumentNullException.ThrowIfNull(traffic);
        ArgumentNullException.ThrowIfNull(fires);

        Array.Clear(_terrainScratch);
        long total = 0;
        var count = 0;
        for (var blockY = 0; blockY < _halfHeight; blockY++)
        {
            for (var blockX = 0; blockX < _halfWidth; blockX++)
            {
                var built = false;
                var pollution = 0;
                for (var y = blockY * 2; y <= (blockY * 2) + 1; y++)
                {
                    for (var x = blockX * 2; x <= (blockX * 2) + 1; x++)
                    {
                        var state = _map.StateAtOrDefault(x, y, new TileState(TileKind.Grass));
                        if (fires.IsBurning(x, y))
                        {
                            pollution += FirePollution;
                            continue;
                        }

                        var natural = state.Use == TileUse.None
                            ? state.Kind is TileKind.Trees or TileKind.Water
                            : state.Use == TileUse.Park && Parks.IsWoodedAt(x, y);
                        if (state.Use != TileUse.None && !natural)
                        {
                            built = true;
                            pollution += PollutionOf(x, y, state, traffic);
                        }
                        else if (natural)
                        {
                            _terrainScratch[((blockY / 2) * _quarterWidth) + (blockX / 2)] += TerrainPerTile;
                        }
                    }
                }

                var block = (blockY * _halfWidth) + blockX;
                _pollutionRaw[block] = Math.Min(pollution, MostBlockPollution);

                var value = 0;
                if (built)
                {
                    value = ((LandValueReach - DistanceToCenter(blockX, blockY)) * 4) + _terrain[((blockY / 2) * _quarterWidth) + (blockX / 2)];
                    value -= _pollution[block];
                    if (_crime[block] > HighCrime)
                    {
                        value -= HighCrimeLandValue;
                    }

                    value = Math.Clamp(value, 1, MaxLandValue);
                    total += value;
                    count++;
                }

                _landValue[block] = value;
            }
        }

        LandValueAverage = count > 0 ? (int)(total / count) : 0;

        Smooth(_pollutionRaw, _pollutionScratch, _halfWidth, _halfHeight);
        Smooth(_pollutionScratch, _pollution, _halfWidth, _halfHeight);
        PollutionAverage = AverageOfNonZero(_pollution);

        SmoothTerrain();
    }

    /// <summary>
    /// Works crime out again, as Micropolis does once the police coverage has spread: for each block
    /// with land value, 128 less the land value, plus the population density, no more than 300, less
    /// the police coverage there, from 0 to <see cref="MostCrime"/>. A block with no land value has none.
    /// </summary>
    /// <param name="services">The police coverage, spread this cycle.</param>
    public void UpdateCrime(ServiceCoverage services)
    {
        ArgumentNullException.ThrowIfNull(services);

        long total = 0;
        var count = 0;
        for (var blockY = 0; blockY < _halfHeight; blockY++)
        {
            for (var blockX = 0; blockX < _halfWidth; blockX++)
            {
                var block = (blockY * _halfWidth) + blockX;
                var landValue = _landValue[block];
                if (landValue == 0)
                {
                    _crime[block] = 0;
                    continue;
                }

                var crime = Math.Min(CrimeBase - landValue + _density[block], MostCrimeBeforePolice);
                crime -= services.PoliceCoverageAt(blockX * 2, blockY * 2);
                crime = Math.Clamp(crime, 0, MostCrime);
                _crime[block] = crime;
                total += crime;
                count++;
            }
        }

        CrimeAverage = count > 0 ? (int)(total / count) : 0;
    }

    // A tile's pollution: a developed industrial zone's or a coal plant's, whichever tile of it this
    // is, or a road's by the traffic it showed when the scan last passed.
    private int PollutionOf(int x, int y, TileState state, TrafficMap traffic) => state.Use switch
    {
        TileUse.Industrial => _map.ZoneStateAt(x, y).Development > 0 ? IndustryPollution : 0,
        TileUse.CoalPowerPlant => CoalPlantPollution,
        TileUse.Seaport or TileUse.Airport => _definitions.Facility(state.Use).Pollution,
        _ => traffic.LevelAt(x, y) switch
        {
            TrafficLevel.Light => LightTrafficPollution,
            TrafficLevel.Heavy => HeavyTrafficPollution,
            _ => 0,
        },
    };

    // The average of the blocks that have any, as Micropolis counts it.
    private static int AverageOfNonZero(int[] blocks)
    {
        long total = 0;
        var count = 0;
        foreach (var value in blocks)
        {
            if (value != 0)
            {
                total += value;
                count++;
            }
        }

        return count > 0 ? (int)(total / count) : 0;
    }

    /// <summary>
    /// Works population density out again, from each zone's population spread over its block and
    /// smoothed three times, and with it the city's centre of mass and the commercial rate.
    /// </summary>
    public void UpdateDensity(CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        Array.Clear(_density);
        long totalX = 0;
        long totalY = 0;
        var zones = 0;
        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (!state.IsZoneCenter)
                {
                    continue;
                }

                // Residents count as they are; each commercial and industrial unit as eight.
                var population = definitions.Zone(state.Use).PopulationOf(_map, x, y);
                var weight = state.Use == TileUse.Residential ? population : population * 8;
                _density[((y / 2) * _halfWidth) + (x / 2)] = Math.Min(weight * 8, 254);
                totalX += x;
                totalY += y;
                zones++;
            }
        }

        Smooth(_density, _densityScratch, _halfWidth, _halfHeight);
        Smooth(_densityScratch, _density, _halfWidth, _halfHeight);
        Smooth(_density, _densityScratch, _halfWidth, _halfHeight);
        for (var i = 0; i < _density.Length; i++)
        {
            _density[i] = _densityScratch[i] * 2;
        }

        (CenterX, CenterY) = zones > 0 ? ((int)(totalX / zones), (int)(totalY / zones)) : (_map.Width / 2, _map.Height / 2);

        for (var i = 0; i < _commercialRate.Length; i++)
        {
            var (blockX, blockY) = (i % _eighthWidth, i / _eighthWidth);
            _commercialRate[i] = 64 - (DistanceToCenter(blockX * 4, blockY * 4) / 4);
        }
    }

    // In 2 by 2 blocks, across plus down, no farther than 32.
    private int DistanceToCenter(int blockX, int blockY) =>
        Math.Min(Math.Abs(blockX - (CenterX / 2)) + Math.Abs(blockY - (CenterY / 2)), FarthestDistance);

    // Each cell becomes a quarter of itself and its four neighbours, no more than 255.
    private static void Smooth(int[] from, int[] to, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var z = from[(y * width) + x];
                z += x > 0 ? from[(y * width) + x - 1] : 0;
                z += x + 1 < width ? from[(y * width) + x + 1] : 0;
                z += y > 0 ? from[((y - 1) * width) + x] : 0;
                z += y + 1 < height ? from[((y + 1) * width) + x] : 0;
                to[(y * width) + x] = Math.Min(z / 4, 255);
            }
        }
    }

    // The terrain's worth spreads a quarter to its neighbours and keeps half of itself.
    private void SmoothTerrain()
    {
        for (var y = 0; y < _quarterHeight; y++)
        {
            for (var x = 0; x < _quarterWidth; x++)
            {
                var around = 0;
                around += x > 0 ? _terrainScratch[(y * _quarterWidth) + x - 1] : 0;
                around += x + 1 < _quarterWidth ? _terrainScratch[(y * _quarterWidth) + x + 1] : 0;
                around += y > 0 ? _terrainScratch[((y - 1) * _quarterWidth) + x] : 0;
                around += y + 1 < _quarterHeight ? _terrainScratch[((y + 1) * _quarterWidth) + x] : 0;
                _terrain[(y * _quarterWidth) + x] = (around / 4) + (_terrainScratch[(y * _quarterWidth) + x] / 2);
            }
        }
    }

    internal CityMapsSave Capture() => new()
    {
        CenterX = CenterX,
        CenterY = CenterY,
        LandValueAverage = LandValueAverage,
        LandValue = [.. _landValue],
        Density = [.. _density],
        Terrain = [.. _terrain],
        CommercialRate = [.. _commercialRate],
    };

    internal void Restore(CityMapsSave save)
    {
        CenterX = SaveChecks.InRange(save.CenterX, 0, _map.Width, "cityMaps.centerX");
        CenterY = SaveChecks.InRange(save.CenterY, 0, _map.Height, "cityMaps.centerY");
        LandValueAverage = SaveChecks.InRange(save.LandValueAverage, 0, MaxLandValue, "cityMaps.landValueAverage");
        SaveChecks.CopyInto(save.LandValue, _landValue, "cityMaps.landValue");
        SaveChecks.CopyInto(save.Density, _density, "cityMaps.density");
        SaveChecks.CopyInto(save.Terrain, _terrain, "cityMaps.terrain");
        SaveChecks.CopyInto(save.CommercialRate, _commercialRate, "cityMaps.commercialRate");
    }

    internal BlockMapSave CapturePollution() => new() { Blocks = [.. _pollution], Average = PollutionAverage };

    internal BlockMapSave CaptureCrime() => new() { Blocks = [.. _crime], Average = CrimeAverage };

    internal void RestorePollution(BlockMapSave save)
    {
        PollutionAverage = SaveChecks.InRange(save.Average, 0, MostPollution, "pollution.average");
        RestoreBlocks(save.Blocks, _pollution, MostPollution, "pollution.blocks");
    }

    internal void RestoreCrime(BlockMapSave save)
    {
        CrimeAverage = SaveChecks.InRange(save.Average, 0, MostCrime, "crime.average");
        RestoreBlocks(save.Blocks, _crime, MostCrime, "crime.blocks");
    }

    private static void RestoreBlocks(int[]? from, int[] to, int most, string at)
    {
        SaveChecks.CopyInto(from, to, at);
        for (var i = 0; i < to.Length; i++)
        {
            SaveChecks.InRange(to[i], 0, most, $"{at}[{i}]");
        }
    }
}
