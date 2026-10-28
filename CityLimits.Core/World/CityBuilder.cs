using CityLimits.Core.Economy;

namespace CityLimits.Core.World;

/// <summary>Applies the player's building work to the city, and takes the money for it.</summary>
public sealed class CityBuilder
{
    private readonly TileMap _map;
    private readonly Treasury _treasury;
    private readonly RoadNetwork _roads;
    private readonly CityDefinitions _definitions;
    private readonly Hazards? _hazards;

    // Apply works the stroke out again here, so a preview the player saw earlier can't go stale.
    private readonly BuildPlan _applying = new();

    // The water a road stroke has still to bridge; kept so planning every frame doesn't allocate.
    private readonly List<(int X, int Y)> _water = [];

    /// <summary>
    /// Creates a builder that works on the given map, at the prices its definitions set: the base
    /// game's when none are given. With the city's hazards, it leaves burning and flooded tiles, and
    /// the holes they leave in buildings, alone.
    /// </summary>
    /// <exception cref="ArgumentNullException">One of the parts of the city is null.</exception>
    public CityBuilder(TileMap map, Treasury treasury, RoadNetwork roads, CityDefinitions? definitions = null, Hazards? hazards = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(treasury);
        ArgumentNullException.ThrowIfNull(roads);

        _map = map;
        _treasury = treasury;
        _roads = roads;
        _definitions = definitions ?? CityDefinitions.Base;
        _hazards = hazards;
    }

    /// <summary>
    /// Whether building costs nothing, as in a scenario's editor, where whole cities are laid out;
    /// the city's money is left as it is. It belongs to the editor, not the city, so it isn't saved.
    /// </summary>
    public bool BuildsForFree { get; set; }

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
            case BuildAction.PowerLine:
                PlanPowerLine(bounds, plan);
                break;
            case BuildAction.Rail:
                PlanRail(bounds, plan);
                break;
            case BuildAction.Bulldoze:
                PlanBulldozer(bounds, plan);
                break;
            case BuildAction.CoalPowerPlant:
                PlanFootprint(bounds.X, bounds.Y, stroke.Use, _definitions.Plant(stroke.Use).Price, plan);
                break;
            case BuildAction.NuclearPowerPlant:
                PlanFootprint(bounds.X, bounds.Y, stroke.Use, _definitions.Plant(stroke.Use).Price, plan);
                break;
            case BuildAction.Park:
                PlanParks(bounds, plan);
                break;
            case BuildAction.FireStation:
            case BuildAction.PoliceStation:
                PlanFootprint(bounds.X, bounds.Y, stroke.Use, _definitions.Service(stroke.Use).Price, plan);
                break;
            case BuildAction.Stadium:
            case BuildAction.Seaport:
            case BuildAction.Airport:
                PlanFootprint(bounds.X, bounds.Y, stroke.Use, _definitions.CostOf(stroke.Action), plan);
                break;
            default:
                PlanZones(bounds, stroke.Use, plan);
                break;
        }

        if (BuildsForFree)
        {
            plan.Waive();
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
            roadsChanged |= tile.State.CarriesTrips || _map.StateAt(tile.X, tile.Y).CarriesTrips;
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

        if (plan.SkippedBecause(BuildRefusal.OnFire) > 0)
        {
            return BuildResult.Refused(BuildRefusal.OnFire);
        }

        if (plan.SkippedBecause(BuildRefusal.Flooded) > 0)
        {
            return BuildResult.Refused(BuildRefusal.Flooded);
        }

        if (plan.SkippedBecause(BuildRefusal.PartOfAZone) > 0)
        {
            return BuildResult.Refused(BuildRefusal.PartOfAZone);
        }

        return plan.SkippedBecause(BuildRefusal.AlreadyDone) > 0
            ? BuildResult.Unchanged(BuildRefusal.AlreadyDone)
            : BuildResult.Unchanged(BuildRefusal.NothingToClear);
    }

    // A road on land is laid where it can be, and over a straight power line or straight rail it
    // makes a crossing. A road on water is a bridge, which needs a road beside it to start from,
    // so a stroke can start on either bank, as in Micropolis.
    private void PlanRoad(TileRect bounds, BuildPlan plan)
    {
        _water.Clear();

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (Blocked(x, y, plan))
                {
                    continue;
                }

                if (state.HasRoad)
                {
                    plan.Skip(BuildRefusal.AlreadyDone);
                }
                else if (state.Kind == TileKind.Water && state.Use == TileUse.None)
                {
                    _water.Add((x, y));
                }
                else if (state.Use == TileUse.PowerLine && state.Kind == TileKind.Grass)
                {
                    PlanCrossing(x, y, CrossingOverLine(PowerLines.ConnectionsAt(_map, x, y)), _definitions.Infrastructure.Road, plan);
                }
                else if (state.Use == TileUse.Rail && state.Kind == TileKind.Grass)
                {
                    PlanCrossing(x, y, Crossing(Rails.ConnectionsAt(_map, x, y), TileUse.RailAcrossRoad, TileUse.RailDownRoad), _definitions.Infrastructure.Road, plan);
                }
                else if (state.Use != TileUse.None || !TileRules.CanLayRoad(state.Kind))
                {
                    plan.Skip(BuildRefusal.CannotBuildHere);
                }
                else
                {
                    plan.Change(x, y, new TileState(TileKind.Grass, TileUse.Road), _definitions.Infrastructure.Road + ClearingCost(state));
                }
            }
        }

        CrossWater(new TileState(TileKind.Water, TileUse.Road), _definitions.Infrastructure.Bridge, RoadNetwork.RunsToward, plan);
    }

    // A power line is strung like a road: in one straight line, clearing trees and rubble. It
    // crosses a straight road or straight rail to make one tile that is both, and water as a line
    // under water, which needs something beside it that carries power to start from, as a bridge
    // needs a road.
    private void PlanPowerLine(TileRect bounds, BuildPlan plan)
    {
        _water.Clear();

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (Blocked(x, y, plan))
                {
                    continue;
                }

                if (state.HasPowerLine)
                {
                    plan.Skip(BuildRefusal.AlreadyDone);
                }
                else if (state.Kind == TileKind.Water && state.Use == TileUse.None)
                {
                    _water.Add((x, y));
                }
                else if (state.Use == TileUse.Road && state.Kind == TileKind.Grass)
                {
                    PlanCrossing(x, y, CrossingUnderRoad(RoadNetwork.ConnectionsAt(_map, x, y)), _definitions.Infrastructure.PowerLine, plan);
                }
                else if (state.Use == TileUse.Rail && state.Kind == TileKind.Grass)
                {
                    PlanCrossing(x, y, Crossing(Rails.ConnectionsAt(_map, x, y), TileUse.RailAcrossPowerLine, TileUse.RailDownPowerLine), _definitions.Infrastructure.PowerLine, plan);
                }
                else if (state.Use != TileUse.None || !TileRules.CanBuildOn(state.Kind))
                {
                    plan.Skip(BuildRefusal.CannotBuildHere);
                }
                else
                {
                    plan.Change(x, y, new TileState(TileKind.Grass, TileUse.PowerLine), _definitions.Infrastructure.PowerLine + ClearingCost(state));
                }
            }
        }

        CrossWater(new TileState(TileKind.Water, TileUse.PowerLine), _definitions.Infrastructure.UnderwaterPowerLine, PowerLines.RunsToward, plan);
    }

    // Rail is laid as a road is, as in Micropolis: in one straight line, clearing trees and rubble,
    // crossing a straight road or a straight power line to make one tile that is both, and under
    // water as a tunnel, which needs rail beside it to start from.
    private void PlanRail(TileRect bounds, BuildPlan plan)
    {
        _water.Clear();

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (Blocked(x, y, plan))
                {
                    continue;
                }

                if (state.IsRail)
                {
                    plan.Skip(BuildRefusal.AlreadyDone);
                }
                else if (state.Kind == TileKind.Water && state.Use == TileUse.None)
                {
                    _water.Add((x, y));
                }
                else if (state.Use == TileUse.Road && state.Kind == TileKind.Grass)
                {
                    // Rail laid on a road runs the other way to it.
                    PlanCrossing(x, y, Crossing(RoadNetwork.ConnectionsAt(_map, x, y), TileUse.RailDownRoad, TileUse.RailAcrossRoad), _definitions.Infrastructure.Rail, plan);
                }
                else if (state.Use == TileUse.PowerLine && state.Kind == TileKind.Grass)
                {
                    PlanCrossing(x, y, Crossing(PowerLines.ConnectionsAt(_map, x, y), TileUse.RailDownPowerLine, TileUse.RailAcrossPowerLine), _definitions.Infrastructure.Rail, plan);
                }
                else if (state.Use != TileUse.None || !TileRules.CanBuildOn(state.Kind))
                {
                    plan.Skip(BuildRefusal.CannotBuildHere);
                }
                else
                {
                    plan.Change(x, y, new TileState(TileKind.Grass, TileUse.Rail), _definitions.Infrastructure.Rail + ClearingCost(state));
                }
            }
        }

        CrossWater(new TileState(TileKind.Water, TileUse.Rail), _definitions.Infrastructure.Tunnel, Rails.RunsToward, plan);
    }

    // Water is crossed in passes until no more of it can be: a tile is crossed once something
    // beside it, planned or already there, runs toward it. So a stroke can start on either bank.
    private void CrossWater(TileState crossing, int cost, Func<TileState, RoadConnections, bool> runsToward, BuildPlan plan)
    {
        var crossed = true;
        while (crossed)
        {
            crossed = false;
            for (var i = _water.Count - 1; i >= 0; i--)
            {
                var (x, y) = _water[i];
                var reached = false;
                foreach (var (side, dx, dy) in Sides.All)
                {
                    reached |= runsToward(plan.StateAt(x + dx, y + dy), Sides.Opposite(side));
                }

                if (!reached)
                {
                    continue;
                }

                plan.Change(x, y, crossing, cost);
                _water.RemoveAt(i);
                crossed = true;
            }
        }

        for (var i = 0; i < _water.Count; i++)
        {
            plan.Skip(BuildRefusal.CannotBuildHere);
        }
    }

    // A crossing goes only where the road or the line already runs straight; a corner or a
    // junction can't take one.
    private static void PlanCrossing(int x, int y, TileUse? crossing, int cost, BuildPlan plan)
    {
        if (crossing is null)
        {
            plan.Skip(BuildRefusal.CannotBuildHere);
            return;
        }

        plan.Change(x, y, new TileState(TileKind.Grass, crossing.Value), cost);
    }

    // A road laid on a line runs the other way to it. A line joined to nothing is drawn across,
    // as in Micropolis, so a road on it runs down.
    private static TileUse? CrossingOverLine(RoadConnections line) => line switch
    {
        _ when (line & (RoadConnections.North | RoadConnections.South)) == 0 => TileUse.RoadDownPowerLine,
        _ when (line & (RoadConnections.East | RoadConnections.West)) == 0 => TileUse.RoadAcrossPowerLine,
        _ => null,
    };

    // What a crossing becomes over something that runs straight: the first when it runs across,
    // east to west, the second when it runs down; a corner or a junction takes none. Something
    // joined to nothing is drawn across, as in Micropolis.
    private static TileUse? Crossing(RoadConnections under, TileUse whenAcross, TileUse whenDown) => under switch
    {
        _ when (under & (RoadConnections.North | RoadConnections.South)) == 0 => whenAcross,
        _ when (under & (RoadConnections.East | RoadConnections.West)) == 0 => whenDown,
        _ => null,
    };

    // A line strung over a road runs the other way to it. A road joined to nothing is drawn
    // across, so a line over it runs down.
    private static TileUse? CrossingUnderRoad(RoadConnections road) => road switch
    {
        _ when (road & (RoadConnections.North | RoadConnections.South)) == 0 => TileUse.RoadAcrossPowerLine,
        _ when (road & (RoadConnections.East | RoadConnections.West)) == 0 => TileUse.RoadDownPowerLine,
        _ => null,
    };

    // As in Micropolis, a park goes only on clear ground or rubble, which it clears for $1 more;
    // trees, water, and anything built refuse it.
    private void PlanParks(TileRect bounds, BuildPlan plan)
    {
        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (Blocked(x, y, plan))
                {
                    continue;
                }

                if (state.Use == TileUse.Park)
                {
                    plan.Skip(BuildRefusal.AlreadyDone);
                }
                else if (state.Use != TileUse.None || state.Kind is not (TileKind.Grass or TileKind.Rubble))
                {
                    plan.Skip(BuildRefusal.CannotBuildHere);
                }
                else
                {
                    plan.Change(x, y, new TileState(TileKind.Grass, TileUse.Park), _definitions.Infrastructure.Park + ClearingCost(state));
                }
            }
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
                if (ActiveHazard(left + column, top + row) is { } hazard)
                {
                    plan.Skip(hazard);
                    return;
                }

                if (state.Use != TileUse.None || !TileRules.CanZone(state.Kind) || _hazards?.IsHole(left + column, top + row) == true)
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
                // A plant has power from the moment it is placed, as in Micropolis; the rest wait for the scan.
                var state = new TileState(TileKind.Grass, use, zonePart: Footprint.PartAt(column, row, size), powered: TileRules.IsPowerPlant(use));
                plan.Change(left + column, top + row, state, 0);
            }
        }

        plan.AddCost(cost);
    }

    // The bulldozer clears roads, bridges, rail, tunnels, crossings, trees, and rubble tile by tile, for $1 each. A zone or
    // a plant comes down only when its centre is in the stroke: all its tiles turn to rubble, for $1.
    private void PlanBulldozer(TileRect bounds, BuildPlan plan)
    {
        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var state = _map.StateAt(x, y);
                if (ActiveHazard(x, y) is { } hazard)
                {
                    plan.Skip(hazard);
                    continue;
                }

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

                var cleared = state.Kind == TileKind.Water ? new TileState(TileKind.Water) : new TileState(TileKind.Grass);
                plan.Change(x, y, cleared, _definitions.Infrastructure.Bulldozer);
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

                // A tile burning or flooded stays so: the bulldozer can't clear fire or water, as in Micropolis.
                var size = Footprint.SizeOf(center.Use);
                for (var dy = -1; dy < size - 1; dy++)
                {
                    for (var dx = -1; dx < size - 1; dx++)
                    {
                        if (_hazards?.IsActive(x + dx, y + dy) != true)
                        {
                            plan.Change(x + dx, y + dy, new TileState(TileKind.Rubble), 0);
                        }
                    }
                }

                plan.AddCost(_definitions.Infrastructure.Bulldozer);
            }
        }
    }

    // Nothing is built on a burning or flooded tile, or in the hole a hazard left in a building; the
    // bulldozer decides for itself, since it may clear a hole's rubble but never a fire or a flood.
    private bool Blocked(int x, int y, BuildPlan plan)
    {
        if (ActiveHazard(x, y) is { } hazard)
        {
            plan.Skip(hazard);
            return true;
        }

        if (_hazards?.IsHole(x, y) == true)
        {
            plan.Skip(BuildRefusal.CannotBuildHere);
            return true;
        }

        return false;
    }

    // Why a fire or a flood on a tile refuses the work, or null when there is neither.
    private BuildRefusal? ActiveHazard(int x, int y) =>
        _hazards?.Fires.IsBurning(x, y) == true ? BuildRefusal.OnFire
        : _hazards?.Floods.IsFlooded(x, y) == true ? BuildRefusal.Flooded
        : null;

    private int ClearingCost(TileState state) =>
        TileRules.NeedsClearing(state.Kind) ? _definitions.Infrastructure.Clearing : 0;
}
