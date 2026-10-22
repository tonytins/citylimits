using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace CityLimits.Core.Saving;

/// <summary>
/// Writes a section of the save model as bytes, and reads it back, for the binary city format. It
/// walks the section's properties in the order they are declared, so the binary format holds
/// exactly what the flat format does and a field added to the model is in both. Whole numbers are
/// variable-length (zigzag, seven bits a byte), so the many small numbers a city holds take a byte
/// or two; text is UTF-8 with its length first. Reading checks every length against what is left,
/// so a damaged chunk can't ask for any amount of memory.
/// </summary>
/// <remarks>
/// It reads properties by reflection, which a trimmed build would cut away. The save model's classes
/// carry <c>[DynamicallyAccessedMembers(SectionCodec.ModelMembers)]</c>, which tells the trimmer to
/// keep their properties and constructors, so the codec still works trimmed.
/// </remarks>
internal static class SectionCodec
{
    /// <summary>What the codec reads of each class in the save model, kept by the trimmer for it.</summary>
    public const DynamicallyAccessedMemberTypes ModelMembers = DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

    private const string ModelKept = "Every class the codec reads is a save model class, marked [DynamicallyAccessedMembers(SectionCodec.ModelMembers)], so its properties and constructor survive trimming.";

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

    /// <summary>Writes a section.</summary>
    public static byte[] Write(object section)
    {
        ArgumentNullException.ThrowIfNull(section);
        using var stream = new MemoryStream();
        WriteObject(stream, section);
        return stream.ToArray();
    }

    /// <summary>Reads a section of the given type from a chunk's bytes, all of which it must use.</summary>
    /// <exception cref="InvalidSave">The bytes don't hold the section.</exception>
    public static T Read<[DynamicallyAccessedMembers(ModelMembers)] T>(ReadOnlySpan<byte> bytes, string chunk)
        where T : class
    {
        var reader = new Reader(bytes, chunk);
        var section = (T)reader.ReadObject(typeof(T));
        if (!reader.AtEnd)
        {
            throw new InvalidSave($"{chunk}: has bytes left over after its last field.");
        }

        return section;
    }

    private static PropertyInfo[] PropertiesOf([DynamicallyAccessedMembers(ModelMembers)] Type type)
    {
        if (!Properties.TryGetValue(type, out var properties))
        {
            properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite).OrderBy(p => p.MetadataToken)];
            Properties[type] = properties;
        }

        return properties;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = ModelKept)]
    private static void WriteObject(Stream stream, object section)
    {
        foreach (var property in PropertiesOf(section.GetType()))
        {
            WriteValue(stream, property.PropertyType, property.GetValue(section));
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = ModelKept)]
    private static void WriteValue(Stream stream, Type type, object? value)
    {
        switch (value)
        {
            case int number:
                WriteVarint(stream, number);
                return;
            case long number:
                WriteVarint(stream, number);
                return;
            case bool flag:
                stream.WriteByte(flag ? (byte)1 : (byte)0);
                return;
            case double number:
                WriteVarint(stream, BitConverter.DoubleToInt64Bits(number));
                return;
            case JsonElement element:
                WriteText(stream, element.GetRawText());
                return;
        }

        if (type == typeof(string))
        {
            WriteText(stream, (string?)value);
        }
        else if (type == typeof(int[]))
        {
            var numbers = (int[]?)value ?? [];
            WriteVarint(stream, numbers.Length);
            foreach (var number in numbers)
            {
                WriteVarint(stream, number);
            }
        }
        else if (type.IsArray)
        {
            var items = (Array?)value ?? Array.CreateInstance(type.GetElementType()!, 0);
            WriteVarint(stream, items.Length);
            foreach (var item in items)
            {
                WriteValue(stream, type.GetElementType()!, item);
            }
        }
        else if (type.IsClass)
        {
            // A section that may be missing, such as last year's budget, says first whether it is there.
            stream.WriteByte(value is null ? (byte)0 : (byte)1);
            if (value is not null)
            {
                WriteObject(stream, value);
            }
        }
        else
        {
            throw new NotSupportedException($"The save model can't hold a {type.Name}.");
        }
    }

    private static void WriteText(Stream stream, string? text)
    {
        if (text is null)
        {
            WriteVarint(stream, -1);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        WriteVarint(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVarint(Stream stream, long value)
    {
        var zigzag = (ulong)((value << 1) ^ (value >> 63));
        while (zigzag >= 0x80)
        {
            stream.WriteByte((byte)(zigzag | 0x80));
            zigzag >>= 7;
        }

        stream.WriteByte((byte)zigzag);
    }

    private ref struct Reader(ReadOnlySpan<byte> bytes, string chunk)
    {
        private readonly ReadOnlySpan<byte> _bytes = bytes;
        private readonly string _chunk = chunk;
        private int _position;

        public readonly bool AtEnd => _position == _bytes.Length;

        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = ModelKept)]
        public object ReadObject([DynamicallyAccessedMembers(ModelMembers)] Type type)
        {
            var section = Activator.CreateInstance(type) ?? throw new InvalidSave($"{_chunk}: can't hold a {type.Name}.");
            foreach (var property in PropertiesOf(type))
            {
                property.SetValue(section, ReadValue(property.PropertyType));
            }

            return section;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = ModelKept)]
        [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = ModelKept)]
        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = ModelKept)]
        private object? ReadValue(Type type)
        {
            if (type == typeof(int))
            {
                var value = ReadVarint();
                return value is >= int.MinValue and <= int.MaxValue ? (int)value : throw Damaged("a number out of range");
            }

            if (type == typeof(long))
            {
                return ReadVarint();
            }

            if (type == typeof(bool))
            {
                return ReadByte() switch
                {
                    0 => false,
                    1 => true,
                    _ => throw Damaged("a yes or no that is neither"),
                };
            }

            if (type == typeof(double))
            {
                return BitConverter.Int64BitsToDouble(ReadVarint());
            }

            if (type == typeof(JsonElement))
            {
                var text = ReadText() ?? throw Damaged("missing definitions");
                try
                {
                    return JsonDocument.Parse(text).RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    throw new InvalidSave($"{_chunk}: the definitions aren't JSON: {ex.Message}");
                }
            }

            if (type == typeof(string))
            {
                return ReadText();
            }

            if (type.IsArray)
            {
                // Every element takes a byte at least, so a count beyond what's left is damage, not a
                // reason to allocate.
                var count = ReadCount();
                var elementType = type.GetElementType()!;
                var items = Array.CreateInstance(elementType, count);
                for (var i = 0; i < count; i++)
                {
                    items.SetValue(ReadValue(elementType), i);
                }

                return items;
            }

            if (type.IsClass)
            {
                return ReadByte() switch
                {
                    0 => null,
                    1 => ReadObject(type),
                    _ => throw Damaged("a section that is neither there nor missing"),
                };
            }

            throw new InvalidSave($"{_chunk}: can't hold a {type.Name}.");
        }

        private string? ReadText()
        {
            var length = ReadVarint();
            if (length == -1)
            {
                return null;
            }

            if (length < 0 || length > _bytes.Length - _position)
            {
                throw Damaged("text longer than the chunk");
            }

            var text = Encoding.UTF8.GetString(_bytes.Slice(_position, (int)length));
            _position += (int)length;
            return text;
        }

        private int ReadCount()
        {
            var count = ReadVarint();
            return count >= 0 && count <= _bytes.Length - _position ? (int)count : throw Damaged("a list longer than the chunk");
        }

        private byte ReadByte() => _position < _bytes.Length ? _bytes[_position++] : throw Damaged("it ends too soon");

        private long ReadVarint()
        {
            ulong zigzag = 0;
            for (var shift = 0; shift < 64; shift += 7)
            {
                var b = ReadByte();
                zigzag |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return (long)(zigzag >> 1) ^ -(long)(zigzag & 1);
                }
            }

            throw Damaged("a number too long");
        }

        private readonly InvalidSave Damaged(string what) => new($"{_chunk}: is damaged: {what}.");
    }
}
