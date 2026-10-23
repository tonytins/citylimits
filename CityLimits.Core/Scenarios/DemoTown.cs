namespace CityLimits.Core.Scenarios;

/// <summary>
/// The demo town: a road nine tiles long with a residential, a commercial, and an industrial
/// zone along its north side, a coal plant up a power line to the north, far enough that its
/// pollution doesn't reach them, and the industry knocked down after a year. It is the fallback when a
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
          "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/scenario.v2.0.schema.json",
          "format": "2.0",
          "name": "Demo town",
          "description": "A road with a residential, a commercial, and an industrial zone, powered by a coal plant up a line to the north; the industry is knocked down after a year.",
          "seed": 2000,
          "funds": 20000,
          "ends": "1902-01",
          "actions": [
            { "at": "1900-01", "tool": "Road", "from": { "x": 34, "y": 53 }, "to": { "x": 42, "y": 53 }, "expect": "Built" },
            { "at": "1900-01", "tool": "ZoneResidential", "from": { "x": 35, "y": 51 }, "expect": "Built" },
            { "at": "1900-01", "tool": "ZoneCommercial", "from": { "x": 38, "y": 51 }, "expect": "Built" },
            { "at": "1900-01", "tool": "ZoneIndustrial", "from": { "x": 41, "y": 51 }, "expect": "Built" },
            { "at": "1900-01", "tool": "CoalPowerPlant", "from": { "x": 35, "y": 40 }, "expect": "Built" },
            { "at": "1900-01", "tool": "PowerLine", "from": { "x": 35, "y": 43 }, "to": { "x": 35, "y": 49 }, "expect": "Built" },
            { "at": "1901-01", "tool": "Bulldoze", "from": { "x": 41, "y": 51 }, "expect": "Built" }
          ],
          "checks": [
            { "at": "1900-01", "count": "Road", "equals": 9 },
            { "at": "1902-01", "tile": { "x": 35, "y": 51 }, "use": "Residential", "development": 0 },
            { "at": "1902-01", "tile": { "x": 38, "y": 51 }, "use": "Commercial", "development": 0 },
            { "at": "1902-01", "count": "Developed", "equals": 1 },
            { "at": "1902-01", "tile": { "x": 38, "y": 50 }, "served": true, "powered": true },
            { "at": "1902-01", "tile": { "x": 42, "y": 52 }, "kind": "Rubble", "use": "None" },
            { "at": "1902-01", "fundsEquals": 16572 },
            { "at": "1902-01", "count": "Industrial", "equals": 0 }
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
