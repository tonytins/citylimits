using CityLimits.Core.Definitions;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ZoneGrowthTests
{
    private static readonly ZoneSpec Residential = CityDefinitions.Base.Zone(TileUse.Residential);

    // A zone centred at (4, 4), with a road along its bottom edge on row 6 unless asked not to,
    // and a coal plant that powers it down a line from its east side, far enough off that its
    // pollution doesn't reach the zone, or right against it when asked.
    private static CitySimulation LoneZone(BuildAction zone, bool road = true, CityDefinitions? definitions = null, bool plantBeside = false)
    {
        var map = TileMap.FromTerrain(24, 9, new TileKind[24 * 9]);
        var city = new CitySimulation(map, 1, definitions: definitions);
        if (plantBeside)
        {
            city.Builder.Apply(BuildAction.CoalPowerPlant, 7, 3);
        }
        else
        {
            city.Builder.Apply(BuildAction.CoalPowerPlant, 20, 3);
            var line = new BuildStroke(BuildAction.PowerLine, 6, 4);
            line.DragTo(18, 4);
            city.Builder.Apply(line);
        }

        if (road)
        {
            var stroke = new BuildStroke(BuildAction.Road, 2, 6);
            stroke.DragTo(6, 6);
            city.Builder.Apply(stroke);
        }

        city.Builder.Apply(zone, 4, 4);
        return city;
    }

    // The small-town scenario's city, built and ready to run.
    private static CitySimulation SmallTown()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scenarios", "small-town.json");
        Assert.True(ScenarioLoader.TryLoadFile(path, out var scenario, out var error), error);
        var map = TileMapGenerator.Generate(scenario.Seed, scenario.MapWidth, scenario.MapHeight);
        var city = new CitySimulation(map, scenario.Seed);
        foreach (var action in scenario.Actions)
        {
            var stroke = new BuildStroke(action.Tool, action.FromX, action.FromY);
            stroke.DragTo(action.ToX, action.ToY);
            city.Builder.Apply(stroke);
        }

        return city;
    }

    private static void RunCounts(CitySimulation city, int counts)
    {
        for (var i = 0; i < counts * CityCalendar.StepsPerCount; i++)
        {
            city.Tick();
        }
    }

    private static int Houses(TileMap map) =>
        Enumerable.Range(3, 3).Sum(y => Enumerable.Range(3, 3).Count(x => map.StateAt(x, y).HasHouse));

    [Fact]
    public void AResidentialZoneGrowsItsFirstHouseBesideTheRoad()
    {
        var city = LoneZone(BuildAction.ZoneResidential);
        (int X, int Y)? first = null;
        city.Map.TileChanged += (x, y) => first ??= city.Map.StateAt(x, y).HasHouse ? (x, y) : null;

        RunCounts(city, 200);

        Assert.NotNull(first);
        Assert.Equal(5, first.Value.Y);
    }

    [Fact]
    public void AResidentialZoneBesideACoalPlantDoesntGrowForItsPollution()
    {
        // The plant's pollution comes off the zone's land value, so its worth stays too low to grow;
        // the same zone with the plant down a line grows its first house within these counts.
        var city = LoneZone(BuildAction.ZoneResidential, plantBeside: true);
        var grew = false;
        city.Map.TileChanged += (x, y) => grew |= city.Map.StateAt(x, y).HasHouse;

        RunCounts(city, 200);

        Assert.True(city.Map.StateAt(4, 4).Powered);
        Assert.True(city.Maps.PollutionAt(4, 4) > 0);
        Assert.False(grew);
    }

    [Fact]
    public void AResidentialZoneBetweenTwoCoalPlantsDoesntGrowAtAll()
    {
        // Above 128 pollution a home doesn't grow, however high its score. Pollution comes off land
        // value, and again off the zone's worth, as in Micropolis, so this only decides anything where
        // the woods' worth runs land value over its cap of 250: here every free tile is wooded, and
        // without the rule the zone would grow.
        var map = TileMap.FromTerrain(14, 12, new TileKind[14 * 12]);
        var city = new CitySimulation(map, 1);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 2, 3);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 9, 3);
        var road = new BuildStroke(BuildAction.Road, 5, 6);
        road.DragTo(7, 6);
        city.Builder.Apply(road);
        city.Builder.Apply(BuildAction.ZoneResidential, 6, 4);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.StateAt(x, y).Use == TileUse.None)
                {
                    map.SetState(x, y, new TileState(TileKind.Trees));
                }
            }
        }

        var grew = false;
        city.Map.TileChanged += (x, y) => grew |= city.Map.StateAt(x, y).HasHouse;

        RunCounts(city, 200);

        Assert.True(city.Maps.PollutionAt(6, 4) > 128, $"pollution {city.Maps.PollutionAt(6, 4)}");
        Assert.False(grew);
    }

    [Fact]
    public void ALoneResidentialZoneStaysAtHousesBecauseNothingAroundItIsDense()
    {
        var city = LoneZone(BuildAction.ZoneResidential);
        var mostHouses = 0;
        city.Map.TileChanged += (_, _) => mostHouses = Math.Max(mostHouses, Houses(city.Map));

        RunCounts(city, 400);

        Assert.Equal(0, city.Map.StateAt(4, 4).Development);
        Assert.InRange(mostHouses, 1, Residential.Houses);
    }

    [Fact]
    public void AModdedHouseLimitHolds()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var mod = Directory.CreateDirectory(Path.Combine(folder, "few-houses")).FullName;
            File.WriteAllText(Path.Combine(mod, "mod.json"), """{ "format": 1, "id": "few-houses", "name": "Few houses" }""");
            File.WriteAllText(Path.Combine(mod, "zones.json"), """{ "format": 1, "zones": [ { "id": "residential", "houses": 2 } ] }""");
            var definitions = ModLoader.Load(folder, out var problems);
            Assert.Empty(problems);
            var city = LoneZone(BuildAction.ZoneResidential, definitions: definitions);
            var mostHouses = 0;
            city.Map.TileChanged += (_, _) => mostHouses = Math.Max(mostHouses, Houses(city.Map));

            RunCounts(city, 400);

            Assert.Equal(2, mostHouses);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ZonesChangeALevelAtATimeAndNeverPassTheirSpecsTop()
    {
        var city = SmallTown();
        var levels = new Dictionary<(int, int), int>();
        var jumps = new List<string>();
        city.Map.TileChanged += (x, y) =>
        {
            var state = city.Map.StateAt(x, y);
            if (!state.IsZoneCenter)
            {
                return;
            }

            var before = levels.GetValueOrDefault((x, y));
            if (Math.Abs(state.Development - before) > 1 || state.Development > city.Definitions.Zone(state.Use).Levels)
            {
                jumps.Add($"{state.Use} at ({x}, {y}): {before} to {state.Development}");
            }

            levels[(x, y)] = state.Development;
        };

        RunCounts(city, CityCalendar.CountsPerYear * 10);

        Assert.Empty(jumps);
        Assert.Contains(levels, pair => pair.Value > 1);
    }

    [Fact]
    public void BusinessesNeverHoldHousesAndBuildingsNeverStandAmongHouses()
    {
        var city = SmallTown();
        var problems = new List<string>();
        city.Map.TileChanged += (x, y) =>
        {
            var state = city.Map.StateAt(x, y);
            if (state.HasHouse && (state.Use != TileUse.Residential || city.Map.ZoneStateAt(x, y).Development > 0))
            {
                problems.Add($"a house at ({x}, {y}) in a {state.Use} zone at level {city.Map.ZoneStateAt(x, y).Development}");
            }
        };

        RunCounts(city, CityCalendar.CountsPerYear * 10);

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData(BuildAction.ZoneCommercial, TileUse.Commercial)]
    [InlineData(BuildAction.ZoneIndustrial, TileUse.Industrial)]
    public void ABusinessWithNoRoadDeclinesToNothing(BuildAction zone, TileUse use)
    {
        var city = LoneZone(zone, road: false);
        city.Map.SetState(4, 4, new TileState(TileKind.Grass, use, 3, valueClass: 1));

        RunCounts(city, 400);

        Assert.Equal(0, city.Map.StateAt(4, 4).Development);
    }

    [Fact]
    public void AResidentialBuildingWithNoRoadDeclinesAndNeverBuildsAgain()
    {
        // As in Micropolis, an empty zone takes no trip, so it can grow a few houses before their
        // own trips find no road and they go again; but it never gets a building back.
        var city = LoneZone(BuildAction.ZoneResidential, road: false);
        city.Map.SetState(4, 4, new TileState(TileKind.Grass, TileUse.Residential, 2, valueClass: 1));
        var levels = new List<int>();
        city.Map.TileChanged += (x, y) =>
        {
            // The centre also changes when its power does; only its level is recorded.
            var level = city.Map.StateAt(4, 4).Development;
            if ((x, y) == (4, 4) && (levels.Count == 0 || levels[^1] != level))
            {
                levels.Add(level);
            }
        };

        RunCounts(city, 400);

        Assert.Equal([1, 0], levels);
    }

    [Fact]
    public void CommerceGrowsOnlyAsFarAsLandValueCarriesIt()
    {
        // A shop in one corner with residents beside it, and a large industrial district in the
        // far corner that pulls the city's centre away, so the shop's land value is only 8.
        var map = TileMap.FromTerrain(128, 128, new TileKind[128 * 128]);
        var city = new CitySimulation(map, 3, funds: 1_000_000);
        var road = new BuildStroke(BuildAction.Road, 1, 6);
        road.DragTo(30, 6);
        city.Builder.Apply(road);

        // A nuclear plant, with a line along the zones' tops and another down to the district.
        city.Builder.Apply(BuildAction.NuclearPowerPlant, 33, 3);
        foreach (var (fromX, fromY, toX, toY) in new[] { (1, 2, 31, 2), (36, 2, 112, 2), (112, 3, 112, 98) })
        {
            var line = new BuildStroke(BuildAction.PowerLine, fromX, fromY);
            line.DragTo(toX, toY);
            Assert.Equal(BuildStatus.Built, city.Builder.Apply(line).Status);
        }
        for (var x = 2; x <= 14; x += 3)
        {
            city.Builder.Apply(BuildAction.ZoneResidential, x, 4);
            map.SetState(x, 4, new TileState(TileKind.Grass, TileUse.Residential, 4));
        }

        city.Builder.Apply(BuildAction.ZoneCommercial, 20, 4);
        var district = new BuildStroke(BuildAction.ZoneIndustrial, 100, 100);
        district.DragTo(124, 124);
        city.Builder.Apply(district);
        var mostLevel = 0;
        map.TileChanged += (x, y) =>
        {
            if ((x, y) == (20, 4))
            {
                mostLevel = Math.Max(mostLevel, map.StateAt(20, 4).Development);
            }
        };

        RunCounts(city, CityCalendar.CountsPerYear * 20);

        Assert.Equal(8, city.Maps.LandValueAt(20, 4));
        Assert.Equal(1, mostLevel);
    }

    [Fact]
    public void ResidentialBuildingsTakeTheirClassFromLandValue()
    {
        var city = SmallTown();

        RunCounts(city, CityCalendar.CountsPerYear * 10);

        // The town is near the centre, where land value puts buildings in the middle classes.
        var classes = Enumerable.Range(0, city.Map.Height).SelectMany(y => Enumerable.Range(0, city.Map.Width).Select(x => city.Map.StateAt(x, y)))
            .Where(s => s.IsZoneCenter && s.Use == TileUse.Residential && s.Development > 0)
            .Select(s => s.ValueClass)
            .ToList();
        Assert.NotEmpty(classes);
        Assert.All(classes, valueClass => Assert.InRange(valueClass, 1, 3));
    }

    [Fact]
    public void TheReadoutDescribesTheWholeZone()
    {
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        var city = new CitySimulation(map, 1);
        city.Builder.Apply(BuildAction.ZoneResidential, 4, 4);

        Assert.Equal("Residential zone - not developed yet, no power", TileInfo.Describe(map, 3, 3, city.Definitions));

        map.SetState(4, 4, new TileState(TileKind.Grass, TileUse.Residential, powered: true));
        map.SetState(3, 3, new TileState(TileKind.Grass, TileUse.Residential, zonePart: 0, house: 2));
        map.SetState(5, 5, new TileState(TileKind.Grass, TileUse.Residential, zonePart: 8, house: 5));
        Assert.Equal("Residential zone - 2 houses", TileInfo.Describe(map, 4, 4, city.Definitions));

        map.SetState(4, 4, new TileState(TileKind.Grass, TileUse.Residential, development: 3, valueClass: 2, powered: true));
        Assert.Equal("Residential zone - level 3 of 4, population 32", TileInfo.Describe(map, 5, 3, city.Definitions));
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
