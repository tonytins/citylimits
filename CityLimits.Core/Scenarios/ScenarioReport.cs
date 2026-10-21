using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>Whether every check passed and every action met what it expected.</summary>
    public required bool Passed { get; init; }

    /// <summary>The scenario that was asked for and why it couldn't run, when the demo town ran instead.</summary>
    public ScenarioFallback? Fallback { get; init; }

    /// <summary>The city when the run ended.</summary>
    public required ScenarioFinal Final { get; init; }

    /// <summary>Each action, in the order it was made.</summary>
    public required IReadOnlyList<ActionReport> Actions { get; init; }

    /// <summary>Each check, in the order it was made.</summary>
    public required IReadOnlyList<CheckReport> Checks { get; init; }
}

/// <summary>Which scenario ran: its name, where it came from (an id or a path), its seed, and how many ticks it ran.</summary>
public sealed record ScenarioSummary(string Name, string Source, int Seed, int Ticks);

/// <summary>The scenario that was asked for, and why it couldn't be loaded.</summary>
public sealed record ScenarioFallback(string Requested, string Reason);

/// <summary>The city when a run ended.</summary>
/// <param name="Tick">The ticks that ran.</param>
/// <param name="CityTime">The calendar's count: 48ths of a year since the city started.</param>
/// <param name="Date">The month, as YYYY-MM.</param>
/// <param name="Funds">The city's money.</param>
/// <param name="Counts">What stood on the map.</param>
public sealed record ScenarioFinal(long Tick, long CityTime, string Date, int Funds, CityCounts Counts);

/// <summary>What stood on the map: road tiles, bridges among them, zones of each kind, developed zones, and power plants of each kind.</summary>
public sealed record CityCounts(int Road, int Bridge, int Residential, int Commercial, int Industrial, int Developed, int CoalPowerPlant = 0, int NuclearPowerPlant = 0)
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
public sealed record ActionReport(
    int Index,
    int Tick,
    string Tool,
    string Status,
    string? Reason,
    int Tiles,
    IReadOnlyDictionary<string, int> Skipped,
    int Cost,
    bool? Met);

/// <summary>What came of one check: whether it passed, and when it didn't, what was expected and what was found.</summary>
public sealed record CheckReport(int Index, int Tick, bool Passed, string? Expected, string? Actual);

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
    };

    /// <summary>The report as indented JSON, with the fields that are absent left out.</summary>
    public static string Serialize(ScenarioReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, Options);
    }
}
