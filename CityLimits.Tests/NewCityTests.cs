using CityLimits.Core.Simulation;

namespace CityLimits.Tests;

public class NewCityTests
{
    [Theory]
    [InlineData("New Dullsville", "", "New Dullsville", null)]
    [InlineData("  Joffburg ", " 2000 ", "Joffburg", 2000)]
    [InlineData("Radial", "-15", "Radial", -15)]
    [InlineData("Kowloon", "2147483647", "Kowloon", int.MaxValue)]
    [InlineData("Deadwood", null, "Deadwood", null)]
    public void TheFormGivesTheNameTrimmedAndTheSeedOrNone(string name, string? seed, string expectedName, int? expectedSeed)
    {
        Assert.True(NewCity.TryRead(name, seed, out var city, out var problem));

        Assert.Null(problem);
        Assert.Equal(expectedName, city!.Name);
        Assert.Equal(expectedSeed, city.Seed);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "12")]
    [InlineData(null, "")]
    [InlineData("A name far too long to fit in the top bar", "")]
    public void ANameACityCantHaveIsRefused(string? name, string seed)
    {
        Assert.False(NewCity.TryRead(name, seed, out var city, out var problem));

        Assert.Null(city);
        Assert.Contains("name", problem);
    }

    [Theory]
    [InlineData("twelve")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("1,000")]
    public void ASeedThatIsntAWholeNumberIsRefused(string seed)
    {
        Assert.False(NewCity.TryRead("Happy Isle", seed, out var city, out var problem));

        Assert.Null(city);
        Assert.Contains("seed", problem);
    }
}
