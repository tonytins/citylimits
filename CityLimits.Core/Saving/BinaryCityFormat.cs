using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CityLimits.Core.Definitions;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Saving;

/// <summary>
/// The binary city format, <c>.cl</c>, CityLimits' own save: compact, and quick to read and write.
/// It takes Micropolis' city files as its pattern, big-endian numbers and the map as one 16-bit word
/// a tile, column by column, but holds CityLimits' own state, the same as the flat format: a header,
/// then tagged chunks, each with its length and a CRC-32 checksum. A chunk this version doesn't know
/// is skipped and noted, so a later version can add one; a known chunk that is damaged refuses the
/// whole file, since a city needs all of them to run on exactly. <c>docs/saving.md</c> describes it.
/// </summary>
public static class BinaryCityFormat
{
    /// <summary>The extension of a binary city file.</summary>
    public const string Extension = ".cl";

    /// <summary>
    /// The version of the format this game writes, and the newest it reads. The header holds the
    /// major version, so files from before minor versions stay valid; the minor version is in a
    /// small <c>VERS</c> chunk, and a file without one is a .0. A 2.0 file names its features in a
    /// <c>FEAT</c> chunk and its mods in a <c>MODS</c> one; a 1.x file lists its parts in a
    /// <c>PART</c> chunk and holds its definitions in a <c>DEFS</c> one.
    /// </summary>
    public static readonly FormatVersion Format = SaveUpgrades.Current;

    /// <summary>The largest file read, so a damaged or wrong one can't run away with memory.</summary>
    public const long MaxBytes = 16 * 1024 * 1024;

    private const int TagLength = 4;
    private const int ChunkOverhead = TagLength + 4 + 4;

    private static readonly byte[] Magic = "CLCITY"u8.ToArray();

    // The chunks, in the order they are written; each holds a section of the save model, apart from
    // the minor version's.
    private const string VersionTag = "VERS";
    private const string PartsTag = "PART";
    private const string FeaturesTag = "FEAT";
    private const string ModsTag = "MODS";
    private const string CityTag = "CITY";
    private const string DefinitionsTag = "DEFS";
    private const string MapTag = "MAP ";
    private const string BudgetTag = "BUDG";
    private const string CensusTag = "CENS";
    private const string DemandTag = "DEMD";
    private const string PowerTag = "POWR";
    private const string ServicesTag = "SERV";
    private const string CommunityTag = "COMM";
    private const string CityMapsTag = "CMAP";
    private const string CycleTag = "CYCL";
    private const string TrafficTag = "TRAF";
    private const string RoadTrafficTag = "RTRF";
    private const string RailTag = "RAIL";
    private const string PollutionTag = "POLL";
    private const string CrimeTag = "CRIM";
    private const string FireTag = "FIRE";
    private const string FloodTag = "FLOD";
    private const string ChallengeTag = "CHAL";
    private const string FacilitiesTag = "FCLT";
    private const string FoundingTag = "FNDG";
    private const string OrdinancesTag = "ORDN";
    private const string ConnectionsTag = "CONN";
    private const string RadiationTag = "RADN";

    // The chunks every file has: the city's core, with its definitions in format 1 and its mods' names in 2.0.
    private static readonly string[] Core = [CityTag, MapTag, BudgetTag, CensusTag, DemandTag, PowerTag, ServicesTag, CommunityTag, CityMapsTag, CycleTag];
    private static readonly string[] Required = [.. Core, DefinitionsTag];
    private static readonly string[] RequiredFrom2 = [.. Core, ModsTag];

    // The chunks a file has only when it needs them, such as the challenge of a city being played as
    // one, and the features added since 2.0, which a 2.0 file may go without and 1.10 never has.
    private static readonly string[] Optional = [ChallengeTag, OrdinancesTag, ConnectionsTag, RadiationTag];

    // The chunks each minor version added, required from that version on; an older file goes without.
    private static readonly (string Tag, FormatVersion Since)[] Added = [(TrafficTag, new FormatVersion(1, 1)), (RoadTrafficTag, new FormatVersion(1, 3)), (RailTag, new FormatVersion(1, 4)), (PollutionTag, new FormatVersion(1, 5)), (CrimeTag, new FormatVersion(1, 5)), (FireTag, new FormatVersion(1, 6)), (FloodTag, new FormatVersion(1, 7)), (FacilitiesTag, new FormatVersion(1, 9)), (FoundingTag, new FormatVersion(1, 10))];

    /// <summary>
    /// Writes a city as the format's bytes: version 2.0, or, when <paramref name="legacy"/> asks, 1.10,
    /// which 0.4 reads, with the city's definitions and its parts listed by version. A 1.10 file leaves
    /// out what format 1 can't hold, such as a newer save's features kept here.
    /// </summary>
    public static byte[] Write(CitySimulation city, bool legacy = false)
    {
        ArgumentNullException.ThrowIfNull(city);
        var save = FlatCityFormat.Capture(city, legacy, Extension);
        var kept = legacy ? [] : city.Kept?.ForSaving(Extension) ?? [];

        using var stream = new MemoryStream();
        stream.Write(Magic);
        WriteUInt16(stream, save.Format.Major);
        var minor = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(minor, (ushort)save.Format.Minor);
        WriteChunk(stream, VersionTag, minor);
        if (legacy)
        {
            WriteChunk(stream, PartsTag, SectionCodec.Write(new PartsChunk { Parts = save.Parts! }));
        }
        else
        {
            // The features by name, each with its chunk, the reserved ones kept from a newer save included.
            var entries = save.Features!
                .SelectMany(name => SaveParts.FeatureChunks.Where(feature => feature.Name == name))
                .Select(feature => new FeatureEntry { Name = feature.Name, Chunk = feature.Chunk })
                .ToArray();
            WriteChunk(stream, FeaturesTag, SectionCodec.Write(new FeaturesChunk { Features = entries }));
        }

        WriteChunk(stream, CityTag, SectionCodec.Write(new CityChunk
        {
            Name = save.Name,
            Seed = save.Seed,
            Random = save.Random,
            Tick = save.Tick,
            CityTime = save.CityTime,
            Funds = save.Funds,
        }));
        if (legacy)
        {
            WriteChunk(stream, DefinitionsTag, SectionCodec.Write(new DefinitionsChunk { Definitions = save.Definitions!.Value, Mods = save.Mods }));
        }
        else
        {
            WriteChunk(stream, ModsTag, SectionCodec.Write(new ModsChunk { Mods = save.Mods }));
        }

        WriteChunk(stream, MapTag, MapBytes(city));
        WriteChunk(stream, BudgetTag, SectionCodec.Write(save.Budget));
        WriteChunk(stream, CensusTag, SectionCodec.Write(save.Census));
        WriteChunk(stream, DemandTag, SectionCodec.Write(save.Demand));
        WriteChunk(stream, PowerTag, SectionCodec.Write(save.Power));
        WriteChunk(stream, ServicesTag, SectionCodec.Write(save.Services));
        WriteChunk(stream, CommunityTag, SectionCodec.Write(save.Community));
        WriteChunk(stream, CityMapsTag, SectionCodec.Write(save.CityMaps));
        WriteChunk(stream, CycleTag, SectionCodec.Write(save.Cycle));
        WriteChunk(stream, TrafficTag, SectionCodec.Write(save.Traffic!));
        WriteChunk(stream, RoadTrafficTag, SectionCodec.Write(save.RoadTraffic!));
        WriteChunk(stream, RailTag, SectionCodec.Write(save.Rail!));
        WriteChunk(stream, PollutionTag, SectionCodec.Write(save.Pollution!));
        WriteChunk(stream, CrimeTag, SectionCodec.Write(save.Crime!));
        WriteChunk(stream, FireTag, SectionCodec.Write(save.Fire!));
        WriteChunk(stream, FloodTag, SectionCodec.Write(save.Flood!));
        WriteChunk(stream, FacilitiesTag, SectionCodec.Write(save.Facilities!));
        WriteChunk(stream, FoundingTag, SectionCodec.Write(save.Founding!));
        if (save.Ordinances is { } ordinances)
        {
            WriteChunk(stream, OrdinancesTag, SectionCodec.Write(ordinances));
        }

        if (save.Connections is { } connections)
        {
            WriteChunk(stream, ConnectionsTag, SectionCodec.Write(connections));
        }

        if (save.Radiation is { } radiation)
        {
            WriteChunk(stream, RadiationTag, SectionCodec.Write(radiation));
        }

        if (save.Challenge is { } challenge)
        {
            WriteChunk(stream, ChallengeTag, SectionCodec.Write(challenge));
        }

        foreach (var part in kept)
        {
            WriteChunk(stream, part.Chunk, city.Kept!.Chunks[part.Chunk]);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Reads a city from the format's bytes, or says what is wrong with them. The notes say what was
    /// read but not used, such as a chunk from a later version.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes) =>
        TryRead(bytes, CityDefinitions.Base, out city, out problem, out notes);

    /// <summary>
    /// Reads a city from the format's bytes, running on the definitions given, or says what is wrong
    /// with them. The notes say what was read but not used, and whether the city was played with other mods.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> bytes, CityDefinitions definitions, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        city = null;
        var skipped = new List<string>();
        notes = skipped;
        try
        {
            var save = ReadSave(bytes, skipped, out var kept);
            var opened = new List<string>();
            city = CitySimulation.Restore(save, definitions, opened);
            city.Kept = kept;
            notes = [.. SaveParts.NotesFor(kept), .. skipped, .. opened];
            problem = null;
            return true;
        }
        catch (InvalidSave ex)
        {
            problem = ex.Message;
            return false;
        }
    }

    /// <summary>Reads a city from a file, checking its size before reading it.</summary>
    public static bool TryReadFile(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes) =>
        TryReadFile(path, CityDefinitions.Base, out city, out problem, out notes);

    /// <summary>Reads a city from a file on the definitions given, checking its size before reading it.</summary>
    public static bool TryReadFile(string path, CityDefinitions definitions, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        notes = [];
        if (!SaveFiles.TryReadBytes(path, MaxBytes, out var bytes, out problem))
        {
            return false;
        }

        return TryRead(bytes, definitions, out city, out problem, out notes);
    }

    /// <summary>Writes a city to a file, through a temporary file, so a failure leaves the save before it whole; as 1.10 when <paramref name="legacy"/> asks.</summary>
    public static bool TryWriteFile(CitySimulation city, string path, [NotNullWhen(false)] out string? problem, bool legacy = false)
    {
        ArgumentNullException.ThrowIfNull(city);
        return SaveFiles.TryWriteBytes(Write(city, legacy), path, out problem);
    }

    /// <summary>Reads what a list of saved cities shows about one, without restoring the whole city.</summary>
    public static bool TryReadSummary(string path, [NotNullWhen(true)] out SavedCitySummary? summary)
    {
        summary = null;
        if (!SaveFiles.TryReadBytes(path, MaxBytes, out var bytes, out _))
        {
            return false;
        }

        try
        {
            var save = ReadSave(bytes, [], out _);
            summary = SaveFiles.Summarize(path, save);
            return summary is not null;
        }
        catch (InvalidSave)
        {
            return false;
        }
    }

    // Reads the chunks into the save model without restoring the city, refusing damage.
    internal static CitySave ReadSave(ReadOnlySpan<byte> bytes, List<string> skipped, out KeptParts? kept)
    {
        kept = null;
        if (bytes.Length > MaxBytes)
        {
            throw new InvalidSave($"The file is bigger than {MaxBytes / (1024 * 1024)} MB, so it isn't a city CityLimits saved.");
        }

        if (bytes.Length < Magic.Length + 2 || !bytes[..Magic.Length].SequenceEqual(Magic))
        {
            throw new InvalidSave("The file isn't a binary CityLimits city: it doesn't start with CLCITY.");
        }

        var major = BinaryPrimitives.ReadUInt16BigEndian(bytes[Magic.Length..]);
        if (major < 1 || major > Format.Major)
        {
            throw new InvalidSave($"format: {new FormatVersion(major, 0).ProblemReadingWith(Format)}");
        }

        var minor = 0;
        PartSave[]? parts = null;
        FeatureEntry[]? features = null;
        var chunks = new Dictionary<string, byte[]>();
        var unknown = new Dictionary<string, byte[]>();
        var position = Magic.Length + 2;
        while (position < bytes.Length)
        {
            if (bytes.Length - position < ChunkOverhead)
            {
                throw new InvalidSave($"The file ends partway through a chunk, at byte {position}.");
            }

            var tagBytes = bytes.Slice(position, TagLength);
            var tag = Encoding.ASCII.GetString(tagBytes);
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes[(position + TagLength)..]);
            if (length > (uint)(bytes.Length - position - ChunkOverhead))
            {
                throw new InvalidSave($"{tag.Trim()}: says it is {length} bytes, more than the file has left.");
            }

            var payload = bytes.Slice(position + TagLength + 4, (int)length);
            var stored = BinaryPrimitives.ReadUInt32BigEndian(bytes[(position + TagLength + 4 + (int)length)..]);
            if (Crc32.Compute(payload, Crc32.Compute(tagBytes)) != stored)
            {
                throw new InvalidSave($"{tag.Trim()}: is damaged: its checksum doesn't match.");
            }

            position += ChunkOverhead + (int)length;
            if (tag == VersionTag)
            {
                minor = payload.Length == 2 ? BinaryPrimitives.ReadUInt16BigEndian(payload) : throw new InvalidSave("VERS: needs the minor version as one 16-bit number.");
                continue;
            }

            if (tag == PartsTag)
            {
                parts = SectionCodec.Read<PartsChunk>(payload, PartsTag).Parts;
                continue;
            }

            if (tag == FeaturesTag)
            {
                features = SectionCodec.Read<FeaturesChunk>(payload, FeaturesTag).Features;
                continue;
            }

            if (!Required.Contains(tag) && tag != ModsTag && !Added.Any(added => added.Tag == tag) && !Optional.Contains(tag))
            {
                if (!unknown.TryAdd(tag, payload.ToArray()))
                {
                    throw new InvalidSave($"{tag.Trim()}: the file holds it twice.");
                }

                continue;
            }

            if (!chunks.TryAdd(tag, payload.ToArray()))
            {
                throw new InvalidSave($"{tag.Trim()}: the file holds it twice.");
            }
        }

        // A 2.0 file's unknown chunks are left out, here and when the city is saved, unless one is a
        // feature this version reserves, by the name its features list gives it, which is kept as it
        // was. In a 1.x file, a newer minor version's unknown chunks are kept when its parts list lets
        // them be, as 0.4 read them; any other is skipped and noted, as it always was.
        var version = new FormatVersion(major, minor);
        if (major >= 2)
        {
            if (unknown.Count > 0)
            {
                var names = SaveParts.FeatureChunks.Concat((features ?? []).Select(feature => (feature.Name, feature.Chunk))).ToList();
                kept = SaveParts.KeepFeatures(version, Extension, unknown.Keys.Select(tag => (names.FirstOrDefault(entry => entry.Chunk == tag).Name ?? tag.Trim(), tag)));
                foreach (var part in kept.Parts.Where(part => part.Keepable))
                {
                    kept.Chunks[part.Chunk] = unknown[part.Chunk];
                }
            }
        }
        else
        {
            var legacy = SaveUpgrades.Legacy;
            var newerLegacy = major == legacy.Major && minor > legacy.Minor;
            var named = newerLegacy ? [.. unknown.Keys] : unknown.Keys.Where(tag => parts?.Any(part => part.Chunk == tag) == true).ToList();
            if (newerLegacy || named.Count > 0)
            {
                kept = SaveParts.Keep(version, Extension, parts, named, part => part.Chunk);
                foreach (var part in kept.Parts)
                {
                    kept.Chunks[part.Chunk] = unknown[part.Chunk];
                }
            }

            skipped.AddRange(unknown.Keys.Except(named).Select(tag => $"Skipped a chunk this version doesn't know: '{tag}'."));
        }

        // A 2.0 file needs its core; any feature it leaves out is filled in as an upgrade fills it.
        var needed = major >= 2 ? RequiredFrom2 : Required.Concat(Added.Where(added => version >= added.Since).Select(added => added.Tag));
        var missing = needed.FirstOrDefault(tag => !chunks.ContainsKey(tag));
        if (missing is not null)
        {
            throw new InvalidSave($"{missing.Trim()}: the file is missing it.");
        }

        var head = SectionCodec.Read<CityChunk>(chunks[CityTag], CityTag);
        var definitions = major >= 2 ? null : SectionCodec.Read<DefinitionsChunk>(chunks[DefinitionsTag], DefinitionsTag);
        return new CitySave
        {
            Format = version,
            Features = features?.Select(feature => feature.Name).ToArray(),
            Name = head.Name,
            Seed = head.Seed,
            Random = head.Random,
            Tick = head.Tick,
            CityTime = head.CityTime,
            Funds = head.Funds,
            Definitions = definitions?.Definitions,
            Mods = definitions?.Mods ?? SectionCodec.Read<ModsChunk>(chunks[ModsTag], ModsTag).Mods,
            Map = ReadMap(chunks[MapTag]),
            Budget = SectionCodec.Read<BudgetSave>(chunks[BudgetTag], BudgetTag),
            Census = SectionCodec.Read<CensusSave>(chunks[CensusTag], CensusTag),
            Demand = SectionCodec.Read<DemandSave>(chunks[DemandTag], DemandTag),
            Power = SectionCodec.Read<PowerSave>(chunks[PowerTag], PowerTag),
            Services = SectionCodec.Read<ServicesSave>(chunks[ServicesTag], ServicesTag),
            Community = SectionCodec.Read<CommunitySave>(chunks[CommunityTag], CommunityTag),
            CityMaps = SectionCodec.Read<CityMapsSave>(chunks[CityMapsTag], CityMapsTag),
            Cycle = SectionCodec.Read<CycleSave>(chunks[CycleTag], CycleTag),
            Traffic = chunks.TryGetValue(TrafficTag, out var traffic) ? SectionCodec.Read<TrafficSave>(traffic, TrafficTag) : null,
            RoadTraffic = chunks.TryGetValue(RoadTrafficTag, out var roadTraffic) ? SectionCodec.Read<RoadTrafficSave>(roadTraffic, RoadTrafficTag) : null,
            Rail = chunks.TryGetValue(RailTag, out var rail) ? SectionCodec.Read<RailSave>(rail, RailTag) : null,
            Pollution = chunks.TryGetValue(PollutionTag, out var pollution) ? SectionCodec.Read<BlockMapSave>(pollution, PollutionTag) : null,
            Crime = chunks.TryGetValue(CrimeTag, out var crime) ? SectionCodec.Read<BlockMapSave>(crime, CrimeTag) : null,
            Fire = chunks.TryGetValue(FireTag, out var fire) ? SectionCodec.Read<FireSave>(fire, FireTag) : null,
            Flood = chunks.TryGetValue(FloodTag, out var flood) ? SectionCodec.Read<FloodSave>(flood, FloodTag) : null,
            Facilities = chunks.TryGetValue(FacilitiesTag, out var facilities) ? SectionCodec.Read<FacilitiesSave>(facilities, FacilitiesTag) : null,
            Founding = chunks.TryGetValue(FoundingTag, out var founding) ? SectionCodec.Read<FoundingSave>(founding, FoundingTag) : null,
            Ordinances = chunks.TryGetValue(OrdinancesTag, out var ordinances) ? SectionCodec.Read<OrdinancesSave>(ordinances, OrdinancesTag) : null,
            Connections = chunks.TryGetValue(ConnectionsTag, out var connections) ? SectionCodec.Read<ConnectionsSave>(connections, ConnectionsTag) : null,
            Radiation = chunks.TryGetValue(RadiationTag, out var radiation) ? SectionCodec.Read<RadiationSave>(radiation, RadiationTag) : null,
            Challenge = chunks.TryGetValue(ChallengeTag, out var challenge) ? SectionCodec.Read<ChallengeSave>(challenge, ChallengeTag) : null,
        };
    }

    // The map's chunk: its width and height, then a word a tile, column by column.
    private static byte[] MapBytes(CitySimulation city)
    {
        // Every standing building is written whole; the fire part holds the holes fire left.
        var words = MapCodec.EncodeWords(FireCodec.WholeMap(city.Map, city.Hazards));
        var bytes = new byte[4 + (words.Length * 2)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)city.Map.Width);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), (ushort)city.Map.Height);
        for (var i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4 + (i * 2)), words[i]);
        }

        return bytes;
    }

    private static MapSave ReadMap(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            throw new InvalidSave("MAP: is too short to hold the map's size.");
        }

        var width = BinaryPrimitives.ReadUInt16BigEndian(bytes);
        var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2));
        if (bytes.Length != 4 + (width * height * 2))
        {
            throw new InvalidSave($"MAP: a {width} by {height} map needs {4 + (width * height * 2)} bytes, not {bytes.Length}.");
        }

        var words = new ushort[width * height];
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4 + (i * 2)));
        }

        return MapCodec.DecodeWords(width, height, words);
    }

    private static void WriteChunk(Stream stream, string tag, byte[] payload)
    {
        var tagBytes = Encoding.ASCII.GetBytes(tag);
        stream.Write(tagBytes);
        WriteUInt32(stream, (uint)payload.Length);
        stream.Write(payload);
        WriteUInt32(stream, Crc32.Compute(payload, Crc32.Compute(tagBytes)));
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    // The city's own fields, the first chunk.
    [DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
    private sealed class CityChunk
    {
        public string Name { get; set; } = string.Empty;

        public int Seed { get; set; }

        public string Random { get; set; } = string.Empty;

        public long Tick { get; set; }

        public long CityTime { get; set; }

        public int Funds { get; set; }
    }

    [DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
    private sealed class PartsChunk
    {
        public PartSave[] Parts { get; set; } = [];
    }

    // A 2.0 file's features, each by its name and its chunk.
    [DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
    private sealed class FeaturesChunk
    {
        public FeatureEntry[] Features { get; set; } = [];
    }

    [DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
    private sealed class FeatureEntry
    {
        public string Name { get; set; } = string.Empty;

        public string Chunk { get; set; } = string.Empty;
    }

    [DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
    private sealed class ModsChunk
    {
        public ModSave[] Mods { get; set; } = [];
    }

    [DynamicallyAccessedMembers(SectionCodec.ModelMembers)]
    private sealed class DefinitionsChunk
    {
        public System.Text.Json.JsonElement Definitions { get; set; }

        public ModSave[] Mods { get; set; } = [];
    }
}
