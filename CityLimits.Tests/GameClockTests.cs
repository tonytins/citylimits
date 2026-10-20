using CityLimits.Core.Simulation;

namespace CityLimits.Tests;

public class GameClockTests
{
    [Fact]
    public void AShortFrameDoesNotRunATick()
    {
        var clock = new GameClock(tickSeconds: 0.25);

        Assert.Equal(0, clock.Advance(0.1));
    }

    [Fact]
    public void ALongFrameRunsWholeTicks()
    {
        var clock = new GameClock(tickSeconds: 0.25);

        Assert.Equal(4, clock.Advance(1.0));
    }

    [Fact]
    public void TheRemainderIsKeptForTheNextFrame()
    {
        var clock = new GameClock(tickSeconds: 0.25);

        Assert.Equal(0, clock.Advance(0.2));
        Assert.Equal(1, clock.Advance(0.1));
    }

    [Fact]
    public void TheBacklogIsDroppedRatherThanReplayed()
    {
        var clock = new GameClock(tickSeconds: 0.25, maxTicksPerFrame: 4);

        Assert.Equal(4, clock.Advance(10.0));
        Assert.Equal(0, clock.Advance(0.01));
    }

    [Fact]
    public void TimeThatDoesNotMoveDoesNothing()
    {
        var clock = new GameClock(tickSeconds: 0.25);

        Assert.Equal(0, clock.Advance(0));
        Assert.Equal(0, clock.Advance(-1));
        Assert.Equal(0, clock.Advance(double.NaN));
    }

    [Fact]
    public void AClockWithNoTickLengthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(tickSeconds: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(maxTicksPerFrame: 0));
    }
}
