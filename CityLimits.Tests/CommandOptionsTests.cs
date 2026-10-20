using CityLimits.CommandLine;

namespace CityLimits.Tests;

public class CommandOptionsTests
{
    private static readonly OptionSpec Spec = new(["--seed", "--ticks"], ["--json"], MaxPositionals: 1);

    private static CommandOptions Parse(params string[] args)
    {
        Assert.True(CommandOptions.TryParse(args, Spec, out var options, out var error), error);
        return options;
    }

    private static string Refuse(params string[] args)
    {
        Assert.False(CommandOptions.TryParse(args, Spec, out _, out var error));
        return error;
    }

    [Fact]
    public void NoArgumentsMeanNoOptions()
    {
        var options = Parse();

        Assert.False(options.Has("--json"));
        Assert.Null(options.ValueOf("--seed"));
        Assert.Empty(options.Positionals);
    }

    [Fact]
    public void ValuesFlagsAndPositionalsAreRead()
    {
        var options = Parse("--seed", "42", "town", "--json");

        Assert.Equal("42", options.ValueOf("--seed"));
        Assert.True(options.Has("--json"));
        Assert.Equal(["town"], options.Positionals);
    }

    [Fact]
    public void AnUnknownOptionIsRefused() =>
        Assert.Equal("Unknown option '--seeed'.", Refuse("--seeed", "42"));

    [Fact]
    public void AnOptionWithNoValueIsRefused() =>
        Assert.Equal("--ticks needs a value.", Refuse("--ticks"));

    [Fact]
    public void AnotherOptionDoesNotCountAsAValue() =>
        Assert.Equal("--ticks needs a value.", Refuse("--ticks", "--json"));

    [Fact]
    public void ANegativeNumberCountsAsAValue() =>
        Assert.Equal("-5", Parse("--seed", "-5").ValueOf("--seed"));

    [Theory]
    [InlineData("--seed", "1", "--seed", "2")]
    [InlineData("--json", "--json")]
    public void AnOptionGivenTwiceIsRefused(params string[] args) =>
        Assert.EndsWith("was given more than once.", Refuse(args));

    [Fact]
    public void AnArgumentWithNoRoomIsRefused() =>
        Assert.Equal("Unexpected argument 'two'.", Refuse("one", "two"));

    [Fact]
    public void AMissingNumberTakesTheDefault()
    {
        Assert.True(Parse().TryGetInt("--ticks", 100, 0, 1000, out var ticks, out _));

        Assert.Equal(100, ticks);
    }

    [Fact]
    public void ANumberInRangeIsRead()
    {
        Assert.True(Parse("--ticks", "12").TryGetInt("--ticks", 100, 0, 1000, out var ticks, out _));

        Assert.Equal(12, ticks);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public void TextThatIsNotAWholeNumberIsRefused(string text)
    {
        Assert.False(Parse("--ticks", text).TryGetInt("--ticks", 100, 0, 1000, out var ticks, out var error));

        Assert.Equal($"--ticks needs a whole number, not '{text}'.", error);
        Assert.Equal(100, ticks);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1001")]
    public void ANumberOutOfRangeIsRefused(string text)
    {
        Assert.False(Parse("--ticks", text).TryGetInt("--ticks", 100, 0, 1000, out _, out var error));

        Assert.Equal($"--ticks needs a number from 0 to 1000, not {text}.", error);
    }
}
