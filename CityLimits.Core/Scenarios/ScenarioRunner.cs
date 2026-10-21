using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// Runs a scenario: builds its city from the seed, makes its strokes and its checks at their
/// ticks, and reports what came of them. A run is deterministic, touches no file, and logs
/// nothing; it raises an event for each action and check so a caller can log them.
/// </summary>
public sealed class ScenarioRunner
{
    /// <summary>Raised after each action is made.</summary>
    public event Action<ActionReport>? ActionMade;

    /// <summary>Raised after each check is made.</summary>
    public event Action<CheckReport>? CheckMade;

    /// <summary>The last tick an action or a check needs, so a run can't be cut short of it.</summary>
    public static int LatestTick(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var latest = 0;
        foreach (var action in scenario.Actions)
        {
            latest = Math.Max(latest, action.Tick);
        }

        foreach (var check in scenario.Checks)
        {
            latest = Math.Max(latest, check.Tick);
        }

        return latest;
    }

    /// <summary>
    /// Runs a scenario for its ticks, or for <paramref name="ticks"/> when given. At each tick the
    /// actions due are made in file order, then the checks due, and then the city takes a step.
    /// </summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <param name="source">Where the scenario came from, an id or a path, for the report.</param>
    /// <param name="ticks">How many ticks to run instead of the scenario's own, or null.</param>
    /// <param name="fallback">When the demo town runs in place of another scenario, which one and why.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are outside 0 to <see cref="Scenario.MaxTicks"/>, or end before an action or a check is due.</exception>
    public ScenarioReport Run(Scenario scenario, string source, int? ticks = null, ScenarioFallback? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var end = ticks ?? scenario.Ticks;
        ArgumentOutOfRangeException.ThrowIfNegative(end, nameof(ticks));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, Scenario.MaxTicks, nameof(ticks));
        ArgumentOutOfRangeException.ThrowIfLessThan(end, LatestTick(scenario), nameof(ticks));

        var map = TileMapGenerator.Generate(scenario.Seed, scenario.MapWidth, scenario.MapHeight);
        var city = new CitySimulation(map, scenario.Seed, scenario.Funds);
        var plan = new BuildPlan();
        var actions = scenario.Actions.OrderBy(a => a.Tick).ThenBy(a => a.Index).ToList();
        var checks = scenario.Checks.OrderBy(c => c.Tick).ThenBy(c => c.Index).ToList();
        var actionReports = new List<ActionReport>(actions.Count);
        var checkReports = new List<CheckReport>(checks.Count);
        var nextAction = 0;
        var nextCheck = 0;

        for (var tick = 0; ; tick++)
        {
            while (nextAction < actions.Count && actions[nextAction].Tick == tick)
            {
                var report = Make(city, plan, actions[nextAction++]);
                actionReports.Add(report);
                ActionMade?.Invoke(report);
            }

            while (nextCheck < checks.Count && checks[nextCheck].Tick == tick)
            {
                var report = Check(city, checks[nextCheck++]);
                checkReports.Add(report);
                CheckMade?.Invoke(report);
            }

            if (tick == end)
            {
                break;
            }

            city.Tick();
        }

        return new ScenarioReport
        {
            Scenario = new ScenarioSummary(scenario.Name, source, scenario.Seed, end),
            Passed = checkReports.All(c => c.Passed) && actionReports.All(a => a.Met != false),
            Fallback = fallback,
            Final = new ScenarioFinal(city.TickCount, city.CityTime, city.Date.ToString(), city.Treasury.Funds, Count(map, city.Definitions)),
            Actions = actionReports,
            Checks = checkReports,
        };
    }

    /// <summary>
    /// What stands on a map: road tiles, bridges among them, zones of each kind, developed zones,
    /// which are those with any population, houses included, power plants of each kind, and
    /// power line tiles, those under water or under a road included.
    /// </summary>
    public static CityCounts Count(TileMap map, CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(definitions);

        int road = 0, bridge = 0, residential = 0, commercial = 0, industrial = 0, developed = 0, coal = 0, nuclear = 0, lines = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                if (state.IsRoad)
                {
                    road++;
                    bridge += state.IsBridge ? 1 : 0;
                }

                lines += state.HasPowerLine ? 1 : 0;
                if (state.IsBuildingCenter)
                {
                    coal += state.Use == TileUse.CoalPowerPlant ? 1 : 0;
                    nuclear += state.Use == TileUse.NuclearPowerPlant ? 1 : 0;
                }

                if (!state.IsZoneCenter)
                {
                    continue;
                }

                residential += state.Use == TileUse.Residential ? 1 : 0;
                commercial += state.Use == TileUse.Commercial ? 1 : 0;
                industrial += state.Use == TileUse.Industrial ? 1 : 0;
                developed += definitions.Zone(state.Use).PopulationOf(map, x, y) > 0 ? 1 : 0;
            }
        }

        return new CityCounts(road, bridge, residential, commercial, industrial, developed, coal, nuclear, lines);
    }

    // The stroke is planned first, for what it would skip and why, then applied as the tool
    // applies it. Both see the same map, so they agree.
    private static ActionReport Make(CitySimulation city, BuildPlan plan, ScenarioAction action)
    {
        var stroke = new BuildStroke(action.Tool, action.FromX, action.FromY);
        stroke.DragTo(action.ToX, action.ToY);
        city.Builder.Plan(stroke, plan);
        var result = city.Builder.Apply(stroke);

        var skipped = new Dictionary<string, int>();
        foreach (var reason in Enum.GetValues<BuildRefusal>())
        {
            var count = plan.SkippedBecause(reason);
            if (count > 0)
            {
                skipped[reason.ToString()] = count;
            }
        }

        var built = result.Status == BuildStatus.Built;
        return new ActionReport(
            action.Index,
            action.Tick,
            action.Tool.ToString(),
            result.Status.ToString(),
            built ? null : result.Reason.ToString(),
            result.TilesChanged,
            skipped,
            built ? result.Cost : 0,
            action.Expect?.IsMetBy(result));
    }

    private static CheckReport Check(CitySimulation city, ScenarioCheck check)
    {
        var failure = check.Subject switch
        {
            CheckSubject.Tile => CheckTile(city, check),
            CheckSubject.Funds => CheckFunds(city.Treasury.Funds, check),
            _ => CheckCount(Count(city.Map, city.Definitions).Of(check.CountOf), check),
        };

        return failure is { } found
            ? new CheckReport(check.Index, check.Tick, false, found.Expected, found.Actual)
            : new CheckReport(check.Index, check.Tick, true, null, null);
    }

    // Each check returns the first condition that doesn't hold, or null when they all do.
    private static (string Expected, string Actual)? CheckTile(CitySimulation city, ScenarioCheck check)
    {
        var map = city.Map;
        var state = map.StateAt(check.X, check.Y);
        var development = map.ZoneStateAt(check.X, check.Y).Development;
        var tile = $"tile ({check.X}, {check.Y})";

        if (check.Kind is { } kind && state.Kind != kind)
        {
            return ($"{tile} kind is {kind}", $"{state.Kind}");
        }

        if (check.Use is { } use && state.Use != use)
        {
            return ($"{tile} use is {use}", $"{state.Use}");
        }

        if (check.Development is { } exactly && development != exactly)
        {
            return ($"{tile} development is {exactly}", $"{development}");
        }

        if (check.DevelopmentAtLeast is { } least && development < least)
        {
            return ($"{tile} development at least {least}", $"{development}");
        }

        if (check.DevelopmentAtMost is { } most && development > most)
        {
            return ($"{tile} development at most {most}", $"{development}");
        }

        if (check.Served is { } served)
        {
            if (!state.IsZoned)
            {
                return ($"{tile} served is {Lower(served)}", "the tile has no zone");
            }

            city.Roads.EnsureCurrent(map);
            var (centerX, centerY) = map.ZoneCenterOf(check.X, check.Y);
            var isServed = city.Roads.IsZoneServed(centerX, centerY);
            if (isServed != served)
            {
                return ($"{tile} served is {Lower(served)}", Lower(isServed));
            }
        }

        return null;
    }

    private static (string Expected, string Actual)? CheckFunds(int funds, ScenarioCheck check)
    {
        if (check.FundsEquals is { } exactly && funds != exactly)
        {
            return ($"funds equal {exactly}", $"{funds}");
        }

        if (check.FundsAtLeast is { } least && funds < least)
        {
            return ($"funds at least {least}", $"{funds}");
        }

        if (check.FundsAtMost is { } most && funds > most)
        {
            return ($"funds at most {most}", $"{funds}");
        }

        return null;
    }

    private static (string Expected, string Actual)? CheckCount(int count, ScenarioCheck check)
    {
        var what = $"{check.CountOf} count";

        if (check.CountEquals is { } exactly && count != exactly)
        {
            return ($"{what} equals {exactly}", $"{count}");
        }

        if (check.CountAtLeast is { } least && count < least)
        {
            return ($"{what} at least {least}", $"{count}");
        }

        if (check.CountAtMost is { } most && count > most)
        {
            return ($"{what} at most {most}", $"{count}");
        }

        return null;
    }

    private static string Lower(bool value) => value ? "true" : "false";
}
