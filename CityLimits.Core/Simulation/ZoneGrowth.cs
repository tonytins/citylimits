using CityLimits.Core.Randomness;

namespace CityLimits.Core.Simulation;

/// <summary>
/// How a zone takes a step up, as in Micropolis: a residential zone with no building grows single
/// houses on its outer tiles, and once it holds its spec's number of houses they make way for a
/// building; a building grows a level at a time, to the most its spec has.
/// </summary>
internal static class ZoneGrowth
{
    // The outer tiles in the order Micropolis looks at them for a house: across the top, the two
    // sides, then across the bottom.
    private static readonly (int Dx, int Dy)[] Lots =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    ];

    /// <summary>Takes the zone centred on a tile one step up, if it has a step left to take.</summary>
    public static void GrowOneStep(TileMap map, DeterministicRandom random, ZoneSpec spec, int centerX, int centerY)
    {
        var center = map.StateAt(centerX, centerY);

        if (center.Development == 0 && spec.Houses > 0)
        {
            if (CountHouses(map, centerX, centerY) < spec.Houses)
            {
                BuildHouse(map, random, centerX, centerY, center.ValueClass);
                return;
            }

            ClearHouses(map, centerX, centerY);
        }

        if (center.Development < spec.Levels)
        {
            map.SetState(centerX, centerY, new TileState(center.Kind, center.Use, center.Development + 1, valueClass: center.ValueClass));
        }
    }

    private static int CountHouses(TileMap map, int centerX, int centerY)
    {
        var houses = 0;
        foreach (var (dx, dy) in Lots)
        {
            houses += map.StateAt(centerX + dx, centerY + dy).HasHouse ? 1 : 0;
        }

        return houses;
    }

    // A house goes on the free outer tile with the most roads beside it; a tie goes to a later
    // tile one time in eight, from the city's own generator.
    private static void BuildHouse(TileMap map, DeterministicRandom random, int centerX, int centerY, int valueClass)
    {
        var best = -1;
        var bestScore = 0;

        for (var i = 0; i < Lots.Length; i++)
        {
            var (x, y) = (centerX + Lots[i].Dx, centerY + Lots[i].Dy);
            if (map.StateAt(x, y).HasHouse)
            {
                continue;
            }

            var score = 1 + RoadsBeside(map, x, y);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
            else if (score == bestScore && random.NextInt(0, 8) == 0)
            {
                best = i;
            }
        }

        if (best < 0)
        {
            return;
        }

        var (lotX, lotY) = (centerX + Lots[best].Dx, centerY + Lots[best].Dy);
        var lot = map.StateAt(lotX, lotY);
        var house = (valueClass * 3) + random.NextInt(0, 3) + 1;
        map.SetState(lotX, lotY, new TileState(lot.Kind, lot.Use, zonePart: lot.ZonePart, house: house));
    }

    private static void ClearHouses(TileMap map, int centerX, int centerY)
    {
        foreach (var (dx, dy) in Lots)
        {
            var lot = map.StateAt(centerX + dx, centerY + dy);
            map.SetState(centerX + dx, centerY + dy, new TileState(lot.Kind, lot.Use, zonePart: lot.ZonePart));
        }
    }

    private static int RoadsBeside(TileMap map, int x, int y) =>
        (map.StateAtOrDefault(x, y - 1).IsRoad ? 1 : 0) +
        (map.StateAtOrDefault(x + 1, y).IsRoad ? 1 : 0) +
        (map.StateAtOrDefault(x, y + 1).IsRoad ? 1 : 0) +
        (map.StateAtOrDefault(x - 1, y).IsRoad ? 1 : 0);
}
