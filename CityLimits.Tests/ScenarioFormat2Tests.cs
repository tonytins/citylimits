using CityLimits.Core.Saving;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ScenarioFormat2Tests
{
    // A 2.0 scenario with the given fields after its name, on a small map.
    private static string File(string fields) =>
        "{ \"format\": \"2.0\", \"name\": \"A\", \"seed\": 1, \"map\": { \"width\": 32, \"height\": 32 }, " + fields + " }";

    private static Scenario Load(string json, string? folder = null)
    {
        Assert.True(ScenarioLoader.TryParse(json, folder, out var scenario, out var error), error);
        return scenario;
    }

    private static string Refusal(string json, string? folder = null)
    {
        Assert.False(ScenarioLoader.TryParse(json, folder, out _, out var error));
        return error;
    }

    [Fact]
    public void DatesBecomeTicksFromTheStart()
    {
        var scenario = Load(File("""
            "ends": "1901-01",
            "actions": [
              { "at": "1900-03", "tool": "Road", "from": { "x": 1, "y": 1 } },
              { "at": "1900-03", "week": 2, "tool": "Road", "from": { "x": 2, "y": 1 } }
            ]
            """));

        Assert.Equal(CityCalendar.StepsPerYear, scenario.Ticks);
        Assert.Equal(2 * CityCalendar.StepsPerMonth, scenario.Actions[0].Tick);
        Assert.Equal((2 * CityCalendar.StepsPerMonth) + CityCalendar.StepsPerCount, scenario.Actions[1].Tick);
        Assert.Equal(new ScenarioDate(1900, 3, 2), scenario.DateAt(scenario.Actions[1].Tick));
        Assert.False(scenario.HasGoals);
    }

    [Fact]
    public void ALaterStartCountsFromItsOwnYear()
    {
        var scenario = Load(File("""
            "start": 1906,
            "ends": "1907-01",
            "checks": [ { "at": "1906-01", "fundsAtLeast": 0 } ]
            """));

        Assert.Equal(1906, scenario.StartYear);
        Assert.Equal(CityCalendar.StepsPerYear, scenario.Ticks);
        Assert.Equal(0, scenario.Checks[0].Tick);

        var report = new ScenarioRunner().Run(scenario, "test");
        Assert.Equal("1907-01", report.Final.Date);
        Assert.Equal(("1906-01", "1907-01"), (report.Scenario.Start, report.Scenario.Ends));
        Assert.Equal("1906-01", report.Checks[0].Date);
    }

    [Theory]
    [InlineData("\"ends\": \"1902-3\"", "ends: needs a month written YYYY-MM, from 1900-01 to 9999-12, not '1902-3'.")]
    [InlineData("\"ends\": \"1899-12\"", "ends: needs a month written YYYY-MM, from 1900-01 to 9999-12, not '1899-12'.")]
    [InlineData("\"ends\": \"1902-13\"", "ends: needs a month written YYYY-MM, from 1900-01 to 9999-12, not '1902-13'.")]
    [InlineData("\"start\": 1906, \"ends\": \"1905-12\"", "ends: 1905-12 is before the scenario starts, in 1906-01.")]
    [InlineData("\"ends\": \"2100-01\"", "ends: 2100-01 is 153600 ticks from the start, in 1900-01; a scenario may run at most 100000, about 130 years.")]
    [InlineData("\"start\": 1899, \"ends\": \"1902-01\"", "start: needs a number from 1900 to 9999, not 1899.")]
    [InlineData("\"ends\": \"1901-01\", \"actions\": [ { \"at\": \"1901-02\", \"tool\": \"Road\", \"from\": { \"x\": 1, \"y\": 1 } } ]", "actions[0].at: 1901-02 is outside the scenario, which runs from 1900-01 to 1901-01.")]
    [InlineData("\"ends\": \"1901-01\", \"checks\": [ { \"at\": \"1900-02\", \"week\": 5, \"fundsAtLeast\": 0 } ]", "checks[0].week: needs a number from 1 to 4, not 5.")]
    [InlineData("\"ends\": \"1901-01\", \"actions\": [ { \"tick\": 0, \"tool\": \"Road\", \"from\": { \"x\": 1, \"y\": 1 } } ]", "actions[0].tick: is not a field a scenario has.")]
    [InlineData("\"ticks\": 768", "ticks: is not a field a scenario has.")]
    public void A2FileThatIsWrongIsRefusedAndSaysWhere(string fields, string expected)
    {
        Assert.Equal(expected, Refusal(File(fields)));
    }

    [Fact]
    public void A1FileCantUseWhat2Added()
    {
        Assert.Equal(
            "goals: is not a field a scenario has.",
            Refusal("""{ "format": "1.3", "name": "A", "seed": 1, "ticks": 1, "goals": [] }"""));
    }

    [Fact]
    public void GoalsReadAsTheirOwnWordsOrAsTheGameWritesThem()
    {
        var scenario = Load(File("""
            "ends": "1901-01",
            "goals": [
              { "measure": "Population", "atLeast": 2000 },
              { "measure": "Crime", "atMost": 60 },
              { "measure": "Funds", "atLeast": 5000, "atMost": 20000 },
              { "measure": "Traffic", "atMost": 80, "text": "Keep the roads moving" }
            ]
            """));

        Assert.True(scenario.HasGoals);
        Assert.Equal(
            ["Have a population of at least 2,000", "Keep average crime at most 60", "Have funds from $5,000 to $20,000", "Keep the roads moving"],
            scenario.Goals.Select(goal => goal.Text));
    }

    [Theory]
    [InlineData("{ \"measure\": \"Population\" }", "goals[0]: needs atLeast, atMost, or both.")]
    [InlineData("{ \"measure\": \"Happiness\", \"atLeast\": 1 }", "goals[0].measure: 'Happiness' is not one of Population, Funds, Pollution, Crime, Traffic.")]
    [InlineData("{ \"measure\": \"Crime\", \"atLeast\": 50, \"atMost\": 10 }", "goals[0]: atLeast 50 is more than atMost 10, so it can never be met.")]
    [InlineData("{ \"measure\": \"Pollution\", \"atMost\": 300 }", "goals[0].atMost: needs a number from 0 to 255, not 300.")]
    public void AGoalThatCantBeMetOrReadIsRefused(string goal, string expected)
    {
        Assert.Equal(expected, Refusal(File($"\"ends\": \"1901-01\", \"goals\": [ {goal} ]")));
    }

    [Fact]
    public void GoalsAreJudgedAtTheEndAndDecideWonButNotPassed()
    {
        var scenario = Load(File("""
            "ends": "1900-06",
            "goals": [ { "measure": "Funds", "atLeast": 19000 }, { "measure": "Population", "atLeast": 1000000 } ]
            """));

        var report = new ScenarioRunner().Run(scenario, "test");

        Assert.True(report.Passed);
        Assert.False(report.Won);
        Assert.Equal([true, false], report.Goals.Select(goal => goal.Met));
        Assert.Equal(20000, report.Goals[0].Found);
    }

    [Fact]
    public void AScenarioWithNoGoalsNeitherWinsNorLoses()
    {
        var report = new ScenarioRunner().Run(Load(File("\"ends\": \"1900-02\"")), "test");

        Assert.Null(report.Won);
        Assert.Empty(report.Goals);
    }

    [Fact]
    public void EventsStrikeOnTheirDatesWhateverTheDisasterSwitchSays()
    {
        // A grid of empty shops, which an earthquake wrecks and a fire can take hold in, on a map
        // the seed gives a river, so a flood finds a shore.
        var scenario = Load(File("""
            "ends": "1900-06",
            "actions": [ { "at": "1900-01", "tool": "ZoneCommercial", "from": { "x": 1, "y": 1 }, "to": { "x": 30, "y": 30 } } ],
            "events": [
              { "at": "1900-02", "disaster": "Earthquake" },
              { "at": "1900-03", "week": 3, "disaster": "Fire" },
              { "at": "1900-04", "disaster": "Flood" }
            ]
            """));
        var runner = new ScenarioRunner();
        var heard = new List<EventReport>();
        runner.EventHappened += heard.Add;

        var report = runner.Run(scenario, "test");

        Assert.False(scenario.Disasters);
        Assert.Equal(["Earthquake", "Fire", "Flood"], report.Events.Select(e => e.Disaster));
        Assert.Equal([true, true, true], report.Events.Select(e => e.Happened));
        Assert.Equal("1900-03 week 3", report.Events[1].Date);
        Assert.Equal(report.Events, heard);
    }

    [Fact]
    public void ARepeatingEventStrikesEverySoManyWeeksAsManyTimesAsItSays()
    {
        // Hamburg's fire bombs, as Micropolis has them: four raids, ten weeks apart.
        var scenario = Load(File("""
            "ends": "1901-01",
            "actions": [ { "at": "1900-01", "tool": "ZoneCommercial", "from": { "x": 1, "y": 1 }, "to": { "x": 30, "y": 30 } } ],
            "events": [ { "at": "1900-02", "disaster": "Fire", "repeat": { "weeks": 10, "times": 4 } } ]
            """));

        var report = new ScenarioRunner().Run(scenario, "test");

        Assert.Equal(new EventRepeat(10, 4), scenario.Events[0].Repeat);
        Assert.Equal(["1900-02", "1900-04 week 3", "1900-07", "1900-09 week 3"], report.Events.Select(e => e.Date));
        Assert.All(report.Events, e => Assert.Equal(0, e.Index));
    }

    [Fact]
    public void AChallengeStrikesEachRepeatOnItsDate()
    {
        var scenario = Load(File("""
            "ends": "1901-06",
            "goals": [ { "measure": "Funds", "atLeast": 1 } ],
            "events": [ { "at": "1900-02", "disaster": "Earthquake", "repeat": { "weeks": 24, "times": 3 } } ]
            """));
        var city = ChallengeShelf.Begin(scenario);

        Assert.Equal(
            [new ScenarioDate(1900, 2), new ScenarioDate(1900, 8), new ScenarioDate(1901, 2)],
            city.Challenge!.UpcomingEvents.Select(e => e.Date));
    }

    [Theory]
    [InlineData("{ \"weeks\": 0, \"times\": 4 }", "events[0].repeat.weeks: needs a number from 1 to 6250, not 0.")]
    [InlineData("{ \"weeks\": 10, \"times\": 0 }", "events[0].repeat.times: needs a number from 1 to 6250, not 0.")]
    [InlineData("{ \"weeks\": 10 }", "events[0].repeat.times: is required.")]
    [InlineData("{ \"weeks\": 20, \"times\": 4 }", "events[0].repeat: striking 4 times, 20 weeks apart, runs past the scenario's end, 1901-01.")]
    public void ARepeatThatCantBeKeptIsRefused(string repeat, string expected)
    {
        Assert.Equal(expected, Refusal(File($"\"ends\": \"1901-01\", \"events\": [ {{ \"at\": \"1900-02\", \"disaster\": \"Fire\", \"repeat\": {repeat} }} ]")));
    }

    [Fact]
    public void ARepeatWrittenAsWordsIsRefused()
    {
        Assert.StartsWith("events[0].repeat.weeks", Refusal(File("""
            "ends": "1901-01",
            "events": [ { "at": "1900-02", "disaster": "Fire", "repeat": { "weeks": "10", "times": "4" } } ]
            """)));
    }

    [Fact]
    public void AFloodWithNoShoreFindsNowhereToStrike()
    {
        var city = new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[256]), 1);

        Assert.False(city.Strike(Disaster.Flood));
        Assert.Equal(0, city.Floods.Count);
    }

    [Fact]
    public void AnEventNeedsADisasterTheGameHas()
    {
        Assert.Equal(
            "events[0].disaster: 'Volcano' is not one of Fire, Flood, Earthquake, Meltdown.",
            Refusal(File("\"ends\": \"1901-01\", \"events\": [ { \"at\": \"1900-02\", \"disaster\": \"Volcano\" } ]")));
    }

    [Fact]
    public void AScenarioCanStartFromASavedCityBesideIt()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var city = new CitySimulation(TileMap.FromTerrain(24, 24, new TileKind[576]), 9, funds: 12345, startYear: 1944);
            city.Builder.Apply(BuildAction.ZoneResidential, 5, 5);
            Assert.True(CityFiles.TryWrite(city, Path.Combine(folder, "town.fcl"), out var problem), problem);
            var json = """
                { "format": "2.0", "name": "From a city", "city": "town.fcl", "ends": "1945-01",
                  "checks": [ { "at": "1944-01", "count": "Residential", "equals": 1 } ] }
                """;

            var scenario = Load(json, folder);
            var report = new ScenarioRunner().Run(scenario, "test");

            Assert.Equal((9, 24, 24, 12345 - 100, 1944), (scenario.Seed, scenario.MapWidth, scenario.MapHeight, scenario.Funds, scenario.StartYear));
            Assert.True(report.Passed);
            Assert.Equal("1945-01", report.Final.Date);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("\"city\": \"../town.fcl\"", "city: '../town.fcl' needs to be a file beside the scenario, or in a folder beside it.")]
    [InlineData("\"city\": \"town.json\"", "city: needs a .fcl or a .cl file, not 'town.json'.")]
    [InlineData("\"city\": \"missing.fcl\"", "city: 'missing.fcl' couldn't be opened: ")]
    [InlineData("\"city\": \"town.fcl\", \"seed\": 1", "seed: a scenario that starts from a saved city takes its seed, map, money, and date from the city, so it can't also have seed.")]
    public void ASavedCityMustBeBesideTheScenarioAndItsOwn(string fields, string expected)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var json = "{ \"format\": \"2.0\", \"name\": \"A\", \"ends\": \"1901-01\", " + fields + " }";
            Assert.StartsWith(expected, Refusal(json, folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AScenarioReadFromTextAloneCantStartFromASavedCity()
    {
        Assert.Equal(
            "city: only a scenario read from a file can start from a saved city, which is read from beside it.",
            Refusal("""{ "format": "2.0", "name": "A", "city": "town.fcl", "ends": "1901-01" }"""));
    }
}
