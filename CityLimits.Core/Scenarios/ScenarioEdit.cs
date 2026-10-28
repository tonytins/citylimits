using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// everything built during it; starting over opens the scenario's city as it was last saved. Its
/// details are changed as a whole: written out, with the changes, and read back as any file is, so
/// the editor can't hold what a file couldn't. Saving writes the scenario to the player's own
/// scenarios folder with its city beside it, so the city built is what the scenario starts from; a
/// game's own scenario is saved there as a copy, and the original is left as it was.
/// </summary>
public sealed class ScenarioEdit
{
    /// <summary>The money a new scenario starts with.</summary>
    public const int NewFunds = 20_000;

    /// <summary>How many years a new scenario runs for.</summary>
    public const int NewYears = 10;

    // The city as it was when the test began, as a flat save; none while the scenario isn't being tested.
    private string? _checkpoint;

    // The scenario and its city as they were last read or saved, to tell whether anything has changed
    // since; none for a new scenario that hasn't been saved yet, or a city not yet opened.
    private string? _savedScenario;
    private string? _savedCity;

    /// <summary>Edits a scenario read from a file, to be saved to the player's scenarios folder given.</summary>
    public ScenarioEdit(Scenario scenario, string path, string saveFolder)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveFolder);
        Scenario = scenario;
        Path = path;
        SaveFolder = saveFolder;
        _savedScenario = TryWriteScenario(scenario, FolderOf(path));
    }

    /// <summary>The scenario being edited, as its details were last changed.</summary>
    public Scenario Scenario { get; private set; }

    /// <summary>The file the scenario was read from, or was last saved to; a new one's file to be.</summary>
    public string Path { get; private set; }

    /// <summary>The player's scenarios folder, where saving writes.</summary>
    public string SaveFolder { get; }

    /// <summary>Tells whether the scenario is being tested, so there is a checkpoint to go back to.</summary>
    public bool IsTesting => _checkpoint is not null;

    /// <summary>
    /// Starts a new scenario from a seed: a map grown from it, starting in 1900 with $20,000, and
    /// running ten years, with no goals or disasters yet. Nothing is written until it is saved.
    /// </summary>
    /// <exception cref="ArgumentException">The name can't be a scenario's, or a file's.</exception>
    public static ScenarioEdit New(string name, int seed, string saveFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveFolder);
        var file = new JsonObject
        {
            ["format"] = $"{ScenarioLoader.Format.Major}.{ScenarioLoader.Format.Minor}",
            ["name"] = name,
            ["seed"] = seed,
            ["start"] = CityCalendar.StartYear,
            ["ends"] = $"{CityCalendar.StartYear + NewYears:0000}-01",
            ["funds"] = NewFunds,
        };
        if (!ScenarioLoader.TryParse(file.ToJsonString(), saveFolder, out var scenario, out var error))
        {
            throw new ArgumentException(error, nameof(name));
        }

        return new ScenarioEdit(scenario, System.IO.Path.Combine(saveFolder, FileName(scenario.Name, ".json")), saveFolder) { _savedScenario = null };
    }

    /// <summary>
    /// Opens the scenario's city as it was last saved, with building free and no challenge on it; a
    /// test under way is dropped. Call it to start editing, and again to start over.
    /// </summary>
    /// <exception cref="InvalidDataException">The scenario's saved city can't be opened any more.</exception>
    public CitySimulation OpenCity()
    {
        _checkpoint = null;
        var city = ChallengeShelf.CityFor(Scenario);
        city.Builder.BuildsForFree = true;

        // A saved city's start and money changed since it was saved are put on it, as they will be
        // when it is saved again.
        if (Scenario.CityFile is not null && city.IsAtFounding)
        {
            if (city.FoundedYear != Scenario.StartYear)
            {
                city.MoveFounding(Scenario.StartYear);
            }

            city.Treasury.Restore(Scenario.Funds);
        }

        // The first city opened is the one the scenario was read with; one made from a seed by a new
        // scenario has never been saved.
        if (_savedScenario is not null)
        {
            _savedCity ??= FlatCityFormat.Write(city);
        }

        return city;
    }

    /// <summary>
    /// Tells whether the scenario or its city has changed since it was last read or saved; a new
    /// scenario that hasn't been saved always has.
    /// </summary>
    public bool HasChanges(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        return _savedScenario is null
            || _savedCity is null
            || TryWriteScenario(Scenario, FolderOf(Path)) != _savedScenario
            || FlatCityFormat.Write(city) != _savedCity;
    }

    /// <summary>
    /// Tells whether the scenario's start year and money can be changed: always for one made from a
    /// seed, and for one that starts from a saved city only while that city is at its founding.
    /// </summary>
    public bool CanMoveStart(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        return Scenario.CityFile is null || city.IsAtFounding;
    }

    /// <summary>
    /// Works out the scenario with its details changed, checked as a file would be, without taking
    /// it yet; <paramref name="problem"/> says what is wrong when it can't be. A scenario that starts
    /// from a saved city is checked against the city as it would be, its money and founding moved.
    /// </summary>
    public bool TryChange(ScenarioDetails details, CitySimulation city, [NotNullWhen(true)] out Scenario? changed, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(city);
        changed = null;
        var folder = FolderOf(Path);
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

        var years = details.StartYear - Scenario.StartYear;
        CitySimulation? asIfSaved = null;
        if (Scenario.CityFile is null)
        {
            file["start"] = details.StartYear;
            file["funds"] = details.Funds;
        }
        else if (years != 0 || details.Funds != Scenario.Funds)
        {
            if (!CanMoveStart(city))
            {
                problem = "Its city has run since it was founded, so its start and its money are its own.";
                return false;
            }

            if (!FlatCityFormat.TryRead(FlatCityFormat.Write(city), out asIfSaved, out var copyProblem))
            {
                problem = copyProblem;
                return false;
            }

            asIfSaved.Treasury.Restore(details.Funds);
            if (years != 0)
            {
                asIfSaved.MoveFounding(details.StartYear);
            }
        }

        // The editor doesn't show the scenario's own strokes and checks, so they move with its start,
        // as its city does, keeping their place in its story; what it shows is taken as it is given.
        Shift(file["actions"] as JsonArray, years);
        Shift(file["checks"] as JsonArray, years);

        file["name"] = details.Name;
        file.Remove("description");
        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            file["description"] = details.Description;
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

        if (!ScenarioLoader.TryParse(file.ToJsonString(), folder, asIfSaved, out changed, out var error))
        {
            problem = error;
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>
    /// Takes a changed scenario as the one being edited, and keeps the city in step with it: the city
    /// starts with the scenario's money, and, when its start year moved, moves its own founding there,
    /// everything built on it kept. The map doesn't depend on the year, so nothing is grown again.
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

        if (changed.StartYear != Scenario.StartYear)
        {
            city.MoveFounding(changed.StartYear);
        }

        city.Treasury.Restore(changed.Funds);
        Scenario = changed;
    }

    /// <summary>The file saving writes the scenario to: its name, in the player's scenarios folder.</summary>
    /// <exception cref="ArgumentException">The scenario's name can't be a file's.</exception>
    public string SavePath() => System.IO.Path.Combine(SaveFolder, FileName(Scenario.Name, ".json"));

    /// <summary>
    /// Saves the scenario as it stands, with its city beside it as a flat save of the same name, to
    /// the player's scenarios folder, replacing what is there: the scenario then starts from that
    /// city, whatever it was made from. <paramref name="problem"/> says why when it can't be saved.
    /// When <paramref name="legacy"/> asks, the scenario and its city are written in the formats 0.4 reads.
    /// </summary>
    public bool TrySave(CitySimulation city, [NotNullWhen(false)] out string? problem, bool legacy = false)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (IsTesting)
        {
            problem = "A scenario can't be saved while it is tested: reset it first.";
            return false;
        }

        string path, cityName;
        try
        {
            path = SavePath();
            cityName = FileName(Scenario.Name, FlatCityFormat.Extension);
        }
        catch (ArgumentException ex)
        {
            problem = ex.Message;
            return false;
        }

        JsonObject file;
        try
        {
            file = JsonNode.Parse(ScenarioWriter.Write(Scenario, SaveFolder, legacy))!.AsObject();
        }
        catch (InvalidDataException ex)
        {
            problem = ex.Message;
            return false;
        }

        // The city written is what the scenario starts from now: its seed, map, money, and date.
        foreach (var field in new[] { "seed", "map", "funds", "start" })
        {
            file.Remove(field);
        }

        file["city"] = cityName;
        try
        {
            Directory.CreateDirectory(SaveFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"The scenarios folder {SaveFolder} couldn't be made: {ex.Message}";
            return false;
        }

        if (!CityFiles.TryWrite(city, System.IO.Path.Combine(SaveFolder, cityName), out problem, legacy))
        {
            return false;
        }

        var text = file.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        try
        {
            File.WriteAllText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"{System.IO.Path.GetFileName(path)} couldn't be written: {ex.Message}";
            return false;
        }

        if (!ScenarioLoader.TryLoadFile(path, out var saved, out var error))
        {
            problem = $"{System.IO.Path.GetFileName(path)} was written but doesn't read back: {error}";
            return false;
        }

        (Scenario, Path) = (saved, path);
        _savedScenario = TryWriteScenario(saved, SaveFolder);
        _savedCity = FlatCityFormat.Write(city);
        return true;
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

    // A file name from the scenario's name, made safe as a city's file name is.
    private static string FileName(string name, string extension)
    {
        var asCity = CityFiles.FileNameFor(name.Length > CitySimulation.MaxNameLength ? name[..CitySimulation.MaxNameLength] : name);
        return System.IO.Path.ChangeExtension(asCity, extension);
    }

    private static string FolderOf(string path) => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? string.Empty;

    private static string? TryWriteScenario(Scenario scenario, string folder)
    {
        try
        {
            return ScenarioWriter.Write(scenario, folder);
        }
        catch (InvalidDataException)
        {
            return null;
        }
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
}
