using CityLimits.CommandLine;

namespace CityLimits.Tests;

public class SubcommandDispatcherTests
{
    private sealed class RecordingSubcommand(string name, bool supportsHeadless = true) : ISubcommand
    {
        public SubcommandContext? Received { get; private set; }

        public string Name => name;

        public string Description => $"The {name} command";

        public string Usage => $"Usage: citylimits {name}";

        public bool SupportsHeadless => supportsHeadless;

        public int Execute(SubcommandContext context)
        {
            Received = context;
            return 7;
        }
    }

    private readonly RecordingSubcommand _scenario = new("scenario");
    private readonly RecordingSubcommand _windowOnly = new("editor", supportsHeadless: false);
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private SubcommandContext? _defaultReceived;

    private SubcommandDispatcher CreateDispatcher(bool defaultSupportsHeadless = true) =>
        new(
            [_scenario, _windowOnly],
            new DefaultCommand(
                "Start the game",
                "  --seed <seed>",
                context =>
                {
                    _defaultReceived = context;
                    return 5;
                },
                defaultSupportsHeadless),
            _output,
            _error);

    [Fact]
    public void NoArgumentsRunTheDefaultCommand()
    {
        Assert.Equal(5, CreateDispatcher().Dispatch([]));

        Assert.NotNull(_defaultReceived);
        Assert.Null(_defaultReceived.Subcommand);
        Assert.Null(_scenario.Received);
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
        Assert.Equal(7, CreateDispatcher().Dispatch(["Scenario", "--ticks", "3"]));

        Assert.Equal(["--ticks", "3"], _scenario.Received!.Args);
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
        Assert.Contains("scenario", usage);
        Assert.Contains("--headless", usage);
        Assert.DoesNotContain("play", usage);
        Assert.Null(_defaultReceived);
    }

    [Fact]
    public void HelpAfterACommandPrintsItsUsageAndRunsNothing()
    {
        Assert.Equal(ExitCodes.Success, CreateDispatcher().Dispatch(["scenario", "--help"]));

        Assert.Contains("Usage: citylimits scenario", _output.ToString());
        Assert.Null(_scenario.Received);
    }

    [Fact]
    public void HelpIsNotPassedOnAsAnArgument() =>
        Assert.Empty(SubcommandParser.Parse(["scenario", "-h"]).Args);

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
                [new RecordingSubcommand("scenario"), new RecordingSubcommand("SCENARIO")],
                new DefaultCommand("", "", _ => 0)));

    [Theory]
    [InlineData("--headless", "--seed", "1")]
    [InlineData("--seed", "1", "--headless")]
    public void HeadlessAnywhereRunsTheDefaultCommandWithNoWindow(params string[] args)
    {
        CreateDispatcher().Dispatch(args);

        Assert.True(_defaultReceived!.Headless);
        Assert.Equal(["--seed", "1"], _defaultReceived.Args);
    }

    [Fact]
    public void WithoutTheFlagTheCommandIsNotHeadless()
    {
        CreateDispatcher().Dispatch(["scenario", "town"]);

        Assert.False(_scenario.Received!.Headless);
    }

    [Fact]
    public void HeadlessAfterACommandReachesIt()
    {
        CreateDispatcher().Dispatch(["scenario", "town", "--headless"]);

        Assert.True(_scenario.Received!.Headless);
        Assert.Equal(["town"], _scenario.Received.Args);
    }

    [Fact]
    public void ACommandThatNeedsAWindowRefusesHeadless()
    {
        Assert.Equal(ExitCodes.Usage, CreateDispatcher().Dispatch(["editor", "--headless"]));

        Assert.Equal("'citylimits editor' cannot run with --headless.", _error.ToString().Trim());
        Assert.Null(_windowOnly.Received);
    }

    [Fact]
    public void ADefaultThatNeedsAWindowRefusesHeadless()
    {
        Assert.Equal(ExitCodes.Usage, CreateDispatcher(defaultSupportsHeadless: false).Dispatch(["--headless"]));

        Assert.Null(_defaultReceived);
    }

    [Fact]
    public void HelpWinsOverHeadless()
    {
        Assert.Equal(ExitCodes.Success, CreateDispatcher().Dispatch(["--headless", "--help"]));

        Assert.Null(_defaultReceived);
    }
}
