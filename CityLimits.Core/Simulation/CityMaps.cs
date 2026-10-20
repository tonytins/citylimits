namespace CityLimits.Core.Simulation;

/// <summary>
/// The city's overlay maps, as Micropolis keeps them: land value and population density on a grid
/// of 2 by 2 tile blocks, the terrain's worth on 4 by 4 blocks, and the commercial rate on 8 by 8
/// blocks, with the city's centre of mass that land value and the commercial rate measure from.
/// Land value is worked out in phase 12 and population density in phase 14. Every grid is made
/// once, with the map, and reused.
/// </summary>
/// <remarks>
/// Until the systems they come from exist, pollution and crime are none, so land value is the
/// distance to the city's centre and the terrain alone.
/// </remarks>
public sealed class CityMaps
{
    /// <summary>The most land value a block can have.</summary>
    public const int MaxLandValue = 250;

    // Land value starts from 34 blocks out from the centre, times four, and stops falling at 32.
    private const int LandValueReach = 34;
    private const int FarthestDistance = 32;
    private const int TerrainPerTile = 15;

    private readonly TileMap _map;
    private readonly int _halfWidth;
    private readonly int _halfHeight;
    private readonly int[] _landValue;
    private readonly int[] _density;
    private readonly int[] _densityScratch;
    private readonly int _quarterWidth;
    private readonly int _quarterHeight;
    private readonly int[] _terrain;
    private readonly int[] _terrainScratch;
    private readonly int _eighthWidth;
    private readonly int[] _commercialRate;

    /// <summary>Makes the maps for a city's map; nothing is worked out until the phases run.</summary>
    public CityMaps(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _halfWidth = (map.Width + 1) / 2;
        _halfHeight = (map.Height + 1) / 2;
        _landValue = new int[_halfWidth * _halfHeight];
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

    /// <summary>The column of the city's centre of mass: the average of its zones' centres.</summary>
    public int CenterX { get; private set; }

    /// <summary>The row of the city's centre of mass.</summary>
    public int CenterY { get; private set; }

    /// <summary>The land value under a tile, from 0 for a block with nothing built on it up to <see cref="MaxLandValue"/>.</summary>
    public int LandValueAt(int x, int y) => _map.Contains(x, y) ? _landValue[((y / 2) * _halfWidth) + (x / 2)] : 0;

    /// <summary>The population density around a tile.</summary>
    public int DensityAt(int x, int y) => _map.Contains(x, y) ? _density[((y / 2) * _halfWidth) + (x / 2)] : 0;

    /// <summary>How much commerce wants to be at a tile: higher nearer the city's centre.</summary>
    public int CommercialRateAt(int x, int y) => _map.Contains(x, y) ? _commercialRate[((y / 8) * _eighthWidth) + (x / 8)] : 0;

    /// <summary>
    /// Works land value out again: for each 2 by 2 block with something built on it, closeness to
    /// the city's centre plus the worth of the terrain around it, from 1 to <see cref="MaxLandValue"/>;
    /// a block with nothing built on it has none. The terrain's worth is the trees and water nearby,
    /// as of the last time land value was worked out, as Micropolis has it.
    /// </summary>
    public void UpdateLandValue()
    {
        Array.Clear(_terrainScratch);
        for (var blockY = 0; blockY < _halfHeight; blockY++)
        {
            for (var blockX = 0; blockX < _halfWidth; blockX++)
            {
                var built = false;
                for (var y = blockY * 2; y <= (blockY * 2) + 1; y++)
                {
                    for (var x = blockX * 2; x <= (blockX * 2) + 1; x++)
                    {
                        var state = _map.StateAtOrDefault(x, y, new TileState(TileKind.Grass));
                        if (state.Use != TileUse.None)
                        {
                            built = true;
                        }
                        else if (state.Kind is TileKind.Trees or TileKind.Water)
                        {
                            _terrainScratch[((blockY / 2) * _quarterWidth) + (blockX / 2)] += TerrainPerTile;
                        }
                    }
                }

                var value = 0;
                if (built)
                {
                    value = ((LandValueReach - DistanceToCenter(blockX, blockY)) * 4) + _terrain[((blockY / 2) * _quarterWidth) + (blockX / 2)];
                    value = Math.Clamp(value, 1, MaxLandValue);
                }

                _landValue[(blockY * _halfWidth) + blockX] = value;
            }
        }

        SmoothTerrain();
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
}
