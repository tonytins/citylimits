using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// A scenario being played: its goals, the step they are judged at, and the strokes and disasters
/// still to come, each at the step it is due, counted from January 1900 as the city's steps are.
/// The city runs it at the top of each step (<see cref="CitySimulation.Challenge"/>), so what is
/// due is made before the step, as a scenario run makes it. A scenario's checks are for agents'
/// runs and aren't played. What is overdue, as when a city an older version played on is opened
/// again, is made at once; the goals are judged once, on the end step or the first after it, and
/// the city plays on after, won or lost.
/// </summary>
public sealed class Challenge
{
    private readonly List<(long Step, ScenarioAction Action)> _actions;
    private readonly List<(long Step, Disaster Disaster)> _events;

    private Challenge(string name, string? description, IReadOnlyList<ScenarioGoal> goals, long endStep, List<(long, ScenarioAction)> actions, List<(long, Disaster)> events, bool? won)
    {
        Name = name;
        Description = description;
        Goals = goals;
        EndStep = endStep;
        _actions = actions;
        _events = events;
        Won = won;
    }

    /// <summary>Raised when the goals are judged, with how each stood.</summary>
    public event Action<IReadOnlyList<GoalReport>>? Finished;

    /// <summary>The scenario's name.</summary>
    public string Name { get; }

    /// <summary>The scenario's description, if it has one.</summary>
    public string? Description { get; }

    /// <summary>What the player has to meet by the end.</summary>
    public IReadOnlyList<ScenarioGoal> Goals { get; }

    /// <summary>The step the goals are judged at, counted from January 1900.</summary>
    public long EndStep { get; }

    /// <summary>The date the goals are judged at.</summary>
    public ScenarioDate Ends => ScenarioDate.AtCount(EndStep / CityCalendar.StepsPerCount);

    /// <summary>Whether the challenge was won, once its goals have been judged; null while it is under way.</summary>
    public bool? Won { get; private set; }

    /// <summary>How many strokes and disasters are still to come.</summary>
    public int Pending => _actions.Count + _events.Count;

    /// <summary>The disasters still to come, in order, each with the date it strikes.</summary>
    public IEnumerable<(ScenarioDate Date, Disaster Disaster)> UpcomingEvents =>
        _events.Select(e => (ScenarioDate.AtCount(e.Step / CityCalendar.StepsPerCount), e.Disaster));

    /// <summary>Starts a scenario's challenge on the city it starts with, which is at the scenario's first step.</summary>
    public static Challenge Begin(Scenario scenario, CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(city);

        var start = city.TickCount;
        return new Challenge(
            scenario.Name,
            scenario.Description,
            scenario.Goals,
            start + scenario.Ticks,
            [.. scenario.Actions.OrderBy(a => a.Tick).ThenBy(a => a.Index).Select(a => (start + a.Tick, a))],
            [.. scenario.Strikes().Select(e => (start + e.Tick, e.Disaster))],
            won: null);
    }

    /// <summary>Makes what is due by the city's step, and judges the goals once their step has come.</summary>
    public void BeforeStep(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);

        while (_actions.Count > 0 && _actions[0].Step <= city.TickCount)
        {
            var action = _actions[0].Action;
            _actions.RemoveAt(0);
            var stroke = new BuildStroke(action.Tool, action.FromX, action.FromY);
            stroke.DragTo(action.ToX, action.ToY);
            city.Builder.Apply(stroke);
        }

        while (_events.Count > 0 && _events[0].Step <= city.TickCount)
        {
            var disaster = _events[0].Disaster;
            _events.RemoveAt(0);
            city.Strike(disaster);
        }

        if (Won is null && city.TickCount >= EndStep)
        {
            var goals = Goals.Select(goal => ScenarioRunner.Judge(city, goal)).ToList();
            Won = goals.All(goal => goal.Met);
            Finished?.Invoke(goals);
        }
    }

    internal ChallengeSave Capture() => new()
    {
        Name = Name,
        Description = Description,
        EndStep = EndStep,
        Won = Won,
        Goals = [.. Goals.Select(goal => new GoalSave { Measure = goal.Measure.ToString(), AtLeast = goal.AtLeast, AtMost = goal.AtMost, Text = goal.Text })],
        Actions = [.. _actions.Select(a => new ChallengeActionSave
        {
            Step = a.Step,
            Tool = a.Action.Tool.ToString(),
            FromX = a.Action.FromX,
            FromY = a.Action.FromY,
            ToX = a.Action.ToX,
            ToY = a.Action.ToY,
        })],
        Events = [.. _events.Select(e => new ChallengeEventSave { Step = e.Step, Disaster = e.Disaster.ToString() })],
    };

    /// <exception cref="InvalidSave">Something in the part is wrong; the message says what and where.</exception>
    internal static Challenge Restore(ChallengeSave save)
    {
        var name = save.Name?.Trim();
        if (name is not { Length: > 0 and <= 80 })
        {
            throw new InvalidSave("challenge.name: needs 1 to 80 characters.");
        }

        var endStep = SaveChecks.InRange(save.EndStep, 0, long.MaxValue, "challenge.endStep");
        var goals = new List<ScenarioGoal>();
        foreach (var (goal, i) in (save.Goals ?? []).Select((g, i) => (g, i)))
        {
            var at = $"challenge.goals[{i}]";
            if (!Enum.TryParse<GoalMeasure>(goal.Measure, ignoreCase: false, out var measure) || !Enum.IsDefined(measure))
            {
                throw new InvalidSave($"{at}.measure: '{goal.Measure}' is not a measure.");
            }

            if (goal.Text is not { Length: > 0 and <= ScenarioLoader.MaxGoalTextLength } || (goal.AtLeast is null && goal.AtMost is null))
            {
                throw new InvalidSave($"{at}: needs words of 1 to {ScenarioLoader.MaxGoalTextLength} characters, and atLeast, atMost, or both.");
            }

            goals.Add(new ScenarioGoal(i, measure, goal.AtLeast, goal.AtMost, goal.Text));
        }

        var actions = new List<(long, ScenarioAction)>();
        foreach (var (action, i) in (save.Actions ?? []).Select((a, i) => (a, i)))
        {
            if (!Enum.TryParse<BuildAction>(action.Tool, ignoreCase: false, out var tool) || !Enum.IsDefined(tool))
            {
                throw new InvalidSave($"challenge.actions[{i}].tool: '{action.Tool}' is not a tool.");
            }

            var step = SaveChecks.InRange(action.Step, 0, long.MaxValue, $"challenge.actions[{i}].step");
            var (fromX, fromY, toX, toY) = (
                SaveChecks.InRange(action.FromX, ScenarioLoader.MinCoordinate, ScenarioLoader.MaxCoordinate, $"challenge.actions[{i}].fromX"),
                SaveChecks.InRange(action.FromY, ScenarioLoader.MinCoordinate, ScenarioLoader.MaxCoordinate, $"challenge.actions[{i}].fromY"),
                SaveChecks.InRange(action.ToX, ScenarioLoader.MinCoordinate, ScenarioLoader.MaxCoordinate, $"challenge.actions[{i}].toX"),
                SaveChecks.InRange(action.ToY, ScenarioLoader.MinCoordinate, ScenarioLoader.MaxCoordinate, $"challenge.actions[{i}].toY"));
            actions.Add((step, new ScenarioAction(i, 0, tool, fromX, fromY, toX, toY, null)));
        }

        var events = new List<(long, Disaster)>();
        foreach (var (happening, i) in (save.Events ?? []).Select((e, i) => (e, i)))
        {
            if (!Enum.TryParse<Disaster>(happening.Disaster, ignoreCase: false, out var disaster) || !Enum.IsDefined(disaster))
            {
                throw new InvalidSave($"challenge.events[{i}].disaster: '{happening.Disaster}' is not a disaster.");
            }

            events.Add((SaveChecks.InRange(happening.Step, 0, long.MaxValue, $"challenge.events[{i}].step"), disaster));
        }

        var description = save.Description?.Trim();
        return new Challenge(name, string.IsNullOrEmpty(description) ? null : description, goals, endStep, [.. actions.OrderBy(a => a.Item1)], [.. events.OrderBy(e => e.Item1)], save.Won);
    }
}
