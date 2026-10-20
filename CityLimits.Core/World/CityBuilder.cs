using CityLimits.Core.Economy;

namespace CityLimits.Core.World;

/// <summary>Applies the player's building work to the city, and takes the money for it.</summary>
public sealed class CityBuilder
{
    private readonly TileMap _map;
    private readonly Treasury _treasury;
    private readonly RoadNetwork _roads;

    /// <summary>Creates a builder that works on the given map.</summary>
    /// <exception cref="ArgumentNullException">One of the parts of the city is null.</exception>
    public CityBuilder(TileMap map, Treasury treasury, RoadNetwork roads)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(treasury);
        ArgumentNullException.ThrowIfNull(roads);

        _map = map;
        _treasury = treasury;
        _roads = roads;
    }

    /// <summary>Does the work, and tells the player what came of it.</summary>
    public BuildResult Apply(BuildAction action, int x, int y)
    {
        if (!_map.Contains(x, y))
        {
            return BuildResult.Refused(BuildRefusal.OffTheMap);
        }

        var state = _map.StateAt(x, y);

        return action switch
        {
            BuildAction.Road => LayRoad(x, y, state),
            BuildAction.Bulldoze => Bulldoze(x, y, state),
            _ => Zone(x, y, state, ZoneFor(action)),
        };
    }

    private static TileUse ZoneFor(BuildAction action) => action switch
    {
        BuildAction.ZoneCommercial => TileUse.Commercial,
        BuildAction.ZoneIndustrial => TileUse.Industrial,
        _ => TileUse.Residential,
    };

    private BuildResult LayRoad(int x, int y, TileState state)
    {
        if (state.IsRoad)
        {
            return BuildResult.Unchanged(BuildRefusal.AlreadyDone);
        }

        if (state.IsZoned)
        {
            return BuildResult.Refused(BuildRefusal.CannotBuildHere);
        }

        if (!TileRules.CanLayRoad(state.Kind))
        {
            return BuildResult.Refused(BuildRefusal.CannotBuildHere);
        }

        var cost = TileCosts.Road + ClearingCost(state);
        if (!_treasury.TrySpend(cost))
        {
            return BuildResult.Refused(BuildRefusal.NotEnoughFunds, cost);
        }

        _map.SetState(x, y, new TileState(TileKind.Grass, TileUse.Road));
        _roads.MarkDirty();
        return BuildResult.Built(cost);
    }

    private BuildResult Zone(int x, int y, TileState state, TileUse use)
    {
        if (state.IsRoad)
        {
            return BuildResult.Refused(BuildRefusal.CannotBuildHere);
        }

        if (state.Use == use)
        {
            return BuildResult.Unchanged(BuildRefusal.AlreadyDone);
        }

        if (!TileRules.CanZone(state.Kind))
        {
            return BuildResult.Refused(BuildRefusal.CannotBuildHere);
        }

        var cost = TileCosts.Zone + ClearingCost(state);
        if (!_treasury.TrySpend(cost))
        {
            return BuildResult.Refused(BuildRefusal.NotEnoughFunds, cost);
        }

        _map.SetState(x, y, new TileState(TileKind.Grass, use));
        return BuildResult.Built(cost);
    }

    private BuildResult Bulldoze(int x, int y, TileState state)
    {
        if (!TileRules.HasSomethingToClear(state))
        {
            return BuildResult.Unchanged(BuildRefusal.NothingToClear);
        }

        var refund = state.Use switch
        {
            TileUse.Road => TileCosts.RefundFor(TileCosts.Road),
            TileUse.Residential or TileUse.Commercial or TileUse.Industrial => TileCosts.RefundFor(TileCosts.Zone),
            _ => 0,
        };

        _map.SetState(x, y, new TileState(TileKind.Grass));
        _treasury.Refund(refund);

        if (state.IsRoad)
        {
            _roads.MarkDirty();
        }

        return BuildResult.Built(refund: refund);
    }

    private static int ClearingCost(TileState state) =>
        TileRules.NeedsClearing(state.Kind) ? TileCosts.Clearing : 0;
}
