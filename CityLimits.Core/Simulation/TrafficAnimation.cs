namespace CityLimits.Core.Simulation;

/// <summary>
/// Which frame the roads' traffic shows, as Micropolis animates it: a frame every eighth of a
/// second at Normal, and every 25 milliseconds at Fast and Super fast, so the cars keep up with
/// the city; paused, they stand still. It only paces the drawing and never steers the simulation.
/// </summary>
public sealed class TrafficAnimation
{
    /// <summary>How many frames the animation has before it starts again.</summary>
    public const int Frames = 4;

    // Micropolis' animation delays, in seconds.
    private const double NormalSeconds = 0.125;
    private const double FastSeconds = 0.025;

    private double _carried;

    /// <summary>The frame to draw, from 0 to <see cref="Frames"/> less one.</summary>
    public int Frame { get; private set; }

    /// <summary>How long a frame lasts at a speed; nothing for a paused game or a speed that doesn't exist.</summary>
    public static double SecondsPerFrame(int speed) => speed switch
    {
        GameSpeed.Normal => NormalSeconds,
        GameSpeed.Fast or GameSpeed.SuperFast => FastSeconds,
        _ => 0,
    };

    /// <summary>
    /// Moves the animation on by the time a frame of the game took, at the speed the city runs, and
    /// tells how many animation steps that was, for anything else that moves at the same pace, such as the train.
    /// </summary>
    public int Advance(double elapsedSeconds, int speed)
    {
        var perFrame = SecondsPerFrame(speed);
        if (perFrame <= 0 || double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0)
        {
            return 0;
        }

        // A long stall moves the cars on a few frames at most, not one for every frame it missed.
        _carried = Math.Min(_carried + elapsedSeconds, perFrame * Frames);
        var due = (int)(_carried / perFrame);
        _carried -= due * perFrame;
        Frame = (Frame + due) % Frames;
        return due;
    }
}
