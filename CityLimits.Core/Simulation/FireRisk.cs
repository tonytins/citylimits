namespace CityLimits.Core.Simulation;

/// <summary>
/// Where the city's fire risk runs high, for the news to hint at before a fire, as SimCity 3000's
/// ticker does, which Tony Bark asked for. It only reads the city. In Micropolis, fire coverage
/// doesn't change how often fires start, only how fast they burn out and how far they spread, so a
/// hint warns of the damage a fire would do, not that one is coming.
/// </summary>
public static class FireRisk
{
    /// <summary>A zone with less fire coverage than this is out of an engine's reach.</summary>
    public const int ReachingCover = 20;

    /// <summary>Fire funding below this effect, out of 1 000, is a cut the firefighters feel.</summary>
    public const int FundingCut = 700;

    /// <summary>A home with at least this many tiles of trees around it stands against the woods.</summary>
    public const int WoodsAround = 4;

    // A city needs more people than this before its fire service is news.
    private const int PeopleBeforeFireIsNews = 20;

    /// <summary>Tells whether more than half the city's zones are out of an engine's reach.</summary>
    public static bool ZonesOutOfReach(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        var (zones, unreached) = (0, 0);
        for (var y = 0; y < city.Map.Height; y++)
        {
            for (var x = 0; x < city.Map.Width; x++)
            {
                if (city.Map.StateAt(x, y).IsZoneCenter)
                {
                    zones++;
                    unreached += city.Services.FireCoverageAt(x, y) < ReachingCover ? 1 : 0;
                }
            }
        }

        return IsNews(city) && unreached * 2 > zones;
    }

    /// <summary>
    /// Tells whether the city's fire stations are funded below <see cref="FundingCut"/>, as the city's
    /// own message about fire funding measures it; a city with no stations asks for nothing, so it is
    /// never cut.
    /// </summary>
    public static bool FundingIsCut(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        return IsNews(city) && city.Budget.FireEffect < FundingCut;
    }

    /// <summary>Tells whether any residential zone with people in it has woods right around it.</summary>
    public static bool HomesAgainstWoods(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (!IsNews(city))
        {
            return false;
        }

        var residential = city.Definitions.Zone(TileUse.Residential);
        for (var y = 0; y < city.Map.Height; y++)
        {
            for (var x = 0; x < city.Map.Width; x++)
            {
                var state = city.Map.StateAt(x, y);
                if (state.Use == TileUse.Residential && state.IsZoneCenter && residential.PopulationOf(city.Map, x, y) > 0 && TreesAround(city.Map, x, y) >= WoodsAround)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Fire risk is news only in a city with people, where fires can start on their own.
    private static bool IsNews(CitySimulation city) => city.RandomFires && city.Census.CityPopulation > PeopleBeforeFireIsNews;

    // The tiles of trees in the ring just outside a 3 by 3 zone.
    private static int TreesAround(TileMap map, int x, int y)
    {
        var trees = 0;
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                if (Math.Abs(dx) == 2 || Math.Abs(dy) == 2)
                {
                    var state = map.StateAtOrDefault(x + dx, y + dy);
                    trees += state.Use == TileUse.None && state.Kind == TileKind.Trees ? 1 : 0;
                }
            }
        }

        return trees;
    }
}
