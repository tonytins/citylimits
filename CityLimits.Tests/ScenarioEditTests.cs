using CityLimits.Core.Saving;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class ScenarioEditTests
{
    // A small scenario: a road in its first month, a fire in its third, a goal by mid-year.
    private const string Json = """
        { "format": "2.0", "name": "Little Edit", "seed": 5, "map": { "width": 32, "height": 32 },
          "start": 1920, "ends": "1920-07", "funds": 100,
          "actions": [ { "at": "1920-01", "tool": "Road", "from": { "x": 2, "y": 10 }, "to": { "x": 12, "y": 10 } } ],
          "events": [ { "at": "1920-03", "disaster": "Fire" } ],
          "goals": [ { "measure": "Population", "atLeast": 1000000 } ] }
        """;

    private static ScenarioEdit Edit()
    {
        Assert.True(ScenarioLoader.TryParse(Json, out var scenario, out var error), error);
        return new ScenarioEdit(scenario, "little-edit.json");
    }

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // The centre of the first square of the size given, inside the map, with no water in it.
    private static (int X, int Y) DryLand(TileMap map, int size)
    {
        for (var y = 1; y + size < map.Height; y++)
        {
            for (var x = 1; x + size < map.Width; x++)
            {
                var dry = true;
                for (var dy = -1; dy < size - 1 && dry; dy++)
                {
                    for (var dx = -1; dx < size - 1 && dry; dx++)
                    {
                        dry = map.StateAt(x + dx, y + dy).Kind != TileKind.Water;
                    }
                }

                if (dry)
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException("The map has no dry land that big.");
    }

    [Fact]
    public void AScenarioOpensForEditingNamedForItWithNoChallengeAndBuildingFree()
    {
        var city = Edit().OpenCity();

        Assert.Equal("Little Edit", city.Name);
        Assert.Null(city.Challenge);
        Assert.Equal(new GameDate(1920, 1), city.Date);

        // A coal plant costs far more than the scenario's $100, and is built anyway, for nothing.
        var (x, y) = DryLand(city.Map, 4);
        var result = city.Builder.Apply(BuildAction.CoalPowerPlant, x, y);
        Assert.Equal(BuildStatus.Built, result.Status);
        Assert.Equal(0, result.Cost);
        Assert.Equal(100, city.Treasury.Funds);
    }

    [Fact]
    public void AFreePlanSaysSoAndCostsNothing()
    {
        var city = Edit().OpenCity();
        var plan = new BuildPlan();
        var road = new BuildStroke(BuildAction.Road, 2, 20);
        road.DragTo(20, 20);

        city.Builder.Plan(road, plan);

        Assert.True(plan.IsFree);
        Assert.Equal(0, plan.Cost);
        Assert.Equal(BuildRefusal.None, plan.Refusal);
    }

    [Fact]
    public void ATestRunsTheScenarioAndGoingBackPutsTheCityAsItWasAtTheCheckpoint()
    {
        var edit = Edit();
        var city = edit.OpenCity();
        city.Builder.Apply(BuildAction.ZoneResidential, 20, 20);
        var before = FlatCityFormat.Write(city);
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        edit.BeginTest(city);
        Assert.True(edit.IsTesting);
        Assert.NotNull(city.Challenge);
        Run(city, 4 * CityCalendar.StepsPerMonth);
        city.Builder.Apply(BuildAction.ZoneCommercial, 5, 20);

        // The scenario's own stroke and its fire came on their dates, as in a player's challenge.
        Assert.Equal(TileUse.Road, city.Map.StateAt(5, 10).Use);
        Assert.Contains(CityMessage.FireReported, heard);

        var back = edit.EndTest();

        Assert.False(edit.IsTesting);
        Assert.Null(back.Challenge);
        Assert.True(back.Builder.BuildsForFree);
        Assert.Equal(before, FlatCityFormat.Write(back));
    }

    [Fact]
    public void ATestFromTheSameCheckpointRunsTheSameEachTime()
    {
        var edit = Edit();
        var city = edit.OpenCity();

        edit.BeginTest(city);
        Run(city, 2 * CityCalendar.StepsPerMonth);
        var first = FlatCityFormat.Write(city);

        var again = edit.EndTest();
        edit.BeginTest(again);
        Run(again, 2 * CityCalendar.StepsPerMonth);

        Assert.Equal(first, FlatCityFormat.Write(again));
    }

    [Fact]
    public void StartingOverOpensTheScenariosCityAfreshAndDropsTheTest()
    {
        var edit = Edit();
        var city = edit.OpenCity();
        var fresh = FlatCityFormat.Write(city);
        city.Builder.Apply(BuildAction.ZoneResidential, 20, 20);
        edit.BeginTest(city);

        var over = edit.OpenCity();

        Assert.False(edit.IsTesting);
        Assert.Equal(fresh, FlatCityFormat.Write(over));
    }

    [Fact]
    public void ATestCantBeginTwiceOrEndWithoutBeginning()
    {
        var edit = Edit();
        var city = edit.OpenCity();

        Assert.Throws<InvalidOperationException>(() => edit.EndTest());
        edit.BeginTest(city);
        Assert.Throws<InvalidOperationException>(() => edit.BeginTest(city));
    }

    [Fact]
    public void DetailsChangedAreCheckedAsAFileIsAndTakenWhole()
    {
        var edit = Edit();
        var city = edit.OpenCity();
        var details = ScenarioDetails.Of(edit.Scenario) with
        {
            Name = "Renamed",
            Description = "A new briefing.",
            Funds = 5000,
            Ends = new ScenarioDate(1921, 1),
            Goals = [new GoalDetails(GoalMeasure.Funds, 0, null, "Stay out of debt")],
            Events = [new EventDetails(new ScenarioDate(1920, 2, 3), Disaster.Flood, new EventRepeat(8, 3))],
        };

        Assert.True(edit.TryChange(details, out var changed, out var problem), problem);
        edit.Accept(changed, city);

        Assert.Equal("Renamed", edit.Scenario.Name);
        Assert.Equal("A new briefing.", edit.Scenario.Description);
        Assert.Equal(new ScenarioDate(1921, 1), edit.Scenario.DateAt(edit.Scenario.Ticks));
        Assert.Equal("Stay out of debt", Assert.Single(edit.Scenario.Goals).Text);
        Assert.Equal(new EventRepeat(8, 3), Assert.Single(edit.Scenario.Events).Repeat);
        Assert.Equal(new ScenarioDate(1920, 2, 3), edit.Scenario.DateAt(edit.Scenario.Events[0].Tick));
        Assert.Equal(5000, city.Treasury.Funds);

        // The scenario's own stroke is kept as it was.
        Assert.Single(edit.Scenario.Actions);
        var back = ScenarioDetails.Of(edit.Scenario);
        Assert.Equal(details with { Goals = [], Events = [] }, back with { Goals = [], Events = [] });
        Assert.Equal(details.Goals, back.Goals);
        Assert.Equal(details.Events, back.Events);
    }

    [Fact]
    public void AMovedStartYearMovesTheCitysFoundingWithEverythingOnItKept()
    {
        var edit = Edit();
        var city = edit.OpenCity();
        var (x, y) = DryLand(city.Map, 4);
        city.Builder.Apply(BuildAction.CoalPowerPlant, x, y);
        var details = ScenarioDetails.Of(edit.Scenario) with
        {
            StartYear = 1950,
            Ends = new ScenarioDate(1951, 1),
            Events = [new EventDetails(new ScenarioDate(1950, 6), Disaster.Fire, null)],
        };

        Assert.True(edit.TryChange(details, out var changed, out var problem), problem);
        edit.Accept(changed, city);

        Assert.Equal(new GameDate(1950, 1), city.Date);
        Assert.Equal(1950, city.FoundedYear);
        Assert.Equal(TileUse.CoalPowerPlant, city.Map.StateAt(x, y).Use);
        Assert.Equal(new ScenarioDate(1950, 1), edit.Scenario.DateAt(Assert.Single(edit.Scenario.Actions).Tick));
        Assert.Equal(new ScenarioDate(1950, 6), edit.Scenario.DateAt(Assert.Single(edit.Scenario.Events).Tick));
    }

    [Fact]
    public void AMovedCityIsAsOneFoundedInItsNewYear()
    {
        var map = TileMapGenerator.Generate(7, 32, 32);
        var moved = new CitySimulation(TileMapGenerator.Generate(7, 32, 32), 7, startYear: 1920);
        var founded = new CitySimulation(map, 7, startYear: 1975);

        moved.MoveFounding(1975);

        Assert.Equal(FlatCityFormat.Write(founded), FlatCityFormat.Write(moved));
        for (var i = 0; i < CityCalendar.StepsPerYear; i++)
        {
            moved.Tick();
            founded.Tick();
        }

        Assert.Equal(FlatCityFormat.Write(founded), FlatCityFormat.Write(moved));
    }

    [Fact]
    public void OnlyACityAtItsFoundingMoves()
    {
        var city = new CitySimulation(TileMapGenerator.Generate(7, 32, 32), 7, startYear: 1920);
        city.Tick();

        Assert.False(city.IsAtFounding);
        Assert.Throws<InvalidOperationException>(() => city.MoveFounding(1930));
        Assert.Equal(1920, city.FoundedYear);
    }

    [Fact]
    public void DetailsThatCantBeAFileAreRefusedWithWhy()
    {
        var edit = Edit();
        var details = ScenarioDetails.Of(edit.Scenario) with { Ends = new ScenarioDate(1919, 1) };

        Assert.False(edit.TryChange(details, out _, out var problem));
        Assert.Equal("ends: 1919-01 is before the scenario starts, in 1920-01.", problem);
        Assert.Equal(new ScenarioDate(1920, 7), edit.Scenario.DateAt(edit.Scenario.Ticks));
    }

    [Fact]
    public void DetailsCantChangeWhileATestIsUnderWay()
    {
        var edit = Edit();
        var city = edit.OpenCity();
        Assert.True(edit.TryChange(ScenarioDetails.Of(edit.Scenario), out var changed, out var problem), problem);
        edit.BeginTest(city);

        Assert.Throws<InvalidOperationException>(() => edit.Accept(changed, city));
    }
}
