using System.Globalization;

namespace CityLimits.Core.Saving;

/// <summary>
/// When and where a city saves itself: every <see cref="IntervalSeconds"/> of play, counting only
/// time the city runs, into its own autosave files beside its saves, one or two of them. With two,
/// the newer replaces the older, so the one before is still there if the last went wrong. The game
/// skips an autosave when the city is as it was last saved.
/// </summary>
public sealed class Autosave
{
    /// <summary>How long a city plays between autosaves: fifteen minutes.</summary>
    public const double IntervalSeconds = 15 * 60;

    private const string Marker = " (autosave";

    /// <summary>The seconds of play since the last autosave, or since the city was opened.</summary>
    public double PlayedSeconds { get; private set; }

    /// <summary>
    /// Counts a frame's time when the city is running, and tells whether an autosave is due; once it
    /// is, the count starts again.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The time is negative or not a number.</exception>
    public bool Advance(double seconds, bool running)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A frame's time is a number of seconds, not less than zero.");
        }

        if (!running)
        {
            return false;
        }

        PlayedSeconds += seconds;
        if (PlayedSeconds < IntervalSeconds)
        {
            return false;
        }

        PlayedSeconds = 0;
        return true;
    }

    /// <summary>The name of a city's autosave file in a slot, from 1: "Joffburg (autosave).cl", or "Joffburg (autosave 2).cl" for the second.</summary>
    /// <exception cref="ArgumentException">The name isn't one a city can have.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is outside 1 to the most there are.</exception>
    public static string FileNameFor(string cityName, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slot, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slot, Settings.GameSettings.MaxAutosaveSlots);

        var stem = Path.GetFileNameWithoutExtension(CityFiles.FileNameFor(cityName));
        var suffix = slot == 1 ? ")" : $" {slot.ToString(CultureInfo.InvariantCulture)})";
        return $"{stem}{Marker}{suffix}{BinaryCityFormat.Extension}";
    }

    /// <summary>
    /// The file the next autosave goes to: the first slot when there is one, or, with two, the
    /// slot not yet written, or else the older.
    /// </summary>
    public static string NextPath(string folder, string cityName, int slots)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slots, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slots, Settings.GameSettings.MaxAutosaveSlots);

        var paths = Enumerable.Range(1, slots).Select(slot => Path.Combine(folder, FileNameFor(cityName, slot))).ToList();
        return paths.FirstOrDefault(path => !File.Exists(path))
            ?? paths.OrderBy(File.GetLastWriteTimeUtc).First();
    }

    /// <summary>Tells whether a file is an autosave, from its name.</summary>
    public static bool IsAutosave(string path) => Path.GetFileNameWithoutExtension(path).Contains(Marker, StringComparison.Ordinal);

    /// <summary>The file the city an autosave belongs to is saved to: "Joffburg.cl" for "Joffburg (autosave 2).cl".</summary>
    public static string CityFileFor(string autosavePath)
    {
        var name = Path.GetFileNameWithoutExtension(autosavePath);
        var marker = name.LastIndexOf(Marker, StringComparison.Ordinal);
        var stem = marker > 0 ? name[..marker] : name;
        return Path.Combine(Path.GetDirectoryName(autosavePath) ?? string.Empty, stem + BinaryCityFormat.Extension);
    }
}
