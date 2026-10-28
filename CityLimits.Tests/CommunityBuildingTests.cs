using CityLimits.Core.Definitions;
using CityLimits.Core.Randomness;
using CityLimits.Core.Scenarios;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class CommunityBuildingTests
{
    // An empty residential zone centred at (4, 4), powered.
    private static TileMap MapWithEmptyZone()
    {
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        for (var part = 0; part < ZoneLayout.Parts; part++)
        {
            var (dx, dy) = ZoneLayout.OffsetFromCenter(part);
            map.SetState(4 + dx, 4 + dy, new TileState(TileKind.Grass, TileUse.Residential, zonePart: part, powered: part == ZoneLayout.CenterPart));
        }

        return map;
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 0, 0)]
    [InlineData(256, 0, 1)]
    [InlineData(600, 1, 1)]
    [InlineData(600, 2, 0)]
    [InlineData(100, 1, -1)]
    public void TheCityNeedsOneOfEachForEvery256Residents(int residents, int hospitals, int need)
    {
        var community = new CommunityBuildings();
        var map = TileMap.FromTerrain(9, 9, new TileKind[81]);
        for (var i = 0; i < hospitals; i++)
        {
            map.SetState(4, 4, new TileState(TileKind.Grass, TileUse.Hospital));
            community.Visit(map, new DeterministicRandom(1), 4, 4);
        }

        community.UpdateNeeds(residents);

        Assert.Equal(need, community.HospitalNeed);
        Assert.Equal(Math.Sign(residents / 256), community.ChurchNeed);
    }

    [Fact]
    public void ARollMakesOneBuildingAHospitalFirst()
    {
        var community = new CommunityBuildings();
        community.UpdateNeeds(600);
        var first = MapWithEmptyZone();
        var second = MapWithEmptyZone();
        var third = MapWithEmptyZone();

        Assert.True(community.TryBuildIn(first, 4, 4));
        Assert.True(community.TryBuildIn(second, 4, 4));
        Assert.False(community.TryBuildIn(third, 4, 4));

        Assert.Equal(TileUse.Hospital, first.StateAt(3, 3).Use);
        Assert.Equal(TileUse.Church, second.StateAt(5, 5).Use);
        Assert.Equal(TileUse.Residential, third.StateAt(4, 4).Use);
        Assert.True(first.StateAt(4, 4).IsBuildingCenter);
        Assert.True(first.StateAt(4, 4).Powered);
        Assert.False(first.StateAt(4, 4).IsZoned);
    }

    [Fact]
    public void OneTooManyNowAndThenTurnsBackIntoAnEmptyZone()
    {
        var community = new CommunityBuildings();
        community.UpdateNeeds(600);
        var map = MapWithEmptyZone();
        community.TryBuildIn(map, 4, 4);

        // As in a cycle: the scan counts the hospital, then the month finds no one needs it.
        community.StartCount();
        community.Visit(map, new DeterministicRandom(1), 4, 4);
        community.UpdateNeeds(0);
        var random = new DeterministicRandom(7);

        var scans = 0;
        while (map.StateAt(4, 4).Use == TileUse.Hospital && scans < 1000)
        {
            community.Visit(map, random, 4, 4);
            scans++;
        }

        Assert.Equal(new TileState(TileKind.Grass, TileUse.Residential, powered: true), map.StateAt(4, 4));
        Assert.Equal(TileUse.Residential, map.StateAt(3, 3).Use);
        Assert.InRange(scans, 1, 200);
    }

    [Fact]
    public void ANeededOneStays()
    {
        var community = new CommunityBuildings();
        community.UpdateNeeds(600);
        var map = MapWithEmptyZone();
        community.TryBuildIn(map, 4, 4);
        community.UpdateNeeds(600);
        var random = new DeterministicRandom(7);

        for (var scan = 0; scan < 500; scan++)
        {
            community.Visit(map, random, 4, 4);
        }

        Assert.Equal(TileUse.Hospital, map.StateAt(4, 4).Use);
    }

    [Fact]
    public void AGrowingCityGetsAHospitalAndAChurchInItsEmptyZones()
    {
        // Fifteen residential buildings of 40 (600 residents, two of each wanted), the shops and
        // factories to employ them, and ten empty residential zones, all powered and on a road, which
        // stops a tile short of the edge so no neighbour's demand comes into it.
        var city = new CitySimulation(TileMap.FromTerrain(132, 8, new TileKind[132 * 8]), 5, funds: 1_000_000);
        var road = new BuildStroke(BuildAction.Road, 1, 3);
        road.DragTo(125, 3);
        city.Builder.Apply(road);
        Zones(city, BuildAction.ZoneResidential, 1, 43, TileUse.Residential, 4);
        Zones(city, BuildAction.ZoneCommercial, 46, 67, TileUse.Commercial, 5);
        Zones(city, BuildAction.ZoneIndustrial, 70, 94, TileUse.Industrial, 4);
        Zones(city, BuildAction.ZoneResidential, 97, 124, TileUse.Residential, 0);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(BuildAction.CoalPowerPlant, 127, 1).Status);

        for (var i = 0; i < 200 * CityCalendar.StepsPerCount; i++)
        {
            city.Tick();
        }

        var counts = ScenarioRunner.Count(city.Map, CityDefinitions.Base);
        Assert.True(counts.Hospital >= 1 && counts.Church >= 1, $"hospitals {counts.Hospital}, churches {counts.Church}");
    }

    // A row of zones from one centre to another, each built to a level when one is given.
    private static void Zones(CitySimulation city, BuildAction action, int fromX, int toX, TileUse use, int level)
    {
        var stroke = new BuildStroke(action, fromX, 1);
        stroke.DragTo(toX, 1);
        Assert.Equal(BuildStatus.Built, city.Builder.Apply(stroke).Status);
        for (var x = fromX; level > 0 && x <= toX; x += 3)
        {
            city.Map.SetState(x, 1, new TileState(TileKind.Grass, use, development: level, valueClass: 1));
        }
    }

    [Fact]
    public void TheReadoutNamesThemAndScenariosCountThem()
    {
        var community = new CommunityBuildings();
        community.UpdateNeeds(600);
        var map = MapWithEmptyZone();
        community.TryBuildIn(map, 4, 4);

        Assert.Equal("Hospital - the city built it on a residential zone", TileInfo.Describe(map, 3, 5, CityDefinitions.Base));
        Assert.Equal(1, ScenarioRunner.Count(map, CityDefinitions.Base).Hospital);
        Assert.Equal(0, ScenarioRunner.Count(map, CityDefinitions.Base).Residential);
    }
}
