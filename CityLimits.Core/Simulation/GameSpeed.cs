namespace CityLimits.Core.Simulation;

/// <summary>
/// The speeds the city runs at, Micropolis' Normal, Fast, and Super fast. A speed changes how much
/// time the clock is fed, and so how many steps run, never the size of a step.
/// </summary>
public static class GameSpeed
{
    /// <summary>The city stands still.</summary>
    public const int Paused = 0;

    /// <summary>A step every quarter of a second: a month in 16 seconds, a year in about 3 minutes.</summary>
    public const int Normal = 1;

    /// <summary>Five times Normal.</summary>
    public const int Fast = 2;

    /// <summary>Fifty times Normal.</summary>
    public const int SuperFast = 3;

    /// <summary>How many times faster than Normal a speed runs; nothing for a speed that doesn't exist.</summary>
    public static double Multiplier(int speed) => speed switch
    {
        Normal => 1,
        Fast => 5,
        SuperFast => 50,
        _ => 0,
    };
}
