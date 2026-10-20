namespace CityLimits.Core.World;

/// <summary>What a kind of ground is called, and what the player can do with it.</summary>
public static class TileInfo
{
    /// <summary>The name of a kind of ground, as the player sees it.</summary>
    public static string Name(TileKind kind) => kind switch
    {
        TileKind.Water => "Water",
        TileKind.Trees => "Trees",
        TileKind.Rock => "Rock",
        _ => "Grass",
    };

    /// <summary>What the player can do with a kind of ground.</summary>
    public static string BuildAdvice(TileKind kind) => kind switch
    {
        TileKind.Water => "cannot be built on",
        TileKind.Rock => "cannot be built on",
        TileKind.Trees => "clear it before building",
        _ => "ready to build on",
    };

    /// <summary>The line the readout shows for a kind of ground.</summary>
    public static string Describe(TileKind kind) => $"{Name(kind)} - {BuildAdvice(kind)}";
}
