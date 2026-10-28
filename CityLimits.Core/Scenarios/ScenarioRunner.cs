using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// Runs a scenario: builds its city from the seed in its start year, or opens its saved city,
/// makes its strokes, strikes its disasters, and makes its checks at their ticks, judges its goals
/// at the end, and reports what came of them. A run is deterministic, reads no file but the saved
/// city a scenario starts from, and logs nothing; it raises an event for each action, disaster,
/// and check so a caller can log them.
/// </summary>
public sealed class ScenarioRunner
{
    /// <summary>Raised after each action is made.</summary>
    public event Action<ActionReport>? ActionMade;

    /// <summary>Raised after each check is made.</summary>
    public event Action<CheckReport>? CheckMade;

    /// <summary>Raised after each of the scenario's disasters strikes, or finds nowhere to.</summary>
    public event Action<EventReport>? EventHappened;

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

        foreach (var happening in scenario.Strikes())
        {
            latest = Math.Max(latest, happening.Tick);
        }

        return latest;
    }

    /// <summary>
    /// Whether each action's report lists every tile it changed (<see cref="ActionReport.Changes"/>),
    /// as <c>citylimits scenario --verbose</c> asks; off, a report gives only the totals.
    /// </summary>
    public bool ListsTiles { get; set; }

    /// <summary>
    /// Runs a scenario for its ticks, or for <paramref name="ticks"/> when given. At each tick the
    /// actions due are made in file order, then the disasters due strike, then the checks due are
    /// made, and then the city takes a step; on the last tick the goals are judged.
    /// </summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <param name="source">Where the scenario came from, an id or a path, for the report.</param>
    /// <param name="ticks">How many ticks to run instead of the scenario's own, or null.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are outside 0 to <see cref="Scenario.MaxTicks"/>, or end before an action, a disaster, or a check is due.</exception>
    /// <exception cref="InvalidDataException">The saved city the scenario starts from can't be opened any more.</exception>
    public ScenarioReport Run(Scenario scenario, string source, int? ticks = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var end = ticks ?? scenario.Ticks;
        ArgumentOutOfRangeException.ThrowIfNegative(end, nameof(ticks));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, Scenario.MaxTicks, nameof(ticks));
        ArgumentOutOfRangeException.ThrowIfLessThan(end, LatestTick(scenario), nameof(ticks));

        var city = Start(scenario);
        city.RandomFires = scenario.Disasters;
        city.RandomFloods = scenario.Disasters;
        city.RandomEarthquakes = scenario.Disasters;
        city.RandomMeltdowns = scenario.Disasters;
        var plan = new BuildPlan();
        var actions = scenario.Actions.OrderBy(a => a.Tick).ThenBy(a => a.Index).ToList();
        var events = scenario.Strikes();
        var checks = scenario.Checks.OrderBy(c => c.Tick).ThenBy(c => c.Index).ToList();
        var actionReports = new List<ActionReport>(actions.Count);
        var eventReports = new List<EventReport>(events.Count);
        var checkReports = new List<CheckReport>(checks.Count);
        var goalReports = new List<GoalReport>(scenario.Goals.Count);
        var (nextAction, nextEvent, nextCheck) = (0, 0, 0);

        for (var tick = 0; ; tick++)
        {
            var date = scenario.DateAt(tick).ToString();
            while (nextAction < actions.Count && actions[nextAction].Tick == tick)
            {
                var report = Make(city, plan, actions[nextAction++], date, ListsTiles);
                actionReports.Add(report);
                ActionMade?.Invoke(report);
            }

            while (nextEvent < events.Count && events[nextEvent].Tick == tick)
            {
                var happening = events[nextEvent++];
                var report = new EventReport(happening.Index, tick, date, happening.Disaster.ToString(), city.Strike(happening.Disaster));
                eventReports.Add(report);
                EventHappened?.Invoke(report);
            }

            while (nextCheck < checks.Count && checks[nextCheck].Tick == tick)
            {
                var report = Check(city, checks[nextCheck++], date);
                checkReports.Add(report);
                CheckMade?.Invoke(report);
            }

            if (tick == end)
            {
                goalReports.AddRange(scenario.Goals.Select(goal => Judge(city, goal)));
                break;
            }

            city.Tick();
        }

        return new ScenarioReport
        {
            Scenario = new ScenarioSummary(scenario.Name, source, scenario.Seed, end, scenario.DateAt(0).ToString(), scenario.DateAt(end).ToString()),
            Passed = checkReports.All(c => c.Passed) && actionReports.All(a => a.Met != false),
            Won = scenario.HasGoals ? goalReports.All(g => g.Met) : null,
            Final = new ScenarioFinal(city.TickCount, city.CityTime, city.Date.ToString(), city.Treasury.Funds, Count(city.Map, city.Definitions)),
            Actions = actionReports,
            Events = eventReports,
            Checks = checkReports,
            Goals = goalReports,
        };
    }

    /// <summary>The scenario's city as it starts: its saved city, opened afresh, or one made from its seed in its start year.</summary>
    /// <exception cref="InvalidDataException">The saved city can't be opened any more.</exception>
    public static CitySimulation Start(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.CityFile is { } file)
        {
            return CityFiles.TryRead(file, out var saved, out var problem, out _)
                ? saved
                : throw new InvalidDataException($"The scenario's city '{file}' couldn't be opened: {problem}");
        }

        var map = TileMapGenerator.Generate(scenario.Seed, scenario.MapWidth, scenario.MapHeight);
        return new CitySimulation(map, scenario.Seed, scenario.Funds, startYear: scenario.StartYear);
    }

    /// <summary>What a goal measures in a city now.</summary>
    public static int Measure(CitySimulation city, GoalMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(city);
        return measure switch
        {
            GoalMeasure.Population => city.Census.CityPopulation,
            GoalMeasure.Funds => city.Treasury.Funds,
            GoalMeasure.Pollution => city.Maps.PollutionAverage,
            GoalMeasure.Crime => city.Maps.CrimeAverage,
            _ => city.Traffic.Average,
        };
    }

    /// <summary>Whether a city meets a goal now, and what was asked and found when it doesn't.</summary>
    public static GoalReport Judge(CitySimulation city, ScenarioGoal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        var found = Measure(city, goal.Measure);
        var met = (goal.AtLeast is not { } least || found >= least) && (goal.AtMost is not { } most || found <= most);
        return new GoalReport(goal.Index, goal.Text, met, found);
    }

    /// <summary>
    /// What stands on a map: road tiles, bridges among them, zones of each kind, developed zones,
    /// which are those with any population, houses included, power plants of each kind, power
    /// line tiles, those under water or under a road or rail included, stations of each kind, park tiles, hospitals, churches,
    /// rail tiles, tunnels and crossings included, stadiums, seaports, and airports; a road tile is any tile with a road on
    /// it, a rail crossing too.
    /// </summary>
    public static CityCounts Count(TileMap map, CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(definitions);

        int road = 0, bridge = 0, residential = 0, commercial = 0, industrial = 0, developed = 0, coal = 0, nuclear = 0, lines = 0, fire = 0, police = 0, parks = 0, hospitals = 0, churches = 0, rail = 0, stadiums = 0, seaports = 0, airports = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var state = map.StateAt(x, y);
                if (state.HasRoad)
                {
                    road++;
                    bridge += state.IsBridge ? 1 : 0;
                }

                rail += state.IsRail ? 1 : 0;

                lines += state.HasPowerLine ? 1 : 0;
                parks += state.Use == TileUse.Park ? 1 : 0;
                if (state.IsBuildingCenter)
                {
                    coal += state.Use == TileUse.CoalPowerPlant ? 1 : 0;
                    nuclear += state.Use == TileUse.NuclearPowerPlant ? 1 : 0;
                    fire += state.Use == TileUse.FireStation ? 1 : 0;
                    police += state.Use == TileUse.PoliceStation ? 1 : 0;
                    hospitals += state.Use == TileUse.Hospital ? 1 : 0;
                    churches += state.Use == TileUse.Church ? 1 : 0;
                    stadiums += state.Use == TileUse.Stadium ? 1 : 0;
                    seaports += state.Use == TileUse.Seaport ? 1 : 0;
                    airports += state.Use == TileUse.Airport ? 1 : 0;
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

        return new CityCounts(road, bridge, residential, commercial, industrial, developed, coal, nuclear, lines, fire, police, parks, hospitals, churches, rail, stadiums, seaports, airports);
    }

    // The stroke is planned first, for what it would skip and why, then applied as the tool
    // applies it. Both see the same map, so they agree.
    private static ActionReport Make(CitySimulation city, BuildPlan plan, ScenarioAction action, string date, bool listsTiles)
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

        // What the plan would change is what was changed, when the stroke was built.
        IReadOnlyList<TileChange>? changes = null;
        if (listsTiles)
        {
            changes = built
                ? plan.Tiles.Select(tile => new TileChange(tile.X, tile.Y, tile.State.Kind.ToString(), tile.State.Use.ToString())).ToList()
                : [];
        }

        return new ActionReport(
            action.Index,
            action.Tick,
            action.Tool.ToString(),
            result.Status.ToString(),
            built ? null : result.Reason.ToString(),
            result.TilesChanged,
            skipped,
            built ? result.Cost : 0,
            action.Expect?.IsMetBy(result),
            date,
            changes);
    }

    private static CheckReport Check(CitySimulation city, ScenarioCheck check, string date)
    {
        var failure = check.Subject switch
        {
            CheckSubject.Tile => CheckTile(city, check),
            CheckSubject.Funds => CheckFunds(city.Treasury.Funds, check),
            _ => CheckCount(Count(city.Map, city.Definitions).Of(check.CountOf), check),
        };

        return failure is { } found
            ? new CheckReport(check.Index, check.Tick, false, found.Expected, found.Actual, date)
            : new CheckReport(check.Index, check.Tick, true, null, null, date);
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

        if (check.Powered is { } powered)
        {
            if (!state.HasFootprint)
            {
                return ($"{tile} powered is {Lower(powered)}", "the tile has no zone or plant");
            }

            var isPowered = map.ZoneStateAt(check.X, check.Y).Powered;
            if (isPowered != powered)
            {
                return ($"{tile} powered is {Lower(powered)}", Lower(isPowered));
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
