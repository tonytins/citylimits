using System.Text.Json.Nodes;
using CityLimits.Core.Saving;
using CityLimits.Core.Simulation;
using CityLimits.Core.World;

namespace CityLimits.Tests;

public class FireTests
{
    private static CitySimulation Flat(int width, int height, int seed = 1) =>
        new(TileMap.FromTerrain(width, height, new TileKind[width * height]), seed);

    private static void RunCounts(CitySimulation city, int counts)
    {
        for (var i = 0; i < counts * CityCalendar.StepsPerCount; i++)
        {
            city.Tick();
        }
    }

    // A road along row 4, from one side of the map to the other.
    private static CitySimulation Street(int seed = 1)
    {
        var city = Flat(20, 9, seed);
        var road = new BuildStroke(BuildAction.Road, 0, 4);
        road.DragTo(19, 4);
        city.Builder.Apply(road);
        return city;
    }

    [Fact]
    public void ABurningTileIsRubbleAndBurnsOutInTime()
    {
        var city = Street();

        city.Fires.Ignite(10, 4);

        Assert.True(city.Fires.IsBurning(10, 4));
        Assert.Equal(new TileState(TileKind.Rubble), city.Map.StateAt(10, 4));
        RunCounts(city, 400);
        Assert.False(city.Fires.IsBurning(10, 4));
        Assert.Equal(new TileState(TileKind.Rubble), city.Map.StateAt(10, 4));
    }

    [Fact]
    public void FireSpreadsAlongWhatCanBurnButNotOverBareGround()
    {
        var spread = 0;
        for (var seed = 1; seed <= 20; seed++)
        {
            var city = Street(seed);
            city.Fires.Ignite(10, 4);
            RunCounts(city, 200);

            // A road burned to rubble either side of the fire is spread; bare ground never catches.
            spread += city.Map.StateAt(9, 4).Kind == TileKind.Rubble || city.Map.StateAt(11, 4).Kind == TileKind.Rubble ? 1 : 0;
            Assert.All(Enumerable.Range(0, 20), x => Assert.Equal(TileKind.Grass, city.Map.StateAt(x, 3).Kind));
        }

        Assert.InRange(spread, 1, 20);
    }

    [Fact]
    public void FireCoverageBurnsFiresOutSooner()
    {
        static double MeanCountsToBurnOut(bool station)
        {
            var total = 0;
            for (var seed = 1; seed <= 30; seed++)
            {
                var city = Flat(20, 12, seed);
                if (station)
                {
                    city.Builder.Apply(BuildAction.FireStation, 10, 9);
                    RunCounts(city, 2);
                }

                // Trees can burn, and nothing near the fire but bare ground lets it spread.
                city.Map.SetState(10, 4, new TileState(TileKind.Trees));
                city.Fires.Ignite(10, 4);
                var counts = 0;
                while (city.Fires.IsBurning(10, 4) && counts < 1000)
                {
                    RunCounts(city, 1);
                    counts++;
                }

                total += counts;
            }

            return total / 30.0;
        }

        Assert.True(MeanCountsToBurnOut(station: true) < MeanCountsToBurnOut(station: false));
    }

    [Fact]
    public void AnOuterTileBurningLeavesAHoleNothingCanBeBuiltIn()
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneCommercial, 5, 5);

        city.Fires.Ignite(4, 4);

        Assert.True(city.Map.StateAt(5, 5).IsZoneCenter);
        Assert.True(city.Hazards.IsHole(4, 4));
        Assert.Equal((5, 5), city.Hazards.HoleOwner(4, 4));
        Assert.Equal(BuildRefusal.OnFire, city.Builder.Apply(BuildAction.Road, 4, 4).Reason);
        Assert.Equal(BuildRefusal.OnFire, city.Builder.Apply(BuildAction.Bulldoze, 4, 4).Reason);

        city.Fires.PutOut(4, 4);
        Assert.True(city.Hazards.IsHole(4, 4));
        Assert.Equal(BuildRefusal.CannotBuildHere, city.Builder.Apply(BuildAction.Road, 4, 4).Reason);
    }

    [Fact]
    public void AHoleIsRedrawnWhenTheBuildingIsButNotWhileAnyOfItBurns()
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneCommercial, 5, 5);
        city.Fires.Ignite(4, 4);
        city.Fires.Ignite(6, 6);

        Assert.True(city.Fires.AnyBurningIn(5, 5));
        city.Fires.PutOut(6, 6);
        city.Hazards.RepairHoles(5, 5);

        Assert.Equal(TileUse.Commercial, city.Map.StateAt(6, 6).Use);
        Assert.Equal(Footprint.PartAt(2, 2, 3), city.Map.StateAt(6, 6).ZonePart);
        Assert.Equal(TileKind.Rubble, city.Map.StateAt(4, 4).Kind);
        Assert.True(city.Fires.IsBurning(4, 4));
    }

    [Fact]
    public void WhenAZonesCentreCatchesItsHousesCatchAndItsEmptyLotsGoBare()
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneResidential, 5, 5);
        city.Map.SetState(4, 4, new TileState(TileKind.Grass, TileUse.Residential, zonePart: 0, house: 2));

        Assert.NotNull(city.Fires.Ignite(5, 5));

        Assert.True(city.Fires.IsBurning(5, 5));
        Assert.True(city.Fires.IsBurning(4, 4));
        Assert.Equal(2, city.Fires.Count);
        Assert.Equal(new TileState(TileKind.Grass), city.Map.StateAt(6, 6));
        Assert.False(city.Hazards.IsHole(6, 6));
    }

    [Fact]
    public void WhenABuildingsCentreCatchesEveryTileOfItCatches()
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneCommercial, 5, 5);
        city.Map.SetState(5, 5, new TileState(TileKind.Grass, TileUse.Commercial, development: 2));

        city.Fires.Ignite(5, 5);

        Assert.Equal(9, city.Fires.Count);
    }

    [Fact]
    public void BurningIndustryExplodesAndSetsTheCornersAlight()
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneIndustrial, 5, 5);
        city.Map.SetState(5, 5, new TileState(TileKind.Grass, TileUse.Industrial, development: 1));
        city.Map.SetState(4, 5, new TileState(TileKind.Trees));
        var heard = new List<CityMessage>();
        var exploded = new List<(int, int)>();
        city.MessageSent += heard.Add;
        city.Exploded += (x, y) => exploded.Add((x, y));

        // The fire reaches the centre from a burning tree beside it; spreading is a roll, so run until it does.
        city.Map.SetState(5, 5, new TileState(TileKind.Grass, TileUse.Industrial, development: 1));
        for (var i = 0; i < 2000 && exploded.Count == 0; i++)
        {
            if (!city.Fires.IsBurning(4, 5) && city.Map.StateAt(5, 5).IsZoneCenter)
            {
                city.Map.SetState(4, 5, new TileState(TileKind.Trees));
                city.Fires.Ignite(4, 5);
            }

            city.Tick();
        }

        Assert.Equal([(5, 5)], exploded);
        Assert.Contains(CityMessage.ExplosionReported, heard);
        Assert.False(city.Map.StateAt(5, 5).IsZoneCenter);
    }

    [Fact]
    public void TurningRandomFiresOffChangesNothingElseTheCityDoes()
    {
        // With nothing that could catch, a city with random fires and one without stay the same.
        var on = Street();
        var off = Street();
        off.RandomFires = false;

        RunCounts(on, 3000);
        RunCounts(off, 3000);

        Assert.Equal(on.Random.NextInt(0, int.MaxValue), off.Random.NextInt(0, int.MaxValue));
        Assert.Equal(on.Treasury.Funds, off.Treasury.Funds);
    }

    [Fact]
    public void FiresStartOnTheirOwnAndAreReportedOnlyWhenAllowed()
    {
        // A map of empty shops, nearly every tile of which can catch.
        static CitySimulation Shops(bool allowed)
        {
            var city = Flat(30, 30, seed: 4);
            var zones = new BuildStroke(BuildAction.ZoneCommercial, 1, 1);
            zones.DragTo(28, 28);
            city.Builder.Apply(zones);
            city.RandomFires = allowed;

            // An earthquake would wreck the shops before a fire found them; this is about fires alone.
            city.RandomEarthquakes = false;
            city.RandomFloods = false;
            return city;
        }

        var allowed = Shops(allowed: true);
        var heard = new List<CityMessage>();
        allowed.MessageSent += heard.Add;
        for (var counts = 0; counts < 20_000 && !heard.Contains(CityMessage.FireReported); counts++)
        {
            RunCounts(allowed, 1);
        }

        Assert.Contains(CityMessage.FireReported, heard);

        var refused = Shops(allowed: false);
        var heardNothing = new List<CityMessage>();
        refused.MessageSent += heardNothing.Add;
        RunCounts(refused, (int)allowed.CityTime);
        Assert.DoesNotContain(CityMessage.FireReported, heardNothing);
        Assert.Equal(0, refused.Fires.Count);
    }

    [Fact]
    public void AFireOnTheMapPollutes90()
    {
        var map = TileMap.FromTerrain(16, 16, new TileKind[256]);
        var fires = new Fires(map);
        fires.Ignite(8, 8);
        var maps = new CityMaps(map);

        maps.UpdateLandValue(new TrafficMap(map), fires);

        // 90 on its block, a quarter of it to each block beside, then (22 * 5) / 4 back on its own.
        Assert.Equal(27, maps.PollutionAt(8, 8));
    }

    [Fact]
    public void FiresAndHolesAreSavedAndLoadedInBothFormats()
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneCommercial, 5, 5);
        var road = new BuildStroke(BuildAction.Road, 0, 9);
        road.DragTo(11, 9);
        city.Builder.Apply(road);
        city.Fires.Ignite(4, 4);
        city.Fires.Ignite(6, 6);
        city.Fires.PutOut(6, 6);
        city.Fires.Ignite(3, 9);

        Assert.True(FlatCityFormat.TryRead(FlatCityFormat.Write(city), out var flat, out var problem), problem);
        Assert.True(BinaryCityFormat.TryRead(BinaryCityFormat.Write(city), out var binary, out problem, out _), problem);

        foreach (var loaded in new[] { flat, binary })
        {
            Assert.True(loaded.Fires.IsBurning(4, 4) && loaded.Hazards.IsHole(4, 4));
            Assert.True(!loaded.Fires.IsBurning(6, 6) && loaded.Hazards.IsHole(6, 6));
            Assert.True(loaded.Fires.IsBurning(3, 9) && !loaded.Hazards.IsHole(3, 9));
            Assert.Equal(3, loaded.Fires.Count + 1);
            Assert.All(Enumerable.Range(0, 12), x => Assert.Equal(city.Map.StateAt(x, 4), loaded.Map.StateAt(x, 4)));
        }
    }

    [Fact]
    public void ASavesMapHoldsItsBuildingsWholeSoAnOlderVersionOpensThemRepaired()
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneCommercial, 5, 5);
        city.Fires.Ignite(4, 4);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();

        Assert.Equal('C', save["map"]!["use"]![4]!.GetValue<string>()[4]);
        Assert.Equal('b', save["fire"]!["tiles"]![4]!.GetValue<string>()[4]);

        // As 0.3.106 opens it: no fire part, so the city as it was before it burned.
        save["format"] = "1.5";
        save.Remove("fire");
        Assert.True(FlatCityFormat.TryRead(save.ToJsonString(), out var older, out var problem), problem);
        Assert.Equal(0, older.Fires.Count);
        Assert.Equal(TileUse.Commercial, older.Map.StateAt(4, 4).Use);
    }

    [Theory]
    [InlineData(0, 0, 'f', "fire.tiles[0]: the fire at 0 is on Grass with None, not rubble.")]
    [InlineData(0, 0, 'x', "fire.tiles[0]: the hole at 0 isn't an outer tile of a building.")]
    [InlineData(5, 5, 'x', "fire.tiles[5]: the hole at 5 isn't an outer tile of a building.")]
    [InlineData(0, 0, '?', "fire.tiles[0]: needs '.', 'f', 'x', or 'b' for each tile, not '?' at 0.")]
    public void AFirePartThatDoesntFitTheMapIsRefusedAndSaysWhere(int x, int y, char mark, string expected)
    {
        var city = Flat(12, 12);
        city.Builder.Apply(BuildAction.ZoneCommercial, 5, 5);
        var save = JsonNode.Parse(FlatCityFormat.Write(city))!.AsObject();
        var rows = save["fire"]!["tiles"]!.AsArray();
        var row = rows[y]!.GetValue<string>().ToCharArray();
        row[x] = mark;
        rows[y] = new string(row);

        Assert.False(FlatCityFormat.TryRead(save.ToJsonString(), out _, out var problem));
        Assert.Equal(expected, problem);
    }

    // A zone of eight houses by a road, with the given trees in the ring around it, run for a month
    // so the census counts its people.
    private static CitySimulation Homes(bool woods)
    {
        var city = Flat(20, 20);
        var road = new BuildStroke(BuildAction.Road, 7, 12);
        road.DragTo(13, 12);
        city.Builder.Apply(road);
        city.Builder.Apply(BuildAction.ZoneResidential, 10, 10);
        foreach (var (dx, dy) in new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) })
        {
            city.Map.SetState(10 + dx, 10 + dy, new TileState(TileKind.Grass, TileUse.Residential, zonePart: Footprint.PartAt(dx + 1, dy + 1, 3), house: 1));
        }

        if (woods)
        {
            for (var x = 8; x <= 12; x++)
            {
                city.Map.SetState(x, 8, new TileState(TileKind.Trees));
            }
        }

        RunCounts(city, 4);
        return city;
    }

    [Fact]
    public void TheNewsHintsAtHomesAgainstTheWoods()
    {
        Assert.True(FireRisk.HomesAgainstWoods(Homes(woods: true)));
        Assert.False(FireRisk.HomesAgainstWoods(Homes(woods: false)));
    }

    [Fact]
    public void TheNewsHintsAtZonesOutOfAnEnginesReachUntilAStationCoversThem()
    {
        var city = Homes(woods: false);
        Assert.True(FireRisk.ZonesOutOfReach(city));

        city.Builder.Apply(BuildAction.FireStation, 10, 15);
        RunCounts(city, 2);
        Assert.False(FireRisk.ZonesOutOfReach(city));
    }

    [Fact]
    public void TheNewsHintsAtCutsToFireFunding()
    {
        var city = Homes(woods: false);
        city.Builder.Apply(BuildAction.FireStation, 10, 15);
        RunCounts(city, 2);
        Assert.False(FireRisk.FundingIsCut(city));

        city.Budget.SetFunding(100, 50, 100);
        RunCounts(city, 2);
        Assert.True(FireRisk.FundingIsCut(city));
    }

    [Fact]
    public void TheNewsDoesntHintAtFireRiskWhenFiresCantStart()
    {
        var city = Homes(woods: true);
        city.RandomFires = false;

        Assert.False(FireRisk.HomesAgainstWoods(city));
        Assert.False(FireRisk.ZonesOutOfReach(city));
    }
}
