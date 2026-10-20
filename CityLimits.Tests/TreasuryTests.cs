using CityLimits.Core.Economy;

namespace CityLimits.Tests;

public class TreasuryTests
{
    [Fact]
    public void ANewCityStartsWithSomeMoney()
    {
        var treasury = new Treasury();

        Assert.Equal(Treasury.StartingFunds, treasury.Funds);
        Assert.True(treasury.CanAfford(Treasury.StartingFunds));
    }

    [Fact]
    public void SpendingTakesTheMoneyAway()
    {
        var treasury = new Treasury(100);

        Assert.True(treasury.TrySpend(40));

        Assert.Equal(60, treasury.Funds);
    }

    [Fact]
    public void TheCityCannotSpendWhatItDoesNotHave()
    {
        var treasury = new Treasury(30);

        Assert.False(treasury.TrySpend(40));
        Assert.False(treasury.CanAfford(31));

        Assert.Equal(30, treasury.Funds);
    }

    [Fact]
    public void ARefundGivesTheMoneyBack()
    {
        var treasury = new Treasury(100);

        treasury.Refund(15);

        Assert.Equal(115, treasury.Funds);
    }

    [Fact]
    public void NegativeAmountsAreRejected()
    {
        var treasury = new Treasury(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Treasury(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => treasury.TrySpend(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => treasury.Refund(-1));
    }

    [Fact]
    public void TheBulldozerGivesBackHalfOfWhatATileCost()
    {
        Assert.Equal(TileCosts.Road / 2, TileCosts.RefundFor(TileCosts.Road));
        Assert.Equal(TileCosts.Zone / 2, TileCosts.RefundFor(TileCosts.Zone));
    }
}
