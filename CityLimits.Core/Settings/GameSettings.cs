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

    /// <summary>The fewest autosaves a city keeps: none, which turns autosave off.</summary>
    public const int MinAutosaveSlots = 0;

    /// <summary>The most autosaves a city keeps.</summary>
    public const int MaxAutosaveSlots = 2;

    /// <summary>
    /// Creates settings, rejecting values the game cannot honour. A settings file from before
    /// autosave has no slots, and gets one; one from before fullscreen is windowed; and one from before
    /// disasters has them on, as Micropolis starts. Saving in the formats 0.4 reads is off unless asked for.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside its allowed range.</exception>
    public GameSettings(int windowWidth, int windowHeight, int speed, int autosaveSlots = 1, bool fullscreen = false, bool disasters = true, bool fires = true, bool floods = true, bool earthquakes = true, bool saveOlderFormats = false, bool meltdowns = true, bool airCrashes = true, bool shipwrecks = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowWidth, MinWindowWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(windowWidth, MaxWindowWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(windowHeight, MinWindowHeight);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(windowHeight, MaxWindowHeight);
        ArgumentOutOfRangeException.ThrowIfLessThan(speed, MinSpeed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(speed, MaxSpeed);
        ArgumentOutOfRangeException.ThrowIfLessThan(autosaveSlots, MinAutosaveSlots);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(autosaveSlots, MaxAutosaveSlots);

        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        Speed = speed;
        AutosaveSlots = autosaveSlots;
        Fullscreen = fullscreen;
        Disasters = disasters;
        Fires = fires;
        Floods = floods;
        Earthquakes = earthquakes;
        SaveOlderFormats = saveOlderFormats;
        Meltdowns = meltdowns;
        AirCrashes = airCrashes;
        Shipwrecks = shipwrecks;
    }

    /// <summary>The width of the window, in pixels.</summary>
    public int WindowWidth { get; init; }

    /// <summary>The height of the window, in pixels.</summary>
    public int WindowHeight { get; init; }

    /// <summary>The speed the game runs at, where zero is paused.</summary>
    public int Speed { get; init; }

    /// <summary>How many autosaves a city keeps: none, which turns autosave off, one, or two, the newer replacing the older.</summary>
    public int AutosaveSlots { get; init; }

    /// <summary>
    /// Whether the game fills the screen, as a borderless window the size of the monitor. The window's
    /// width and height stay the size it goes back to.
    /// </summary>
    public bool Fullscreen { get; init; }

    /// <summary>
    /// Whether disasters happen on their own at all; each has its own switch as well, such as
    /// <see cref="Fires"/>. It is the player's, kept here and never in a city, so every city follows it.
    /// </summary>
    public bool Disasters { get; init; }

    /// <summary>Whether fires start on their own, when <see cref="Disasters"/> are on too.</summary>
    public bool Fires { get; init; }

    /// <summary>Whether floods rise on their own, when <see cref="Disasters"/> are on too.</summary>
    public bool Floods { get; init; }

    /// <summary>Whether earthquakes strike, when <see cref="Disasters"/> are on too. A quake's fires are its own.</summary>
    public bool Earthquakes { get; init; }

    /// <summary>Whether nuclear plants melt down, when <see cref="Disasters"/> are on too.</summary>
    public bool Meltdowns { get; init; }

    /// <summary>Whether the plane and the helicopter can collide, when <see cref="Disasters"/> are on too.</summary>
    public bool AirCrashes { get; init; }

    /// <summary>Whether the ship can run aground, when <see cref="Disasters"/> are on too.</summary>
    public bool Shipwrecks { get; init; }

    /// <summary>
    /// Whether cities, and scenarios' cities, are saved in the formats 0.4 reads, so they can go back
    /// to it; whatever those formats can't hold is left out. 0.5 offers it here; 0.6 moves it to the
    /// command line, and 0.7 drops it.
    /// </summary>
    public bool SaveOlderFormats { get; init; }

    /// <summary>Whether fires start on their own: both switches are on.</summary>
    public bool RandomFires => Disasters && Fires;

    /// <summary>Whether floods rise on their own: both switches are on.</summary>
    public bool RandomFloods => Disasters && Floods;

    /// <summary>Whether earthquakes strike: both switches are on.</summary>
    public bool RandomEarthquakes => Disasters && Earthquakes;

    /// <summary>Whether nuclear plants melt down: both switches are on.</summary>
    public bool RandomMeltdowns => Disasters && Meltdowns;

    /// <summary>Whether the plane and the helicopter can collide: both switches are on.</summary>
    public bool RandomAirCrashes => Disasters && AirCrashes;

    /// <summary>Whether the ship can run aground: both switches are on.</summary>
    public bool RandomShipwrecks => Disasters && Shipwrecks;

    /// <summary>The settings the game starts with when there are none.</summary>
    public static GameSettings Default { get; } = new(1280, 720, 1);

    /// <summary>Tells whether the game is paused.</summary>
    public bool IsPaused => Speed == MinSpeed;
}
