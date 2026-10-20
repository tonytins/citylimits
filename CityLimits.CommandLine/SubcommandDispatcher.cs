namespace CityLimits.CommandLine;

/// <summary>Dispatches CLI arguments to registered subcommands, or to the default command when none is named.</summary>
public sealed class SubcommandDispatcher
{
    private const int NameColumn = 12;

    private readonly IReadOnlyList<ISubcommand> _subcommands;
    private readonly DefaultCommand _defaultCommand;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    /// <summary>Creates a new dispatcher.</summary>
    /// <param name="subcommands">The named subcommands; their names must be unique and must not start with '-'.</param>
    /// <param name="defaultCommand">What runs when no subcommand is named.</param>
    /// <param name="output">Where usage and command output go; the console by default.</param>
    /// <param name="error">Where command-line errors go; the console's error stream by default.</param>
    public SubcommandDispatcher(
        IEnumerable<ISubcommand> subcommands,
        DefaultCommand defaultCommand,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        ArgumentNullException.ThrowIfNull(subcommands);
        ArgumentNullException.ThrowIfNull(defaultCommand);

        _subcommands = subcommands.ToList();
        _defaultCommand = defaultCommand;
        _output = output ?? Console.Out;
        _error = error ?? Console.Error;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var subcommand in _subcommands)
        {
            if (string.IsNullOrWhiteSpace(subcommand.Name) || subcommand.Name.StartsWith('-'))
            {
                throw new ArgumentException($"'{subcommand.Name}' is not a usable subcommand name.", nameof(subcommands));
            }

            if (!names.Add(subcommand.Name))
            {
                throw new ArgumentException($"Two subcommands are named '{subcommand.Name}'.", nameof(subcommands));
            }
        }
    }

    /// <summary>Dispatches the given arguments and returns the exit code.</summary>
    public int Dispatch(string[] args)
    {
        var context = SubcommandParser.Parse(args) with { Output = _output, Error = _error };

        if (context.Subcommand is null)
        {
            if (context.HelpRequested)
            {
                WriteUsage(_output);
                return ExitCodes.Success;
            }

            return _defaultCommand.Execute(context);
        }

        var subcommand = _subcommands.FirstOrDefault(s => string.Equals(s.Name, context.Subcommand, StringComparison.OrdinalIgnoreCase));

        if (subcommand is null)
        {
            _error.WriteLine($"Unknown command '{context.Subcommand}'.");
            WriteUsage(_error);
            return ExitCodes.Usage;
        }

        if (context.HelpRequested)
        {
            WriteSubcommandHelp(subcommand, _output);
            return ExitCodes.Success;
        }

        return subcommand.Execute(context);
    }

    /// <summary>Writes the general usage text.</summary>
    public void WriteUsage(TextWriter writer)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CityLimits");
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.AppendLine($"  citylimits [options]             {_defaultCommand.Description}");
        builder.AppendLine("  citylimits <command> [options]");
        builder.AppendLine();
        builder.AppendLine("Options:");
        builder.AppendLine(_defaultCommand.Usage.TrimEnd());
        builder.AppendLine();
        builder.AppendLine("Commands:");
        foreach (var subcommand in _subcommands)
        {
            builder.AppendLine($"  {subcommand.Name.PadRight(NameColumn)} {subcommand.Description}");
        }

        builder.AppendLine();
        builder.AppendLine("Use 'citylimits <command> --help' for more information about a command.");
        writer.WriteLine(builder.ToString().TrimEnd());
    }

    /// <summary>Writes help for a specific subcommand.</summary>
    public static void WriteSubcommandHelp(ISubcommand subcommand, TextWriter writer)
    {
        writer.WriteLine(subcommand.Description);
        writer.WriteLine();
        writer.WriteLine(subcommand.Usage.TrimEnd());
    }
}
