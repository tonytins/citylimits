using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// The scenarios that can be run by id: the demo town, which lives in code, and the scenario
/// files in one folder, each named for its id. An id is lowercase letters, digits, and single
/// hyphens, so it can never name a path outside the folder.
/// </summary>
public sealed partial class ScenarioCatalog
{
    private const int MaxIdLength = 64;

    /// <summary>Creates a catalog over the scenario files in a folder; the folder may be missing.</summary>
    public ScenarioCatalog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = directory;
    }

    /// <summary>The folder the scenario files are read from.</summary>
    public string Directory { get; }

    /// <summary>Tells whether text can be a scenario's id.</summary>
    public static bool IsValidId(string? id) => id is { Length: > 0 and <= MaxIdLength } && IdPattern().IsMatch(id);

    /// <summary>
    /// The ids that can be run, the demo town first and then the files in name order. A folder
    /// that is missing or can't be read gives only the demo town, and says why in <paramref name="problem"/>.
    /// </summary>
    public IReadOnlyList<string> ListIds(out string? problem)
    {
        problem = null;
        var ids = new List<string> { DemoTown.Id };

        try
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return ids;
            }

            ids.AddRange(System.IO.Directory.EnumerateFiles(Directory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => IsValidId(id) && id != DemoTown.Id)
                .Order(StringComparer.Ordinal)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"The scenario folder '{Directory}' could not be read: {ex.Message}";
        }

        return ids;
    }

    /// <summary>Loads a scenario by id: the demo town from code, any other from its file.</summary>
    /// <exception cref="ArgumentException">The id isn't a valid id; check it with <see cref="IsValidId"/> first.</exception>
    public bool TryLoad(string id, [NotNullWhen(true)] out Scenario? scenario, out string error)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException($"'{id}' is not a scenario id.", nameof(id));
        }

        if (id == DemoTown.Id)
        {
            scenario = DemoTown.Scenario;
            error = string.Empty;
            return true;
        }

        return ScenarioLoader.TryLoadFile(Path.Combine(Directory, id + ".json"), out scenario, out error);
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
