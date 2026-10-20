using CityLimits.Core.Simulation;

namespace CityLimits.Tests;

public class GameDateTests
{
    [Fact]
    public void AMonthBecomesTheNextMonth()
    {
        var date = new GameDate(2000, 1);

        Assert.Equal(new GameDate(2000, 2), date.AddMonths(1));
    }

    [Fact]
    public void DecemberBecomesJanuaryOfTheNextYear()
    {
        var date = new GameDate(2000, 12);

        Assert.Equal(new GameDate(2001, 1), date.AddMonths(1));
    }

    [Fact]
    public void ManyMonthsCarryOverIntoLaterYears()
    {
        var date = new GameDate(2000, 1);

        Assert.Equal(new GameDate(2001, 1), date.AddMonths(12));
        Assert.Equal(new GameDate(2002, 7), date.AddMonths(30));
    }

    [Fact]
    public void GoingBackOverAYearIsPossible()
    {
        var date = new GameDate(2001, 1);

        Assert.Equal(new GameDate(2000, 1), date.AddMonths(-12));
    }

    [Fact]
    public void AMonthOutsideTheCalendarIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(2000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(2000, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(-1, 1));
    }

    [Fact]
    public void TheDateReadsAsYearAndMonth()
    {
        Assert.Equal("2000-01", new GameDate(2000, 1).ToString());
        Assert.Equal("1999-12", new GameDate(1999, 12).ToString());
    }
}
