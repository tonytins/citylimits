using CityLimits.Core.Scenarios;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ScenarioLoaderTests
{
    private const string Minimal = """{ "format": 1, "name": "Empty", "seed": 7, "ticks": 64 }""";

    private static Scenario Load(string json)
    {
        Assert.True(ScenarioLoader.TryParse(json, out var scenario, out var error), error);
        return scenario;
    }

    private static string Refuse(string json)
    {
        Assert.False(ScenarioLoader.TryParse(json, out var scenario, out var error));
        Assert.Null(scenario);
        return error;
    }

    // A scenario with one action or check, the rest of the file kept minimal.
    private static string With(string field, string entry) =>
        $$"""{ "format": 1, "name": "Test", "seed": 1, "ticks": 100, "{{field}}": [ {{entry}} ] }""";

    [Fact]
    public void AMinimalScenarioTakesTheDefaults()
    {
        var scenario = Load(Minimal);

        Assert.Equal("Empty", scenario.Name);
        Assert.Equal(7, scenario.Seed);
        Assert.Equal(64, scenario.Ticks);
        Assert.Equal(MapSize.DefaultWidth, scenario.MapWidth);
        Assert.Equal(MapSize.DefaultHeight, scenario.MapHeight);
        Assert.Equal(Core.Economy.Treasury.StartingFunds, scenario.Funds);
        Assert.Empty(scenario.Actions);
        Assert.Empty(scenario.Checks);
    }

    [Fact]
    public void TheDemoTownIsAValidScenario()
    {
        Assert.True(ScenarioLoader.TryParse(DemoTown.Json, out var scenario, out var error), error);
        Assert.Equal("Demo town", scenario.Name);
        Assert.Equal(7, scenario.Actions.Count);
        Assert.Same(DemoTown.Scenario, DemoTown.Scenario);
    }

    [Fact]
    public void AnActionIsAStrokeFromATileToAnother()
    {
        var action = Load(With("actions", """{ "tick": 3, "tool": "Road", "from": { "x": 1, "y": 2 }, "to": { "x": 9, "y": 2 }, "expect": "Refused:NotEnoughFunds" }""")).Actions[0];

        Assert.Equal(new ScenarioAction(0, 3, BuildAction.Road, 1, 2, 9, 2, new ExpectedResult(BuildStatus.Refused, BuildRefusal.NotEnoughFunds)), action);
    }

    [Fact]
    public void AStrokeWithNoEndEndsWhereItStarts()
    {
        var action = Load(With("actions", """{ "tick": 0, "tool": "ZoneIndustrial", "from": { "x": 5, "y": 6 } }""")).Actions[0];

        Assert.Equal((5, 6), (action.ToX, action.ToY));
        Assert.Null(action.Expect);
    }

    [Fact]
    public void TheThreeKindsOfCheckAreRead()
    {
        var scenario = Load("""
            { "format": 1, "name": "Checks", "seed": 1, "ticks": 10, "checks": [
              { "tick": 10, "tile": { "x": 3, "y": 4 }, "use": "Residential", "developmentAtLeast": 1, "served": true },
              { "tick": 5, "fundsAtLeast": 100, "fundsAtMost": 200 },
              { "tick": 0, "count": "Bridge", "equals": 0 } ] }
            """);

        Assert.Equal(CheckSubject.Tile, scenario.Checks[0].Subject);
        Assert.Equal((3, 4), (scenario.Checks[0].X, scenario.Checks[0].Y));
        Assert.Equal(TileUse.Residential, scenario.Checks[0].Use);
        Assert.Equal(1, scenario.Checks[0].DevelopmentAtLeast);
        Assert.True(scenario.Checks[0].Served);
        Assert.Equal(CheckSubject.Funds, scenario.Checks[1].Subject);
        Assert.Equal(200, scenario.Checks[1].FundsAtMost);
        Assert.Equal(CheckSubject.Count, scenario.Checks[2].Subject);
        Assert.Equal(CountSubject.Bridge, scenario.Checks[2].CountOf);
        Assert.Equal(0, scenario.Checks[2].CountEquals);
    }

    [Theory]
    [InlineData("""{ "name": "A", "seed": 1, "ticks": 1 }""", "format: is required.")]
    [InlineData("""{ "format": 4, "name": "A", "seed": 1, "ticks": 1 }""", "format: this is format 4.0; this version of CityLimits reads up to 3.0.")]
    [InlineData("""{ "format": "2.3", "name": "A", "seed": 1, "ends": "1901-01" }""", "format: this is format 2.3; this version of CityLimits reads up to 2.2.")]
    [InlineData("""{ "format": "1.5", "name": "A", "seed": 1, "ticks": 1 }""", "format: this is format 1.5; this version of CityLimits reads up to 1.3.")]
    [InlineData("""{ "format": 1, "name": " ", "seed": 1, "ticks": 1 }""", "name: needs 1 to 80 characters, not 0.")]
    [InlineData("""{ "format": 1, "name": "A", "ticks": 1 }""", "seed: is required.")]
    [InlineData("""{ "format": 1, "name": "A", "seed": 1, "ticks": 100001 }""", "ticks: needs a number from 0 to 100000, not 100001.")]
    [InlineData("""{ "format": 1, "name": "A", "seed": 1, "ticks": 1, "map": { "width": 8, "height": 64 } }""", "map.width: needs a number from 16 to 512, not 8.")]
    [InlineData("""{ "format": 1, "name": "A", "seed": 1, "ticks": 1, "funds": -1 }""", "funds: needs a number from 0 to 1000000, not -1.")]
    public void TheFileItselfIsChecked(string json, string error) =>
        Assert.Equal(error, Refuse(json));

    [Theory]
    [InlineData("""{ "tick": 0, "tool": "Road", "from": { "x": 5000, "y": 0 } }""", "actions[0].from.x: needs a number from -512 to 1023, not 5000.")]
    [InlineData("""{ "tick": 101, "tool": "Road", "from": { "x": 0, "y": 0 } }""", "actions[0].tick: needs a number from 0 to 100, not 101.")]
    [InlineData("""{ "tick": 0, "tool": "road", "from": { "x": 0, "y": 0 } }""", "actions[0].tool: 'road' is not one of Road, ZoneResidential, ZoneCommercial, ZoneIndustrial, Bulldoze, CoalPowerPlant, NuclearPowerPlant, PowerLine, FireStation, PoliceStation, Park, Rail, Stadium, Seaport, Airport.")]
    [InlineData("""{ "tick": 0, "tool": "0", "from": { "x": 0, "y": 0 } }""", "actions[0].tool: '0' is not one of Road, ZoneResidential, ZoneCommercial, ZoneIndustrial, Bulldoze, CoalPowerPlant, NuclearPowerPlant, PowerLine, FireStation, PoliceStation, Park, Rail, Stadium, Seaport, Airport.")]
    [InlineData("""{ "tick": 0, "tool": "Road" }""", "actions[0].from: is required.")]
    [InlineData("""{ "tick": 0, "tool": "Road", "from": { "x": 0, "y": 0 }, "expect": "Built:CannotBuildHere" }""", "actions[0].expect: a stroke that was built has no reason, so 'Built:CannotBuildHere' can never be met.")]
    [InlineData("""{ "tick": 0, "tool": "Road", "from": { "x": 0, "y": 0 }, "expect": "Refused:Broke" }""", "actions[0].expect: 'Broke' is not one of None, OffTheMap, CannotBuildHere, AlreadyDone, NotEnoughFunds, NothingToClear, PartOfAZone, OnFire, Flooded, Radioactive.")]
    [InlineData("null", "actions[0]: needs an action, not null.")]
    public void EachActionIsChecked(string action, string error) =>
        Assert.Equal(error, Refuse(With("actions", action)));

    [Theory]
    [InlineData("""{ "tick": 0, "tile": { "x": 120, "y": 0 }, "use": "Road" }""", "checks[0].tile: (120, 0) is outside the 120 by 100 map.")]
    [InlineData("""{ "tick": 0, "tile": { "x": 1, "y": 1 }, "count": "Road", "equals": 1 }""", "checks[0]: checks one subject, so it can't have both tile and count.")]
    [InlineData("""{ "tick": 0, "tile": { "x": 1, "y": 1 }, "equals": 1 }""", "checks[0]: a tile check takes only kind, use, development, developmentAtLeast, developmentAtMost, served, or powered.")]
    [InlineData("""{ "tick": 0, "tile": { "x": 1, "y": 1 } }""", "checks[0]: a tile check needs at least one of kind, use, development, developmentAtLeast, developmentAtMost, served, or powered.")]
    [InlineData("""{ "tick": 0, "count": "Zones", "equals": 1 }""", "checks[0].count: 'Zones' is not one of Road, Bridge, Residential, Commercial, Industrial, Developed, CoalPowerPlant, NuclearPowerPlant, PowerLine, FireStation, PoliceStation, Park, Hospital, Church, Rail, Stadium, Seaport, Airport.")]
    [InlineData("""{ "tick": 0 }""", "checks[0]: a funds check needs at least one of fundsAtLeast, fundsAtMost, or fundsEquals; or give it a tile or a count.")]
    [InlineData("""{ "tick": 0, "tile": { "x": 1, "y": 1 }, "development": 6 }""", "checks[0].development: needs a number from 0 to 5, not 6.")]
    public void EachCheckIsChecked(string check, string error) =>
        Assert.Equal(error, Refuse(With("checks", check)));

    [Fact]
    public void AnUnknownFieldIsRefusedSoAMisspeltCheckCantPass() =>
        Assert.Equal("checks[0].fundsAtleast: is not a field a scenario has.", Refuse(With("checks", """{ "tick": 0, "fundsAtleast": 5 }""")));

    [Fact]
    public void AValueOfTheWrongTypeIsRefused() =>
        Assert.Equal("seed: has the wrong type of value.", Refuse("""{ "format": 1, "name": "A", "seed": "one", "ticks": 1 }"""));

    [Theory]
    [InlineData("")]
    [InlineData("{ ")]
    [InlineData("""{ "format": 1, }""")]
    public void TextThatIsNotJsonIsRefused(string json) =>
        Assert.EndsWith("is not valid JSON.", Refuse(json));

    [Fact]
    public void AFileOverTheLimitIsRefusedBeforeItIsRead()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, new string(' ', (int)ScenarioLoader.MaxFileBytes + 1));

            Assert.False(ScenarioLoader.TryLoadFile(path, out _, out var error));
            Assert.Contains("the most is 1048576", error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AMissingFileIsReported()
    {
        Assert.False(ScenarioLoader.TryLoadFile("/no/such/scenario.json", out _, out var error));
        Assert.Equal("There is no scenario file at '/no/such/scenario.json'.", error);
    }

    [Theory]
    [InlineData("demo-town", true)]
    [InlineData("river-2", true)]
    [InlineData("Demo-Town", false)]
    [InlineData("../secrets", false)]
    [InlineData("a--b", false)]
    [InlineData("-a", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AnIdCanNeverNameAPathOutsideTheFolder(string? id, bool valid) =>
        Assert.Equal(valid, ScenarioCatalog.IsValidId(id));

    [Fact]
    public void TheCatalogListsTheDemoTownFirstThenItsFiles()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "zebra.json"), Minimal);
            File.WriteAllText(Path.Combine(folder, "apple.json"), Minimal);
            File.WriteAllText(Path.Combine(folder, "Not An Id.json"), Minimal);
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "");
            var catalog = new ScenarioCatalog(folder);

            Assert.Equal(["demo-town", "apple", "zebra"], catalog.ListIds(out var problem));
            Assert.Null(problem);
            Assert.True(catalog.TryLoad("apple", out var scenario, out _));
            Assert.Equal("Empty", scenario.Name);
            Assert.False(catalog.TryLoad("missing", out _, out var error));
            Assert.StartsWith("There is no scenario file", error);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AMissingFolderStillListsTheDemoTown()
    {
        var catalog = new ScenarioCatalog("/no/such/folder");

        Assert.Equal(["demo-town"], catalog.ListIds(out var problem));
        Assert.Null(problem);
        Assert.True(catalog.TryLoad("demo-town", out var scenario, out _));
        Assert.Same(DemoTown.Scenario, scenario);
    }
}
