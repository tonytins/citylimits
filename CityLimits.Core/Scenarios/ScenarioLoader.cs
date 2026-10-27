using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CityLimits.Core.Simulation;

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
    /// The newest scenario format this version reads. Format 2 names its times as the game's dates, a
    /// month and a week of it, rather than ticks; it starts a city in a year of its own or from a saved
    /// city, and adds goals and disasters on dates. 2.1 added the Stadium, Seaport, and Airport tools and
    /// counts, and 2.2 a disaster that repeats. The built-in scenarios are written in 2.0, since none of
    /// them needs what came after.
    /// </summary>
    public static readonly FormatVersion Format = new(2, 2);

    /// <summary>
    /// The newest format 1 this version still reads, its times in ticks: 1.1 added the Rail tool
    /// and the Rail count, 1.2 the <c>disasters</c> switch and the OnFire refusal, and 1.3 the Flooded
    /// refusal, with floods and earthquakes among the disasters the switch lets happen. A file
    /// written as a whole number, such as <c>1</c>, is 1.0.
    /// </summary>
    public static readonly FormatVersion LegacyFormat = new(1, 3);

    /// <summary>The longest a goal's own words may be.</summary>
    public const int MaxGoalTextLength = 120;

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

    // The traffic average can't pass the most a block holds, 240, times the year's 2.4.
    private const int MostTrafficAverage = 576;
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

    // The format alone is read first, with every other field let by, to tell which document the file is.
    private static readonly JsonSerializerOptions FormatOptions = new(Options)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    /// <summary>
    /// Reads a scenario file, refusing one that is too big, can't be read, or isn't valid. A saved
    /// city it starts from is read from beside it.
    /// </summary>
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

            return TryParse(File.ReadAllText(path), Path.GetDirectoryName(Path.GetFullPath(path)), out scenario, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = $"The scenario file '{path}' could not be read: {ex.Message}";
            return false;
        }
    }

    /// <summary>Reads a scenario from its JSON text, and checks it; one that starts from a saved city is refused, with no folder to find it in.</summary>
    public static bool TryParse(string json, [NotNullWhen(true)] out Scenario? scenario, out string error) =>
        TryParse(json, folder: null, out scenario, out error);

    /// <summary>Reads a scenario from its JSON text, and checks it, reading a saved city it starts from in the given folder.</summary>
    public static bool TryParse(string json, string? folder, [NotNullWhen(true)] out Scenario? scenario, out string error)
    {
        ArgumentNullException.ThrowIfNull(json);
        scenario = null;
        error = string.Empty;

        if (json.Length > MaxFileBytes)
        {
            error = $"The scenario is {json.Length} characters long; the most is {MaxFileBytes}.";
            return false;
        }

        try
        {
            if (JsonSerializer.Deserialize(json, (JsonTypeInfo<FormatDocument>)FormatOptions.GetTypeInfo(typeof(FormatDocument))) is not { } header)
            {
                error = "The scenario is empty.";
                return false;
            }

            var format = header.Format ?? throw new InvalidScenario("format: is required.");
            if (format.Major == LegacyFormat.Major)
            {
                if (format.ProblemReadingWith(LegacyFormat) is { } legacyProblem)
                {
                    throw new InvalidScenario($"format: {legacyProblem}");
                }

                scenario = Validate(JsonSerializer.Deserialize(json, (JsonTypeInfo<ScenarioDocument>)Options.GetTypeInfo(typeof(ScenarioDocument)))!);
                return true;
            }

            if (format.ProblemReadingWith(Format) is { } problem)
            {
                throw new InvalidScenario($"format: {problem}");
            }

            scenario = Validate(JsonSerializer.Deserialize(json, (JsonTypeInfo<ScenarioDocument2>)Options.GetTypeInfo(typeof(ScenarioDocument2)))!, folder);
            return true;
        }
        catch (JsonException ex)
        {
            var where = string.IsNullOrEmpty(ex.Path) || ex.Path == "$" ? "the file" : ex.Path.TrimStart('$', '.');
            error = $"{where}: {Describe(ex)}";
            return false;
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

    // Format 1: times in ticks from the start of January 1900.
    private static Scenario Validate(ScenarioDocument document)
    {
        var (name, description) = NameAndDescription(document.Name, document.Description);
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

    // Format 2: times as the game's dates, from a city made from a seed in a year of its own, or
    // from a saved city beside the scenario, which brings its own seed, map, money, and date.
    private static Scenario Validate(ScenarioDocument2 document, string? folder)
    {
        var (name, description) = NameAndDescription(document.Name, document.Description);

        int seed, width, height, funds, startYear;
        long startStep;
        string? cityFile = null;
        if (document.City is { } city)
        {
            foreach (var (given, field) in new[] { (document.Seed is not null, "seed"), (document.Map is not null, "map"), (document.Funds is not null, "funds"), (document.Start is not null, "start") })
            {
                if (given)
                {
                    throw new InvalidScenario($"{field}: a scenario that starts from a saved city takes its seed, map, money, and date from the city, so it can't also have {field}.");
                }
            }

            cityFile = CityPath(city, folder);
            if (!CityFiles.TryRead(cityFile, out var saved, out var problem, out _))
            {
                throw new InvalidScenario($"city: '{city}' couldn't be opened: {problem}");
            }

            (seed, width, height, funds, startYear, startStep) = (saved.Seed, saved.Map.Width, saved.Map.Height, saved.Treasury.Funds, saved.Date.Year, saved.TickCount);
        }
        else
        {
            seed = Required(document.Seed, "seed");
            width = document.Map is null ? MapSize.DefaultWidth : InRange(Required(document.Map.Width, "map.width"), MinMapSide, MaxMapSide, "map.width");
            height = document.Map is null ? MapSize.DefaultHeight : InRange(Required(document.Map.Height, "map.height"), MinMapSide, MaxMapSide, "map.height");
            funds = InRange(document.Funds ?? Treasury.StartingFunds, 0, MaxFunds, "funds");
            startYear = InRange(document.Start ?? CityCalendar.StartYear, CityCalendar.StartYear, CityCalendar.LatestStartYear, "start");
            startStep = (long)(startYear - CityCalendar.StartYear) * CityCalendar.StepsPerYear;
        }

        var start = ScenarioDate.AtCount((startStep + CityCalendar.StepsPerCount - 1) / CityCalendar.StepsPerCount);
        var ends = Date(Required(document.Ends, "ends"), null, "ends");
        var length = ends.Step - startStep;
        if (length < 0)
        {
            throw new InvalidScenario($"ends: {ends} is before the scenario starts, in {start}.");
        }

        if (length > Scenario.MaxTicks)
        {
            throw new InvalidScenario($"ends: {ends} is {length} ticks from the start, in {start}; a scenario may run at most {Scenario.MaxTicks}, about 130 years.");
        }

        var ticks = (int)length;
        int TickAt(string? at, int? week, string where)
        {
            var date = Date(Required(at, $"{where}.at"), week, $"{where}.at");
            var tick = date.Step - startStep;
            if (tick < 0 || tick > ticks)
            {
                throw new InvalidScenario($"{where}.at: {date} is outside the scenario, which runs from {start} to {ends}.");
            }

            return (int)tick;
        }

        var actions = new List<ScenarioAction>();
        var actionDocuments = document.Actions ?? [];
        Limit(actionDocuments.Count, "actions");
        for (var i = 0; i < actionDocuments.Count; i++)
        {
            var action = actionDocuments[i] ?? throw new InvalidScenario($"actions[{i}]: needs an action, not null.");
            actions.Add(Stroke(action, $"actions[{i}]", i, TickAt(action.At, action.Week, $"actions[{i}]")));
        }

        var checks = new List<ScenarioCheck>();
        var checkDocuments = document.Checks ?? [];
        Limit(checkDocuments.Count, "checks");
        for (var i = 0; i < checkDocuments.Count; i++)
        {
            var check = checkDocuments[i] ?? throw new InvalidScenario($"checks[{i}]: needs a check, not null.");
            checks.Add(Conditions(check, $"checks[{i}]", i, TickAt(check.At, check.Week, $"checks[{i}]"), width, height));
        }

        var goals = new List<ScenarioGoal>();
        var goalDocuments = document.Goals ?? [];
        Limit(goalDocuments.Count, "goals");
        for (var i = 0; i < goalDocuments.Count; i++)
        {
            goals.Add(Goal(goalDocuments[i], i));
        }

        var events = new List<ScenarioEvent>();
        var eventDocuments = document.Events ?? [];
        Limit(eventDocuments.Count, "events");
        for (var i = 0; i < eventDocuments.Count; i++)
        {
            var happening = eventDocuments[i] ?? throw new InvalidScenario($"events[{i}]: needs an event, not null.");
            var disaster = EnumNamed<Disaster>(Required(happening.Disaster, $"events[{i}].disaster"), $"events[{i}].disaster");
            var tick = TickAt(happening.At, happening.Week, $"events[{i}]");
            events.Add(new ScenarioEvent(i, tick, disaster, Repeat(happening.Repeat, $"events[{i}]", tick, ticks, ends)));
        }

        return new Scenario(name, description, seed, width, height, funds, ticks, actions, checks, document.Disasters ?? false)
        {
            StartYear = startYear,
            CityFile = cityFile,
            StartStep = startStep,
            Goals = goals,
            Events = events,
        };
    }

    // A repeat strikes every so many weeks, as many times as it says, the first included; its last
    // strike must come by the scenario's end. Both are counted no further than a scenario can run,
    // so working out the last strike can't overflow.
    private static EventRepeat? Repeat(RepeatDocument? repeat, string where, int first, int ticks, ScenarioDate ends)
    {
        if (repeat is null)
        {
            return null;
        }

        const int MostWeeks = Scenario.MaxTicks / CityCalendar.StepsPerCount;
        var weeks = InRange(Required(repeat.Weeks, $"{where}.repeat.weeks"), 1, MostWeeks, $"{where}.repeat.weeks");
        var times = InRange(Required(repeat.Times, $"{where}.repeat.times"), 1, MostWeeks, $"{where}.repeat.times");
        var last = first + ((long)(times - 1) * weeks * CityCalendar.StepsPerCount);
        if (last > ticks)
        {
            throw new InvalidScenario($"{where}.repeat: striking {times} times, {weeks} weeks apart, runs past the scenario's end, {ends}.");
        }

        return new EventRepeat(weeks, times);
    }

    private static (string Name, string? Description) NameAndDescription(string? givenName, string? givenDescription)
    {
        var name = Required(givenName, "name").Trim();
        if (name.Length is 0 or > MaxNameLength)
        {
            throw new InvalidScenario($"name: needs 1 to {MaxNameLength} characters, not {name.Length}.");
        }

        var description = givenDescription?.Trim();
        if (description is { Length: > MaxDescriptionLength })
        {
            throw new InvalidScenario($"description: needs at most {MaxDescriptionLength} characters, not {description.Length}.");
        }

        return (name, description);
    }

    // A month written YYYY-MM, from 1900 to 9999, and the week of it, 1 to 4, the first when none is given.
    private static ScenarioDate Date(string text, int? week, string at)
    {
        if (week is { } given && given is < 1 or > ScenarioDate.WeeksPerMonth)
        {
            throw new InvalidScenario($"{at[..at.LastIndexOf('.')]}.week: needs a number from 1 to {ScenarioDate.WeeksPerMonth}, not {given}.");
        }

        return ScenarioDate.TryParse(text, week ?? 1, out var date)
            ? date.Value
            : throw new InvalidScenario($"{at}: needs a month written YYYY-MM, from {CityCalendar.StartYear}-01 to {CityCalendar.LatestStartYear}-12, not '{text}'.");
    }

    // A saved city's file, named as a path inside the scenario's folder: no folder above it, and
    // no other drive, so a scenario can't read a file it shouldn't.
    private static string CityPath(string city, string? folder)
    {
        if (folder is null)
        {
            throw new InvalidScenario("city: only a scenario read from a file can start from a saved city, which is read from beside it.");
        }

        var extension = Path.GetExtension(city);
        if (!extension.Equals(FlatCityFormat.Extension, StringComparison.OrdinalIgnoreCase) && !extension.Equals(BinaryCityFormat.Extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidScenario($"city: needs a {FlatCityFormat.Extension} or a {BinaryCityFormat.Extension} file, not '{city}'.");
        }

        var root = Path.GetFullPath(folder);
        var full = Path.GetFullPath(Path.Combine(root, city));
        if (Path.IsPathRooted(city) || !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidScenario($"city: '{city}' needs to be a file beside the scenario, or in a folder beside it.");
        }

        return full;
    }

    private static ScenarioGoal Goal(GoalDocument? goal, int index)
    {
        var at = $"goals[{index}]";
        if (goal is null)
        {
            throw new InvalidScenario($"{at}: needs a goal, not null.");
        }

        var measure = EnumNamed<GoalMeasure>(Required(goal.Measure, $"{at}.measure"), $"{at}.measure");
        var (lowest, highest) = measure switch
        {
            GoalMeasure.Funds => (-MaxFunds * 1000, MaxFunds * 1000),
            GoalMeasure.Pollution => (0, CityMaps.MostPollution),
            GoalMeasure.Crime => (0, CityMaps.MostCrime),
            GoalMeasure.Traffic => (0, MostTrafficAverage),
            _ => (0, int.MaxValue),
        };
        var atLeast = Optional(goal.AtLeast, lowest, highest, $"{at}.atLeast");
        var atMost = Optional(goal.AtMost, lowest, highest, $"{at}.atMost");
        if (atLeast is null && atMost is null)
        {
            throw new InvalidScenario($"{at}: needs atLeast, atMost, or both.");
        }

        if (atLeast > atMost)
        {
            throw new InvalidScenario($"{at}: atLeast {atLeast} is more than atMost {atMost}, so it can never be met.");
        }

        var text = goal.Text?.Trim();
        if (text is { Length: 0 or > MaxGoalTextLength })
        {
            throw new InvalidScenario($"{at}.text: needs 1 to {MaxGoalTextLength} characters, not {text.Length}.");
        }

        return new ScenarioGoal(index, measure, atLeast, atMost, text ?? GoalText(measure, atLeast, atMost));
    }

    // How a goal reads to a player when its file gives no words of its own.
    internal static string GoalText(GoalMeasure measure, int? atLeast, int? atMost)
    {
        static string Number(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        static string Money(int value) => value < 0 ? $"-${Number(-value)}" : $"${Number(value)}";
        var (what, show) = measure switch
        {
            GoalMeasure.Population => ("a population", (Func<int, string>)Number),
            GoalMeasure.Funds => ("funds", Money),
            GoalMeasure.Pollution => ("average pollution", Number),
            GoalMeasure.Crime => ("average crime", Number),
            _ => ("a traffic average", Number),
        };

        return (atLeast, atMost) switch
        {
            ({ } low, { } high) => $"Have {what} from {show(low)} to {show(high)}",
            ({ } low, null) => $"Have {what} of at least {show(low)}",
            _ => $"Keep {what} at most {show(atMost!.Value)}",
        };
    }

    private static ScenarioAction ValidateAction(ActionDocument? action, int index, int ticks)
    {
        var at = $"actions[{index}]";
        if (action is null)
        {
            throw new InvalidScenario($"{at}: needs an action, not null.");
        }

        return Stroke(action, at, index, InRange(Required(action.Tick, $"{at}.tick"), 0, ticks, $"{at}.tick"));
    }

    // What an action does, whichever format named its time.
    private static ScenarioAction Stroke(ActionFields action, string at, int index, int tick)
    {
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

        return Conditions(check, at, index, InRange(Required(check.Tick, $"{at}.tick"), 0, ticks, $"{at}.tick"), width, height);
    }

    // What a check looks at, whichever format named its time.
    private static ScenarioCheck Conditions(CheckFields check, string at, int index, int tick, int width, int height)
    {
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
    internal static JsonTypeInfo SchemaDocument() => Options.GetTypeInfo(typeof(ScenarioDocument2));
}
