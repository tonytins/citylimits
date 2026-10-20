using CityLimits.Core.Definitions;
using CityLimits.Core.Economy;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class BuildStrokeTests
{
    // The base game's price for a zone; DefinitionTests pins it at Micropolis' $100.
    private static readonly int ZonePrice = CityDefinitions.Base.Zone(TileUse.Residential).Price;

    [Fact]
    public void AStrokeStartsAsOneTile() =>
        Assert.Equal(new TileRect(3, 4, 1, 1), new BuildStroke(BuildAction.Road, 3, 4).Bounds);

    [Theory]
    [InlineData(7, 5, 3, 4, 5, 1)]  // dragged mostly across: a line across
    [InlineData(0, 3, 0, 4, 4, 1)]  // across to the left
    [InlineData(4, 9, 3, 4, 1, 6)]  // dragged mostly down: a line down
    [InlineData(2, 1, 3, 1, 1, 4)]  // up
    [InlineData(5, 6, 3, 4, 3, 1)]  // as far across as down: across wins
    public void ARoadRunsInOneStraightLine(int toX, int toY, int x, int y, int width, int height)
    {
        var stroke = new BuildStroke(BuildAction.Road, 3, 4);

        stroke.DragTo(toX, toY);

        Assert.Equal(new TileRect(x, y, width, height), stroke.Bounds);
    }

    [Fact]
    public void TheBulldozerCoversTheRectangleDragged()
    {
        var stroke = new BuildStroke(BuildAction.Bulldoze, 3, 4);

        stroke.DragTo(1, 6);

        Assert.Equal(new TileRect(1, 4, 3, 3), stroke.Bounds);
    }

    [Theory]
    [InlineData(10, 10, 9, 9, 3, 3)]     // a click: one zone centred there
    [InlineData(12, 10, 9, 9, 3, 3)]     // two tiles right: not yet a second zone
    [InlineData(13, 10, 9, 9, 6, 3)]     // three tiles right: a second zone
    [InlineData(16, 13, 9, 9, 9, 6)]     // right and down: a grid of 3 by 2 zones
    [InlineData(7, 10, 6, 9, 6, 3)]      // three tiles left: the second zone is on the left
    [InlineData(10, 4, 9, 3, 3, 9)]      // six tiles up: three zones up
    public void ZonesAreLaidAsAGridCentredWhereTheStrokeStarted(int toX, int toY, int x, int y, int width, int height)
    {
        var stroke = new BuildStroke(BuildAction.ZoneCommercial, 10, 10);

        stroke.DragTo(toX, toY);

        Assert.Equal(new TileRect(x, y, width, height), stroke.Bounds);
    }

    [Fact]
    public void DraggingAZoneStrokeLaysEveryZoneInTheGrid()
    {
        var city = NewCity(size: 9, funds: 1000);

        var result = city.Builder.Apply(Drag(BuildAction.ZoneResidential, 1, 1, 7, 1));

        Assert.Equal(3 * ZoneLayout.Parts, result.TilesChanged);
        Assert.Equal(1000 - (3 * ZonePrice), city.Treasury.Funds);
        Assert.True(city.Map.StateAt(1, 1).IsZoneCenter);
        Assert.True(city.Map.StateAt(4, 1).IsZoneCenter);
        Assert.True(city.Map.StateAt(7, 1).IsZoneCenter);
    }

    [Fact]
    public void AZoneThatCantGoDownIsSkippedAndTheRestOfTheGridIsLaid()
    {
        var city = NewCity(size: 9, funds: 1000);
        city.Map.SetState(4, 2, new TileState(TileKind.Water));

        var result = city.Builder.Apply(Drag(BuildAction.ZoneResidential, 1, 1, 7, 1));

        Assert.Equal(2 * ZoneLayout.Parts, result.TilesChanged);
        Assert.Equal(TileUse.None, city.Map.StateAt(4, 1).Use);
        Assert.True(city.Map.StateAt(7, 1).IsZoneCenter);
    }

    [Fact]
    public void APlanChangesNothing()
    {
        var city = NewCity();
        var stroke = Drag(BuildAction.Road, 0, 1, 4, 1);
        var plan = new BuildPlan();

        city.Builder.Plan(stroke, plan);

        Assert.Equal(5, plan.Tiles.Count);
        Assert.Equal(5 * TileCosts.Road, plan.Cost);
        Assert.True(plan.WillChange);
        Assert.Equal(1000, city.Treasury.Funds);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 1).Use);
    }

    [Fact]
    public void ApplyingAStrokeBuildsItAllAndChargesTheTotal()
    {
        var city = NewCity();

        var result = city.Builder.Apply(Drag(BuildAction.Road, 0, 1, 4, 1));

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(5, result.TilesChanged);
        Assert.Equal(1000 - (5 * TileCosts.Road), city.Treasury.Funds);
        Assert.All(Enumerable.Range(0, 5), x => Assert.True(city.Map.StateAt(x, 1).IsRoad));
    }

    [Fact]
    public void AStrokeTheCityCantPayForIsRefusedWhole()
    {
        var city = NewCity(funds: (3 * TileCosts.Road) - 1);
        var plan = new BuildPlan();

        city.Builder.Plan(Drag(BuildAction.Road, 0, 1, 2, 1), plan);
        var result = city.Builder.Apply(Drag(BuildAction.Road, 0, 1, 2, 1));

        Assert.Equal(BuildRefusal.NotEnoughFunds, plan.Refusal);
        Assert.False(plan.WillChange);
        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Equal(BuildRefusal.NotEnoughFunds, result.Reason);
        Assert.Equal(3 * TileCosts.Road, result.Cost);
        Assert.Equal((3 * TileCosts.Road) - 1, city.Treasury.Funds);
        Assert.All(Enumerable.Range(0, 3), x => Assert.False(city.Map.StateAt(x, 1).IsRoad));
    }

    [Fact]
    public void AStrokeThatRunsOffTheMapIsRefusedWhole()
    {
        var city = NewCity();
        var plan = new BuildPlan();

        city.Builder.Plan(Drag(BuildAction.Road, 4, 1, 6, 1), plan);
        var result = city.Builder.Apply(Drag(BuildAction.Road, 4, 1, 6, 1));

        Assert.Equal(BuildRefusal.OffTheMap, plan.Refusal);
        Assert.False(plan.WillChange);
        Assert.Equal(BuildRefusal.OffTheMap, result.Reason);
        Assert.False(city.Map.StateAt(4, 1).IsRoad);
        Assert.Equal(1000, city.Treasury.Funds);
    }

    [Fact]
    public void TilesThatCantBeBuiltOnAreSkippedAndTheRestIsBuilt()
    {
        var city = NewCity();
        city.Map.SetState(2, 1, new TileState(TileKind.Rock));
        city.Map.SetState(3, 1, new TileState(TileKind.Grass, TileUse.Road));
        var plan = new BuildPlan();

        city.Builder.Plan(Drag(BuildAction.Road, 0, 1, 4, 1), plan);
        var result = city.Builder.Apply(Drag(BuildAction.Road, 0, 1, 4, 1));

        Assert.Equal(1, plan.SkippedBecause(BuildRefusal.CannotBuildHere));
        Assert.Equal(1, plan.SkippedBecause(BuildRefusal.AlreadyDone));
        Assert.Equal(3, result.TilesChanged);
        Assert.Equal(1000 - (3 * TileCosts.Road), city.Treasury.Funds);
        Assert.Equal(TileKind.Rock, city.Map.StateAt(2, 1).Kind);
    }

    [Fact]
    public void ApplyingWorksTheStrokeOutAgainAgainstTheMapAsItIsNow()
    {
        var city = NewCity();
        var stroke = Drag(BuildAction.Road, 0, 1, 2, 1);
        var plan = new BuildPlan();
        city.Builder.Plan(stroke, plan);

        // The map changes between the preview and the release.
        city.Map.SetState(1, 1, new TileState(TileKind.Rock));
        var result = city.Builder.Apply(stroke);

        Assert.Equal(3, plan.Tiles.Count);
        Assert.Equal(2, result.TilesChanged);
        Assert.Equal(TileKind.Rock, city.Map.StateAt(1, 1).Kind);
    }

    [Fact]
    public void BulldozingARectangleClearsItForADollarATile()
    {
        var city = NewCity();
        city.Builder.Apply(Drag(BuildAction.Road, 0, 1, 2, 1));
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(Drag(BuildAction.Bulldoze, 0, 0, 2, 2));

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(3, result.TilesChanged);
        Assert.Equal(funds - (3 * TileCosts.Bulldoze), city.Treasury.Funds);
        Assert.False(city.Map.StateAt(1, 1).IsRoad);
        Assert.True(city.Roads.IsDirty);
    }

    [Fact]
    public void AStrokeThatChangesNothingSaysWhy()
    {
        var city = NewCity();

        var result = city.Builder.Apply(Drag(BuildAction.Bulldoze, 0, 0, 2, 2));

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.NothingToClear, result.Reason);
    }

    [Fact]
    public void ThePlanShowsHowNewRoadsWouldJoin()
    {
        var city = NewCity();
        city.Map.SetState(2, 0, new TileState(TileKind.Grass, TileUse.Road));
        var plan = new BuildPlan();

        city.Builder.Plan(Drag(BuildAction.Road, 0, 1, 4, 1), plan);

        Assert.Equal(RoadConnections.East, plan.RoadConnectionsAt(0, 1));
        Assert.Equal(RoadConnections.North | RoadConnections.East | RoadConnections.West, plan.RoadConnectionsAt(2, 1));
        Assert.Equal(RoadConnections.South, plan.RoadConnectionsAt(2, 0));
        Assert.Equal(RoadConnections.None, RoadNetwork.ConnectionsAt(city.Map, 2, 0));
    }

    [Fact]
    public void APlanCanBeFilledAgainAndForgetsTheLastStroke()
    {
        var city = NewCity();
        var plan = new BuildPlan();

        city.Builder.Plan(Drag(BuildAction.Road, 0, 1, 4, 1), plan);
        city.Builder.Plan(Drag(BuildAction.Road, 0, 3, 1, 3), plan);

        Assert.Equal(2, plan.Tiles.Count);
        Assert.Equal(2 * TileCosts.Road, plan.Cost);
        Assert.False(plan.TryGetPlanned(4, 1, out _));
        Assert.True(plan.TryGetPlanned(1, 3, out var planned));
        Assert.True(planned.IsRoad);
    }

    private static BuildStroke Drag(BuildAction action, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(action, fromX, fromY);
        stroke.DragTo(toX, toY);
        return stroke;
    }

    private static (TileMap Map, Treasury Treasury, RoadNetwork Roads, CityBuilder Builder) NewCity(int funds = 1000, int size = 5)
    {
        var map = TileMap.FromTerrain(size, size, new TileKind[size * size]);
        var treasury = new Treasury(funds);
        var roads = new RoadNetwork();
        return (map, treasury, roads, new CityBuilder(map, treasury, roads));
    }
}
