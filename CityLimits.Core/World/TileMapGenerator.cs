namespace CityLimits.Core.World;

/// <summary>Builds maps from a seed, so the same seed always yields the same world.</summary>
public static class TileMapGenerator
{
    private const double WaterLevel = 0.32;
    private const double RockLevel = 0.76;
    private const double ForestLevel = 0.60;
    private const int ElevationOctaves = 4;
    private const int MoistureOctaves = 2;

    /// <summary>Generates a map of the given size from the given seed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The width or the height is not positive.</exception>
    public static TileMap Generate(int seed, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var tiles = new TileKind[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var elevation = Octave(seed, x, y, ElevationOctaves, 8);
                var moisture = Octave(seed ^ 0x5F356495, x, y, MoistureOctaves, 6);
                tiles[(y * width) + x] = Classify(elevation, moisture);
            }
        }

        return TileMap.FromTerrain(width, height, tiles);
    }

    private static TileKind Classify(double elevation, double moisture)
    {
        if (elevation < WaterLevel)
        {
            return TileKind.Water;
        }

        if (elevation > RockLevel)
        {
            return TileKind.Rock;
        }

        return moisture > ForestLevel ? TileKind.Trees : TileKind.Grass;
    }

    private static double Octave(int seed, int x, int y, int octaves, int baseFrequency)
    {
        var total = 0.0;
        var amplitude = 1.0;
        var amplitudeSum = 0.0;
        var frequency = baseFrequency;

        for (var octave = 0; octave < octaves; octave++)
        {
            total += amplitude * Noise(seed, x, y, frequency);
            amplitudeSum += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }

        return total / amplitudeSum;
    }

    private static double Noise(int seed, int x, int y, int frequency)
    {
        var scaledX = (double)x / frequency;
        var scaledY = (double)y / frequency;
        var x0 = (int)Math.Floor(scaledX);
        var y0 = (int)Math.Floor(scaledY);
        var fractionX = scaledX - x0;
        var fractionY = scaledY - y0;

        var topLeft = Hash(seed, x0, y0);
        var topRight = Hash(seed, x0 + 1, y0);
        var bottomLeft = Hash(seed, x0, y0 + 1);
        var bottomRight = Hash(seed, x0 + 1, y0 + 1);

        var top = topLeft + ((topRight - topLeft) * Smooth(fractionX));
        var bottom = bottomLeft + ((bottomRight - bottomLeft) * Smooth(fractionX));
        return top + ((bottom - top) * Smooth(fractionY));
    }

    private static double Smooth(double value) => value * value * (3.0 - (2.0 * value));

    private static double Hash(int seed, int x, int y)
    {
        var hash = unchecked(((ulong)(uint)seed * 0x9E3779B97F4A7C15UL)
            ^ ((ulong)(uint)x * 0xC2B2AE3D27D4EB4FUL)
            ^ ((ulong)(uint)y * 0x165667B19E3779F9UL));

        hash ^= hash >> 30;
        hash *= 0xBF58476D1CE4E5B9UL;
        hash ^= hash >> 27;
        hash *= 0x94D049BB133111EBUL;
        hash ^= hash >> 31;
        return (hash >> 11) * (1.0 / 9007199254740992.0);
    }
}
