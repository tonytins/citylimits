using System.Text.Json;
using CityLimits.Core.Scenarios;

namespace CityLimits.Tests;

public class ScenarioRunnerTests
{
    // A flat 16 by 16 map isn't available from a seed, so these scenarios use seed 2000's map
    // around the demo town, where the ground is clear grass.
    private static Scenario Load(string json)
    {
        Assert.True(ScenarioLoader.TryParse(json, out var scenario, out var error), error);
        return scenario;
    }

    private static ScenarioReport Run(string json, int? ticks = null) =>
        new ScenarioRunner().Run(Load(json), "test", ticks);

    [Fact]
    public void TheDemoTownPassesWithTheFiguresTheSpecGives()
    {
        var report = new ScenarioRunner().Run(DemoTown.Scenario, DemoTown.Id);

        Assert.True(report.Passed, string.Join("; ", report.Checks.Where(c => !c.Passed).Select(c => $"{c.Expected}: {c.Actual}")));
        Assert.Equal(new ScenarioSummary("Demo town", "demo-town", 2000, 1536), report.Scenario);
        Assert.Equal(1536, report.Final.Tick);
        Assert.Equal(96, report.Final.CityTime);
        Assert.Equal("1902-01", report.Final.Date);
        Assert.Equal(16609, report.Final.Funds);
        Assert.Equal(new CityCounts(9, 0, 1, 1, 0, 1, CoalPowerPlant: 1), report.Final.Counts);
        Assert.Equal([90, 100, 100, 100, 3000, 1], report.Actions.Select(a => a.Cost));
        Assert.Equal([9, 9, 9, 9, 16, 9], report.Actions.Select(a => a.Tiles));
        Assert.All(report.Actions, a => Assert.True(a.Met));
        Assert.Null(report.Fallback);
    }

    [Fact]
    public void TheSameScenarioGivesTheSameReport()
    {
        var first = ScenarioReportJson.Serialize(new ScenarioRunner().Run(DemoTown.Scenario, DemoTown.Id));
        var second = ScenarioReportJson.Serialize(new ScenarioRunner().Run(DemoTown.Scenario, DemoTown.Id));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ACheckSeesTheActionsOfItsOwnTick()
    {
        var report = Run("""
            { "format": 1, "name": "Same tick", "seed": 2000, "ticks": 0,
              "actions": [ { "tick": 0, "tool": "Road", "from": { "x": 34, "y": 53 }, "to": { "x": 36, "y": 53 } } ],
              "checks": [ { "tick": 0, "count": "Road", "equals": 3 }, { "tick": 0, "fundsEquals": 19970 } ] }
            """);

        Assert.True(report.Passed);
        Assert.Equal(0, report.Final.Tick);
    }

    [Fact]
    public void ActionsOnOneTickRunInFileOrder()
    {
        // The road is laid first, so the zone that would cover it can't go down.
        var report = Run("""
            { "format": 1, "name": "Order", "seed": 2000, "ticks": 0, "actions": [
              { "tick": 0, "tool": "Road", "from": { "x": 35, "y": 51 } },
              { "tick": 0, "tool": "ZoneResidential", "from": { "x": 35, "y": 51 }, "expect": "Refused:CannotBuildHere" } ] }
            """);

        Assert.True(report.Passed);
        Assert.Equal(new Dictionary<string, int> { ["CannotBuildHere"] = 1 }, report.Actions[1].Skipped);
        Assert.Equal("CannotBuildHere", report.Actions[1].Reason);
        Assert.Equal(0, report.Actions[1].Cost);
    }

    [Fact]
    public void AnUnmetExpectationFailsTheRun()
    {
        var report = Run("""
            { "format": 1, "name": "Broke", "seed": 2000, "ticks": 0, "funds": 5,
              "actions": [ { "tick": 0, "tool": "Road", "from": { "x": 34, "y": 53 }, "expect": "Built" } ] }
            """);

        Assert.False(report.Passed);
        Assert.False(report.Actions[0].Met);
        Assert.Equal("Refused", report.Actions[0].Status);
        Assert.Equal("NotEnoughFunds", report.Actions[0].Reason);
    }

    [Fact]
    public void AFailedCheckSaysWhatWasExpectedAndWhatWasFound()
    {
        var report = Run("""
            { "format": 1, "name": "Wrong", "seed": 2000, "ticks": 0, "checks": [
              { "tick": 0, "fundsEquals": 1 },
              { "tick": 0, "count": "Road", "atLeast": 2 },
              { "tick": 0, "tile": { "x": 34, "y": 50 }, "served": true },
              { "tick": 0, "tile": { "x": 34, "y": 50 }, "use": "Road" } ] }
            """);

        Assert.False(report.Passed);
        Assert.Equal(("funds equal 1", "20000"), (report.Checks[0].Expected, report.Checks[0].Actual));
        Assert.Equal(("Road count at least 2", "0"), (report.Checks[1].Expected, report.Checks[1].Actual));
        Assert.Equal(("tile (34, 50) served is true", "the tile has no zone"), (report.Checks[2].Expected, report.Checks[2].Actual));
        Assert.Equal(("tile (34, 50) use is Road", "None"), (report.Checks[3].Expected, report.Checks[3].Actual));
    }

    [Fact]
    public void ATileOfAZoneIsCheckedByItsZone()
    {
        var report = Run("""
            { "format": 1, "name": "Zone", "seed": 2000, "ticks": 0, "actions": [
              { "tick": 0, "tool": "ZoneCommercial", "from": { "x": 35, "y": 51 } } ],
              "checks": [
              { "tick": 0, "tile": { "x": 34, "y": 50 }, "use": "Commercial", "development": 0, "served": false },
              { "tick": 0, "count": "Commercial", "equals": 1 } ] }
            """);

        Assert.True(report.Passed);
    }

    [Fact]
    public void ARunCanBeLongerButNotShorterThanItsChecksNeed()
    {
        var scenario = Load("""{ "format": 1, "name": "Late", "seed": 2000, "ticks": 64, "checks": [ { "tick": 64, "fundsAtLeast": 0 } ] }""");
        var runner = new ScenarioRunner();

        Assert.Equal(64, ScenarioRunner.LatestTick(scenario));
        Assert.Equal(128, runner.Run(scenario, "test", ticks: 128).Final.Tick);
        Assert.Equal("1900-03", runner.Run(scenario, "test", ticks: 128).Final.Date);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(scenario, "test", ticks: 63));
    }

    [Fact]
    public void EachActionAndCheckIsAnnouncedAsItIsMade()
    {
        var runner = new ScenarioRunner();
        var heard = new List<string>();
        runner.ActionMade += a => heard.Add($"action {a.Index}");
        runner.CheckMade += c => heard.Add($"check {c.Index}");

        runner.Run(DemoTown.Scenario, DemoTown.Id);

        Assert.Equal(["action 0", "action 1", "action 2", "action 3", "action 4", "check 0", "action 5", "check 1", "check 2", "check 3", "check 4", "check 5", "check 6", "check 7"], heard);
    }

    [Fact]
    public void AFallbackIsRecordedInTheReport()
    {
        var fallback = new ScenarioFallback("river", "There is no scenario file at 'river.json'.");

        var report = new ScenarioRunner().Run(DemoTown.Scenario, DemoTown.Id, fallback: fallback);

        Assert.Equal(fallback, report.Fallback);
    }

    [Fact]
    public void TheJsonReportHasTheDocumentedShape()
    {
        var json = ScenarioReportJson.Serialize(new ScenarioRunner().Run(DemoTown.Scenario, DemoTown.Id));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("format").GetInt32());
        Assert.True(root.GetProperty("passed").GetBoolean());
        Assert.False(root.TryGetProperty("fallback", out _));
        Assert.Equal("demo-town", root.GetProperty("scenario").GetProperty("source").GetString());
        Assert.Equal(9, root.GetProperty("final").GetProperty("counts").GetProperty("road").GetInt32());
        var road = root.GetProperty("actions")[0];
        Assert.Equal("Road", road.GetProperty("tool").GetString());
        Assert.Equal("Built", road.GetProperty("status").GetString());
        Assert.False(road.TryGetProperty("reason", out _));
        Assert.Equal(JsonValueKind.Object, road.GetProperty("skipped").ValueKind);
        Assert.True(root.GetProperty("checks")[0].GetProperty("passed").GetBoolean());
        Assert.False(root.GetProperty("checks")[0].TryGetProperty("expected", out _));
    }
}
