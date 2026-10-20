namespace CityLimits.Core.Economy;

/// <summary>
/// What the player pays for roads, bridges, clearing, and the bulldozer; the prices are Micropolis'.
/// A zone's price is in its <see cref="Definitions.ZoneSpec"/>, where a mod can change it.
/// </summary>
public static class TileCosts
{
    /// <summary>What one tile of road costs.</summary>
    public const int Road = 10;

    /// <summary>What one tile of road over water costs.</summary>
    public const int Bridge = 50;

    /// <summary>What the bulldozer costs for each tile it clears, or for each zone it knocks down.</summary>
    public const int Bulldoze = 1;

    /// <summary>What it costs for a tool to clear a tile of trees or rubble before it builds there.</summary>
    public const int AutoClear = 1;
}
