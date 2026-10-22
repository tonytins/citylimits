namespace CityLimits.Core.Economy;

/// <summary>
/// What the player pays for roads, bridges, rail, tunnels, power lines, parks, clearing, and the bulldozer; the prices are Micropolis'.
/// A zone's price is in its <see cref="Definitions.ZoneSpec"/> and a plant's in its
/// <see cref="Definitions.PlantSpec"/>, where a mod can change them.
/// </summary>
public static class TileCosts
{
    /// <summary>What one tile of road costs.</summary>
    public const int Road = 10;

    /// <summary>What one tile of road over water costs.</summary>
    public const int Bridge = 50;

    /// <summary>What the bulldozer costs for each tile it clears, or for each zone or plant it knocks down.</summary>
    public const int Bulldoze = 1;

    /// <summary>What it costs for a tool to clear a tile of trees or rubble before it builds there.</summary>
    public const int AutoClear = 1;

    /// <summary>What one tile of power line costs.</summary>
    public const int PowerLine = 5;

    /// <summary>What one tile of power line under water costs.</summary>
    public const int UnderwaterPowerLine = 25;

    /// <summary>What one tile of park costs.</summary>
    public const int Park = 10;

    /// <summary>What one tile of rail costs; Micropolis' price, approved by Tony Bark for Radial.</summary>
    public const int Rail = 20;

    /// <summary>What one tile of rail under water, a tunnel, costs; Micropolis' price, approved by Tony Bark for Radial.</summary>
    public const int Tunnel = 100;
}
