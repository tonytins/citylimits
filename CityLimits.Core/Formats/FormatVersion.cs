using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CityLimits.Core.Formats;

/// <summary>
/// A file format's version, major and minor, as saves, scenarios, and mod definitions carry it. A
/// minor version only adds, so a reader of 1.1 reads 1.0 too, filling in what 1.0 didn't have; a
/// major version breaks with the one before. JSON files write it as a string, such as <c>"1.1"</c>;
/// files from before major and minor versions wrote a whole number, such as <c>1</c>, which reads
/// as 1.0.
/// </summary>
[JsonConverter(typeof(FormatVersionConverter))]
public readonly record struct FormatVersion(int Major, int Minor) : IComparable<FormatVersion>
{
    /// <summary>Reads a version written as "major.minor", or a whole major number.</summary>
    public static bool TryParse(string? text, out FormatVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('.');
        if (parts.Length is < 1 or > 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || (parts.Length == 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }

        var minor = parts.Length == 2 ? int.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture) : 0;
        version = new FormatVersion(major, minor);
        return true;
    }

    /// <summary>
    /// Says why a reader of the given version can't read this one, or null when it can: the same
    /// major version, and a minor version no newer than the reader's.
    /// </summary>
    public string? ProblemReadingWith(FormatVersion reader) =>
        Major == reader.Major && Minor <= reader.Minor
            ? null
            : this > reader
                ? $"this is format {this}; this version of CityLimits reads up to {reader}."
                : $"this is format {this}; this version of CityLimits reads {reader.Major}.x.";

    /// <inheritdoc />
    public int CompareTo(FormatVersion other) => Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    /// <summary>Whether one version is newer than another.</summary>
    public static bool operator >(FormatVersion left, FormatVersion right) => left.CompareTo(right) > 0;

    /// <summary>Whether one version is older than another.</summary>
    public static bool operator <(FormatVersion left, FormatVersion right) => left.CompareTo(right) < 0;

    /// <summary>Whether one version is no older than another.</summary>
    public static bool operator >=(FormatVersion left, FormatVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Whether one version is no newer than another.</summary>
    public static bool operator <=(FormatVersion left, FormatVersion right) => left.CompareTo(right) <= 0;

    /// <summary>The version as "major.minor".</summary>
    public override string ToString() => $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Reads a format version from a string such as "1.1" or a whole number such as 1, and writes it as a string.</summary>
public sealed class FormatVersionConverter : JsonConverter<FormatVersion>
{
    /// <inheritdoc />
    public override FormatVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var major) && major >= 0)
        {
            return new FormatVersion(major, 0);
        }

        if (reader.TokenType == JsonTokenType.String && FormatVersion.TryParse(reader.GetString(), out var version))
        {
            return version;
        }

        throw new JsonException("A format is a version such as \"1.0\".");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FormatVersion value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
