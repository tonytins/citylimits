namespace CityLimits.Core.World;

/// <summary>The size of a new city's map, shared by the game, the headless run, and scenarios so they cannot drift apart.</summary>
public static class MapSize
{
    /// <summary>The width of a new map, in tiles.</summary>
    public const int DefaultWidth = 128;

    /// <summary>The height of a new map, in tiles.</summary>
    public const int DefaultHeight = 128;
}
