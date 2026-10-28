using System.Text.Json;

namespace CityLimits.Core.Saving;

/// <summary>
/// A part of a save beyond its core, as a 1.x save's parts list or a 2.0 save's features list names
/// it: its section in the flat format, its chunk in the binary one, in format 1 the version that
/// added it, and what a game that doesn't know it may do with it. It follows PNG's model: an
/// ignorable part may be left unread, so the file still opens; a keepable part may be written back
/// unchanged after the city has changed, because nothing in it goes wrong when the rest of the city
/// moves on. In 2.0 a feature is known by its name, as HTML's are, not by the version that added it.
/// </summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class PartSave
{
    /// <summary>The part's section in a flat save.</summary>
    public required string Name { get; set; }

    /// <summary>The part's chunk in a binary save.</summary>
    public required string Chunk { get; set; }

    /// <summary>In format 1, the version that added the part, such as "1.1"; a 2.0 feature has none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Since { get; set; }

    /// <summary>Whether a game that doesn't know the part may open the file without it.</summary>
    public required bool Ignorable { get; set; }

    /// <summary>Whether a game that doesn't know the part, and has changed the city, may write it back as it was.</summary>
    public required bool Keepable { get; set; }
}

/// <summary>
/// The parts this version knows, and what a city carries of a newer save's parts it doesn't: each
/// one's raw contents and its entry in the parts list, so they can be written back. Every format reads
/// and writes through here, so a newer save opens and saves the same way in each.
/// </summary>
internal static class SaveParts
{
    /// <summary>The parts every version from 1.1 added, with what an older game may do with each.</summary>
    public static readonly PartSave[] Known =
    [
        // Traffic is any amount on any road, and fades on its own, so it may be left out or kept stale.
        new() { Name = "traffic", Chunk = "TRAF", Since = "1.1", Ignorable = true, Keepable = true },

        // Each road's traffic is set again by the scan within a cycle, and a level left on a tile
        // that is no longer a road is dropped when it is read, so it may be left out or kept stale.
        new() { Name = "roadTraffic", Chunk = "RTRF", Since = "1.3", Ignorable = true, Keepable = true },

        // The map holds what lies beneath the rail, so a game that doesn't know rail opens the city
        // as if it had none, as Tony Bark chose. It can't keep the rail, since it may build where it was.
        new() { Name = "rail", Chunk = "RAIL", Since = "1.4", Ignorable = true, Keepable = false },

        // Pollution and crime are worked out again every cycle from the map, so they may be left out
        // or kept stale; land value reads a stale one for a cycle at most.
        new() { Name = "pollution", Chunk = "POLL", Since = "1.5", Ignorable = true, Keepable = true },
        new() { Name = "crime", Chunk = "CRIM", Since = "1.5", Ignorable = true, Keepable = true },

        // The map holds every standing building whole, so a game that doesn't know fire opens the
        // city as if its fires had burned out and its buildings been repaired, as Tony Bark chose. It
        // can't keep the fires, since it may build where they were.
        new() { Name = "fire", Chunk = "FIRE", Since = "1.6", Ignorable = true, Keepable = false },

        // Floods are left out as fires are: the map holds bare ground beneath them, and a flooded hole
        // in a building is marked in the fire part. A game that doesn't know floods can't keep them.
        new() { Name = "flood", Chunk = "FLOD", Since = "1.7", Ignorable = true, Keepable = false },

        // A scenario being played: a game that doesn't know it plays the city on as a free one, and may
        // write it back as it was, since what fell due meanwhile is made, and the goals judged, when the
        // newer game opens it again. Only a city that is a challenge has it.
        new() { Name = "challenge", Chunk = "CHAL", Since = "1.8", Ignorable = true, Keepable = true },

        // The map holds the bare ground beneath each stadium, seaport, and airport, and what was lost
        // from one lies there as rubble, so a game that doesn't know them opens the city as if they had
        // never been built. It can't keep them, since it may build where they stood.
        new() { Name = "facilities", Chunk = "FCLT", Since = "1.9", Ignorable = true, Keepable = false },

        // The year the city was founded only says how old it is, so a game that doesn't know it counts
        // the age from 1900, as it always did, and may write it back as it was.
        new() { Name = "founding", Chunk = "FNDG", Since = "1.10", Ignorable = true, Keepable = true },

        // The first feature of 2.0, with no version: a city's ordinances, CityLimits' own. A game that
        // doesn't know them plays the city as if none were enacted, and may write them back as they
        // were, since enacting one changes nothing else that is saved; the year's ordinance money
        // set aside meanwhile is only what the newer game had, so the year comes out short.
        new() { Name = "ordinances", Chunk = "ORDN", Ignorable = true, Keepable = true },
    ];

    /// <summary>The parts format 1's list names, each with the version that added it, for a save 0.4 opens.</summary>
    public static PartSave[] Legacy => [.. Known.Where(part => part.Since is not null)];

    /// <summary>
    /// Features this version knows by name and carries through as they are, without reading them,
    /// until a later version gives them their contents: connections to neighbouring cities, which
    /// Senri's N5 fills. A save that holds one writes it back unchanged; one that doesn't, leaves it out.
    /// </summary>
    public static readonly PartSave[] Reserved =
    [
        new() { Name = "connections", Chunk = "CONN", Ignorable = true, Keepable = true },
    ];

    /// <summary>
    /// The features a 2.0 save names: the parts above, known by their names rather than the versions
    /// that added them. A feature added from now on goes here, and the version doesn't move.
    /// </summary>
    public static IEnumerable<string> FeatureNames => Known.Select(part => FeatureOf(part.Name)).Distinct();

    /// <summary>
    /// Each feature's chunks in a binary save, the reserved ones' too: one each, but traffic's two,
    /// its average and each road's.
    /// </summary>
    public static IEnumerable<(string Name, string Chunk)> FeatureChunks => Known.Concat(Reserved).Select(part => (FeatureOf(part.Name), part.Chunk));

    // Each road's traffic is part of the traffic feature, as Tony Bark chose for 0.5; its section
    // keeps its own name, roadTraffic, so 0.4 and 0.5.100 read it as they did.
    private static string FeatureOf(string part) => part == "roadTraffic" ? "traffic" : part;

    /// <summary>
    /// What a 2.0 save comes to with sections this version doesn't know, each by its name and where it
    /// is (its section, or its chunk): a reserved feature is kept as it was, and any other is left out,
    /// here and when the city is saved. A feature that can't be left out comes with a new major
    /// version, so a 2.0 save never needs to be refused for one.
    /// </summary>
    public static KeptParts KeepFeatures(FormatVersion fileVersion, string format, IEnumerable<(string Name, string Chunk)> unknown)
    {
        var kept = new KeptParts { FileVersion = fileVersion, Format = format };
        foreach (var (name, chunk) in unknown)
        {
            var reserved = Reserved.Any(part => part.Name == name);
            kept.Parts.Add(new PartSave { Name = name, Chunk = chunk, Ignorable = true, Keepable = reserved });
        }

        return kept;
    }

    /// <summary>
    /// Decides whether a 1.x save may open here with the parts of a newer minor version it has that this
    /// version doesn't know, as 0.4 did. It is refused when one can't be left out. Returns what the city keeps.
    /// </summary>
    /// <exception cref="InvalidSave">A part isn't in the file's parts list, or can't be left out.</exception>
    public static KeptParts Keep(FormatVersion fileVersion, string format, PartSave[]? parts, IEnumerable<string> unknown, Func<PartSave, string> nameIn)
    {
        var kept = new KeptParts { FileVersion = fileVersion, Format = format };
        foreach (var name in unknown)
        {
            var part = (parts ?? []).FirstOrDefault(entry => nameIn(entry) == name)
                ?? throw new InvalidSave($"{name}: is not a part a save has, and the file's parts list doesn't name it.");
            if (!part.Ignorable)
            {
                throw new InvalidSave($"format: this is format {fileVersion}; this version of CityLimits reads up to {SaveUpgrades.Legacy}, and the file's {part.Name}, from {part.Since}, can't be left out.");
            }

            kept.Parts.Add(part);
        }

        return kept;
    }

    /// <summary>The notes a city opened from a newer save shows, saying what is kept and what will be left out.</summary>
    public static IReadOnlyList<string> NotesFor(KeptParts? kept)
    {
        if (kept is null || kept.Parts.Count == 0)
        {
            return [];
        }

        if (kept.FileVersion.Major >= 2)
        {
            return
            [
                "A newer CityLimits saved this city with parts this version doesn't use.",
                .. kept.Parts.Select(part => part.Keepable
                    ? $"Its {part.Name} part is kept as it was when you save it."
                    : $"Its {part.Name} part is left out, and is left out when you save it."),
            ];
        }

        var notes = new List<string>
        {
            kept.FileVersion > SaveUpgrades.Legacy
                ? $"A newer CityLimits saved this city, in format {kept.FileVersion}; this version reads up to {SaveUpgrades.Legacy}."
                : "This city carries parts a newer CityLimits saved, which this version doesn't read.",
        };
        foreach (var part in kept.Parts)
        {
            notes.Add(part.Keepable
                ? $"Its {part.Name}, from {part.Since}, is kept as it was when you save it."
                : $"Its {part.Name}, from {part.Since}, will be left out when you save it, and the newer version will rebuild it.");
        }

        return notes;
    }
}

/// <summary>
/// The parts of a newer save a city was opened from that this version doesn't know: their entries in
/// the parts list, and their contents as the file held them, a flat save's sections or a binary
/// save's chunks. They go back into a save of the same format; a save in the other format, which
/// can't hold them, leaves them out.
/// </summary>
internal sealed class KeptParts
{
    /// <summary>The version the newer save was.</summary>
    public required FormatVersion FileVersion { get; init; }

    /// <summary>The format they came from: <see cref="FlatCityFormat.Extension"/> or <see cref="BinaryCityFormat.Extension"/>.</summary>
    public required string Format { get; init; }

    /// <summary>Each unknown part's entry in the file's parts list.</summary>
    public List<PartSave> Parts { get; } = [];

    /// <summary>A flat save's unknown sections, as JSON.</summary>
    public Dictionary<string, JsonElement> Sections { get; } = [];

    /// <summary>A binary save's unknown chunks, by tag, as the file held them.</summary>
    public Dictionary<string, byte[]> Chunks { get; } = [];

    /// <summary>
    /// The parts that go back into a 2.0 save of a format, listed as its features: the keepable ones.
    /// A part left out is one the newer game fills in again when it opens the save, as it fills in
    /// any feature a save doesn't hold, so the version written is always this one.
    /// </summary>
    public IReadOnlyList<PartSave> ForSaving(string format) =>
        string.Equals(format, Format, StringComparison.OrdinalIgnoreCase)
            ? [.. Parts.Where(part => part.Keepable).Select(part => new PartSave { Name = part.Name, Chunk = part.Chunk, Ignorable = part.Ignorable, Keepable = part.Keepable })]
            : [];
}
