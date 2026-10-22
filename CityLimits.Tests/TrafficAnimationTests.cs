using CityLimits.Core.Simulation;

namespace CityLimits.Tests;

public class TrafficAnimationTests
{
    [Fact]
    public void AFrameLastsAsLongAsMicropolisAnimationDelay()
    {
        Assert.Equal(0.125, TrafficAnimation.SecondsPerFrame(GameSpeed.Normal));
        Assert.Equal(0.025, TrafficAnimation.SecondsPerFrame(GameSpeed.Fast));
        Assert.Equal(0.025, TrafficAnimation.SecondsPerFrame(GameSpeed.SuperFast));
        Assert.Equal(0, TrafficAnimation.SecondsPerFrame(GameSpeed.Paused));
        Assert.Equal(0, TrafficAnimation.SecondsPerFrame(9));
    }

    [Fact]
    public void TheFramesMoveOnWithTheTimeAndStartAgainAfterFour()
    {
        var animation = new TrafficAnimation();

        animation.Advance(0.1, GameSpeed.Normal);
        Assert.Equal(0, animation.Frame);

        animation.Advance(0.05, GameSpeed.Normal);
        Assert.Equal(1, animation.Frame);

        // 0.025 carried over and 0.06 more make three Fast frames, which bring it round to 0.
        animation.Advance(0.06, GameSpeed.Fast);
        Assert.Equal(0, animation.Frame);
    }

    [Fact]
    public void PausedOrGivenNoTimeTheCarsStandStill()
    {
        var animation = new TrafficAnimation();
        animation.Advance(0.2, GameSpeed.Normal);

        animation.Advance(10, GameSpeed.Paused);
        animation.Advance(-1, GameSpeed.Normal);
        animation.Advance(double.NaN, GameSpeed.Normal);

        Assert.Equal(1, animation.Frame);
    }

    [Fact]
    public void ALongStallMovesTheCarsOnAtMostARoundOfFrames()
    {
        var animation = new TrafficAnimation();

        animation.Advance(60, GameSpeed.Normal);
        animation.Advance(0.124, GameSpeed.Normal);

        // The stall leaves at most a round of frames carried over, which comes back to frame 0, and
        // nothing towards the next.
        Assert.Equal(0, animation.Frame);
    }
}
