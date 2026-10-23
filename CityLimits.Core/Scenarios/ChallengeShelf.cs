using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>A scenario file the game lists: the scenario, or why it can't be read.</summary>
/// <param name="Path">The file.</param>
/// <param name="Scenario">The scenario, when the file could be read.</param>
/// <param name="Problem">Why it couldn't, when it couldn't.</param>
public sealed record ScenarioListing(string Path, Scenario? Scenario, string? Problem);

/// <summary>
/// The challenges a player can pick: the scenarios with goals in the game's own folder and the
/// player's, in that order and each in name order. A scenario with only checks is an agent's test
/// and isn't listed. A file that can't be read is listed with why, so the player can tell it's
/// there; it can't be played. Nothing ever falls back to another scenario in its place.
/// </summary>
public static class ChallengeShelf
{
    /// <summary>The most scenario files listed from a folder, so a folder of thousands can't stall the menu.</summary>
    public const int MostPerFolder = 500;

    /// <summary>
    /// Lists the challenges in the folders, in order; a folder that is missing gives none, and one
    /// that can't be read says why in <paramref name="problem"/>.
    /// </summary>
    public static IReadOnlyList<ScenarioListing> List(IEnumerable<string> folders, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(folders);
        problem = null;
        var listings = new List<ScenarioListing>();
        foreach (var folder in folders)
        {
            IEnumerable<string> files;
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                files = [.. Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal).Take(MostPerFolder)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problem = $"The scenario folder '{folder}' could not be read: {ex.Message}";
                continue;
            }

            foreach (var file in files)
            {
                if (!ScenarioLoader.TryLoadFile(file, out var scenario, out var error))
                {
                    listings.Add(new ScenarioListing(file, null, error));
                }
                else if (scenario.HasGoals)
                {
                    listings.Add(new ScenarioListing(file, scenario, null));
                }
            }
        }

        return listings;
    }

    /// <summary>
    /// The city a challenge starts with, being played: its saved city, or one made from its seed on the
    /// base game's definitions, so a challenge is the same whatever mods are installed. A city made
    /// from a seed takes the scenario's name, as far as a city's name may go.
    /// </summary>
    /// <exception cref="InvalidDataException">The scenario's saved city can't be opened any more.</exception>
    public static CitySimulation Begin(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var city = ScenarioRunner.Start(scenario);
        city.Challenge = Challenge.Begin(scenario, city);
        if (scenario.CityFile is null)
        {
            var name = scenario.Name.Length > CitySimulation.MaxNameLength ? scenario.Name[..CitySimulation.MaxNameLength] : scenario.Name;
            if (CitySimulation.IsValidName(name))
            {
                city.Rename(name);
            }
        }

        return city;
    }
}
