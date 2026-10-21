using CityLimits.Core.Economy;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ParkTests
{
    private static CitySimulation City(int width = 9, int height = 9) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, funds: 10_000);

    // The first tile, reading across and down from a corner, whose park has the look wanted.
    private static (int X, int Y) FirstTile(bool wooded, int left, int top, int size)
    {
        for (var y = top; y < top + size; y++)
        {
            for (var x = left; x < left + size; x++)
            {
                if (Parks.IsWoodedAt(x, y) == wooded)
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException("No such tile in the area.");
    }

    [Fact]
    public void AParkTileCostsTenAndARectangleOfThemIsLaidAtOnce()
    {
        var city = City();
        var stroke = new BuildStroke(BuildAction.Park, 1, 1);
        stroke.DragTo(3, 2);

        var result = city.Builder.Apply(stroke);

        Assert.Equal(6, result.TilesChanged);
        Assert.Equal(10_000 - (6 * TileCosts.Park), city.Treasury.Funds);
        Assert.Equal(TileUse.Park, city.Map.StateAt(3, 2).Use);
        Assert.Equal(10, TileCosts.Park);
    }

    [Fact]
    public void AParkClearsRubbleForADollarButRefusesTreesWaterAndAnythingBuilt()
    {
        var city = City();
        city.Map.SetState(1, 1, new TileState(TileKind.Rubble));
        city.Map.SetState(2, 1, new TileState(TileKind.Trees));
        city.Map.SetState(3, 1, new TileState(TileKind.Water));
        city.Builder.Apply(BuildAction.Road, 4, 1);
        var funds = city.Treasury.Funds;
        var plan = new BuildPlan();
        var stroke = new BuildStroke(BuildAction.Park, 1, 1);
        stroke.DragTo(4, 1);

        city.Builder.Plan(stroke, plan);
        var result = city.Builder.Apply(stroke);

        Assert.Equal(3, plan.SkippedBecause(BuildRefusal.CannotBuildHere));
        Assert.Equal(1, result.TilesChanged);
        Assert.Equal(funds - TileCosts.Park - TileCosts.AutoClear, city.Treasury.Funds);
        Assert.Equal(TileKind.Trees, city.Map.StateAt(2, 1).Kind);
    }

    [Fact]
    public void LayingTheSameParkTwiceChangesNothingAndTheBulldozerClearsIt()
    {
        var city = City();
        city.Builder.Apply(BuildAction.Park, 2, 2);

        Assert.Equal(BuildRefusal.AlreadyDone, city.Builder.Apply(BuildAction.Park, 2, 2).Reason);

        city.Builder.Apply(BuildAction.Bulldoze, 2, 2);

        Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(2, 2));
    }

    [Fact]
    public void AboutOneParkInFiveIsAFountainChosenByItsPlace()
    {
        var looks = Enumerable.Range(0, 100).SelectMany(y => Enumerable.Range(0, 100).Select(x => Parks.LookAt(x, y))).ToList();

        Assert.All(looks, look => Assert.InRange(look, 0, Parks.FountainLook));
        Assert.InRange(looks.Count(look => look == Parks.FountainLook), 1700, 2300);
        Assert.Equal(Parks.LookAt(37, 52), Parks.LookAt(37, 52));
        Assert.All(Enumerable.Range(0, Parks.Looks), look => Assert.Contains(look, looks));
    }

    [Fact]
    public void AWoodedParkRaisesTheLandValueAroundItAsTreesDo()
    {
        // A road on block (4, 4); a wooded park in the same 4 by 4 area. Terrain counts the second time.
        var (parkX, parkY) = FirstTile(wooded: true, 10, 8, 2);
        var map = TileMap.FromTerrain(16, 16, new TileKind[256]);
        map.SetState(8, 8, new TileState(TileKind.Grass, TileUse.Road));
        var maps = new CityMaps(map);
        maps.UpdateLandValue();
        var without = maps.LandValueAt(8, 8);

        map.SetState(parkX, parkY, new TileState(TileKind.Grass, TileUse.Park));
        maps.UpdateLandValue();
        maps.UpdateLandValue();

        Assert.True(maps.LandValueAt(8, 8) > without, $"{maps.LandValueAt(8, 8)} vs {without}");
    }

    [Fact]
    public void AWoodedParkIsNotSomethingBuiltButAFountainIs()
    {
        // The two parks are far enough apart to sit in different 2 by 2 blocks.
        var wooded = FirstTile(wooded: true, 0, 0, 8);
        var fountain = FirstTile(wooded: false, 8, 8, 8);
        var map = TileMap.FromTerrain(16, 16, new TileKind[256]);
        map.SetState(wooded.X, wooded.Y, new TileState(TileKind.Grass, TileUse.Park));
        map.SetState(fountain.X, fountain.Y, new TileState(TileKind.Grass, TileUse.Park));
        var maps = new CityMaps(map);

        maps.UpdateLandValue();

        Assert.Equal(0, maps.LandValueAt(wooded.X, wooded.Y));
        Assert.True(maps.LandValueAt(fountain.X, fountain.Y) > 0);
    }

    [Fact]
    public void TheReadoutSaysWhichKindOfParkATileIs()
    {
        var wooded = FirstTile(wooded: true, 0, 0, 8);
        var fountain = FirstTile(wooded: false, 0, 0, 8);
        var map = TileMap.FromTerrain(8, 8, new TileKind[64]);
        map.SetState(wooded.X, wooded.Y, new TileState(TileKind.Grass, TileUse.Park));
        map.SetState(fountain.X, fountain.Y, new TileState(TileKind.Grass, TileUse.Park));

        Assert.Equal("Park - woods that make the land around them worth more", TileInfo.Describe(map, wooded.X, wooded.Y, Core.Definitions.CityDefinitions.Base));
        Assert.Equal("Park - a fountain", TileInfo.Describe(map, fountain.X, fountain.Y, Core.Definitions.CityDefinitions.Base));
    }

    [Fact]
    public void ScenariosCountParkTiles()
    {
        var city = City();
        var stroke = new BuildStroke(BuildAction.Park, 1, 1);
        stroke.DragTo(2, 2);
        city.Builder.Apply(stroke);

        Assert.Equal(4, ScenarioRunner.Count(city.Map, Core.Definitions.CityDefinitions.Base).Park);
    }
}
