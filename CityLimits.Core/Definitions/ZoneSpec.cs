namespace CityLimits.Core.Definitions;

/// <summary>
/// What a kind of zone is, as data rather than code: its name, its price, and how it grows. The
/// base game's specs are Micropolis' numbers, and a mod can change them.
/// </summary>
/// <param name="Id">The id definitions and mods name the zone by, such as <c>residential</c>.</param>
/// <param name="Use">The use the zone's tiles carry.</param>
/// <param name="Name">The zone's name, as the player sees it.</param>
/// <param name="Price">What one 3 by 3 zone costs to lay.</param>
/// <param name="Populations">
/// The population of the zone's building at each development level, from level 1 up. The number
/// of levels is fixed by the drawings the game has for them; a mod can change the numbers.
/// </param>
/// <param name="ValueClasses">How many land-value classes the zone's buildings come in, fixed by the drawings.</param>
/// <param name="Houses">
/// How many single houses the zone grows on its outer tiles before its first building, each
/// counting one; 0 for a zone that builds straight away.
/// </param>
/// <param name="TripsTo">
/// The kinds of zone a trip from this zone is looking for. A residential zone counts as a place
/// to go only once it has houses or a building; the others count as soon as they are zoned.
/// </param>
public sealed record ZoneSpec(
    string Id,
    TileUse Use,
    string Name,
    int Price,
    IReadOnlyList<int> Populations,
    int ValueClasses,
    int Houses,
    IReadOnlyList<TileUse> TripsTo)
{
    /// <summary>The most building levels the zone has.</summary>
    public int Levels => Populations.Count;

    /// <summary>
    /// The population of a zone, read from its centre and, for a zone with no building, the houses
    /// on its outer tiles.
    /// </summary>
    public int PopulationOf(TileMap map, int centerX, int centerY)
    {
        ArgumentNullException.ThrowIfNull(map);

        var center = map.StateAt(centerX, centerY);
        if (center.Development > 0)
        {
            return Populations[Math.Min(center.Development, Levels) - 1];
        }

        var houses = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                houses += map.StateAtOrDefault(centerX + dx, centerY + dy).HasHouse ? 1 : 0;
            }
        }

        return houses;
    }
}
