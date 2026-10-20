using CityLimits.Core.Economy;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class CityBuilderTests
{
    [Fact]
    public void ARoadIsLaidOnClearGround()
    {
        var city = NewCity();
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileUse.Road, city.Map.StateAt(2, 2).Use);
        Assert.Equal(funds - TileCosts.Road, city.Treasury.Funds);
    }

    [Fact]
    public void ARoadCannotBeLaidOnWater()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Water));
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 2).Use);
        Assert.Equal(funds, city.Treasury.Funds);
    }

    [Fact]
    public void ARoadClearsTheTreesAndCostsForIt()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Trees));
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileKind.Grass, city.Map.StateAt(2, 2).Kind);
        Assert.Equal(funds - TileCosts.Road - TileCosts.Clearing, city.Treasury.Funds);
    }

    [Fact]
    public void LayingTheSameRoadTwiceChangesNothing()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.Road, 2, 2);
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.AlreadyDone, result.Reason);
        Assert.Equal(funds, city.Treasury.Funds);
    }

    [Fact]
    public void ARoadCannotBeLaidOnAZone()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 2);

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.Residential, city.Map.StateAt(2, 2).Use);
    }

    [Theory]
    [InlineData(BuildAction.ZoneResidential, TileUse.Residential)]
    [InlineData(BuildAction.ZoneCommercial, TileUse.Commercial)]
    [InlineData(BuildAction.ZoneIndustrial, TileUse.Industrial)]
    public void LandCanBeZoned(BuildAction action, TileUse expected)
    {
        var city = NewCity();
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(action, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(expected, city.Map.StateAt(2, 2).Use);
        Assert.Equal(funds - TileCosts.Zone, city.Treasury.Funds);
    }

    [Fact]
    public void ZoningTheSameZoneTwiceChangesNothing()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 2);
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.ZoneResidential, 2, 2);

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.AlreadyDone, result.Reason);
        Assert.Equal(funds, city.Treasury.Funds);
    }

    [Fact]
    public void ZonedLandCanBeZonedAgain()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Grass, TileUse.Residential, 2));

        var result = city.Builder.Apply(BuildAction.ZoneCommercial, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileUse.Commercial, city.Map.StateAt(2, 2).Use);
        Assert.Equal(0, city.Map.StateAt(2, 2).Development);
    }

    [Fact]
    public void LandCannotBeZonedOnARoad()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.Road, 2, 2);

        var result = city.Builder.Apply(BuildAction.ZoneResidential, 2, 2);

        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.Road, city.Map.StateAt(2, 2).Use);
    }

    [Fact]
    public void TheBulldozerClearsARoadAndGivesHalfTheMoneyBack()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.Road, 2, 2);
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 2).Use);
        Assert.Equal(TileKind.Grass, city.Map.StateAt(2, 2).Kind);
        Assert.Equal(funds + TileCosts.RefundFor(TileCosts.Road), city.Treasury.Funds);
    }

    [Fact]
    public void TheBulldozerClearsAZoneAndGivesHalfTheMoneyBack()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneIndustrial, 2, 2);
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 2).Use);
        Assert.Equal(funds + TileCosts.RefundFor(TileCosts.Zone), city.Treasury.Funds);
    }

    [Fact]
    public void TheBulldozerClearsTheTrees()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Trees));

        var result = city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileKind.Grass, city.Map.StateAt(2, 2).Kind);
        Assert.Equal(0, result.Refund);
    }

    [Fact]
    public void TheBulldozerHasNothingToClearOnBareGround()
    {
        var city = NewCity();

        var result = city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.NothingToClear, result.Reason);
    }

    [Fact]
    public void TheCityCannotSpendWhatItDoesNotHave()
    {
        var city = NewCity(funds: 3);

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Equal(BuildRefusal.NotEnoughFunds, result.Reason);
        Assert.Equal(3, city.Treasury.Funds);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 2).Use);
    }

    [Fact]
    public void WorkOffTheMapIsRefused()
    {
        var city = NewCity();

        var result = city.Builder.Apply(BuildAction.Road, 99, 99);

        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Equal(BuildRefusal.OffTheMap, result.Reason);
    }

    [Fact]
    public void TheRoadsAreMarkedOutOfDateWhenTheCityChanges()
    {
        var city = NewCity();
        city.Roads.Rebuild(city.Map);
        Assert.False(city.Roads.IsDirty);

        city.Builder.Apply(BuildAction.Road, 2, 2);
        Assert.True(city.Roads.IsDirty);

        city.Roads.EnsureCurrent(city.Map);
        Assert.True(city.Roads.HasRoad(2, 2));

        city.Builder.Apply(BuildAction.Bulldoze, 2,  2);
        Assert.True(city.Roads.IsDirty);

        city.Roads.EnsureCurrent(city.Map);
        Assert.False(city.Roads.HasRoad(2, 2));
    }

    [Fact]
    public void ACityWithoutItsPartsIsRejected()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);
        var treasury = new Treasury();
        var roads = new RoadNetwork();

        Assert.Throws<ArgumentNullException>(() => new CityBuilder(null!, treasury, roads));
        Assert.Throws<ArgumentNullException>(() => new CityBuilder(map, null!, roads));
        Assert.Throws<ArgumentNullException>(() => new CityBuilder(map, treasury, null!));
    }

    private static (TileMap Map, Treasury Treasury, RoadNetwork Roads, CityBuilder Builder) NewCity(int funds = 1000)
    {
        var map = TileMap.FromTerrain(6, 6, new TileKind[36]);
        var treasury = new Treasury(funds);
        var roads = new RoadNetwork();
        return (map, treasury, roads, new CityBuilder(map, treasury, roads));
    }
}
