using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// What a scenario's editor changes about it, apart from its map: its name and description, its
/// start year and its starting money (for a scenario made from a seed; a saved city brings its
/// own), its end, its goals, and its disasters. Its strokes and checks are kept as they are.
/// </summary>
/// <param name="Name">The scenario's name.</param>
/// <param name="Description">What it is about, as the briefing tells it; null for none.</param>
/// <param name="StartYear">The year its city starts in.</param>
/// <param name="Ends">The month its goals are judged in.</param>
/// <param name="Funds">The money its city starts with.</param>
/// <param name="Goals">What the player has to meet by the end.</param>
/// <param name="Events">The disasters that strike, and when.</param>
public sealed record ScenarioDetails(
    string Name,
    string? Description,
    int StartYear,
    ScenarioDate Ends,
    int Funds,
    IReadOnlyList<GoalDetails> Goals,
    IReadOnlyList<EventDetails> Events)
{
    /// <summary>The details of a scenario as it stands.</summary>
    public static ScenarioDetails Of(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new ScenarioDetails(
            scenario.Name,
            scenario.Description,
            scenario.CityFile is null ? scenario.StartYear : scenario.DateAt(0).Year,
            scenario.DateAt(scenario.Ticks),
            scenario.Funds,
            [.. scenario.Goals.Select(goal => new GoalDetails(
                goal.Measure,
                goal.AtLeast,
                goal.AtMost,
                goal.Text == ScenarioLoader.GoalText(goal.Measure, goal.AtLeast, goal.AtMost) ? null : goal.Text))],
            [.. scenario.Events.Select(e => new EventDetails(scenario.DateAt(e.Tick), e.Disaster, e.Repeat))]);
    }
}

/// <summary>A goal as the editor holds it.</summary>
/// <param name="Measure">What it measures.</param>
/// <param name="AtLeast">The least the measure may be, or null.</param>
/// <param name="AtMost">The most the measure may be, or null.</param>
/// <param name="Text">Its own words, or null for the ones its measure gives it.</param>
public sealed record GoalDetails(GoalMeasure Measure, int? AtLeast, int? AtMost, string? Text);

/// <summary>A disaster as the editor holds it.</summary>
/// <param name="At">When it first strikes.</param>
/// <param name="Disaster">What strikes.</param>
/// <param name="Repeat">How it repeats, or null to strike once.</param>
public sealed record EventDetails(ScenarioDate At, Disaster Disaster, EventRepeat? Repeat);
