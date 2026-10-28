using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// What came of a scenario run, in the report's format 1. It is a stable interface that agents
/// and scripts read: a field may be added within a format, but renaming, removing, or changing
/// the meaning of one means a new format. Names of statuses, reasons, and tools are written as
/// their C# names.
/// </summary>
public sealed record ScenarioReport
{
    /// <summary>The report's format.</summary>
    public const int CurrentFormat = 1;

    /// <summary>The report's format.</summary>
    public int Format { get; init; } = CurrentFormat;

    /// <summary>Which scenario ran.</summary>
    public required ScenarioSummary Scenario { get; init; }

    /// <summary>Whether every check passed and every action met what it expected. A scenario's goals don't count toward it.</summary>
    public required bool Passed { get; init; }

    /// <summary>Whether every goal was met at the end; absent for a scenario with no goals.</summary>
    public bool? Won { get; init; }

    /// <summary>The city when the run ended.</summary>
    public required ScenarioFinal Final { get; init; }

    /// <summary>Each action, in the order it was made.</summary>
    public required IReadOnlyList<ActionReport> Actions { get; init; }

    /// <summary>Each of the scenario's disasters, in the order it struck.</summary>
    public IReadOnlyList<EventReport> Events { get; init; } = [];

    /// <summary>Each check, in the order it was made.</summary>
    public required IReadOnlyList<CheckReport> Checks { get; init; }

    /// <summary>Each goal, as it was judged at the end.</summary>
    public IReadOnlyList<GoalReport> Goals { get; init; } = [];
}

/// <summary>Which scenario ran: its name, where it came from (an id or a path), its seed, how many ticks it ran, and the dates it started and ended at.</summary>
public sealed record ScenarioSummary(string Name, string Source, int Seed, int Ticks, string? Start = null, string? Ends = null);

/// <summary>The city when a run ended.</summary>
/// <param name="Tick">The ticks that ran.</param>
/// <param name="CityTime">The calendar's count: 48ths of a year since the city started.</param>
/// <param name="Date">The month, as YYYY-MM.</param>
/// <param name="Funds">The city's money.</param>
/// <param name="Counts">What stood on the map.</param>
public sealed record ScenarioFinal(long Tick, long CityTime, string Date, int Funds, CityCounts Counts);

/// <summary>What stood on the map: road tiles, bridges among them, zones of each kind, developed zones, power plants of each kind, power line tiles, stations of each kind, park tiles, hospitals, churches, rail tiles, stadiums, seaports, and airports.</summary>
public sealed record CityCounts(int Road, int Bridge, int Residential, int Commercial, int Industrial, int Developed, int CoalPowerPlant = 0, int NuclearPowerPlant = 0, int PowerLine = 0, int FireStation = 0, int PoliceStation = 0, int Park = 0, int Hospital = 0, int Church = 0, int Rail = 0, int Stadium = 0, int Seaport = 0, int Airport = 0)
{
    /// <summary>One of the counts, as a count check names it.</summary>
    public int Of(CountSubject subject) => subject switch
    {
        CountSubject.Road => Road,
        CountSubject.Bridge => Bridge,
        CountSubject.Residential => Residential,
        CountSubject.Commercial => Commercial,
        CountSubject.Industrial => Industrial,
        CountSubject.CoalPowerPlant => CoalPowerPlant,
        CountSubject.NuclearPowerPlant => NuclearPowerPlant,
        CountSubject.PowerLine => PowerLine,
        CountSubject.FireStation => FireStation,
        CountSubject.PoliceStation => PoliceStation,
        CountSubject.Park => Park,
        CountSubject.Hospital => Hospital,
        CountSubject.Church => Church,
        CountSubject.Rail => Rail,
        CountSubject.Stadium => Stadium,
        CountSubject.Seaport => Seaport,
        CountSubject.Airport => Airport,
        _ => Developed,
    };
}

/// <summary>What came of one action.</summary>
/// <param name="Index">Where the action is in the file, from 0.</param>
/// <param name="Tick">The tick it was made at.</param>
/// <param name="Tool">The tool, as a <see cref="BuildAction"/> name.</param>
/// <param name="Status">The result, as a <see cref="BuildStatus"/> name.</param>
/// <param name="Reason">Why it wasn't built, as a <see cref="BuildRefusal"/> name; absent when it was.</param>
/// <param name="Tiles">How many tiles it changed.</param>
/// <param name="Skipped">How many tiles it skipped for each reason, or zones for a zone stroke; only the reasons that skipped some.</param>
/// <param name="Cost">The money it spent; nothing when it wasn't built.</param>
/// <param name="Met">Whether it came to what it expected; absent when it expected nothing.</param>
/// <param name="Date">The month it was made in, as YYYY-MM.</param>
/// <param name="Changes">Each tile it changed, in the order it changed them, when the run was asked for them (<c>--verbose</c>); absent otherwise.</param>
public sealed record ActionReport(
    int Index,
    int Tick,
    string Tool,
    string Status,
    string? Reason,
    int Tiles,
    IReadOnlyDictionary<string, int> Skipped,
    int Cost,
    bool? Met,
    string? Date = null,
    IReadOnlyList<TileChange>? Changes = null);

/// <summary>A tile an action changed: where it is, and the ground and use it was left with, as <see cref="TileKind"/> and <see cref="TileUse"/> names.</summary>
public sealed record TileChange(int X, int Y, string Ground, string Use);

/// <summary>What came of one check: whether it passed, and when it didn't, what was expected and what was found; and when it was made.</summary>
public sealed record CheckReport(int Index, int Tick, bool Passed, string? Expected, string? Actual, string? Date = null);

/// <summary>What came of one of a scenario's disasters: when it struck, what it was, and whether it found anywhere to strike.</summary>
public sealed record EventReport(int Index, int Tick, string Date, string Disaster, bool Happened);

/// <summary>How a goal stood at the end: its words, whether it was met, and what the city measured.</summary>
public sealed record GoalReport(int Index, string Text, bool Met, int Found);

/// <summary>Writes a report as JSON.</summary>
public static class ScenarioReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,

        // A dollar sign or a quote in a message stays readable rather than escaped.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = ScenarioReportJsonContext.Default,
    };

    /// <summary>The report as indented JSON, with the fields that are absent left out.</summary>
    public static string Serialize(ScenarioReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, (JsonTypeInfo<ScenarioReport>)Options.GetTypeInfo(typeof(ScenarioReport)));
    }
}

// The report's code is generated at build time, so writing it needs no reflection and survives trimming.
[JsonSerializable(typeof(ScenarioReport))]
internal sealed partial class ScenarioReportJsonContext : JsonSerializerContext;
