using CityLimits.Core.Economy;

namespace CityLimits.Core.World;

/// <summary>Applies the player's building work to the city, and takes the money for it.</summary>
public sealed class CityBuilder
{
    private readonly TileMap _map;
    private readonly Treasury _treasury;
    private readonly RoadNetwork _roads;
    private readonly CityDefinitions _definitions;

    // Apply works the stroke out again here, so a preview the player saw earlier can't go stale.
    private readonly BuildPlan _applying = new();

    // The water a road stroke has still to bridge; kept so planning every frame doesn't allocate.
    private readonly List<(int X, int Y)> _water = [];

    /// <summary>Creates a builder that works on the given map, at the prices its definitions set: the base game's when none are given.</summary>
    /// <exception cref="ArgumentNullException">One of the parts of the city is null.</exception>
    public CityBuilder(TileMap map, Treasury treasury, RoadNetwork roads, CityDefinitions? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(treasury);
        ArgumentNullException.ThrowIfNull(roads);

        _map = map;
        _treasury = treasury;
        _roads = roads;
        _definitions = definitions ?? CityDefinitions.Base;
    }

    /// <summary>
    /// Works out what a stroke would do if it were applied now, without changing anything.
    /// A tile or zone the stroke can't change is skipped with its reason; the stroke as a whole
    /// is refused when it runs off the map or the city can't pay for all of it.
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

        switch (stroke.Action)
        {
            case BuildAction.Road:
                PlanRoad(bounds, plan);
                break;
            case BuildAction.Bulldoze:
                PlanBulldozer(bounds, plan);
                break;
            case BuildAction.CoalPowerPlant:
                PlanFootprint(bounds.X, bounds.Y, stroke.Use, TileCosts.CoalPowerPlant, plan);
                break;
            case BuildAction.NuclearPowerPlant:
                PlanFootprint(bounds.X, bounds.Y, stroke.Use, TileCosts.NuclearPowerPlant, plan);
                break;
            default:
                PlanZones(bounds, stroke.Use, plan);
                break;
        }

        if (plan.Cost > _treasury.Funds)
        {
            plan.Refuse(BuildRefusal.NotEnoughFunds);
        }
    }

    /// <summary>
    /// Applies a stroke: everything it can change, or nothing when it runs off the map or the
    /// city can't pay for all of it. It is worked out again against the map as it is now.
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

        if (roadsChanged)
        {
            _roads.MarkDirty();
        }

        return BuildResult.Built(plan.Cost, plan.Tiles.Count);
    }

    /// <summary>Applies a tool where a stroke starts and ends on one tile; a zone is centred there.</summary>
    public BuildResult Apply(BuildAction action, int x, int y) => Apply(new BuildStroke(action, x, y));

    // When nothing would change, the result names the reason that matters most to the player.
    private static BuildResult NothingChanged(BuildPlan plan)
    {
        if (plan.SkippedBecause(BuildRefusal.CannotBuildHere) > 0)
        {
            return BuildResult.Refused(BuildRefusal.CannotBuildHere);
        }

        if (plan.SkippedBecause(BuildRefusal.PartOfAZone) > 0)
        {
            return BuildResult.Refused(BuildRefusal.PartOfAZone);
        }

        return plan.SkippedBecause(BuildRefusal.AlreadyDone) > 0
            ? BuildResult.Unchanged(BuildRefusal.AlreadyDone)
            : BuildResult.Unchanged(BuildRefusal.NothingToClear);
    }

    // A road on land is laid where it can be. A road on water is a bridge, which needs a road
    // beside it to start from, so the water is crossed in passes until no more of it can be:
    // a stroke can start on either bank, as in Micropolis.
    private void PlanRoad(TileRect bounds, BuildPlan plan)
    {
        _water.Clear();

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (state.IsRoad)
                {
                    plan.Skip(BuildRefusal.AlreadyDone);
                }
                else if (state.Kind == TileKind.Water && state.Use == TileUse.None)
                {
                    _water.Add((x, y));
                }
                else if (state.Use != TileUse.None || !TileRules.CanLayRoad(state.Kind))
                {
                    plan.Skip(BuildRefusal.CannotBuildHere);
                }
                else
                {
                    plan.Change(x, y, new TileState(TileKind.Grass, TileUse.Road), TileCosts.Road + ClearingCost(state));
                }
            }
        }

        var bridged = true;
        while (bridged)
        {
            bridged = false;
            for (var i = _water.Count - 1; i >= 0; i--)
            {
                var (x, y) = _water[i];
                if (!plan.StateAt(x, y - 1).IsRoad && !plan.StateAt(x + 1, y).IsRoad &&
                    !plan.StateAt(x, y + 1).IsRoad && !plan.StateAt(x - 1, y).IsRoad)
                {
                    continue;
                }

                plan.Change(x, y, new TileState(TileKind.Water, TileUse.Road), TileCosts.Bridge);
                _water.RemoveAt(i);
                bridged = true;
            }
        }

        for (var i = 0; i < _water.Count; i++)
        {
            plan.Skip(BuildRefusal.CannotBuildHere);
        }
    }

    // Zones are laid as a grid, each one going down whole or not at all.
    private void PlanZones(TileRect bounds, TileUse use, BuildPlan plan)
    {
        for (var top = bounds.Y; top < bounds.Y + bounds.Height; top += ZoneLayout.Size)
        {
            for (var left = bounds.X; left < bounds.X + bounds.Width; left += ZoneLayout.Size)
            {
                PlanFootprint(left, top, use, _definitions.Zone(use).Price, plan);
            }
        }
    }

    // A zone or a plant goes down whole or not at all, on ground with nothing on it; trees and
    // rubble are cleared first, at a price. Its centre is one tile in from its top-left corner.
    private void PlanFootprint(int left, int top, TileUse use, int price, BuildPlan plan)
    {
        var size = Footprint.SizeOf(use);
        var center = _map.StateAt(left + 1, top + 1);
        if (center.IsBuildingCenter && center.Use == use && _map.ZoneCenterOf(left, top) == (left + 1, top + 1))
        {
            plan.Skip(BuildRefusal.AlreadyDone);
            return;
        }

        var cost = price;
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var state = _map.StateAt(left + column, top + row);
                if (state.Use != TileUse.None || !TileRules.CanZone(state.Kind))
                {
                    plan.Skip(BuildRefusal.CannotBuildHere);
                    return;
                }

                cost += ClearingCost(state);
            }
        }

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var state = new TileState(TileKind.Grass, use, zonePart: Footprint.PartAt(column, row, size));
                plan.Change(left + column, top + row, state, 0);
            }
        }

        plan.AddCost(cost);
    }

    // The bulldozer clears roads, bridges, trees, and rubble tile by tile, for $1 each. A zone or
    // a plant comes down only when its centre is in the stroke: all its tiles turn to rubble, for $1.
    private void PlanBulldozer(TileRect bounds, BuildPlan plan)
    {
        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (state.HasFootprint)
                {
                    var (centerX, centerY) = _map.ZoneCenterOf(x, y);
                    var centerInStroke = centerX >= bounds.X && centerX < bounds.X + bounds.Width &&
                        centerY >= bounds.Y && centerY < bounds.Y + bounds.Height;
                    if (!centerInStroke)
                    {
                        plan.Skip(BuildRefusal.PartOfAZone);
                    }

                    continue;
                }

                if (!TileRules.HasSomethingToClear(state))
                {
                    plan.Skip(BuildRefusal.NothingToClear);
                    continue;
                }

                var cleared = state.IsBridge ? new TileState(TileKind.Water) : new TileState(TileKind.Grass);
                plan.Change(x, y, cleared, TileCosts.Bulldoze);
            }
        }

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var center = _map.StateAt(x, y);
                if (!center.IsBuildingCenter)
                {
                    continue;
                }

                var size = Footprint.SizeOf(center.Use);
                for (var dy = -1; dy < size - 1; dy++)
                {
                    for (var dx = -1; dx < size - 1; dx++)
                    {
                        plan.Change(x + dx, y + dy, new TileState(TileKind.Rubble), 0);
                    }
                }

                plan.AddCost(TileCosts.Bulldoze);
            }
        }
    }

    private static int ClearingCost(TileState state) =>
        TileRules.NeedsClearing(state.Kind) ? TileCosts.AutoClear : 0;
}
