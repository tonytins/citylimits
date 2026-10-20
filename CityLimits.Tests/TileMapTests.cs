using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TileMapTests
{
    [Fact]
    public void TheMapKeepsTheTilesItWasGiven()
    {
        var tiles = new[] { TileKind.Grass, TileKind.Water, TileKind.Trees, TileKind.Rock };
        var map = TileMap.FromTerrain(2, 2, tiles);

        Assert.Equal(2, map.Width);
        Assert.Equal(2, map.Height);
        Assert.Equal(TileKind.Grass, map[0, 0]);
        Assert.Equal(TileKind.Water, map[1, 0]);
        Assert.Equal(TileKind.Trees, map[0, 1]);
        Assert.Equal(TileKind.Rock, map[1, 1]);
    }

    [Fact]
    public void TheMapCopiesItsTiles()
    {
        var tiles = new[] { TileKind.Grass };
        var map = TileMap.FromTerrain(1, 1, tiles);

        tiles[0] = TileKind.Rock;

        Assert.Equal(TileKind.Grass, map[0, 0]);
    }

    [Fact]
    public void TheMapRejectsASizeThatDoesNotFit()
    {
        Assert.Throws<ArgumentException>(() => TileMap.FromTerrain(2, 2, new[] { TileKind.Grass }));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.FromTerrain(0, 2, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.FromTerrain(2, -1, []));
    }

    [Fact]
    public void TilesOutsideTheMapAreRejected()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        Assert.False(map.Contains(-1, 0));
        Assert.False(map.Contains(0, 2));
        Assert.True(map.Contains(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map[2, 0]);
    }

    [Fact]
    public void TilesOutsideTheMapFallBack()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        Assert.Equal(TileKind.Water, map.TileAtOrDefault(9, 9, TileKind.Water));
        Assert.Equal(TileKind.Grass, map.TileAtOrDefault(9, 9));
    }

    [Fact]
    public void ANewTileHasNothingBuiltOnIt()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        var state = map.StateAt(1, 1);

        Assert.Equal(TileUse.None, state.Use);
        Assert.Equal(0, state.Development);
        Assert.False(state.IsRoad);
        Assert.False(state.IsZoned);
    }

    [Fact]
    public void TheStateOfATileKeepsWhatWasPutOnIt()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        map.SetState(0, 1, new TileState(TileKind.Grass, TileUse.Road));

        Assert.Equal(TileUse.Road, map.StateAt(0, 1).Use);
        Assert.Equal(TileUse.None, map.StateAt(1, 1).Use);
    }

    [Fact]
    public void TheStateOutsideTheMapIsRejected()
    {
        var map = TileMap.FromTerrain(2, 2, new TileKind[4]);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.StateAt(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetState(2, 0, new TileState(TileKind.Grass)));
        Assert.Equal(TileUse.Road, map.StateAtOrDefault(9, 9, new TileState(TileKind.Grass, TileUse.Road)).Use);
    }

    [Fact]
    public void ChangingATileSaysWhichTileChanged()
    {
        var map = TileMap.FromTerrain(3, 3, new TileKind[9]);
        var changes = new List<(int X, int Y)>();
        map.TileChanged += (x, y) => changes.Add((x, y));

        map.SetState(2, 1, new TileState(TileKind.Grass, TileUse.Road));

        Assert.Equal([(2, 1)], changes);
    }

    [Fact]
    public void SettingTheSameStateChangesNothing()
    {
        var map = TileMap.FromTerrain(3, 3, new TileKind[9]);
        map.SetState(2, 1, new TileState(TileKind.Grass, TileUse.Road));
        var changes = 0;
        map.TileChanged += (_, _) => changes++;

        map.SetState(2, 1, new TileState(TileKind.Grass, TileUse.Road));

        Assert.Equal(0, changes);
    }

    [Fact]
    public void ARefusedChangeOffTheMapRaisesNothing()
    {
        var map = TileMap.FromTerrain(3, 3, new TileKind[9]);
        var changes = 0;
        map.TileChanged += (_, _) => changes++;

        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetState(3, 0, new TileState(TileKind.Grass, TileUse.Road)));

        Assert.Equal(0, changes);
    }

    [Fact]
    public void GrowthReportsTheTilesItDevelops()
    {
        var map = TileMap.FromTerrain(6, 6, new TileKind[36]);
        var simulation = new CityLimits.Core.Simulation.CitySimulation(map, 2000);
        simulation.Builder.Apply(BuildAction.Road, 2, 4);
        simulation.Builder.Apply(BuildAction.ZoneResidential, 2, 2);
        var changes = new List<(int X, int Y)>();
        map.TileChanged += (x, y) => changes.Add((x, y));

        for (var i = 0; i < 200 * CityLimits.Core.Simulation.CityCalendar.StepsPerCount; i++)
        {
            simulation.Tick();
        }

        // The zone's houses go up on its outer tiles; nothing outside the zone changes.
        Assert.NotEmpty(changes);
        Assert.All(changes, change => Assert.True(Math.Abs(change.X - 2) <= 1 && Math.Abs(change.Y - 2) <= 1, $"{change} is outside the zone"));
    }

    [Fact]
    public void EveryTileOfAZoneFindsItsCentre()
    {
        var map = TileMap.FromTerrain(5, 5, new TileKind[25]);
        for (var part = 0; part < ZoneLayout.Parts; part++)
        {
            var (dx, dy) = ZoneLayout.OffsetFromCenter(part);
            map.SetState(2 + dx, 2 + dy, new TileState(TileKind.Grass, TileUse.Commercial, part == ZoneLayout.CenterPart ? 2 : 0, part));
        }

        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
            {
                Assert.Equal((2, 2), map.ZoneCenterOf(x, y));
                Assert.Equal(2, map.ZoneStateAt(x, y).Development);
            }
        }

        Assert.Equal((4, 4), map.ZoneCenterOf(4, 4));
    }
}
