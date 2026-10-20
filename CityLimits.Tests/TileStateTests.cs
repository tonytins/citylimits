using CityLimits.Core.World;

namespace CityLimits.Tests;

public class TileStateTests
{
    [Fact]
    public void ANewTileHasNoUseAndNoDevelopment()
    {
        var state = new TileState(TileKind.Grass);

        Assert.Equal(TileKind.Grass, state.Kind);
        Assert.Equal(TileUse.None, state.Use);
        Assert.Equal(0, state.Development);
    }

    [Fact]
    public void ADevelopmentLevelOutsideTheCityIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileState(TileKind.Grass, TileUse.Residential, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileState(TileKind.Grass, TileUse.Residential, TileState.MaxDevelopment + 1));
    }

    [Theory]
    [InlineData(TileUse.Road, true, false)]
    [InlineData(TileUse.Residential, false, true)]
    [InlineData(TileUse.Commercial, false, true)]
    [InlineData(TileUse.Industrial, false, true)]
    [InlineData(TileUse.None, false, false)]
    public void ATileKnowsWhatItCarries(TileUse use, bool isRoad, bool isZoned)
    {
        var state = new TileState(TileKind.Grass, use);

        Assert.Equal(isRoad, state.IsRoad);
        Assert.Equal(isZoned, state.IsZoned);
    }

    [Fact]
    public void TwoStatesWithTheSamePartsAreTheSame()
    {
        var first = new TileState(TileKind.Grass, TileUse.Commercial, 2);
        var second = new TileState(TileKind.Grass, TileUse.Commercial, 2);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void AStateWithDifferentPartsIsNotTheSame()
    {
        var first = new TileState(TileKind.Grass, TileUse.Commercial, 2);
        var second = new TileState(TileKind.Grass, TileUse.Commercial, 1);

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void AZonePartOutsideTheZoneIsRejected(int part) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileState(TileKind.Grass, TileUse.Residential, zonePart: part));

    [Fact]
    public void OnlyAZoneHasParts()
    {
        var corner = new TileState(TileKind.Grass, TileUse.Residential, zonePart: 0);
        var road = new TileState(TileKind.Grass, TileUse.Road, zonePart: 0);

        Assert.Equal(0, corner.ZonePart);
        Assert.False(corner.IsZoneCenter);
        Assert.Equal(new TileState(TileKind.Grass, TileUse.Road), road);
    }

    [Fact]
    public void TwoTilesOfAZoneDifferByTheirPart() =>
        Assert.NotEqual(
            new TileState(TileKind.Grass, TileUse.Industrial, zonePart: 1),
            new TileState(TileKind.Grass, TileUse.Industrial, zonePart: 2));
}
