using System.Text.Json;

namespace CityLimits.Core.Saving;

/// <summary>
/// A part a save format version added after 1.0, as a save's parts list names it: its section in the
/// flat format, its chunk in the binary one, the version that added it, and what an older game that
/// doesn't know it may do with it. It follows PNG's model: an ignorable part may be left unread, so
/// the file still opens; a keepable part may be written back unchanged after the city has changed,
/// because nothing in it goes wrong when the rest of the city moves on.
/// </summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
internal sealed class PartSave
{
    /// <summary>The part's section in a flat save.</summary>
    public required string Name { get; set; }

    /// <summary>The part's chunk in a binary save.</summary>
    public required string Chunk { get; set; }

    /// <summary>The format version that added the part, such as "1.1".</summary>
    public required string Since { get; set; }

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
    /// <summary>The save model's version, which both save formats write.</summary>
    public static readonly FormatVersion Current = new(1, 0);

    /// <summary>
    /// The parts every version from 1.1 added that this one knows: none, since this version writes 1.0,
    /// so every part a newer save lists is one it looks up in that save's own list.
    /// </summary>
    public static readonly PartSave[] Known = [];

    /// <summary>
    /// Decides whether a save of a newer minor version may open here, from the parts it has that this
    /// version doesn't know, and refuses it when one can't be left out. Returns what the city keeps.
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
                throw new InvalidSave($"format: this is format {fileVersion}; this version of CityLimits reads up to {SaveParts.Current}, and the file's {part.Name}, from {part.Since}, can't be left out.");
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

        var notes = new List<string>
        {
            kept.FileVersion > SaveParts.Current
                ? $"A newer CityLimits saved this city, in format {kept.FileVersion}; this version reads up to {SaveParts.Current}."
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
    /// The parts that go back into a save of a format, and the version it writes: the newer save's,
    /// when it had parts and every one is kept, and otherwise this version's, so the newer game
    /// rebuilds what was left out as an upgrade does. A newer save with no parts this version doesn't
    /// know holds nothing past this version, so it is written as this version.
    /// </summary>
    public (IReadOnlyList<PartSave> Parts, FormatVersion Version) ForSaving(string format)
    {
        if (!string.Equals(format, Format, StringComparison.OrdinalIgnoreCase))
        {
            return ([], SaveParts.Current);
        }

        var keepable = Parts.Where(part => part.Keepable).ToList();
        return (keepable, keepable.Count > 0 && keepable.Count == Parts.Count ? FileVersion : SaveParts.Current);
    }
}
