using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// Writes a scenario as a file in the newest format, 2.2, which <see cref="ScenarioLoader"/> reads
/// back as the same scenario: its times as the game's dates, a goal's words only when they aren't
/// the ones it would be given, and a saved city named from the scenario's own folder. A scenario
/// whose times can't be dates, as a format 1 file's ticks may not be, isn't written.
/// </summary>
public static class ScenarioWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        TypeInfoResolver = ScenarioJson.Default,
    };

    /// <summary>
    /// Writes a scenario as the text of a 2.2 file. A saved city is named relative to the folder the
    /// file goes in, as the loader finds it.
    /// </summary>
    /// <exception cref="InvalidDataException">One of the scenario's times falls between the calendar's weeks.</exception>
    public static string Write(Scenario scenario, string folder)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(folder);

        var fromSeed = scenario.CityFile is null;
        var document = new ScenarioDocument2
        {
            Schema = $"https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/scenario.v{ScenarioLoader.Format.Major}.{ScenarioLoader.Format.Minor}.schema.json",
            Format = ScenarioLoader.Format,
            Name = scenario.Name,
            Description = scenario.Description,
            Seed = fromSeed ? scenario.Seed : null,
            Map = fromSeed ? new MapDocument { Width = scenario.MapWidth, Height = scenario.MapHeight } : null,
            Funds = fromSeed ? scenario.Funds : null,
            Start = fromSeed ? scenario.StartYear : null,
            City = fromSeed ? null : Path.GetRelativePath(folder, scenario.CityFile!).Replace('\\', '/'),
            Ends = Month(scenario, scenario.Ticks, "ends"),
            Disasters = scenario.Disasters ? true : null,
            Actions = scenario.Actions.Count == 0 ? null : [.. scenario.Actions.Select(a => Action(scenario, a))],
            Checks = scenario.Checks.Count == 0 ? null : [.. scenario.Checks.Select(c => Check(scenario, c))],
            Goals = scenario.Goals.Count == 0 ? null : [.. scenario.Goals.Select(Goal)],
            Events = scenario.Events.Count == 0 ? null : [.. scenario.Events.Select(e => Event(scenario, e))],
        };

        return JsonSerializer.Serialize(document, (JsonTypeInfo<ScenarioDocument2>)Options.GetTypeInfo(typeof(ScenarioDocument2)));
    }

    private static ActionDocument2? Action(Scenario scenario, ScenarioAction action)
    {
        var (at, week) = DateOf(scenario, action.Tick, $"actions[{action.Index}]");
        var moves = action.ToX != action.FromX || action.ToY != action.FromY;
        return new ActionDocument2
        {
            At = at,
            Week = week,
            Tool = action.Tool.ToString(),
            From = new PointDocument { X = action.FromX, Y = action.FromY },
            To = moves ? new PointDocument { X = action.ToX, Y = action.ToY } : null,
            Expect = action.Expect?.ToString(),
        };
    }

    private static CheckDocument2? Check(Scenario scenario, ScenarioCheck check)
    {
        var (at, week) = DateOf(scenario, check.Tick, $"checks[{check.Index}]");
        var document = new CheckDocument2 { At = at, Week = week };
        switch (check.Subject)
        {
            case CheckSubject.Tile:
                document.Tile = new PointDocument { X = check.X, Y = check.Y };
                document.Kind = check.Kind?.ToString();
                document.Use = check.Use?.ToString();
                document.Development = check.Development;
                document.DevelopmentAtLeast = check.DevelopmentAtLeast;
                document.DevelopmentAtMost = check.DevelopmentAtMost;
                document.Served = check.Served;
                document.Powered = check.Powered;
                break;
            case CheckSubject.Count:
                document.Count = check.CountOf.ToString();
                document.EqualTo = check.CountEquals;
                document.AtLeast = check.CountAtLeast;
                document.AtMost = check.CountAtMost;
                break;
            default:
                document.FundsAtLeast = check.FundsAtLeast;
                document.FundsAtMost = check.FundsAtMost;
                document.FundsEquals = check.FundsEquals;
                break;
        }

        return document;
    }

    // A goal's words are written only when they aren't the ones the loader would give it.
    private static GoalDocument? Goal(ScenarioGoal goal) => new()
    {
        Measure = goal.Measure.ToString(),
        AtLeast = goal.AtLeast,
        AtMost = goal.AtMost,
        Text = goal.Text == ScenarioLoader.GoalText(goal.Measure, goal.AtLeast, goal.AtMost) ? null : goal.Text,
    };

    private static EventDocument? Event(Scenario scenario, ScenarioEvent happening)
    {
        var (at, week) = DateOf(scenario, happening.Tick, $"events[{happening.Index}]");
        return new EventDocument
        {
            At = at,
            Week = week,
            Disaster = happening.Disaster.ToString(),
            Repeat = happening.Repeat is { } repeat ? new RepeatDocument { Weeks = repeat.Weeks, Times = repeat.Times } : null,
        };
    }

    // A time as the file names it: a month, and its week when that isn't the first. A time between
    // the calendar's weeks, as a format 1 file's ticks may be, can't be written as a date.
    private static (string At, int? Week) DateOf(Scenario scenario, int tick, string where)
    {
        var date = scenario.DateAt(tick);
        if (date.Step - scenario.StartStep != tick)
        {
            throw new InvalidDataException($"{where}: tick {tick} falls between the calendar's weeks, so it can't be written as a date.");
        }

        return ($"{date.Year:0000}-{date.Month:00}", date.Week == 1 ? null : date.Week);
    }

    // The end is a month, as the file writes it.
    private static string Month(Scenario scenario, int tick, string where)
    {
        var (at, week) = DateOf(scenario, tick, where);
        if (week is not null)
        {
            throw new InvalidDataException($"{where}: tick {tick} isn't the start of a month, so it can't be written as one.");
        }

        return at;
    }
}
