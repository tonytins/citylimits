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
}
