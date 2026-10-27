using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// A scenario being edited in the game, as SimCity began: a map editor turned into a game. The
/// scenario's city is played in the ordinary game with building free and the clock stopped, and the
/// scenario's goals, strokes, and disasters are kept aside as a draft that nothing acts on. Playing
/// tests it: the city is set aside as a checkpoint, and the scenario runs on it as a player's
/// challenge would. Going back puts the city as it was at the checkpoint, undoing the test and
/// everything built during it; starting over opens the scenario's city afresh.
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

    /// <summary>The scenario being edited.</summary>
    public Scenario Scenario { get; }

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
