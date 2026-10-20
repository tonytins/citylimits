using CityLimits.Core.Simulation;

namespace CityLimits.Tests;

public class CityCalendarTests
{
    [Theory]
    [InlineData(0, 1900, 1)]
    [InlineData(3, 1900, 1)]
    [InlineData(4, 1900, 2)]
    [InlineData(47, 1900, 12)]
    [InlineData(48, 1901, 1)]
    [InlineData(48 * 100 + 8, 2000, 3)]
    public void TheCountGivesTheMonth(long cityTime, int year, int month) =>
        Assert.Equal(new GameDate(year, month), CityCalendar.DateAt(cityTime));

    [Fact]
    public void ANegativeCountIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CityCalendar.DateAt(-1));

    [Fact]
    public void AMonthTakesSixteenSecondsAndAYearAboutThreeMinutesAtNormalSpeed()
    {
        var month = CityCalendar.StepsPerMonth * GameClock.DefaultTickSeconds;
        var year = CityCalendar.StepsPerYear * GameClock.DefaultTickSeconds;

        Assert.Equal(16, month);
        Assert.Equal(192, year);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(120)]
    [InlineData(5)]
    public void TheBandsCoverEveryColumnOnce(int width)
    {
        var covered = new int[width];
        for (var band = 0; band < CityCalendar.ScanBands; band++)
        {
            var (first, end) = CityCalendar.BandColumns(band, width);
            for (var x = first; x < end; x++)
            {
                covered[x]++;
            }
        }

        Assert.All(covered, count => Assert.Equal(1, count));
    }

    [Theory]
    [InlineData(GameSpeed.Paused, 0)]
    [InlineData(GameSpeed.Normal, 1)]
    [InlineData(GameSpeed.Fast, 5)]
    [InlineData(GameSpeed.SuperFast, 50)]
    [InlineData(4, 0)]
    public void TheSpeedsAreMicropolisNormalFastAndSuperFast(int speed, double multiplier) =>
        Assert.Equal(multiplier, GameSpeed.Multiplier(speed));
}
