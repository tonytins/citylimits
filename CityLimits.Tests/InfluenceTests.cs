using CityLimits.Core.Definitions;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class InfluenceTests
{
    private static CitySimulation Flat(CityDefinitions? definitions = null, int width = 64, int height = 32) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), 1, funds: 1_000_000, definitions: definitions);

    private static void Run(CitySimulation city, long steps)
    {
        for (var i = 0; i < steps; i++)
        {
            city.Tick();
        }
    }

    // The base game with one facilities.json change, applied through a mod folder.
    private static CityDefinitions WithFacilities(string facilities)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var mod = Directory.CreateDirectory(Path.Combine(folder, "m")).FullName;
            File.WriteAllText(Path.Combine(mod, "mod.json"), """{ "format": "2.1", "id": "m", "name": "M" }""");
            File.WriteAllText(Path.Combine(mod, "facilities.json"), $$"""{ "format": "2.1", "facilities": [ {{facilities}} ] }""");
            var definitions = ModLoader.Load(folder, out var problems);
            Assert.Empty(problems);
            return definitions;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // A city with a facility centred at (12, 12), powered by a plant touching its bottom edge unless
    // told otherwise, run two cycles so the scan has found its power.
    private static CitySimulation WithFacility(BuildAction facility, bool powered = true, CityDefinitions? definitions = null)
    {
        var city = Flat(definitions);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(facility, 12, 12).Status);
        if (powered)
        {
            // The footprint starts a tile above its centre; the plant's centre is a tile below its top.
            var bottom = 11 + Footprint.SizeOf(city.Map.StateAt(12, 12).Use) - 1;
            Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.CoalPowerPlant, 12, bottom + 2).Status);
        }

        Run(city, 2 * CityCalendar.StepsPerCount);
        return city;
    }

    [Theory]
    [InlineData(BuildAction.Stadium, 1)]
    [InlineData(BuildAction.Seaport, -1)]
    [InlineData(BuildAction.Airport, -1)]
    public void APoweredFacilityRaisesOrLowersTheLandValueAroundItAndNotFarAway(BuildAction facility, int sign)
    {
        var city = WithFacility(facility);
        city.Influences.Update(InfluenceEffect.LandValue);

        Assert.Equal(sign, Math.Sign(city.Influences.At(InfluenceEffect.LandValue, 12, 12)));
        Assert.Equal(sign, Math.Sign(city.Influences.At(InfluenceEffect.LandValue, 12 + FacilityInfluences.BlockSize, 12)));
        Assert.Equal(0, city.Influences.At(InfluenceEffect.LandValue, 60, 12));
        Assert.Equal(0, city.Influences.At(InfluenceEffect.Crime, 12, 12));
    }

    [Fact]
    public void AnUnpoweredFacilityHasNoInfluence()
    {
        var city = WithFacility(BuildAction.Stadium, powered: false);
        city.Influences.Update(InfluenceEffect.LandValue);

        Assert.False(city.Map.StateAt(12, 12).Powered);
        Assert.Equal(0, city.Influences.At(InfluenceEffect.LandValue, 12, 12));
    }

    [Fact]
    public void ReachIsHowFarItSpreads()
    {
        var near = WithFacility(BuildAction.Stadium, definitions: WithFacilities("""{ "id": "stadium", "influences": [ { "effect": "landValue", "strength": 100, "reach": 0 } ] }"""));
        var far = WithFacility(BuildAction.Stadium, definitions: WithFacilities("""{ "id": "stadium", "influences": [ { "effect": "landValue", "strength": 100, "reach": 3 } ] }"""));
        near.Influences.Update(InfluenceEffect.LandValue);
        far.Influences.Update(InfluenceEffect.LandValue);

        Assert.Equal(100, near.Influences.At(InfluenceEffect.LandValue, 12, 12));
        Assert.Equal(0, near.Influences.At(InfluenceEffect.LandValue, 12 + (2 * FacilityInfluences.BlockSize), 12));
        Assert.True(far.Influences.At(InfluenceEffect.LandValue, 12 + (2 * FacilityInfluences.BlockSize), 12) > 0);
    }

    [Fact]
    public void CrimeAndPollutionInfluencesChangeThoseMaps()
    {
        var plain = WithFacility(BuildAction.Stadium, definitions: WithFacilities("""{ "id": "stadium", "influences": [] }"""));
        var rowdy = WithFacility(BuildAction.Stadium, definitions: WithFacilities("""{ "id": "stadium", "influences": [ { "effect": "crime", "strength": 120, "reach": 1 }, { "effect": "pollution", "strength": 200, "reach": 1 } ] }"""));
        Run(plain, CityCalendar.StepsPerCount);
        Run(rowdy, CityCalendar.StepsPerCount);

        Assert.True(rowdy.Maps.CrimeAt(12, 12) > plain.Maps.CrimeAt(12, 12), $"{rowdy.Maps.CrimeAt(12, 12)} vs {plain.Maps.CrimeAt(12, 12)}");
        Assert.True(rowdy.Maps.PollutionAt(12, 12) > plain.Maps.PollutionAt(12, 12), $"{rowdy.Maps.PollutionAt(12, 12)} vs {plain.Maps.PollutionAt(12, 12)}");
    }

    [Fact]
    public void TheLandValueAroundAStadiumIsHigherThanWithoutItsInfluence()
    {
        var plain = WithFacility(BuildAction.Stadium, definitions: WithFacilities("""{ "id": "stadium", "influences": [] }"""));
        var lifted = WithFacility(BuildAction.Stadium);
        Run(plain, CityCalendar.StepsPerCount);
        Run(lifted, CityCalendar.StepsPerCount);

        Assert.True(lifted.Maps.LandValueAt(12, 12) > plain.Maps.LandValueAt(12, 12), $"{lifted.Maps.LandValueAt(12, 12)} vs {plain.Maps.LandValueAt(12, 12)}");
    }

    [Fact]
    public void ACitySavedWithInfluencesRunsInStepWithOneThatWasnt()
    {
        var city = WithFacility(BuildAction.Airport);
        Run(city, 7);
        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var reopened, out var problem), problem);

        Run(city, CityCalendar.StepsPerYear);
        Run(reopened, CityCalendar.StepsPerYear);

        Assert.Equal(FlatCityFormat.Write(city), FlatCityFormat.Write(reopened));
    }
}
