using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ChallengeTests
{
    // A small challenge: a road and a zone in its first month, a fire in its third, two goals by mid-year.
    private const string Json = """
        { "format": "2.0", "name": "Little Challenge", "description": "Grow, and keep your money.",
          "seed": 5, "map": { "width": 32, "height": 32 }, "start": 1920, "ends": "1920-07",
          "actions": [
            { "at": "1920-01", "tool": "Road", "from": { "x": 2, "y": 10 }, "to": { "x": 12, "y": 10 } },
            { "at": "1920-02", "week": 2, "tool": "ZoneResidential", "from": { "x": 4, "y": 8 } }
          ],
          "checks": [ { "at": "1920-01", "fundsEquals": 1 } ],
          "events": [ { "at": "1920-03", "disaster": "Fire" } ],
          "goals": [ { "measure": "Funds", "atLeast": 1 }, { "measure": "Population", "atLeast": 1000000 } ] }
        """;

    private static Scenario Scenario()
    {
        Assert.True(ScenarioLoader.TryParse(Json, out var scenario, out var error), error);
        return scenario;
    }

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    [Fact]
    public void AChallengeMakesWhatIsDueOnItsDatesAndJudgesItsGoalsOnceAtTheEnd()
    {
        var scenario = Scenario();
        var city = ChallengeShelf.Begin(scenario);
        var judged = new List<IReadOnlyList<GoalReport>>();
        city.Challenge!.Finished += judged.Add;
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        Run(city, 1);
        Assert.Equal(TileUse.Road, city.Map.StateAt(5, 10).Use);
        Assert.Equal(TileUse.None, city.Map.StateAt(4, 8).Use);

        Run(city, CityCalendar.StepsPerMonth + CityCalendar.StepsPerCount);
        Assert.Equal(TileUse.Residential, city.Map.StateAt(4, 8).Use);
        Assert.DoesNotContain(CityMessage.FireReported, heard);

        Run(city, scenario.Ticks);

        Assert.Contains(CityMessage.FireReported, heard);
        Assert.Single(judged);
        Assert.Equal([true, false], judged[0].Select(goal => goal.Met));
        Assert.False(city.Challenge.Won);
        Assert.Equal(0, city.Challenge.Pending);
        Assert.True(city.CityTime > new ScenarioDate(1920, 7).Count);
    }

    [Fact]
    public void AChallengesCityIsNamedForItAndRunsOnTheBaseGame()
    {
        var city = ChallengeShelf.Begin(Scenario());

        Assert.Equal("Little Challenge", city.Name);
        Assert.Empty(city.Definitions.AppliedMods);
        Assert.Equal(new GameDate(1920, 1), city.Date);
        Assert.Equal(new ScenarioDate(1920, 7), city.Challenge!.Ends);
    }

    [Fact]
    public void AChallengeListsTheDisastersToComeWithTheirDatesUntilTheyStrike()
    {
        var city = ChallengeShelf.Begin(Scenario());

        Assert.Equal([(new ScenarioDate(1920, 3), Disaster.Fire)], city.Challenge!.UpcomingEvents);

        Run(city, (3 * CityCalendar.StepsPerMonth) + 1);

        Assert.Empty(city.Challenge.UpcomingEvents);
    }

    [Fact]
    public void AChallengeSavedPartwayCarriesOnAsIfItHadntBeen()
    {
        var unsaved = ChallengeShelf.Begin(Scenario());
        var saved = ChallengeShelf.Begin(Scenario());
        Run(unsaved, 40);
        Run(saved, 40);

        foreach (var reopened in new[] { Reopen(FlatCityFormat.Write(saved), flat: true), Reopen(BinaryCityFormat.Write(saved)) })
        {
            var copy = Run2(Reopen(FlatCityFormat.Write(unsaved), flat: true));
            Run(reopened, CityCalendar.StepsPerYear);
            Assert.Equal(copy.Challenge!.Won, reopened.Challenge!.Won);
            Assert.Equal(copy.Treasury.Funds, reopened.Treasury.Funds);
            Assert.Equal(copy.Map.StateAt(4, 8), reopened.Map.StateAt(4, 8));
            Assert.Equal("Little Challenge", reopened.Challenge.Name);
        }

        static CitySimulation Run2(CitySimulation city)
        {
            Run(city, CityCalendar.StepsPerYear);
            return city;
        }
    }

    [Fact]
    public void WhatIsOverdueIsMadeAtOnceAndTheGoalsJudgedOnTheNextStep()
    {
        // As when an older version played a challenge's city on past its end and saved it back.
        var city = ChallengeShelf.Begin(Scenario());
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["challenge"]!["endStep"] = 0;
        foreach (var due in save["challenge"]!["actions"]!.AsArray().Concat(save["challenge"]!["events"]!.AsArray()))
        {
            due!["step"] = 0;
        }

        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var late, out var problem), problem);
        var judged = 0;
        late.Challenge!.Finished += _ => judged++;

        Run(late, 1);

        Assert.Equal(1, judged);
        Assert.Equal(TileUse.Road, late.Map.StateAt(5, 10).Use);
        Assert.Equal(TileUse.Residential, late.Map.StateAt(4, 8).Use);
        Assert.Equal(0, late.Challenge.Pending);
    }

    [Fact]
    public void AFreeCitysSaveHasNoChallengeAndAChallengesHasOne()
    {
        var free = new CitySimulation(TileMap.FromTerrain(16, 16, new TileKind[256]), 1);
        Assert.Null(JsonNode.Parse(FlatCityFormat.Write(free))!["challenge"]);

        var challenge = JsonNode.Parse(FlatCityFormat.Write(ChallengeShelf.Begin(Scenario())))!["challenge"]!;
        Assert.Equal("Little Challenge", challenge["name"]!.GetValue<string>());
        Assert.Equal(2, challenge["actions"]!.AsArray().Count);
        Assert.Equal("Fire", challenge["events"]![0]!["disaster"]!.GetValue<string>());
    }

    [Fact]
    public void AnOlderVersionOpensAChallengesCityAsAFreeOne()
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(ChallengeShelf.Begin(Scenario())))!.AsObject();
        save["format"] = "1.7";
        save.Remove("challenge");

        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var city, out var problem), problem);
        Assert.Null(city.Challenge);
    }

    [Theory]
    [InlineData("measure", "Happiness", "challenge.goals[0].measure: 'Happiness' is not a measure.")]
    [InlineData("text", "", "challenge.goals[0]: needs words of 1 to 120 characters, and atLeast, atMost, or both.")]
    public void AChallengePartThatIsWrongIsRefusedAndSaysWhere(string field, string value, string expected)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(ChallengeShelf.Begin(Scenario())))!.AsObject();
        save["challenge"]!["goals"]![0]![field] = value;

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void TheShelfListsChallengesDraftsAndUnreadableFilesButNotTests()
    {
        var builtIn = Directory.CreateTempSubdirectory().FullName;
        var player = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(builtIn, "b-test.json"), """{ "format": "2.0", "name": "A test", "seed": 1, "ends": "1900-02", "checks": [ { "at": "1900-01", "fundsAtLeast": 0 } ] }""");
            File.WriteAllText(Path.Combine(builtIn, "a-challenge.json"), Json);
            File.WriteAllText(Path.Combine(player, "broken.json"), "{ not json");
            File.WriteAllText(Path.Combine(player, "mine.json"), Json.Replace("Little Challenge", "My Own"));
            File.WriteAllText(Path.Combine(player, "nearly.json"), """{ "format": "2.2", "name": "Nearly", "seed": 1, "ends": "1910-01" }""");

            var listings = ChallengeShelf.List([builtIn, player, Path.Combine(player, "missing")], out var problem);

            Assert.Null(problem);
            Assert.Equal(["a-challenge.json", "broken.json", "mine.json", "nearly.json"], listings.Select(l => Path.GetFileName(l.Path)));
            Assert.Equal("Little Challenge", listings[0].Scenario!.Name);
            Assert.Null(listings[1].Scenario);
            Assert.NotNull(listings[1].Problem);
            Assert.Equal("My Own", listings[2].Scenario!.Name);
            Assert.True(listings[3].Scenario!.IsDraft);
            Assert.False(listings[3].Scenario!.HasGoals);
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
            Directory.Delete(player, recursive: true);
        }
    }

    [Fact]
    public void ABuiltInChallengeIsListed()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Scenarios");
        var listings = ChallengeShelf.List([folder], out _);

        Assert.Contains(listings, listing => listing.Scenario?.Name == "First Town");
        Assert.All(listings, listing => Assert.True(listing.Scenario?.HasGoals ?? true));
    }

    private static CitySimulation Reopen(object written, bool flat = false)
    {
        if (flat)
        {
            Assert.True(FlatCityFormat.TryRead((string)written, out var city, out var problem), problem);
            return city;
        }

        Assert.True(BinaryCityFormat.TryRead((byte[])written, out var binary, out var error, out _), error);
        return binary;
    }
}
