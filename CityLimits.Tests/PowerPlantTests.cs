using CityLimits.Core.Definitions;
using CityLimits.Core.Economy;
using CityLimits.Core.Scenarios;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class PowerPlantTests
{
    private static readonly int CoalPrice = CityDefinitions.Base.Plant(TileUse.CoalPowerPlant).Price;

    private static (TileMap Map, Treasury Treasury, CityBuilder Builder) NewCity(int funds = 10_000)
    {
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        var treasury = new Treasury(funds);
        return (map, treasury, new CityBuilder(map, treasury, new RoadNetwork()));
    }

    private static BuildStroke Drag(BuildAction action, int fromX, int fromY, int toX, int toY)
    {
        var stroke = new BuildStroke(action, fromX, fromY);
        stroke.DragTo(toX, toY);
        return stroke;
    }

    [Theory]
    [InlineData(BuildAction.CoalPowerPlant, TileUse.CoalPowerPlant, 3000)]
    [InlineData(BuildAction.NuclearPowerPlant, TileUse.NuclearPowerPlant, 5000)]
    public void APlantIsFourByFourAroundACentreOneTileInFromItsCorner(BuildAction action, TileUse use, int price)
    {
        var city = NewCity();

        var result = city.Builder.Apply(action, 3, 3);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(16, result.TilesChanged);
        Assert.Equal(10_000 - price, city.Treasury.Funds);
        for (var y = 2; y <= 5; y++)
        {
            for (var x = 2; x <= 5; x++)
            {
                var state = city.Map.StateAt(x, y);
                Assert.Equal(use, state.Use);
                Assert.Equal(((y - 2) * 4) + (x - 2), state.ZonePart);
                Assert.Equal((3, 3), city.Map.ZoneCenterOf(x, y));
            }
        }

        Assert.True(city.Map.StateAt(3, 3).IsBuildingCenter);
        Assert.False(city.Map.StateAt(3, 3).IsZoneCenter);
        Assert.Equal(TileUse.None, city.Map.StateAt(6, 3).Use);
        Assert.Equal(TileUse.None, city.Map.StateAt(1, 3).Use);
    }

    [Fact]
    public void ThePricesAndCapacitiesAreMicropolis()
    {
        Assert.Equal(new PlantSpec("coal", TileUse.CoalPowerPlant, "Coal power plant", 3000, 700), CityDefinitions.Base.Plant(TileUse.CoalPowerPlant));
        Assert.Equal(new PlantSpec("nuclear", TileUse.NuclearPowerPlant, "Nuclear power plant", 5000, 2000), CityDefinitions.Base.Plant(TileUse.NuclearPowerPlant));
        Assert.Throws<ArgumentOutOfRangeException>(() => CityDefinitions.Base.Plant(TileUse.Residential));
    }

    [Fact]
    public void APlantHasPowerFromTheMomentItIsPlacedButAStationWaitsForTheScan()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);
        city.Builder.Apply(BuildAction.FireStation, 7, 7);

        Assert.True(city.Map.StateAt(3, 3).Powered);
        Assert.False(city.Map.StateAt(2, 2).Powered);
        Assert.False(city.Map.StateAt(7, 7).Powered);
    }

    [Fact]
    public void DraggingAPlantMovesItRatherThanAddingMore()
    {
        var stroke = Drag(BuildAction.CoalPowerPlant, 1, 1, 6, 5);

        Assert.Equal(new TileRect(5, 4, 4, 4), stroke.Bounds);
    }

    [Fact]
    public void APlantGoesDownWholeOrNotAtAll()
    {
        var city = NewCity();
        city.Map.SetState(5, 5, new TileState(TileKind.Water));

        var result = city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(10_000, city.Treasury.Funds);
        Assert.Equal(TileUse.None, city.Map.StateAt(3, 3).Use);
    }

    [Fact]
    public void APlantClearsTreesAndRubbleForADollarATile()
    {
        var city = NewCity();
        city.Map.SetState(2, 2, new TileState(TileKind.Trees));
        city.Map.SetState(5, 5, new TileState(TileKind.Rubble));

        city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);

        Assert.Equal(10_000 - CoalPrice - (2 * TileCosts.AutoClear), city.Treasury.Funds);
        Assert.Equal(TileKind.Grass, city.Map.StateAt(2, 2).Kind);
    }

    [Theory]
    [InlineData(BuildAction.Road, 5, 2)]
    [InlineData(BuildAction.ZoneResidential, 6, 6)]
    public void APlantCantGoOverARoadOrAZone(BuildAction existing, int x, int y)
    {
        var city = NewCity();
        city.Builder.Apply(existing, x, y);
        var funds = city.Treasury.Funds;

        var result = city.Builder.Apply(BuildAction.NuclearPowerPlant, 3, 3);

        Assert.Equal(BuildRefusal.CannotBuildHere, result.Reason);
        Assert.Equal(funds, city.Treasury.Funds);
    }

    [Fact]
    public void BuildingTheSamePlantTwiceChangesNothing()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);

        var result = city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);

        Assert.Equal(BuildStatus.Unchanged, result.Status);
        Assert.Equal(BuildRefusal.AlreadyDone, result.Reason);
        Assert.Equal(10_000 - CoalPrice, city.Treasury.Funds);
    }

    [Fact]
    public void APlantThatWouldRunOffTheMapIsRefused()
    {
        var city = NewCity();

        var result = city.Builder.Apply(BuildAction.CoalPowerPlant, 7, 3);

        Assert.Equal(BuildRefusal.OffTheMap, result.Reason);
        Assert.Equal(10_000, city.Treasury.Funds);
    }

    [Fact]
    public void TheBulldozerWontClearAPlantTileByTile()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);

        var result = city.Builder.Apply(BuildAction.Bulldoze, 5, 5);

        Assert.Equal(BuildRefusal.PartOfAZone, result.Reason);
        Assert.Equal(TileUse.CoalPowerPlant, city.Map.StateAt(5, 5).Use);
    }

    [Fact]
    public void TheBulldozerKnocksDownAPlantThroughItsCentreAndLeavesRubble()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);

        var result = city.Builder.Apply(BuildAction.Bulldoze, 3, 3);

        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(16, result.TilesChanged);
        Assert.Equal(10_000 - CoalPrice - TileCosts.Bulldoze, city.Treasury.Funds);
        for (var y = 2; y <= 5; y++)
        {
            for (var x = 2; x <= 5; x++)
            {
                Assert.Equal(new TileState(TileKind.Rubble), city.Map.StateAt(x, y));
            }
        }
    }

    [Fact]
    public void APlantStateKnowsItsSixteenParts()
    {
        Assert.Equal(5, new TileState(TileKind.Grass, TileUse.CoalPowerPlant).ZonePart);
        Assert.Equal(15, new TileState(TileKind.Grass, TileUse.CoalPowerPlant, zonePart: 15).ZonePart);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileState(TileKind.Grass, TileUse.CoalPowerPlant, zonePart: 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileState(TileKind.Grass, TileUse.Residential, zonePart: 9));
        Assert.Equal(0, new TileState(TileKind.Grass, TileUse.CoalPowerPlant, development: 3).Development);
    }

    [Fact]
    public void ScenariosCountPlantsByTheirCentresAndNotAsZones()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.CoalPowerPlant, 3, 3);

        var counts = ScenarioRunner.Count(city.Map, CityDefinitions.Base);

        Assert.Equal(new CityCounts(0, 0, 0, 0, 0, 0, CoalPowerPlant: 1, NuclearPowerPlant: 0), counts);
    }

    [Fact]
    public void TheReadoutNamesAPlant()
    {
        var city = NewCity();
        city.Builder.Apply(BuildAction.NuclearPowerPlant, 3, 3);

        Assert.Equal(
            "Nuclear power plant - bulldoze its centre to clear it",
            TileInfo.Describe(city.Map, 5, 2, CityDefinitions.Base));
    }
}
