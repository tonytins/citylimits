using CityLimits.CommandLine;

namespace CityLimits.Tests;

public class SubcommandDispatcherTests
{
    private sealed class RecordingSubcommand(string name) : ISubcommand
    {
        public SubcommandContext? Received { get; private set; }

        public string Name => name;

        public string Description => $"The {name} command";

        public string Usage => $"Usage: citylimits {name}";

        public int Execute(SubcommandContext context)
        {
            Received = context;
            return 7;
        }
    }

    private readonly RecordingSubcommand _headless = new("headless");
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private SubcommandContext? _defaultReceived;

    private SubcommandDispatcher CreateDispatcher() =>
        new(
            [_headless],
            new DefaultCommand("Start the game", "  --seed <seed>", context =>
            {
                _defaultReceived = context;
                return 5;
            }),
            _output,
            _error);

    [Fact]
    public void NoArgumentsRunTheDefaultCommand()
    {
        Assert.Equal(5, CreateDispatcher().Dispatch([]));

        Assert.NotNull(_defaultReceived);
        Assert.Null(_defaultReceived.Subcommand);
        Assert.Null(_headless.Received);
    }

    [Fact]
    public void LeadingOptionsGoToTheDefaultCommand()
    {
        CreateDispatcher().Dispatch(["--seed", "42"]);

        Assert.Equal(["--seed", "42"], _defaultReceived!.Args);
    }

    [Fact]
    public void ANamedSubcommandGetsTheArgumentsAfterItsName()
    {
        Assert.Equal(7, CreateDispatcher().Dispatch(["Headless", "--ticks", "3"]));

        Assert.Equal(["--ticks", "3"], _headless.Received!.Args);
        Assert.Null(_defaultReceived);
    }

    [Fact]
    public void AnUnknownCommandIsRefused()
    {
        Assert.Equal(ExitCodes.Usage, CreateDispatcher().Dispatch(["play"]));

        Assert.StartsWith("Unknown command 'play'.", _error.ToString());
        Assert.Null(_defaultReceived);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--seed", "1", "--help")]
    public void HelpWithNoCommandPrintsTheUsageAndRunsNothing(params string[] args)
    {
        Assert.Equal(ExitCodes.Success, CreateDispatcher().Dispatch(args));

        var usage = _output.ToString();
        Assert.Contains("Start the game", usage);
        Assert.Contains("--seed <seed>", usage);
        Assert.Contains("headless", usage);
        Assert.DoesNotContain("play", usage);
        Assert.Null(_defaultReceived);
    }

    [Fact]
    public void HelpAfterACommandPrintsItsUsageAndRunsNothing()
    {
        Assert.Equal(ExitCodes.Success, CreateDispatcher().Dispatch(["headless", "--help"]));

        Assert.Contains("Usage: citylimits headless", _output.ToString());
        Assert.Null(_headless.Received);
    }

    [Fact]
    public void HelpIsNotPassedOnAsAnArgument() =>
        Assert.Empty(SubcommandParser.Parse(["headless", "-h"]).Args);

    [Theory]
    [InlineData("")]
    [InlineData("--headless")]
    public void AnUnusableNameIsRejected(string name) =>
        Assert.Throws<ArgumentException>(() =>
            new SubcommandDispatcher([new RecordingSubcommand(name)], new DefaultCommand("", "", _ => 0)));

    [Fact]
    public void TwoSubcommandsCannotShareAName() =>
        Assert.Throws<ArgumentException>(() =>
            new SubcommandDispatcher(
                [new RecordingSubcommand("headless"), new RecordingSubcommand("HEADLESS")],
                new DefaultCommand("", "", _ => 0)));
}
