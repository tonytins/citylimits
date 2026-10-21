namespace CityLimits.Core.World;

/// <summary>
/// What a park tile looks like, and what it is to the city. As in Micropolis, a park is one of
/// four wooded drawings or, one time in five, a fountain. The owner chose to pick the look from
/// the tile's place rather than the simulation's generator, so the preview shows what will be
/// built and building never spends the city's randomness.
/// </summary>
public static class Parks
{
    /// <summary>The number of looks a park has: four wooded, then the fountain.</summary>
    public const int Looks = 5;

    /// <summary>The look that is a fountain.</summary>
    public const int FountainLook = Looks - 1;

    /// <summary>The look of the park on a tile, from 0 to <see cref="FountainLook"/>, the same every time for the same tile.</summary>
    public static int LookAt(int x, int y)
    {
        // A small integer hash, so neighbouring tiles don't fall into a visible pattern.
        var hash = unchecked((uint)((x * 73856093) ^ (y * 19349663)));
        hash ^= hash >> 13;
        hash *= 0x5BD1E995;
        hash ^= hash >> 15;
        return (int)(hash % Looks);
    }

    /// <summary>
    /// Tells whether the park on a tile is wooded, which counts as trees do toward the land
    /// value around it; a fountain counts as something built, as in Micropolis.
    /// </summary>
    public static bool IsWoodedAt(int x, int y) => LookAt(x, y) != FountainLook;
}
