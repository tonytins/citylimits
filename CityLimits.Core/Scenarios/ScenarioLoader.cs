using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// Reads a scenario file and checks every part of it before anything runs, as the scenario spec
/// lays out. The first problem is reported with where it is, as in
/// <c>actions[3].from.x: needs a number from -512 to 1023, not 5000.</c>
/// </summary>
public static class ScenarioLoader
{
    /// <summary>The largest file read; a bigger one is refused before it is parsed.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    /// <summary>
    /// The newest scenario format this version reads. A file of an older minor version reads too;
    /// one written as a whole number, such as <c>1</c>, is 1.0. 1.1 added the Rail tool and the Rail count,
    /// 1.2 the <c>disasters</c> switch and the OnFire refusal, and 1.3 the Flooded refusal, with floods
    /// and earthquakes among the disasters the switch lets happen.
    /// </summary>
    public static readonly FormatVersion Format = new(1, 3);

    /// <summary>The most actions, and the most checks, a scenario may hold.</summary>
    public const int MaxEntries = 10_000;

    /// <summary>The smallest map side a scenario may ask for.</summary>
    public const int MinMapSide = 16;

    /// <summary>The largest map side a scenario may ask for.</summary>
    public const int MaxMapSide = 512;

    /// <summary>The most money a scenario may start with.</summary>
    public const int MaxFunds = 1_000_000;

    /// <summary>The lowest coordinate a stroke may name, so a stroke can run off the map on purpose.</summary>
    public const int MinCoordinate = -512;

    /// <summary>The highest coordinate a stroke may name.</summary>
    public const int MaxCoordinate = 1023;

    private const int MaxNameLength = 80;
    private const int MaxDescriptionLength = 500;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // An unknown field is refused, so a misspelt check can't silently pass.
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        TypeInfoResolver = ScenarioJson.Default,
    };

    /// <summary>Reads a scenario file, refusing one that is too big, can't be read, or isn't valid.</summary>
    public static bool TryLoadFile(string path, [NotNullWhen(true)] out Scenario? scenario, out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        scenario = null;

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                error = $"There is no scenario file at '{path}'.";
                return false;
            }

            if (file.Length > MaxFileBytes)
            {
                error = $"The scenario file '{path}' is {file.Length} bytes; the most is {MaxFileBytes}.";
                return false;
            }

            return TryParse(File.ReadAllText(path), out scenario, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = $"The scenario file '{path}' could not be read: {ex.Message}";
            return false;
        }
    }

    /// <summary>Reads a scenario from its JSON text, and checks it.</summary>
    public static bool TryParse(string json, [NotNullWhen(true)] out Scenario? scenario, out string error)
    {
        ArgumentNullException.ThrowIfNull(json);
        scenario = null;
        error = string.Empty;

        if (json.Length > MaxFileBytes)
        {
            error = $"The scenario is {json.Length} characters long; the most is {MaxFileBytes}.";
            return false;
        }

        ScenarioDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, (JsonTypeInfo<ScenarioDocument>)Options.GetTypeInfo(typeof(ScenarioDocument)));
        }
        catch (JsonException ex)
        {
            var where = string.IsNullOrEmpty(ex.Path) || ex.Path == "$" ? "the file" : ex.Path.TrimStart('$', '.');
            error = $"{where}: {Describe(ex)}";
            return false;
        }

        if (document is null)
        {
            error = "The scenario is empty.";
            return false;
        }

        try
        {
            scenario = Validate(document);
            return true;
        }
        catch (InvalidScenario ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Describe(JsonException ex) =>
        ex.Message.Contains("could not be mapped", StringComparison.Ordinal)
            ? "is not a field a scenario has."
            : ex.InnerException is FormatException or OverflowException || ex.Message.Contains("could not be converted", StringComparison.Ordinal)
                ? "has the wrong type of value."
                : "is not valid JSON.";

    private static Scenario Validate(ScenarioDocument document)
    {
        var format = document.Format ?? throw new InvalidScenario("format: is required.");
        if (format.ProblemReadingWith(Format) is { } problem)
        {
            throw new InvalidScenario($"format: {problem}");
        }

        var name = Required(document.Name, "name").Trim();
        if (name.Length is 0 or > MaxNameLength)
        {
            throw new InvalidScenario($"name: needs 1 to {MaxNameLength} characters, not {name.Length}.");
        }

        var description = document.Description?.Trim();
        if (description is { Length: > MaxDescriptionLength })
        {
            throw new InvalidScenario($"description: needs at most {MaxDescriptionLength} characters, not {description.Length}.");
        }

        var seed = Required(document.Seed, "seed");
        var width = document.Map is null ? MapSize.DefaultWidth : InRange(Required(document.Map.Width, "map.width"), MinMapSide, MaxMapSide, "map.width");
        var height = document.Map is null ? MapSize.DefaultHeight : InRange(Required(document.Map.Height, "map.height"), MinMapSide, MaxMapSide, "map.height");
        var funds = InRange(document.Funds ?? Treasury.StartingFunds, 0, MaxFunds, "funds");
        var ticks = InRange(Required(document.Ticks, "ticks"), 0, Scenario.MaxTicks, "ticks");

        var actions = new List<ScenarioAction>();
        var actionDocuments = document.Actions ?? [];
        Limit(actionDocuments.Count, "actions");
        for (var i = 0; i < actionDocuments.Count; i++)
        {
            actions.Add(ValidateAction(actionDocuments[i], i, ticks));
        }

        var checks = new List<ScenarioCheck>();
        var checkDocuments = document.Checks ?? [];
        Limit(checkDocuments.Count, "checks");
        for (var i = 0; i < checkDocuments.Count; i++)
        {
            checks.Add(ValidateCheck(checkDocuments[i], i, ticks, width, height));
        }

        return new Scenario(name, description, seed, width, height, funds, ticks, actions, checks, document.Disasters ?? false);
    }

    private static ScenarioAction ValidateAction(ActionDocument? action, int index, int ticks)
    {
        var at = $"actions[{index}]";
        if (action is null)
        {
            throw new InvalidScenario($"{at}: needs an action, not null.");
        }

        var tick = InRange(Required(action.Tick, $"{at}.tick"), 0, ticks, $"{at}.tick");
        var tool = EnumNamed<BuildAction>(Required(action.Tool, $"{at}.tool"), $"{at}.tool");
        var (fromX, fromY) = Coordinates(Required(action.From, $"{at}.from"), $"{at}.from");
        var (toX, toY) = action.To is null ? (fromX, fromY) : Coordinates(action.To, $"{at}.to");
        var expect = action.Expect is null ? (ExpectedResult?)null : Expectation(action.Expect, $"{at}.expect");

        return new ScenarioAction(index, tick, tool, fromX, fromY, toX, toY, expect);
    }

    private static ScenarioCheck ValidateCheck(CheckDocument? check, int index, int ticks, int width, int height)
    {
        var at = $"checks[{index}]";
        if (check is null)
        {
            throw new InvalidScenario($"{at}: needs a check, not null.");
        }

        var tick = InRange(Required(check.Tick, $"{at}.tick"), 0, ticks, $"{at}.tick");
        var tileConditions = check.Kind is not null || check.Use is not null || check.Development is not null ||
            check.DevelopmentAtLeast is not null || check.DevelopmentAtMost is not null || check.Served is not null || check.Powered is not null;
        var fundsConditions = check.FundsAtLeast is not null || check.FundsAtMost is not null || check.FundsEquals is not null;
        var countConditions = check.EqualTo is not null || check.AtLeast is not null || check.AtMost is not null;

        if (check.Tile is not null && check.Count is not null)
        {
            throw new InvalidScenario($"{at}: checks one subject, so it can't have both tile and count.");
        }

        if (check.Tile is not null)
        {
            Only(tileConditions, fundsConditions || countConditions, at, "a tile check", "kind, use, development, developmentAtLeast, developmentAtMost, served, or powered");
            var (x, y) = Coordinates(check.Tile, $"{at}.tile");
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                throw new InvalidScenario($"{at}.tile: ({x}, {y}) is outside the {width} by {height} map.");
            }

            return new ScenarioCheck
            {
                Index = index,
                Tick = tick,
                Subject = CheckSubject.Tile,
                X = x,
                Y = y,
                Kind = check.Kind is null ? null : EnumNamed<TileKind>(check.Kind, $"{at}.kind"),
                Use = check.Use is null ? null : EnumNamed<TileUse>(check.Use, $"{at}.use"),
                Development = Development(check.Development, $"{at}.development"),
                DevelopmentAtLeast = Development(check.DevelopmentAtLeast, $"{at}.developmentAtLeast"),
                DevelopmentAtMost = Development(check.DevelopmentAtMost, $"{at}.developmentAtMost"),
                Served = check.Served,
                Powered = check.Powered,
            };
        }

        if (check.Count is not null)
        {
            Only(countConditions, tileConditions || fundsConditions, at, "a count check", "equals, atLeast, or atMost");
            var most = width * height;
            return new ScenarioCheck
            {
                Index = index,
                Tick = tick,
                Subject = CheckSubject.Count,
                CountOf = EnumNamed<CountSubject>(check.Count, $"{at}.count"),
                CountEquals = Optional(check.EqualTo, 0, most, $"{at}.equals"),
                CountAtLeast = Optional(check.AtLeast, 0, most, $"{at}.atLeast"),
                CountAtMost = Optional(check.AtMost, 0, most, $"{at}.atMost"),
            };
        }

        Only(fundsConditions, tileConditions || countConditions, at, "a funds check", "fundsAtLeast, fundsAtMost, or fundsEquals; or give it a tile or a count");
        return new ScenarioCheck
        {
            Index = index,
            Tick = tick,
            Subject = CheckSubject.Funds,
            FundsAtLeast = Optional(check.FundsAtLeast, 0, int.MaxValue, $"{at}.fundsAtLeast"),
            FundsAtMost = Optional(check.FundsAtMost, 0, int.MaxValue, $"{at}.fundsAtMost"),
            FundsEquals = Optional(check.FundsEquals, 0, int.MaxValue, $"{at}.fundsEquals"),
        };
    }

    // A check needs at least one condition of its own kind, and none of another kind.
    private static void Only(bool hasOwn, bool hasOther, string at, string what, string allowed)
    {
        if (hasOther)
        {
            throw new InvalidScenario($"{at}: {what} takes only {allowed}.");
        }

        if (!hasOwn)
        {
            throw new InvalidScenario($"{at}: {what} needs at least one of {allowed}.");
        }
    }

    // "Built", "Refused", or a status and a reason, as in "Refused:NotEnoughFunds".
    private static ExpectedResult Expectation(string text, string at)
    {
        var parts = text.Split(':');
        if (parts.Length > 2)
        {
            throw new InvalidScenario($"{at}: needs a status, or a status and a reason such as Refused:NotEnoughFunds, not '{text}'.");
        }

        var status = EnumNamed<BuildStatus>(parts[0], at);
        BuildRefusal? reason = parts.Length == 2 ? EnumNamed<BuildRefusal>(parts[1], at) : null;
        if (status == BuildStatus.Built && reason is not null and not BuildRefusal.None)
        {
            throw new InvalidScenario($"{at}: a stroke that was built has no reason, so '{text}' can never be met.");
        }

        return new ExpectedResult(status, reason);
    }

    private static (int X, int Y) Coordinates(PointDocument point, string at) =>
        (InRange(Required(point.X, $"{at}.x"), MinCoordinate, MaxCoordinate, $"{at}.x"),
         InRange(Required(point.Y, $"{at}.y"), MinCoordinate, MaxCoordinate, $"{at}.y"));

    private static int? Development(int? value, string at) => Optional(value, 0, TileState.MaxDevelopment, at);

    private static int? Optional(int? value, int minimum, int maximum, string at) =>
        value is null ? null : InRange(value.Value, minimum, maximum, at);

    private static int InRange(int value, int minimum, int maximum, string at)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidScenario($"{at}: needs a number from {minimum} to {maximum}, not {value}.");
        }

        return value;
    }

    private static void Limit(int count, string at)
    {
        if (count > MaxEntries)
        {
            throw new InvalidScenario($"{at}: holds {count} entries; the most is {MaxEntries}.");
        }
    }

    // Names are matched exactly, with the casing of the C# names, and a number is not a name.
    private static T EnumNamed<T>(string text, string at)
        where T : struct, Enum
    {
        if (!Enum.TryParse<T>(text, ignoreCase: false, out var value) || !Enum.IsDefined(value) || !char.IsLetter(text.FirstOrDefault()))
        {
            throw new InvalidScenario($"{at}: '{text}' is not one of {string.Join(", ", Enum.GetNames<T>())}.");
        }

        return value;
    }

    private static T Required<T>(T? value, string at)
        where T : struct =>
        value ?? throw new InvalidScenario($"{at}: is required.");

    private static T Required<T>(T? value, string at)
        where T : class =>
        value ?? throw new InvalidScenario($"{at}: is required.");

    // Validation stops at the first problem. The exception never leaves TryParse; it keeps each
    // rule to one line instead of threading a result through every check.
    private sealed class InvalidScenario(string message) : Exception(message);

    /// <summary>The scenario file's document, for its schema.</summary>
    internal static JsonTypeInfo SchemaDocument() => Options.GetTypeInfo(typeof(ScenarioDocument));
}
