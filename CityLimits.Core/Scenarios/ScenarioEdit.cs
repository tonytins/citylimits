using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// A scenario being edited in the game, as SimCity began: a map editor turned into a game. The
/// scenario's city is played in the ordinary game with building free and the clock stopped, and the
/// scenario's goals, strokes, and disasters are kept aside as a draft that nothing acts on. Playing
/// tests it: the city is set aside as a checkpoint, and the scenario runs on it as a player's
/// challenge would. Going back puts the city as it was at the checkpoint, undoing the test and
/// everything built during it; starting over opens the scenario's city afresh. Its details are
/// changed as a whole: written out, with the changes, and read back as any file is, so the editor
/// can't hold what a file couldn't.
/// </summary>
public sealed class ScenarioEdit
{
    // The city as it was when the test began, as a flat save; none while the scenario isn't being tested.
    private string? _checkpoint;

    /// <summary>Edits a scenario, read from a file.</summary>
    public ScenarioEdit(Scenario scenario, string path)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Scenario = scenario;
        Path = path;
    }

    /// <summary>The scenario being edited, as its details were last changed.</summary>
    public Scenario Scenario { get; private set; }

    /// <summary>The file the scenario was read from.</summary>
    public string Path { get; }

    /// <summary>Tells whether the scenario is being tested, so there is a checkpoint to go back to.</summary>
    public bool IsTesting => _checkpoint is not null;

    /// <summary>
    /// Opens the scenario's city afresh, with building free and no challenge on it; a test under way
    /// is dropped. Call it to start editing, and again to start over.
    /// </summary>
    /// <exception cref="InvalidDataException">The scenario's saved city can't be opened any more.</exception>
    public CitySimulation OpenCity()
    {
        _checkpoint = null;
        var city = ChallengeShelf.CityFor(Scenario);
        city.Builder.BuildsForFree = true;
        return city;
    }

    /// <summary>
    /// Works out the scenario with its details changed, checked as a file would be, without taking
    /// it yet; <paramref name="problem"/> says what is wrong when it can't be.
    /// </summary>
    public bool TryChange(ScenarioDetails details, [NotNullWhen(true)] out Scenario? changed, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(details);
        changed = null;
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)) ?? string.Empty;
        JsonObject file;
        try
        {
            file = JsonNode.Parse(ScenarioWriter.Write(Scenario, folder))!.AsObject();
        }
        catch (InvalidDataException ex)
        {
            problem = ex.Message;
            return false;
        }

        file["name"] = details.Name;
        file.Remove("description");
        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            file["description"] = details.Description;
        }

        if (Scenario.CityFile is null)
        {
            file["start"] = details.StartYear;
            file["funds"] = details.Funds;

            // The editor doesn't show the scenario's own strokes and checks, so they move with its
            // start, as its city does, keeping their place in its story; what it shows is taken as
            // it is given.
            Shift(file["actions"] as JsonArray, details.StartYear - Scenario.StartYear);
            Shift(file["checks"] as JsonArray, details.StartYear - Scenario.StartYear);
        }

        file["ends"] = $"{details.Ends.Year:0000}-{details.Ends.Month:00}";
        file.Remove("goals");
        if (details.Goals.Count > 0)
        {
            file["goals"] = new JsonArray([.. details.Goals.Select(Goal)]);
        }

        file.Remove("events");
        if (details.Events.Count > 0)
        {
            file["events"] = new JsonArray([.. details.Events.Select(Event)]);
        }

        if (!ScenarioLoader.TryParse(file.ToJsonString(), folder, out changed, out var error))
        {
            problem = error;
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>
    /// Takes a changed scenario as the one being edited, and keeps the city in step with it: one made
    /// from a seed starts with the scenario's money, and, when its start year moved, moves its own
    /// founding there, everything built on it kept. The map doesn't depend on the year, so nothing is
    /// grown again.
    /// </summary>
    /// <exception cref="InvalidOperationException">The scenario is being tested, or the city has taken a step since its founding.</exception>
    public void Accept(Scenario changed, CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(city);
        if (IsTesting)
        {
            throw new InvalidOperationException("A scenario's details can't change while it is being tested.");
        }

        if (changed.CityFile is null)
        {
            if (changed.StartYear != city.FoundedYear)
            {
                city.MoveFounding(changed.StartYear);
            }

            city.Treasury.Restore(changed.Funds);
        }

        Scenario = changed;
    }

    // Moves the months of a list's entries by a number of years, keeping their weeks.
    private static void Shift(JsonArray? entries, int years)
    {
        if (entries is null || years == 0)
        {
            return;
        }

        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (entry["at"]?.GetValue<string>() is { } at && ScenarioDate.TryParse(at, 1, out var date))
            {
                entry["at"] = $"{date.Value.Year + years:0000}-{date.Value.Month:00}";
            }
        }
    }

    private static JsonObject Goal(GoalDetails goal)
    {
        var node = new JsonObject { ["measure"] = goal.Measure.ToString() };
        if (goal.AtLeast is { } least)
        {
            node["atLeast"] = least;
        }

        if (goal.AtMost is { } most)
        {
            node["atMost"] = most;
        }

        if (!string.IsNullOrWhiteSpace(goal.Text))
        {
            node["text"] = goal.Text;
        }

        return node;
    }

    private static JsonObject Event(EventDetails happening)
    {
        var node = new JsonObject
        {
            ["at"] = $"{happening.At.Year:0000}-{happening.At.Month:00}",
            ["disaster"] = happening.Disaster.ToString(),
        };
        if (happening.At.Week != 1)
        {
            node["week"] = happening.At.Week;
        }

        if (happening.Repeat is { } repeat)
        {
            node["repeat"] = new JsonObject { ["weeks"] = repeat.Weeks, ["times"] = repeat.Times };
        }

        return node;
    }

    /// <summary>
    /// Begins a test of the scenario on the city as it stands: the city is kept as the checkpoint,
    /// and the scenario's challenge is put on it, its strokes and disasters counted from now.
    /// </summary>
    /// <exception cref="InvalidOperationException">A test is already under way.</exception>
    public void BeginTest(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (IsTesting)
        {
            throw new InvalidOperationException("The scenario is already being tested.");
        }

        _checkpoint = FlatCityFormat.Write(city);
        city.Challenge = Challenge.Begin(Scenario, city);
    }

    /// <summary>
    /// Ends the test: the city as it was at the checkpoint, with building free and no challenge on it.
    /// </summary>
    /// <exception cref="InvalidOperationException">No test is under way, or the checkpoint can't be read back.</exception>
    public CitySimulation EndTest()
    {
        if (_checkpoint is null)
        {
            throw new InvalidOperationException("The scenario isn't being tested.");
        }

        if (!FlatCityFormat.TryRead(_checkpoint, out var city, out var problem))
        {
            throw new InvalidOperationException($"The city from before the test couldn't be read back: {problem}");
        }

        _checkpoint = null;
        city.Builder.BuildsForFree = true;
        return city;
    }
}
