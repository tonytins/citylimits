using CityLimits.Core.Economy;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class CityBuilderTests
{
    [Fact]
    public void ARoadIsLaidOnClearGround()
    {
        var city = NewCity();

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileUse.Road, city.Map.StateAt(2, 2).Use);
        Assert.Equal(1000 - TileCosts.Road, city.Treasury.Funds);
    }

    [Theory]
    [InlineData(TileKind.Trees)]
    [InlineData(TileKind.Rubble)]
    public void ARoadClearsTreesAndRubbleForADollarMore(TileKind kind)
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(kind));

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileKind.Grass, city.Map.StateAt(2, 2).Kind);
        Assert.Equal(1000 - TileCosts.Road - TileCosts.AutoClear, city.Treasury.Funds);
    }

    [Fact]
    public void ARoadCannotBeLaidOnRock()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Rock));

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(1000, city.Treasury.Funds);
    }

    [Fact]
    public void LayingTheSameRoadTwiceChangesNothing()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.Road, 2, 2);

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.AlreadyDone, result.Reason);
        Assert.Equal(1000 - TileCosts.Road, city.Treasury.Funds);
    }

    [Fact]
    public void ARoadCannotBeLaidOnAZone()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        var result = city.Builder.Apply(BuildAction.Road, 3, 3);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.Residential, city.Map.StateAt(3, 3).Use);
    }

    [Fact]
    public void ARoadAcrossWaterBecomesABridgeFromTheBank()
    {
        var city = NewCity();
        city.Map.SetState(3, 2, new TileState(TileKind.Water));
        city.Map.SetState(4, 2, new TileState(TileKind.Water));

        var result = city.Builder.Apply(Drag(BuildAction.Road, 1, 2, 5, 2));

        Assert.Equal(5, result.TilesChanged);
        Assert.True(city.Map.StateAt(3, 2).IsBridge);
        Assert.True(city.Map.StateAt(4, 2).IsBridge);
        Assert.Equal(1000 - (3 * TileCosts.Road) - (2 * TileCosts.Bridge), city.Treasury.Funds);
    }

    [Fact]
    public void ABridgeCanStartFromTheFarBank()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Water));
        city.Map.SetState(3, 2, new TileState(TileKind.Water));

        // Dragged from the water toward the land: the bridge is built back from the far end.
        var result = city.Builder.Apply(Drag(BuildAction.Road, 2, 2, 4, 2));

        Assert.Equal(3, result.TilesChanged);
        Assert.True(city.Map.StateAt(2, 2).IsBridge);
    }

    [Fact]
    public void ABridgeReachesOutAcrossWaterOneTileAtATime()
    {
        var city = NewCity();
        for (var x = 2; x <= 5; x++)
        {
            city.Map.SetState(x, 2, new TileState(TileKind.Water));
        }

        // Only the near bank is land: each tile of the bridge starts from the one before it.
        var result = city.Builder.Apply(Drag(BuildAction.Road, 1, 2, 5, 2));

        Assert.Equal(5, result.TilesChanged);
        Assert.All(Enumerable.Range(2, 4), x => Assert.True(city.Map.StateAt(x, 2).IsBridge));
        Assert.Equal(1000 - TileCosts.Road - (4 * TileCosts.Bridge), city.Treasury.Funds);
    }

    [Fact]
    public void WaterWithNoRoadToStartFromIsNotBridged()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Water));

        var result = city.Builder.Apply(BuildAction.Road, 2, 2);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 2).Use);
    }

    [Theory]
    [InlineData(BuildAction.ZoneResidential, TileUse.Residential)]
    [InlineData(BuildAction.ZoneCommercial, TileUse.Commercial)]
    [InlineData(BuildAction.ZoneIndustrial, TileUse.Industrial)]
    public void AZoneIsThreeByThreeAroundWhereItIsPlaced(BuildAction action, TileUse expected)
    {
        var city = NewCity();

        var result = city.Builder.Apply(action, 4, 4);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(ZoneLayout.Parts, result.TilesChanged);
        Assert.Equal(1000 - TileCosts.Zone, city.Treasury.Funds);
        for (var y = 3; y <= 5; y++)
        {
            for (var x = 3; x <= 5; x++)
            {
                Assert.Equal(expected, city.Map.StateAt(x, y).Use);
                Assert.Equal((4, 4), city.Map.ZoneCenterOf(x, y));
            }
        }

        Assert.True(city.Map.StateAt(4, 4).IsZoneCenter);
        Assert.False(city.Map.StateAt(3, 3).IsZoneCenter);
        Assert.Equal(TileUse.None, city.Map.StateAt(6, 4).Use);
    }

    [Fact]
    public void AZoneClearsTreesForADollarATile()
    {
        var city = NewCity();
        city.Map.SetState(3, 3, new TileState(TileKind.Trees));
        city.Map.SetState(5, 5, new TileState(TileKind.Trees));

        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal(1000 - TileCosts.Zone - (2 * TileCosts.AutoClear), city.Treasury.Funds);
        Assert.Equal(TileKind.Grass, city.Map.StateAt(5, 5).Kind);
    }

    [Fact]
    public void AZoneGoesDownWholeOrNotAtAll()
    {
        var city = NewCity();
        city.Map.SetState(5, 5, new TileState(TileKind.Water));

        var result = city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.None, city.Map.StateAt(4, 4).Use);
        Assert.Equal(1000, city.Treasury.Funds);
    }

    [Fact]
    public void ZoningTheSameZoneTwiceChangesNothing()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        var result = city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.AlreadyDone, result.Reason);
        Assert.Equal(1000 - TileCosts.Zone, city.Treasury.Funds);
    }

    [Fact]
    public void AZoneHasToBeBulldozedBeforeItCanBeZonedAgain()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        var result = city.Builder.Apply(BuildAction.ZoneCommercial, 4, 4);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.Residential, city.Map.StateAt(4, 4).Use);
    }

    [Fact]
    public void AZoneCannotGoOverARoad()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.Road, 5, 4);

        var result = city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(TileUse.Road, city.Map.StateAt(5, 4).Use);
    }

    [Fact]
    public void AZoneThatWouldRunOffTheMapIsRefused()
    {
        var city = NewCity();

        var result = city.Builder.Apply(BuildAction.ZoneResidential, 0, 4);

        Assert.Equal(BuildRefusal.OffTheMap, result.Reason);
        Assert.Equal(TileUse.None, city.Map.StateAt(0, 4).Use);
    }

    [Fact]
    public void TheBulldozerClearsARoadForADollarAndGivesNothingBack()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.Road, 2, 2);

        var result = city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileUse.None, city.Map.StateAt(2, 2).Use);
        Assert.Equal(1000 - TileCosts.Road - TileCosts.Bulldoze, city.Treasury.Funds);
    }

    [Fact]
    public void TheBulldozerTurnsABridgeBackIntoWater()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Water, TileUse.Road));

        city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(new TileState(TileKind.Water), city.Map.StateAt(2, 2));
    }

    [Theory]
    [InlineData(TileKind.Trees)]
    [InlineData(TileKind.Rubble)]
    public void TheBulldozerClearsTreesAndRubble(TileKind kind)
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(kind));

        var result = city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(TileKind.Grass, city.Map.StateAt(2, 2).Kind);
        Assert.Equal(1000 - TileCosts.Bulldoze, city.Treasury.Funds);
    }

    [Fact]
    public void TheBulldozerKnocksDownAZoneThroughItsCentreAndLeavesRubble()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneIndustrial, 4, 4);

        var result = city.Builder.Apply(BuildAction.Bulldoze, 4, 4);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(ZoneLayout.Parts, result.TilesChanged);
        Assert.Equal(1000 - TileCosts.Zone - TileCosts.Bulldoze, city.Treasury.Funds);
        for (var y = 3; y <= 5; y++)
        {
            for (var x = 3; x <= 5; x++)
            {
                Assert.Equal(new TileState(TileKind.Rubble), city.Map.StateAt(x, y));
            }
        }
    }

    [Fact]
    public void TheBulldozerWontClearAZoneTileByTile()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneIndustrial, 4, 4);

        var result = city.Builder.Apply(BuildAction.Bulldoze, 3, 3);

        Assert.Equal(BuildRefusal.PartOfAZone, result.Reason);
        Assert.Equal(TileUse.Industrial, city.Map.StateAt(3, 3).Use);
    }

    [Fact]
    public void ADraggedBulldozerKnocksDownAZoneWhoseCentreItCovers()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.ZoneIndustrial, 4, 4);

        // The stroke covers the centre and a corner, but not the whole zone.
        var result = city.Builder.Apply(Drag(BuildAction.Bulldoze, 3, 3, 4, 4));

        Assert.Equal(ZoneLayout.Parts, result.TilesChanged);
        Assert.Equal(TileKind.Rubble, city.Map.StateAt(5, 5).Kind);
        Assert.Equal(1000 - TileCosts.Zone - TileCosts.Bulldoze, city.Treasury.Funds);
    }

    [Fact]
    public void TheBulldozerHasNothingToClearOnBareGround()
    {
        var city = NewCity();

        var result = city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.NothingToClear, result.Reason);
        Assert.Equal(1000, city.Treasury.Funds);
    }

    [Fact]
    public void TheCityCannotSpendWhatItDoesNotHave()
    {
        var city = NewCity(funds: TileCosts.Zone - 1);

        var result = city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Equal(BuildRefusal.NotEnoughFunds, result.Reason);
        Assert.Equal(TileCosts.Zone - 1, city.Treasury.Funds);
        Assert.Equal(TileUse.None, city.Map.StateAt(4, 4).Use);
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

        city.Builder.Apply(BuildAction.Bulldoze, 2, 2);
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

    private static BuildStroke Drag(BuildAction action, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(action, fromX, fromY);
        stroke.DragTo(toX, toY);
        return stroke;
    }

    private static (TileMap Map, Treasury Treasury, RoadNetwork Roads, CityBuilder Builder) NewCity(int funds = 1000)
    {
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        var treasury = new Treasury(funds);
        var roads = new RoadNetwork();
        return (map, treasury, roads, new CityBuilder(map, treasury, roads));
    }
}
