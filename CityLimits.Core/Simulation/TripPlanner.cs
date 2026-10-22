using CityLimits.Core.Randomness;

namespace CityLimits.Core.Simulation;

/// <summary>What came of a zone's trip.</summary>
public enum TripOutcome
{
    /// <summary>No road or rail runs along the zone's edge, so nobody could set out; the worst outcome.</summary>
    NoRoad,

    /// <summary>The trip set out but found nowhere to go within its reach.</summary>
    NoDestination,

    /// <summary>The trip reached a zone of a kind it was looking for.</summary>
    Arrived,
}

/// <summary>
/// A zone's trip, as in Micropolis: from the first road or rail along the zone's edge, a drive of up to
/// <see cref="MaxDistance"/> road and rail tiles, each step in a direction picked at random from the city's
/// own generator, looking for a zone of a kind the zone's spec lists in <see cref="ZoneSpec.TripsTo"/>.
/// </summary>
/// <remarks>
/// The route is remembered every other step, and a trip that arrives lays traffic along it on the
/// city's <see cref="TrafficMap"/>, when it has one. A dead end drops the last remembered step and
/// three steps of the drive's reach without moving the driver, as Micropolis does, so a dead end
/// usually ends the trip.
/// </remarks>
public sealed class TripPlanner(TrafficMap? traffic = null)
{
    /// <summary>The most road and rail tiles a trip travels.</summary>
    public const int MaxDistance = 30;

    // The 12 tiles along a zone's edges, just outside it, in the order a trip looks for a road:
    // across the top, down the right, back along the bottom, and up the left.
    private static readonly (int Dx, int Dy)[] Edge =
    [
        (-1, -2), (0, -2), (1, -2),
        (2, -1), (2, 0), (2, 1),
        (1, 2), (0, 2), (-1, 2),
        (-2, 1), (-2, 0), (-2, -1),
    ];

    // North, east, south, and west; a direction's opposite is two along.
    private static readonly (int Dx, int Dy)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private const int NoDirection = -1;

    private readonly List<(int X, int Y)> _route = [];

    /// <summary>The road and rail tiles the last trip passed, every other step, in the order it passed them; only the road tiles take traffic.</summary>
    public IReadOnlyList<(int X, int Y)> Route => _route;

    /// <summary>Sends a trip out from the zone centred on a tile, and says what came of it.</summary>
    public TripOutcome Drive(TileMap map, CityDefinitions definitions, DeterministicRandom random, int centerX, int centerY)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(random);

        _route.Clear();
        var spec = definitions.Zone(map.StateAt(centerX, centerY).Use);

        if (!TryFindEdgeRoad(map, centerX, centerY, out var x, out var y))
        {
            return TripOutcome.NoRoad;
        }

        var cameFrom = NoDirection;
        for (var step = 0; step < MaxDistance; step++)
        {
            if (TryStep(map, random, ref x, ref y, ref cameFrom, step))
            {
                if (IsBesideDestination(map, definitions, spec, x, y))
                {
                    traffic?.AddRoute(map, _route);
                    return TripOutcome.Arrived;
                }
            }
            else if (_route.Count > 0)
            {
                _route.RemoveAt(_route.Count - 1);
                step += 3;
            }
            else
            {
                return TripOutcome.NoDestination;
            }
        }

        return TripOutcome.NoDestination;
    }

    private static bool TryFindEdgeRoad(TileMap map, int centerX, int centerY, out int x, out int y)
    {
        foreach (var (dx, dy) in Edge)
        {
            if (map.StateAtOrDefault(centerX + dx, centerY + dy).CarriesTrips)
            {
                (x, y) = (centerX + dx, centerY + dy);
                return true;
            }
        }

        (x, y) = (centerX, centerY);
        return false;
    }

    // One step along the road or rail, which trips use alike: the directions are tried from a
    // random one round, never straight back.
    private bool TryStep(TileMap map, DeterministicRandom random, ref int x, ref int y, ref int cameFrom, int step)
    {
        var first = random.NextInt(0, Directions.Length);
        for (var turn = 0; turn < Directions.Length; turn++)
        {
            var direction = (first + turn) % Directions.Length;
            if (direction == cameFrom)
            {
                continue;
            }

            var (dx, dy) = Directions[direction];
            if (!map.StateAtOrDefault(x + dx, y + dy).CarriesTrips)
            {
                continue;
            }

            x += dx;
            y += dy;
            cameFrom = (direction + 2) % Directions.Length;
            if (step % 2 == 1)
            {
                _route.Add((x, y));
            }

            return true;
        }

        return false;
    }

    private static bool IsBesideDestination(TileMap map, CityDefinitions definitions, ZoneSpec spec, int x, int y)
    {
        foreach (var (dx, dy) in Directions)
        {
            if (map.Contains(x + dx, y + dy) && IsDestination(map, definitions, spec, x + dx, y + dy))
            {
                return true;
            }
        }

        return false;
    }

    // A zoned tile of a kind the trip is looking for; a residential one only once it has people.
    private static bool IsDestination(TileMap map, CityDefinitions definitions, ZoneSpec spec, int x, int y)
    {
        var state = map.StateAt(x, y);
        if (!state.IsZoned || !spec.TripsTo.Contains(state.Use))
        {
            return false;
        }

        if (state.Use != TileUse.Residential)
        {
            return true;
        }

        var (centerX, centerY) = map.ZoneCenterOf(x, y);
        return state.HasHouse || map.StateAt(centerX, centerY).Development > 0;
    }
}
