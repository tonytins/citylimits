namespace CityLimits.Core.Scenarios;

/// <summary>
/// A scenario after it has been read and checked: a city set up from a seed, the tool strokes
/// made on it at set ticks, and what must be true along the way. <see cref="ScenarioLoader"/>
/// is the only way to make one, so every scenario has passed the same validation.
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
    IReadOnlyList<ScenarioCheck> Checks)
{
    /// <summary>The longest a scenario may run, the same limit as <c>--ticks</c>: about 130 years.</summary>
    public const int MaxTicks = 100_000;
}

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
