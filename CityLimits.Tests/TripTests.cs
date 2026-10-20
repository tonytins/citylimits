using CityLimits.Core.Definitions;
using CityLimits.Core.Randomness;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TripTests
{
    // A flat map with a road along row 6, from column 1 to the given end.
    private static (TileMap Map, CitySimulation City) Town(int width, int roadEnd)
    {
        var map = TileMap.FromTerrain(width, 9, new TileKind[width * 9]);
        var city = new CitySimulation(map, 1, funds: 100_000);
        var road = new BuildStroke(BuildAction.Road, 1, 6);
        road.DragTo(roadEnd, 6);
        city.Builder.Apply(road);
        return (map, city);
    }

    private static TripOutcome Drive(TileMap map, int centerX, int centerY, int seed = 7, CityDefinitions? definitions = null) =>
        new TripPlanner().Drive(map, definitions ?? CityDefinitions.Base, new DeterministicRandom(seed), centerX, centerY);

    [Fact]
    public void AZoneWithNoRoadAlongItsEdgeCantSetOut()
    {
        var (map, city) = Town(20, 15);
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 2);

        Assert.Equal(TripOutcome.NoRoad, Drive(map, 4, 2));
    }

    [Fact]
    public void AResidentialTripReachesAShopAlongTheRoad()
    {
        var (map, city) = Town(20, 15);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        city.Builder.Apply(BuildAction.ZoneCommercial, 12, 4);

        Assert.Equal(TripOutcome.Arrived, Drive(map, 2, 4));
    }

    [Fact]
    public void ATripWithNothingOfItsKindAlongTheRoadFindsNowhereToGo()
    {
        var (map, city) = Town(20, 15);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        city.Builder.Apply(BuildAction.ZoneResidential, 12, 4);

        Assert.Equal(TripOutcome.NoDestination, Drive(map, 2, 4));
    }

    [Fact]
    public void AResidentialZoneIsAPlaceToGoOnlyOnceItHasPeople()
    {
        var (map, city) = Town(20, 15);
        city.Builder.Apply(BuildAction.ZoneIndustrial, 2, 4);
        city.Builder.Apply(BuildAction.ZoneResidential, 12, 4);

        Assert.Equal(TripOutcome.NoDestination, Drive(map, 2, 4));

        // A house on the zone's bottom row, beside the road.
        map.SetState(12, 5, new TileState(TileKind.Grass, TileUse.Residential, zonePart: 7, house: 1));
        Assert.Equal(TripOutcome.Arrived, Drive(map, 2, 4));
    }

    [Fact]
    public void ATripReachesThirtyRoadTilesAndNoFurther()
    {
        // The trip sets out from the first edge road it finds, column 3 (the bottom edge is read
        // from right to left). A shop over column 33 is 30 steps on; one over column 34 is 31.
        var (near, nearCity) = Town(40, 38);
        nearCity.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        nearCity.Builder.Apply(BuildAction.ZoneCommercial, 34, 4);
        Assert.Equal(TripOutcome.Arrived, Drive(near, 2, 4));

        var (far, farCity) = Town(40, 38);
        farCity.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        farCity.Builder.Apply(BuildAction.ZoneCommercial, 35, 4);
        Assert.Equal(TripOutcome.NoDestination, Drive(far, 2, 4));
    }

    [Fact]
    public void ADeadEndUsuallyEndsTheTrip()
    {
        // The road stops at column 9, and the shop is beyond it, off the road.
        var (map, city) = Town(20, 9);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        city.Builder.Apply(BuildAction.ZoneCommercial, 14, 4);

        Assert.Equal(TripOutcome.NoDestination, Drive(map, 2, 4));
    }

    [Fact]
    public void TheRouteIsRememberedEveryOtherStep()
    {
        var (map, city) = Town(20, 15);
        city.Builder.Apply(BuildAction.ZoneResidential, 2, 4);
        city.Builder.Apply(BuildAction.ZoneCommercial, 12, 4);
        var planner = new TripPlanner();

        planner.Drive(map, CityDefinitions.Base, new DeterministicRandom(7), 2, 4);

        // The trip sets out from column 3 and arrives beside the shop at column 11, eight steps on,
        // remembering the second, fourth, sixth, and eighth.
        Assert.Equal([(5, 6), (7, 6), (9, 6), (11, 6)], planner.Route);
    }

    [Fact]
    public void TheSameSeedSendsTheSameTrip()
    {
        // A closed grid of roads, with no dead ends, where each crossing is a choice.
        var map = TileMap.FromTerrain(20, 20, new TileKind[400]);
        var city = new CitySimulation(map, 1, funds: 100_000);
        for (var line = 2; line <= 18; line += 4)
        {
            var across = new BuildStroke(BuildAction.Road, 2, line);
            across.DragTo(18, line);
            city.Builder.Apply(across);
            var down = new BuildStroke(BuildAction.Road, line, 2);
            down.DragTo(line, 18);
            city.Builder.Apply(down);
        }

        map.SetState(8, 4, new TileState(TileKind.Grass, TileUse.Residential));
        var first = new TripPlanner();
        var second = new TripPlanner();

        first.Drive(map, CityDefinitions.Base, new DeterministicRandom(99), 8, 4);
        second.Drive(map, CityDefinitions.Base, new DeterministicRandom(99), 8, 4);

        Assert.Equal(TripPlanner.MaxDistance / 2, first.Route.Count);
        Assert.Equal(first.Route, second.Route);
    }

    [Fact]
    public void AModCanChangeWhereAZonesTripsGo()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var mod = Directory.CreateDirectory(Path.Combine(folder, "trade")).FullName;
            File.WriteAllText(Path.Combine(mod, "mod.json"), """{ "format": 1, "id": "trade", "name": "Trade" }""");
            File.WriteAllText(Path.Combine(mod, "zones.json"), """{ "format": 1, "zones": [ { "id": "industrial", "trips": ["commercial"] } ] }""");
            var definitions = ModLoader.Load(folder, out var problems);
            Assert.Empty(problems);
            var (map, city) = Town(20, 15);
            city.Builder.Apply(BuildAction.ZoneIndustrial, 2, 4);
            city.Builder.Apply(BuildAction.ZoneCommercial, 12, 4);

            Assert.Equal(TripOutcome.NoDestination, Drive(map, 2, 4));
            Assert.Equal(TripOutcome.Arrived, Drive(map, 2, 4, definitions: definitions));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{ "format": 1, "zones": [ { "id": "industrial", "trips": [] } ] }""", "zones.json: zones[0].trips: needs at least one kind of zone to go to.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "industrial", "trips": ["park"] } ] }""", "zones.json: zones[0].trips[0]: 'park' is not one of residential, commercial, industrial.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "industrial", "trips": ["commercial", "commercial"] } ] }""", "zones.json: zones[0].trips[1]: 'commercial' is listed twice.")]
    public void AModsTripsAreChecked(string zones, string expected)
    {
        Assert.False(DefinitionLoader.TryParseMod("m", """{ "format": 1, "id": "m", "name": "M" }""", zones, out _, out var error));
        Assert.Equal(expected, error);
    }
}
