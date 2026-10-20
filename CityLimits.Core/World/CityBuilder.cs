using CityLimits.Core.Economy;

namespace CityLimits.Core.World;

/// <summary>Applies the player's building work to the city, and takes the money for it.</summary>
public sealed class CityBuilder
{
    private readonly TileMap _map;
    private readonly Treasury _treasury;
    private readonly RoadNetwork _roads;

    // Apply works the stroke out again here, so a preview the player saw earlier can't go stale.
    private readonly BuildPlan _applying = new();

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

    /// <summary>
    /// Works out what a stroke would do if it were applied now, without changing anything.
    /// A tile the stroke can't change is skipped with its reason; the stroke as a whole is
    /// refused when it runs off the map or the city can't pay for all of it.
    /// </summary>
    /// <exception cref="ArgumentNullException">The stroke or the plan is null.</exception>
    public void Plan(BuildStroke stroke, BuildPlan plan)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentNullException.ThrowIfNull(plan);

        var bounds = stroke.Bounds;
        plan.Start(_map, bounds);

        if (!bounds.IsInside(_map))
        {
            plan.Refuse(BuildRefusal.OffTheMap);
            return;
        }

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var work = WorkOn(stroke.Action, _map.StateAt(x, y));
                if (work.Reason == BuildRefusal.None)
                {
                    plan.Change(x, y, work.State, work.Cost, work.Refund);
                }
                else
                {
                    plan.Skip(work.Reason);
                }
            }
        }

        if (plan.Cost > _treasury.Funds)
        {
            plan.Refuse(BuildRefusal.NotEnoughFunds);
        }
    }

    /// <summary>
    /// Applies a stroke: every tile it can change, or none of them when it runs off the map or
    /// the city can't pay for all of it. It is worked out again against the map as it is now.
    /// </summary>
    /// <exception cref="ArgumentNullException">The stroke is null.</exception>
    public BuildResult Apply(BuildStroke stroke)
    {
        Plan(stroke, _applying);
        var plan = _applying;

        if (plan.Refusal != BuildRefusal.None)
        {
            return BuildResult.Refused(plan.Refusal, plan.Cost);
        }

        if (plan.Tiles.Count == 0)
        {
            return NothingChanged(plan);
        }

        if (!_treasury.TrySpend(plan.Cost))
        {
            return BuildResult.Refused(BuildRefusal.NotEnoughFunds, plan.Cost);
        }

        var roadsChanged = false;
        foreach (var tile in plan.Tiles)
        {
            roadsChanged |= tile.State.IsRoad || _map.StateAt(tile.X, tile.Y).IsRoad;
            _map.SetState(tile.X, tile.Y, tile.State);
        }

        _treasury.Refund(plan.Refund);
        if (roadsChanged)
        {
            _roads.MarkDirty();
        }

        return BuildResult.Built(plan.Cost, plan.Refund, plan.Tiles.Count);
    }

    /// <summary>Applies a tool to one tile: a stroke that starts and ends there.</summary>
    public BuildResult Apply(BuildAction action, int x, int y) => Apply(new BuildStroke(action, x, y));

    // When nothing would change, the result names the reason that matters most to the player.
    private static BuildResult NothingChanged(BuildPlan plan)
    {
        if (plan.SkippedBecause(BuildRefusal.CannotBuildHere) > 0)
        {
            return BuildResult.Refused(BuildRefusal.CannotBuildHere);
        }

        return plan.SkippedBecause(BuildRefusal.AlreadyDone) > 0
            ? BuildResult.Unchanged(BuildRefusal.AlreadyDone)
            : BuildResult.Unchanged(BuildRefusal.NothingToClear);
    }

    // The rules for one tile: what it would become, and what that costs and gives back.
    // It decides only; Plan collects the answers and Apply makes them happen.
    private static TileWork WorkOn(BuildAction action, TileState state) => action switch
    {
        BuildAction.Road => LayRoad(state),
        BuildAction.Bulldoze => Bulldoze(state),
        _ => Zone(state, ZoneFor(action)),
    };

    private static TileUse ZoneFor(BuildAction action) => action switch
    {
        BuildAction.ZoneCommercial => TileUse.Commercial,
        BuildAction.ZoneIndustrial => TileUse.Industrial,
        _ => TileUse.Residential,
    };

    private static TileWork LayRoad(TileState state)
    {
        if (state.IsRoad)
        {
            return TileWork.Skip(BuildRefusal.AlreadyDone);
        }

        if (state.IsZoned || !TileRules.CanLayRoad(state.Kind))
        {
            return TileWork.Skip(BuildRefusal.CannotBuildHere);
        }

        return new TileWork(new TileState(TileKind.Grass, TileUse.Road), TileCosts.Road + ClearingCost(state));
    }

    private static TileWork Zone(TileState state, TileUse use)
    {
        if (state.Use == use)
        {
            return TileWork.Skip(BuildRefusal.AlreadyDone);
        }

        if (state.IsRoad || !TileRules.CanZone(state.Kind))
        {
            return TileWork.Skip(BuildRefusal.CannotBuildHere);
        }

        return new TileWork(new TileState(TileKind.Grass, use), TileCosts.Zone + ClearingCost(state));
    }

    private static TileWork Bulldoze(TileState state)
    {
        if (!TileRules.HasSomethingToClear(state))
        {
            return TileWork.Skip(BuildRefusal.NothingToClear);
        }

        var refund = state.Use switch
        {
            TileUse.Road => TileCosts.RefundFor(TileCosts.Road),
            TileUse.Residential or TileUse.Commercial or TileUse.Industrial => TileCosts.RefundFor(TileCosts.Zone),
            _ => 0,
        };

        return new TileWork(new TileState(TileKind.Grass), 0, refund);
    }

    private static int ClearingCost(TileState state) =>
        TileRules.NeedsClearing(state.Kind) ? TileCosts.Clearing : 0;

    private readonly record struct TileWork(TileState State, int Cost = 0, int Refund = 0, BuildRefusal Reason = BuildRefusal.None)
    {
        public static TileWork Skip(BuildRefusal reason) => new(default, Reason: reason);
    }
}
