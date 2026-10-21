namespace CityLimits.Core.World;

/// <summary>
/// Which sides of a tile carry power to the tile beside them, as in Micropolis: a power line on
/// every side, a road-and-line crossing only along its line, and every tile of a zone or a plant.
/// </summary>
public static class PowerLines
{
    /// <summary>Tells whether power can leave a tile through one of its sides.</summary>
    public static bool RunsToward(TileState state, RoadConnections side) => state.Use switch
    {
        TileUse.PowerLine => true,
        TileUse.RoadAcrossPowerLine => side is RoadConnections.North or RoadConnections.South,
        TileUse.RoadDownPowerLine => side is RoadConnections.East or RoadConnections.West,
        _ => state.HasFootprint,
    };

    /// <summary>
    /// Tells which sides of a tile with a power line join something that carries power, read
    /// straight from the map. A tile with no line has none, and the map's edge joins nothing.
    /// </summary>
    public static RoadConnections ConnectionsAt(TileMap map, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(map);

        var state = map.StateAtOrDefault(x, y);
        if (!state.HasPowerLine)
        {
            return RoadConnections.None;
        }

        var connections = RoadConnections.None;
        foreach (var (side, dx, dy) in Sides.All)
        {
            if (RunsToward(state, side) && RunsToward(map.StateAtOrDefault(x + dx, y + dy), Sides.Opposite(side)))
            {
                connections |= side;
            }
        }

        return connections;
    }
}
