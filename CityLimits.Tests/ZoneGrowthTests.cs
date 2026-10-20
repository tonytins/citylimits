using CityLimits.Core.Definitions;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ZoneGrowthTests
{
    private static readonly ZoneSpec Residential = CityDefinitions.Base.Zone(TileUse.Residential);
    private static readonly ZoneSpec Commercial = CityDefinitions.Base.Zone(TileUse.Commercial);

    // A zone centred at (4, 4), served by a road along its bottom edge on row 6.
    private static CitySimulation ServedZone(BuildAction zone, int seed = 1, CityDefinitions? definitions = null)
    {
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        var city = new CitySimulation(map, seed, definitions: definitions);
        var road = new BuildStroke(BuildAction.Road, 2, 6);
        road.DragTo(6, 6);
        city.Builder.Apply(road);
        city.Builder.Apply(zone, 4, 4);
        return city;
    }

    // Runs the city until a condition holds, a count at a time, or fails after a long while.
    private static void RunUntil(CitySimulation city, Func<bool> done)
    {
        for (var count = 0; count < 2000 && !done(); count++)
        {
            for (var step = 0; step < CityCalendar.StepsPerCount; step++)
            {
                city.Tick();
            }
        }

        Assert.True(done(), "The zone never got there.");
    }

    private static int Houses(TileMap map) =>
        Enumerable.Range(3, 3).Sum(y => Enumerable.Range(3, 3).Count(x => map.StateAt(x, y).HasHouse));

    [Fact]
    public void AResidentialZoneGrowsItsFirstHouseBesideTheRoad()
    {
        var city = ServedZone(BuildAction.ZoneResidential);

        RunUntil(city, () => Houses(city.Map) == 1);

        // The bottom row of the zone touches the road; the other outer tiles don't.
        var house = Enumerable.Range(3, 3).SelectMany(y => Enumerable.Range(3, 3).Select(x => (x, y))).Single(p => city.Map.StateAt(p.x, p.y).HasHouse);
        Assert.Equal(5, house.y);
        Assert.Equal(0, city.Map.StateAt(4, 4).Development);
        Assert.Equal(1, Residential.PopulationOf(city.Map, 4, 4));
    }

    [Fact]
    public void EightHousesMakeWayForABuilding()
    {
        var city = ServedZone(BuildAction.ZoneResidential);
        var mostHouses = 0;

        RunUntil(city, () =>
        {
            mostHouses = Math.Max(mostHouses, Houses(city.Map));
            return city.Map.StateAt(4, 4).Development == 1;
        });

        Assert.Equal(Residential.Houses, mostHouses);
        Assert.Equal(0, Houses(city.Map));
        Assert.Equal(Residential.Populations[0], Residential.PopulationOf(city.Map, 4, 4));
    }

    [Fact]
    public void ABuildingGrowsALevelAtATimeToItsSpecsTop()
    {
        var city = ServedZone(BuildAction.ZoneResidential);
        var levels = new List<int>();
        city.Map.TileChanged += (x, y) =>
        {
            if ((x, y) == (4, 4))
            {
                levels.Add(city.Map.StateAt(4, 4).Development);
            }
        };

        RunUntil(city, () => city.Map.StateAt(4, 4).Development == Residential.Levels);

        Assert.Equal([1, 2, 3, 4], levels);
        Assert.Equal(40, Residential.PopulationOf(city.Map, 4, 4));
    }

    [Fact]
    public void ACommercialZoneBuildsStraightAwayAndHasFiveLevels()
    {
        var city = ServedZone(BuildAction.ZoneCommercial);

        RunUntil(city, () => city.Map.StateAt(4, 4).Development == Commercial.Levels);

        Assert.Equal(0, Houses(city.Map));
        Assert.Equal(5, Commercial.Levels);
        Assert.Equal(5, Commercial.PopulationOf(city.Map, 4, 4));
    }

    [Fact]
    public void AModdedHouseLimitTakesEffect()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var mod = Directory.CreateDirectory(Path.Combine(folder, "few-houses")).FullName;
            File.WriteAllText(Path.Combine(mod, "mod.json"), """{ "format": 1, "id": "few-houses", "name": "Few houses" }""");
            File.WriteAllText(Path.Combine(mod, "zones.json"), """{ "format": 1, "zones": [ { "id": "residential", "houses": 2, "populations": [10, 20, 30, 50] } ] }""");
            var definitions = ModLoader.Load(folder, out var problems);
            Assert.Empty(problems);
            var city = ServedZone(BuildAction.ZoneResidential, definitions: definitions);
            var mostHouses = 0;

            RunUntil(city, () =>
            {
                mostHouses = Math.Max(mostHouses, Houses(city.Map));
                return city.Map.StateAt(4, 4).Development == 1;
            });

            Assert.Equal(2, mostHouses);
            Assert.Equal(10, definitions.Zone(TileUse.Residential).PopulationOf(city.Map, 4, 4));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{ "format": 1, "zones": [ { "id": "residential", "populations": [1, 2] } ] }""", "zones.json: zones[0].populations: needs 4 levels, one for each building drawing, not 2.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "industrial", "valueClasses": 4 } ] }""", "zones.json: zones[0].valueClasses: a mod can't change it, because each class needs its own drawings.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "industrial", "houses": 3 } ] }""", "zones.json: zones[0].houses: only a residential zone grows single houses.")]
    [InlineData("""{ "format": 1, "zones": [ { "id": "residential", "houses": 9 } ] }""", "zones.json: zones[0].houses: needs a number from 0 to 8, not 9.")]
    public void AModCantChangeWhatTheDrawingsFix(string zones, string expected)
    {
        Assert.False(DefinitionLoader.TryParseMod("m", """{ "format": 1, "id": "m", "name": "M" }""", zones, out _, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void TheReadoutDescribesTheWholeZone()
    {
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        var city = new CitySimulation(map, 1);
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal("Residential zone - not developed yet", TileInfo.Describe(map, 3, 3, city.Definitions));

        map.SetState(3, 3, new TileState(TileKind.Grass, TileUse.Residential, zonePart: 0, house: 2));
        map.SetState(5, 5, new TileState(TileKind.Grass, TileUse.Residential, zonePart: 8, house: 5));
        Assert.Equal("Residential zone - 2 houses", TileInfo.Describe(map, 4, 4, city.Definitions));

        map.SetState(4, 4, new TileState(TileKind.Grass, TileUse.Residential, development: 3, valueClass: 2));
        Assert.Equal("Residential zone - level 3 of 4, population 32", TileInfo.Describe(map, 5, 3, city.Definitions));
    }

    [Theory]
    [InlineData(6, 0, 0)]
    [InlineData(0, 4, 0)]
    [InlineData(0, 0, 13)]
    public void AZoneStateTheCityDoesntHaveIsRejected(int development, int valueClass, int house) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileState(TileKind.Grass, TileUse.Residential, development, 0, valueClass, house));

    [Fact]
    public void OnlyAnOuterTileOfAZoneHoldsAHouse()
    {
        Assert.False(new TileState(TileKind.Grass, TileUse.Residential, house: 3).HasHouse);
        Assert.True(new TileState(TileKind.Grass, TileUse.Residential, zonePart: 1, house: 3).HasHouse);
        Assert.False(new TileState(TileKind.Grass, TileUse.Road, zonePart: 1, house: 3).HasHouse);
    }
}
