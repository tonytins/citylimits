using System.Text;

namespace CityLimits.CommandLine;

/// <summary>Dispatches CLI arguments to registered subcommands.</summary>
public sealed class SubcommandDispatcher
{
    private readonly IReadOnlyList<ISubcommand> _subcommands;
    private readonly ISubcommand? _defaultSubcommand;

    /// <summary>Creates a new dispatcher.</summary>
    public SubcommandDispatcher(IEnumerable<ISubcommand> subcommands, ISubcommand? defaultSubcommand = null)
    {
        _subcommands = subcommands.ToList();
        _defaultSubcommand = defaultSubcommand;
    }

    /// <summary>Dispatches the given arguments and returns the exit code.</summary>
    public int Dispatch(string[] args)
    {
        var parseResult = SubcommandParser.Parse(args);

        if (!parseResult.Success || parseResult.Context is null)
        {
            if (parseResult.Error is not null)
            {
                Console.Error.WriteLine(parseResult.Error);
            }
            WriteUsage();
            return 2;
        }

        var context = parseResult.Context;

        if (context.HelpRequested && context.Subcommand is null)
        {
            WriteUsage();
            return 0;
        }

        if (context.Subcommand is null)
        {
            if (_defaultSubcommand is not null)
            {
                return _defaultSubcommand.Execute(context with { Subcommand = _defaultSubcommand.Name });
            }

            WriteUsage();
            return 0;
        }

        var subcommand = _subcommands.FirstOrDefault(s => string.Equals(s.Name, context.Subcommand, StringComparison.OrdinalIgnoreCase));

        if (subcommand is null)
        {
            Console.Error.WriteLine($"Unknown command '{context.Subcommand}'.");
            WriteUsage();
            return 2;
        }

        if (context.HelpRequested)
        {
            WriteSubcommandHelp(subcommand);
            return 0;
        }

        return subcommand.Execute(context);
    }

    /// <summary>Writes the general usage text to stdout.</summary>
    public void WriteUsage()
    {
        var builder = new StringBuilder();
        builder.AppendLine("CityLimits");
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.AppendLine("  citylimits [command] [options]");
        builder.AppendLine();

        if (_defaultSubcommand is not null)
        {
            builder.AppendLine("Commands:");
            builder.AppendLine($"  {_defaultSubcommand.Name.PadRight(12)} {_defaultSubcommand.Description}");
            foreach (var subcommand in _subcommands.Where(s => s != _defaultSubcommand))
            {
                builder.AppendLine($"  {subcommand.Name.PadRight(12)} {subcommand.Description}");
            }
        }
        else
        {
            builder.AppendLine("Commands:");
            foreach (var subcommand in _subcommands)
            {
                builder.AppendLine($"  {subcommand.Name.PadRight(12)} {subcommand.Description}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Use 'citylimits <command> --help' for more information about a command.");
        Console.WriteLine(builder.ToString().TrimEnd());
    }

    /// <summary>Writes help for a specific subcommand.</summary>
    public void WriteSubcommandHelp(ISubcommand subcommand)
    {
        Console.WriteLine($"Usage: citylimits {subcommand.Name} [options]");
        Console.WriteLine();
        Console.WriteLine(subcommand.Description);
    }
}
