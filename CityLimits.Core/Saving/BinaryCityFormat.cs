using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
    /// small <c>VERS</c> chunk, and a file without one is a .0.
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

    private static readonly string[] Required = [CityTag, DefinitionsTag, MapTag, BudgetTag, CensusTag, DemandTag, PowerTag, ServicesTag, CommunityTag, CityMapsTag, CycleTag];

    // The chunks each minor version added, required from that version on; an older file goes without.
    private static readonly (string Tag, FormatVersion Since)[] Added = [(TrafficTag, new FormatVersion(1, 1))];

    /// <summary>Writes a city as the format's bytes.</summary>
    public static byte[] Write(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        var save = city.Capture();

        using var stream = new MemoryStream();
        stream.Write(Magic);
        WriteUInt16(stream, Format.Major);
        var minor = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(minor, (ushort)Format.Minor);
        WriteChunk(stream, VersionTag, minor);
        WriteChunk(stream, CityTag, SectionCodec.Write(new CityChunk
        {
            Name = save.Name,
            Seed = save.Seed,
            Random = save.Random,
            Tick = save.Tick,
            CityTime = save.CityTime,
            Funds = save.Funds,
        }));
        WriteChunk(stream, DefinitionsTag, SectionCodec.Write(new DefinitionsChunk { Definitions = save.Definitions, Mods = save.Mods }));
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
        return stream.ToArray();
    }

    /// <summary>
    /// Reads a city from the format's bytes, or says what is wrong with them. The notes say what was
    /// read but not used, such as a chunk from a later version.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        var skipped = new List<string>();
        notes = skipped;
        try
        {
            city = CitySimulation.Restore(ReadSave(bytes, skipped));
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
    public static bool TryReadFile(string path, [NotNullWhen(true)] out CitySimulation? city, [NotNullWhen(false)] out string? problem, out IReadOnlyList<string> notes)
    {
        city = null;
        notes = [];
        if (!SaveFiles.TryReadBytes(path, MaxBytes, out var bytes, out problem))
        {
            return false;
        }

        return TryRead(bytes, out city, out problem, out notes);
    }

    /// <summary>Writes a city to a file, through a temporary file, so a failure leaves the save before it whole.</summary>
    public static bool TryWriteFile(CitySimulation city, string path, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(city);
        return SaveFiles.TryWriteBytes(Write(city), path, out problem);
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
            var save = ReadSave(bytes, []);
            summary = SaveFiles.Summarize(path, save);
            return summary is not null;
        }
        catch (InvalidSave)
        {
            return false;
        }
    }

    // Reads the chunks into the save model without restoring the city, refusing damage.
    internal static CitySave ReadSave(ReadOnlySpan<byte> bytes, List<string> skipped)
    {
        if (bytes.Length > MaxBytes)
        {
            throw new InvalidSave($"The file is bigger than {MaxBytes / (1024 * 1024)} MB, so it isn't a city CityLimits saved.");
        }

        if (bytes.Length < Magic.Length + 2 || !bytes[..Magic.Length].SequenceEqual(Magic))
        {
            throw new InvalidSave("The file isn't a binary CityLimits city: it doesn't start with CLCITY.");
        }

        var major = BinaryPrimitives.ReadUInt16BigEndian(bytes[Magic.Length..]);
        if (major != Format.Major)
        {
            throw new InvalidSave($"format: {new FormatVersion(major, 0).ProblemReadingWith(Format)}");
        }

        var minor = 0;
        var chunks = new Dictionary<string, byte[]>();
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

            if (!Required.Contains(tag) && !Added.Any(added => added.Tag == tag))
            {
                skipped.Add($"Skipped a chunk this version doesn't know: '{tag}'.");
                continue;
            }

            if (!chunks.TryAdd(tag, payload.ToArray()))
            {
                throw new InvalidSave($"{tag.Trim()}: the file holds it twice.");
            }
        }

        var version = new FormatVersion(major, minor);
        if (version.ProblemReadingWith(Format) is { } tooNew)
        {
            throw new InvalidSave($"format: {tooNew}");
        }

        var missing = Required.Concat(Added.Where(added => version >= added.Since).Select(added => added.Tag)).FirstOrDefault(tag => !chunks.ContainsKey(tag));
        if (missing is not null)
        {
            throw new InvalidSave($"{missing.Trim()}: the file is missing it.");
        }

        var head = SectionCodec.Read<CityChunk>(chunks[CityTag], CityTag);
        var definitions = SectionCodec.Read<DefinitionsChunk>(chunks[DefinitionsTag], DefinitionsTag);
        return new CitySave
        {
            Format = version,
            Name = head.Name,
            Seed = head.Seed,
            Random = head.Random,
            Tick = head.Tick,
            CityTime = head.CityTime,
            Funds = head.Funds,
            Definitions = definitions.Definitions,
            Mods = definitions.Mods,
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
        };
    }

    // The map's chunk: its width and height, then a word a tile, column by column.
    private static byte[] MapBytes(CitySimulation city)
    {
        var words = MapCodec.EncodeWords(city.Map);
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
    private sealed class DefinitionsChunk
    {
        public System.Text.Json.JsonElement Definitions { get; set; }

        public ModSave[] Mods { get; set; } = [];
    }
}
