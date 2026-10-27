using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// A scenario after it has been read and checked: a city set up from a seed or a saved city, the
/// tool strokes made on it, the disasters that strike it, what must be true along the way, and
/// the goals a player has to meet by its end. A file names its times as the game's dates (format
/// 2.0) or as ticks (format 1); here every time is a tick, counted from the step the scenario
/// starts at. <see cref="ScenarioLoader"/> is the only way to make one, so every scenario has
/// passed the same validation. Disasters happen on their own only when it asks for them, so a
/// scenario's result doesn't depend on the player's settings.
/// </summary>
public sealed record Scenario(
    string Name,
    string? Description,
    int Seed,
    int MapWidth,
    int MapHeight,
    int Funds,
    int Ticks,
    IReadOnlyList<ScenarioAction> Actions,
    IReadOnlyList<ScenarioCheck> Checks,
    bool Disasters = false)
{
    /// <summary>The longest a scenario may run, the same limit as <c>--ticks</c>: about 130 years.</summary>
    public const int MaxTicks = 100_000;

    /// <summary>The year the city's calendar starts in, for a city made from a seed.</summary>
    public int StartYear { get; init; } = CityCalendar.StartYear;

    /// <summary>The saved city the scenario starts from, as a full path, or null for one made from the seed.</summary>
    public string? CityFile { get; init; }

    /// <summary>The step the scenario starts at, counted from January 1900: its start year's, or its city's own.</summary>
    public long StartStep { get; init; }

    /// <summary>What the player has to meet by the end, judged on the last tick; none for a scenario that only checks.</summary>
    public IReadOnlyList<ScenarioGoal> Goals { get; init; } = [];

    /// <summary>The disasters that strike, whatever the player's settings, in file order.</summary>
    public IReadOnlyList<ScenarioEvent> Events { get; init; } = [];

    /// <summary>Every strike of every event, repeats spelled out, in order of when, then of the file.</summary>
    public IReadOnlyList<ScenarioEvent> Strikes() => [.. Events.SelectMany(e => e.Strikes()).OrderBy(e => e.Tick).ThenBy(e => e.Index)];

    /// <summary>Tells whether the scenario is a challenge for a player, with goals to meet, rather than only checks.</summary>
    public bool HasGoals => Goals.Count > 0;

    /// <summary>The date on the calendar a tick of the scenario falls in.</summary>
    public ScenarioDate DateAt(long tick) => ScenarioDate.AtCount((StartStep + tick) / CityCalendar.StepsPerCount);
}

/// <summary>What a goal measures about the city at the end.</summary>
public enum GoalMeasure
{
    /// <summary>The city's population, as the bar shows it.</summary>
    Population,

    /// <summary>The city's money.</summary>
    Funds,

    /// <summary>The city's average pollution, over the blocks that have any.</summary>
    Pollution,

    /// <summary>The city's average crime, over the blocks with land value.</summary>
    Crime,

    /// <summary>The city's traffic average, as the year's evaluation works it out.</summary>
    Traffic,
}

/// <summary>Something the player has to meet by the end of a scenario: a measure of the city, at least or at most a number.</summary>
/// <param name="Index">Where the goal is in the file, from 0.</param>
/// <param name="Measure">What it measures.</param>
/// <param name="AtLeast">The least the measure may be, or null.</param>
/// <param name="AtMost">The most the measure may be, or null.</param>
/// <param name="Text">How the goal reads to a player.</param>
public sealed record ScenarioGoal(int Index, GoalMeasure Measure, int? AtLeast, int? AtMost, string Text);

/// <summary>
/// A disaster that strikes during a scenario, at a random place, as Micropolis' scenarios' do; once,
/// or again and again, as Micropolis' Hamburg is fire-bombed.
/// </summary>
/// <param name="Index">Where the event is in the file, from 0.</param>
/// <param name="Tick">It first strikes after this many ticks, after that tick's actions and before its checks.</param>
/// <param name="Disaster">What strikes.</param>
/// <param name="Repeat">How often it strikes again, and how many times in all; none strikes once.</param>
public sealed record ScenarioEvent(int Index, int Tick, Disaster Disaster, EventRepeat? Repeat = null)
{
    /// <summary>Each strike, in order: the first, then one every so many weeks, as many times as it repeats.</summary>
    public IEnumerable<ScenarioEvent> Strikes()
    {
        var times = Repeat?.Times ?? 1;
        var every = (Repeat?.Weeks ?? 0) * CityCalendar.StepsPerCount;
        for (var i = 0; i < times; i++)
        {
            yield return new ScenarioEvent(Index, Tick + (i * every), Disaster);
        }
    }
}

/// <summary>How a scenario's disaster repeats.</summary>
/// <param name="Weeks">The weeks, quarter-months of the calendar, from one strike to the next.</param>
/// <param name="Times">How many times it strikes in all, the first included.</param>
public sealed record EventRepeat(int Weeks, int Times);

/// <summary>A tool stroke a scenario makes: the tool pressed on one tile and dragged to another, after a number of ticks.</summary>
/// <param name="Index">Where the action is in the file, from 0.</param>
/// <param name="Tick">The stroke is made after this many ticks, before the next one.</param>
/// <param name="Tool">The work the stroke does.</param>
/// <param name="FromX">The column the stroke starts on.</param>
/// <param name="FromY">The row the stroke starts on.</param>
/// <param name="ToX">The column the stroke is dragged to.</param>
/// <param name="ToY">The row the stroke is dragged to.</param>
/// <param name="Expect">What the stroke must come to, or null when the scenario doesn't say.</param>
public sealed record ScenarioAction(int Index, int Tick, BuildAction Tool, int FromX, int FromY, int ToX, int ToY, ExpectedResult? Expect);

/// <summary>What a stroke is expected to come to: a status, and the reason when the scenario names one.</summary>
public readonly record struct ExpectedResult(BuildStatus Status, BuildRefusal? Reason)
{
    /// <summary>Tells whether a stroke's result is what was expected.</summary>
    public bool IsMetBy(BuildResult result) =>
        result.Status == Status && (Reason is null || result.Reason == Reason);

    /// <summary>The expectation as the file writes it, such as <c>Refused:NotEnoughFunds</c>.</summary>
    public override string ToString() => Reason is null ? $"{Status}" : $"{Status}:{Reason}";
}

/// <summary>What a check looks at.</summary>
public enum CheckSubject
{
    /// <summary>One tile, and the zone it belongs to.</summary>
    Tile,

    /// <summary>The city's money.</summary>
    Funds,

    /// <summary>A count over the whole map.</summary>
    Count,
}

/// <summary>What a count check counts.</summary>
public enum CountSubject
{
    /// <summary>Road tiles, bridges included.</summary>
    Road,

    /// <summary>Road tiles over water.</summary>
    Bridge,

    /// <summary>Residential zones, by their centres.</summary>
    Residential,

    /// <summary>Commercial zones, by their centres.</summary>
    Commercial,

    /// <summary>Industrial zones, by their centres.</summary>
    Industrial,

    /// <summary>Zones with a development level above zero.</summary>
    Developed,

    /// <summary>Coal power plants, by their centres.</summary>
    CoalPowerPlant,

    /// <summary>Nuclear power plants, by their centres.</summary>
    NuclearPowerPlant,

    /// <summary>Tiles with a power line, under water or under a road included.</summary>
    PowerLine,

    /// <summary>Fire stations, by their centres.</summary>
    FireStation,

    /// <summary>Police stations, by their centres.</summary>
    PoliceStation,

    /// <summary>Park tiles.</summary>
    Park,

    /// <summary>Hospitals, by their centres.</summary>
    Hospital,

    /// <summary>Churches, by their centres.</summary>
    Church,

    /// <summary>Rail tiles, tunnels and crossings included; from scenario format 1.1.</summary>
    Rail,

    /// <summary>Stadiums, by their centres; from scenario format 2.1.</summary>
    Stadium,

    /// <summary>Seaports, by their centres; from scenario format 2.1.</summary>
    Seaport,

    /// <summary>Airports, by their centres; from scenario format 2.1.</summary>
    Airport,
}

/// <summary>
/// Something that must be true after a number of ticks. It looks at one subject, and every
/// condition it sets must hold; a condition left null isn't checked.
/// </summary>
public sealed record ScenarioCheck
{
    /// <summary>Where the check is in the file, from 0.</summary>
    public required int Index { get; init; }

    /// <summary>The check is made after this many ticks, after that tick's actions.</summary>
    public required int Tick { get; init; }

    /// <summary>What the check looks at.</summary>
    public required CheckSubject Subject { get; init; }

    /// <summary>The tile's column, for a tile check.</summary>
    public int X { get; init; }

    /// <summary>The tile's row, for a tile check.</summary>
    public int Y { get; init; }

    /// <summary>What a count check counts.</summary>
    public CountSubject CountOf { get; init; }

    /// <summary>The ground the tile must be.</summary>
    public TileKind? Kind { get; init; }

    /// <summary>What the tile must carry.</summary>
    public TileUse? Use { get; init; }

    /// <summary>The development the tile's zone must have, exactly.</summary>
    public int? Development { get; init; }

    /// <summary>The least development the tile's zone may have.</summary>
    public int? DevelopmentAtLeast { get; init; }

    /// <summary>The most development the tile's zone may have.</summary>
    public int? DevelopmentAtMost { get; init; }

    /// <summary>Whether the tile's zone must be served by a road, or must not be.</summary>
    public bool? Served { get; init; }

    /// <summary>Whether the tile's zone or plant must have had power when the scan last reached it, or must not.</summary>
    public bool? Powered { get; init; }

    /// <summary>The least money the city may have.</summary>
    public int? FundsAtLeast { get; init; }

    /// <summary>The most money the city may have.</summary>
    public int? FundsAtMost { get; init; }

    /// <summary>The money the city must have, exactly.</summary>
    public int? FundsEquals { get; init; }

    /// <summary>What the count must be, exactly.</summary>
    public int? CountEquals { get; init; }

    /// <summary>The least the count may be.</summary>
    public int? CountAtLeast { get; init; }

    /// <summary>The most the count may be.</summary>
    public int? CountAtMost { get; init; }
}
