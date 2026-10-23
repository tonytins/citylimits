using System.Text.Json.Serialization;

namespace CityLimits.Core.Scenarios;

// The scenario file as it is written, before it is checked. Every field is nullable, so a
// missing one can be reported by name instead of turning silently into a zero. Enum names are
// read as strings and matched exactly by the loader, which can then say what was wrong.

internal sealed class ScenarioDocument
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public FormatVersion? Format { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public int? Seed { get; set; }

    public MapDocument? Map { get; set; }

    public int? Funds { get; set; }

    public int? Ticks { get; set; }

    public bool? Disasters { get; set; }

    public List<ActionDocument?>? Actions { get; set; }

    public List<CheckDocument?>? Checks { get; set; }
}

internal sealed class MapDocument
{
    public int? Width { get; set; }

    public int? Height { get; set; }
}

internal sealed class PointDocument
{
    public int? X { get; set; }

    public int? Y { get; set; }
}

internal sealed class ActionDocument
{
    public int? Tick { get; set; }

    public string? Tool { get; set; }

    public PointDocument? From { get; set; }

    public PointDocument? To { get; set; }

    public string? Expect { get; set; }
}

internal sealed class CheckDocument
{
    public int? Tick { get; set; }

    public PointDocument? Tile { get; set; }

    public string? Count { get; set; }

    public string? Kind { get; set; }

    public string? Use { get; set; }

    public int? Development { get; set; }

    public int? DevelopmentAtLeast { get; set; }

    public int? DevelopmentAtMost { get; set; }

    public bool? Served { get; set; }

    public bool? Powered { get; set; }

    public int? FundsAtLeast { get; set; }

    public int? FundsAtMost { get; set; }

    public int? FundsEquals { get; set; }

    // "equals" in the file; a property can't be called Equals beside object.Equals.
    [JsonPropertyName("equals")]
    public int? EqualTo { get; set; }

    public int? AtLeast { get; set; }

    public int? AtMost { get; set; }
}

// The documents' code is generated at build time, so reading them needs no reflection and
// survives trimming.
[JsonSerializable(typeof(ScenarioDocument))]
internal sealed partial class ScenarioJson : JsonSerializerContext;
