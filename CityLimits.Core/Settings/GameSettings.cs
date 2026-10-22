namespace CityLimits.Core.Settings;

/// <summary>The settings that survive between sessions.</summary>
public sealed record GameSettings
{
    /// <summary>The narrowest window the game allows.</summary>
    public const int MinWindowWidth = 640;

    /// <summary>The widest window the game allows.</summary>
    public const int MaxWindowWidth = 3840;

    /// <summary>The shortest window the game allows.</summary>
    public const int MinWindowHeight = 480;

    /// <summary>The tallest window the game allows.</summary>
    public const int MaxWindowHeight = 2160;

    /// <summary>The slowest speed, which is paused.</summary>
    public const int MinSpeed = 0;

    /// <summary>The fastest speed.</summary>
    public const int MaxSpeed = 3;

    /// <summary>The most autosaves a city keeps.</summary>
    public const int MaxAutosaveSlots = 2;

    /// <summary>
    /// Creates settings, rejecting values the game cannot honour. A settings file from before
    /// autosave has no slots, and gets one.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside its allowed range.</exception>
    public GameSettings(int windowWidth, int windowHeight, int speed, int autosaveSlots = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowWidth, MinWindowWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(windowWidth, MaxWindowWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(windowHeight, MinWindowHeight);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(windowHeight, MaxWindowHeight);
        ArgumentOutOfRangeException.ThrowIfLessThan(speed, MinSpeed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(speed, MaxSpeed);
        ArgumentOutOfRangeException.ThrowIfLessThan(autosaveSlots, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(autosaveSlots, MaxAutosaveSlots);

        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        Speed = speed;
        AutosaveSlots = autosaveSlots;
    }

    /// <summary>The width of the window, in pixels.</summary>
    public int WindowWidth { get; init; }

    /// <summary>The height of the window, in pixels.</summary>
    public int WindowHeight { get; init; }

    /// <summary>The speed the game runs at, where zero is paused.</summary>
    public int Speed { get; init; }

    /// <summary>How many autosaves a city keeps: one, or two, the newer replacing the older.</summary>
    public int AutosaveSlots { get; init; }

    /// <summary>The settings the game starts with when there are none.</summary>
    public static GameSettings Default { get; } = new(1280, 720, 1);

    /// <summary>Tells whether the game is paused.</summary>
    public bool IsPaused => Speed == MinSpeed;
}
