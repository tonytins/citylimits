namespace CityLimits.Core.Economy;

/// <summary>What the player pays for the work the tools do. The prices are Micropolis'.</summary>
public static class TileCosts
{
    /// <summary>What one tile of road costs.</summary>
    public const int Road = 10;

    /// <summary>What one tile of road over water costs.</summary>
    public const int Bridge = 50;

    /// <summary>What a 3 by 3 zone costs.</summary>
    public const int Zone = 100;

    /// <summary>What the bulldozer costs for each tile it clears, or for each zone it knocks down.</summary>
    public const int Bulldoze = 1;

    /// <summary>What it costs for a tool to clear a tile of trees or rubble before it builds there.</summary>
    public const int AutoClear = 1;

    /// <summary>What a tool costs before the ground under it is cleared, as the palette shows it.</summary>
    public static int CostFor(BuildAction action) => action switch
    {
        BuildAction.Road => Road,
        BuildAction.ZoneResidential or BuildAction.ZoneCommercial or BuildAction.ZoneIndustrial => Zone,
        BuildAction.Bulldoze => Bulldoze,
        _ => 0,
    };
}
