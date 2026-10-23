using System.Text.Json.Serialization;

namespace CityLimits.Core.Scenarios;

// The scenario file as it is written, before it is checked. Every field is nullable, so a
// missing one can be reported by name instead of turning silently into a zero. Enum names are
// read as strings and matched exactly by the loader, which can then say what was wrong. Format 1
// names its times in ticks, and format 2 in the game's dates; each has a document of its own, so
// each one's schema holds only its own fields, and the two share what an action and a check do.

// Only the format, read first to tell which document the rest of the file is.
internal sealed class FormatDocument
{
    public FormatVersion? Format { get; set; }
}

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

internal class ActionFields
{
    public string? Tool { get; set; }

    public PointDocument? From { get; set; }

    public PointDocument? To { get; set; }

    public string? Expect { get; set; }
}

internal sealed class ActionDocument : ActionFields
{
    public int? Tick { get; set; }
}

internal class CheckFields
{
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

internal sealed class CheckDocument : CheckFields
{
    public int? Tick { get; set; }
}

internal sealed class ScenarioDocument2
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public FormatVersion? Format { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public int? Seed { get; set; }

    public MapDocument? Map { get; set; }

    public int? Funds { get; set; }

    public int? Start { get; set; }

    public string? City { get; set; }

    public string? Ends { get; set; }

    public bool? Disasters { get; set; }

    public List<ActionDocument2?>? Actions { get; set; }

    public List<CheckDocument2?>? Checks { get; set; }

    public List<GoalDocument?>? Goals { get; set; }

    public List<EventDocument?>? Events { get; set; }
}

internal sealed class ActionDocument2 : ActionFields
{
    public string? At { get; set; }

    public int? Week { get; set; }
}

internal sealed class CheckDocument2 : CheckFields
{
    public string? At { get; set; }

    public int? Week { get; set; }
}

internal sealed class GoalDocument
{
    public string? Measure { get; set; }

    public int? AtLeast { get; set; }

    public int? AtMost { get; set; }

    public string? Text { get; set; }
}

internal sealed class EventDocument
{
    public string? At { get; set; }

    public int? Week { get; set; }

    public string? Disaster { get; set; }
}

// The documents' code is generated at build time, so reading them needs no reflection and
// survives trimming.
[JsonSerializable(typeof(FormatDocument))]
[JsonSerializable(typeof(ScenarioDocument))]
[JsonSerializable(typeof(ScenarioDocument2))]
internal sealed partial class ScenarioJson : JsonSerializerContext;
