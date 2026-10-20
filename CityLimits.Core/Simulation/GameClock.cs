namespace CityLimits.Core.Simulation;

/// <summary>
/// Turns elapsed real time into whole simulation ticks. The tick size is
/// fixed; only the number of ticks per frame changes.
/// </summary>
public sealed class GameClock
{
    /// <summary>The default number of real seconds in one tick.</summary>
    public const double DefaultTickSeconds = 0.25;

    /// <summary>The default number of ticks a single frame may run, which caps the backlog after a stall.</summary>
    public const int DefaultMaxTicksPerFrame = 8;

    /// <summary>Creates a clock.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The tick length or the tick cap is not positive.</exception>
    public GameClock(double tickSeconds = DefaultTickSeconds, int maxTicksPerFrame = DefaultMaxTicksPerFrame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tickSeconds, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTicksPerFrame, 0);

        TickSeconds = tickSeconds;
        MaxTicksPerFrame = maxTicksPerFrame;
    }

    /// <summary>The number of real seconds in one tick.</summary>
    public double TickSeconds { get; }

    /// <summary>The largest number of ticks one call to <see cref="Advance"/> may return.</summary>
    public int MaxTicksPerFrame { get; }

    /// <summary>The time carried over towards the next tick.</summary>
    public double Accumulator { get; private set; }

    /// <summary>
    /// Adds elapsed time and returns the number of whole ticks that are due. When
    /// more ticks are due than the cap allows, the backlog is dropped rather than
    /// replayed, so a long stall cannot spiral.
    /// </summary>
    public int Advance(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0)
        {
            return 0;
        }

        Accumulator += elapsedSeconds;
        var due = (int)(Accumulator / TickSeconds);
        if (due <= 0)
        {
            return 0;
        }

        if (due > MaxTicksPerFrame)
        {
            Accumulator = 0;
            return MaxTicksPerFrame;
        }

        Accumulator -= due * TickSeconds;
        return due;
    }
}
