using System.Globalization;

namespace CityLimits.CommandLine;

/// <summary>The options a command accepts: the ones that take a value, the ones that stand alone, and how many bare arguments may follow.</summary>
/// <param name="Values">Options that take the next argument as their value, such as <c>--seed</c>.</param>
/// <param name="Flags">Options that take no value, such as <c>--json</c>.</param>
/// <param name="MaxPositionals">How many arguments that are not options the command accepts.</param>
public sealed record OptionSpec(IReadOnlyCollection<string> Values, IReadOnlyCollection<string> Flags, int MaxPositionals = 0);

/// <summary>The options a command was given, read strictly against what it accepts.</summary>
/// <remarks>
/// An option the command does not know, an option given twice, an option missing its value,
/// and an argument the command has no room for are all refused, so a typo is never mistaken
/// for a default.
/// </remarks>
public sealed class CommandOptions
{
    private readonly Dictionary<string, string> _values;
    private readonly HashSet<string> _flags;

    private CommandOptions(Dictionary<string, string> values, HashSet<string> flags, List<string> positionals)
    {
        _values = values;
        _flags = flags;
        Positionals = positionals;
    }

    /// <summary>The arguments that are not options, in the order they were given.</summary>
    public IReadOnlyList<string> Positionals { get; }

    /// <summary>Reads the arguments against the spec, and reports the first one that makes no sense.</summary>
    public static bool TryParse(IReadOnlyList<string> args, OptionSpec spec, out CommandOptions options, out string error)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var positionals = new List<string>();
        options = new CommandOptions(values, flags, positionals);
        error = string.Empty;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (!IsOption(arg))
            {
                if (positionals.Count >= spec.MaxPositionals)
                {
                    error = $"Unexpected argument '{arg}'.";
                    return false;
                }

                positionals.Add(arg);
                continue;
            }

            if (spec.Flags.Contains(arg))
            {
                if (!flags.Add(arg))
                {
                    error = $"{arg} was given more than once.";
                    return false;
                }

                continue;
            }

            if (!spec.Values.Contains(arg))
            {
                error = $"Unknown option '{arg}'.";
                return false;
            }

            // A value may be negative, so only a long option stands in for a missing one.
            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"{arg} needs a value.";
                return false;
            }

            if (!values.TryAdd(arg, args[i + 1]))
            {
                error = $"{arg} was given more than once.";
                return false;
            }

            i++;
        }

        return true;
    }

    /// <summary>Whether the flag was given.</summary>
    public bool Has(string flag) => _flags.Contains(flag);

    /// <summary>The value given for the option, or null when it was not given.</summary>
    public string? ValueOf(string option) => _values.GetValueOrDefault(option);

    /// <summary>Reads the option as a whole number in a range, or takes the default when it was not given.</summary>
    public bool TryGetInt(string option, int defaultValue, int minimum, int maximum, out int value, out string error)
    {
        value = defaultValue;
        error = string.Empty;

        if (!_values.TryGetValue(option, out var text))
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            error = $"{option} needs a whole number, not '{text}'.";
            return false;
        }

        if (parsed < minimum || parsed > maximum)
        {
            error = $"{option} needs a number from {minimum} to {maximum}, not {parsed}.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool IsOption(string arg) => arg.Length > 1 && arg[0] == '-' && !char.IsDigit(arg[1]);
}
