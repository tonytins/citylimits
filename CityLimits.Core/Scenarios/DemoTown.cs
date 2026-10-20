namespace CityLimits.Core.Scenarios;

/// <summary>
/// The demo town: a road nine tiles long with a residential, a commercial, and an industrial
/// zone along its north side, the industry knocked down after a year. It is the fallback when a
/// scenario can't be loaded, so it lives in code, where it can't go missing, and it is written as
/// a scenario file and read by <see cref="ScenarioLoader"/>, so it passes the same validation.
/// </summary>
public static class DemoTown
{
    /// <summary>The id the demo town is run by.</summary>
    public const string Id = "demo-town";

    /// <summary>The demo town as a scenario file.</summary>
    public const string Json = """
        {
          "format": 1,
          "name": "Demo town",
          "description": "A road with a residential, a commercial, and an industrial zone; the industry is knocked down after a year.",
          "seed": 2000,
          "funds": 20000,
          "ticks": 1536,
          "actions": [
            { "tick": 0, "tool": "Road", "from": { "x": 64, "y": 67 }, "to": { "x": 72, "y": 67 }, "expect": "Built" },
            { "tick": 0, "tool": "ZoneResidential", "from": { "x": 65, "y": 65 }, "expect": "Built" },
            { "tick": 0, "tool": "ZoneCommercial", "from": { "x": 68, "y": 65 }, "expect": "Built" },
            { "tick": 0, "tool": "ZoneIndustrial", "from": { "x": 71, "y": 65 }, "expect": "Built" },
            { "tick": 768, "tool": "Bulldoze", "from": { "x": 71, "y": 65 }, "expect": "Built" }
          ],
          "checks": [
            { "tick": 0, "count": "Road", "equals": 9 },
            { "tick": 1536, "tile": { "x": 65, "y": 65 }, "use": "Residential", "development": 0 },
            { "tick": 1536, "tile": { "x": 68, "y": 65 }, "use": "Commercial", "development": 0 },
            { "tick": 1536, "count": "Developed", "equals": 1 },
            { "tick": 1536, "tile": { "x": 68, "y": 64 }, "served": true },
            { "tick": 1536, "tile": { "x": 72, "y": 66 }, "kind": "Rubble", "use": "None" },
            { "tick": 1536, "fundsEquals": 19609 },
            { "tick": 1536, "count": "Industrial", "equals": 0 }
          ]
        }
        """;

    private static readonly Lazy<Scenario> Parsed = new(() =>
        ScenarioLoader.TryParse(Json, out var scenario, out var error)
            ? scenario
            : throw new InvalidOperationException($"The demo town is not a valid scenario: {error}"));

    /// <summary>The demo town, read and checked. A test keeps it valid, so reading it can't fail in play.</summary>
    public static Scenario Scenario => Parsed.Value;
}
