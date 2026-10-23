using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class PollutionAndCrimeTests
{
    // Eleven homes in a row with a coal plant at the end, on a strip of a map.
    private static CitySimulation PollutedStrip()
    {
        var city = new CitySimulation(TileMap.FromTerrain(40, 6, new TileKind[240]), 1);
        var zones = new BuildStroke(BuildAction.ZoneResidential, 1, 1);
        zones.DragTo(33, 1);
        city.Builder.Apply(zones);
        city.Builder.Apply(BuildAction.CoalPowerPlant, 34, 1);
        return city;
    }

    // Homes in two far corners of a big map, so the centre of mass falls between them and every
    // block they stand on is worth the least land value can be from distance, 8.
    private static CitySimulation FarCorners()
    {
        var city = new CitySimulation(TileMap.FromTerrain(128, 128, new TileKind[128 * 128]), 1);
        city.Builder.Apply(BuildAction.ZoneResidential, 1, 1);
        city.Builder.Apply(BuildAction.ZoneResidential, 126, 126);
        return city;
    }

    private static void RunCounts(CitySimulation city, int counts)
    {
        for (var i = 0; i < counts * CityCalendar.StepsPerCount; i++)
        {
            city.Tick();
        }
    }

    [Fact]
    public void APollutedCityHearsOfIt()
    {
        var city = PollutedStrip();
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCounts(city, 64);

        Assert.True(city.Maps.PollutionAverage > 60, $"average {city.Maps.PollutionAverage}");
        Assert.Contains(CityMessage.HighPollution, heard);
    }

    [Fact]
    public void ACityWithNoIndustryOrPlantHearsNothingOfPollution()
    {
        var city = FarCorners();
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCounts(city, 64);

        Assert.Equal(0, city.Maps.PollutionAverage);
        Assert.DoesNotContain(CityMessage.HighPollution, heard);
    }

    [Fact]
    public void ACityOfCheapLandHearsOfCrime()
    {
        var city = FarCorners();
        var heard = new List<CityMessage>();
        city.MessageSent += heard.Add;

        RunCounts(city, 64);

        Assert.Equal(120, city.Maps.CrimeAt(1, 1));
        Assert.True(city.Maps.CrimeAverage > 100, $"average {city.Maps.CrimeAverage}");
        Assert.Contains(CityMessage.HighCrime, heard);
    }

    [Fact]
    public void AStationOnTheCheapLandKeepsCrimeDown()
    {
        var policed = FarCorners();
        policed.Builder.Apply(BuildAction.PoliceStation, 5, 1);

        RunCounts(policed, 4);

        Assert.True(policed.Maps.CrimeAt(1, 1) < 120, $"crime {policed.Maps.CrimeAt(1, 1)}");
    }

    [Fact]
    public void PollutionAndCrimeAreSavedAndLoaded()
    {
        var city = PollutedStrip();
        RunCounts(city, 4);
        Assert.True(city.Maps.PollutionAverage > 0);

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);

        foreach (var loaded in new[] { flat, binary })
        {
            Assert.Equal(city.Maps.PollutionAverage, loaded.Maps.PollutionAverage);
            Assert.Equal(city.Maps.CrimeAverage, loaded.Maps.CrimeAverage);
            Assert.All(Enumerable.Range(0, 40), x => Assert.Equal(city.Maps.PollutionAt(x, 1), loaded.Maps.PollutionAt(x, 1)));
            Assert.All(Enumerable.Range(0, 40), x => Assert.Equal(city.Maps.CrimeAt(x, 1), loaded.Maps.CrimeAt(x, 1)));
        }
    }

    [Fact]
    public void ASaveFromBeforePollutionLoadsWithNoneAndWorksItOutWithinACycle()
    {
        var city = PollutedStrip();
        RunCounts(city, 4);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        save["format"] = "1.4";
        save.Remove("pollution");
        save.Remove("crime");

        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var upgraded, out var problem), problem);

        Assert.Equal(0, upgraded.Maps.PollutionAverage);
        Assert.All(Enumerable.Range(0, 40), x => Assert.Equal(0, upgraded.Maps.PollutionAt(x, 1)));
        Assert.Equal("1.5", JsonNode.Parse(FlatCityFormat.Write(upgraded))!["format"]!.GetValue<string>());

        RunCounts(upgraded, 1);
        Assert.Equal(city.Maps.PollutionAt(35, 2), upgraded.Maps.PollutionAt(35, 2));
    }

    [Theory]
    [InlineData("pollution")]
    [InlineData("crime")]
    public void ASave15WithoutPollutionOrCrimeIsRefused(string part)
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(PollutedStrip()))!.AsObject();
        save.Remove(part);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.StartsWith($"{part}: missing", problem);
    }

    [Fact]
    public void PollutionOutOfRangeIsRefusedAndSaysWhere()
    {
        var save = JsonNode.Parse(FlatCityFormat.Write(PollutedStrip()))!.AsObject();
        save["pollution"]!["blocks"]![3] = 256;

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.StartsWith("pollution.blocks[3]: needs 0 to 255", problem);
    }
}
