namespace CityLimits.Core.Economy;

/// <summary>What the player pays for the things the tools leave behind.</summary>
public static class TileCosts
{
    /// <summary>What one tile of road costs.</summary>
    public const int Road = 10;

    /// <summary>What one tile of any zone costs.</summary>
    public const int Zone = 5;

    /// <summary>What clearing the trees off one tile costs.</summary>
    public const int Clearing = 5;

    /// <summary>The share of what a tile cost that the bulldozer gives back.</summary>
    public const int BulldozeRefundPercent = 50;

    /// <summary>What the bulldozer gives back for what a tile cost.</summary>
    public static int RefundFor(int cost) => cost * BulldozeRefundPercent / 100;

    /// <summary>What a piece of work costs, before the ground under it is cleared.</summary>
    public static int CostFor(BuildAction action) => action switch
    {
        BuildAction.Road => Road,
        BuildAction.ZoneResidential or BuildAction.ZoneCommercial or BuildAction.ZoneIndustrial => Zone,
        _ => 0,
    };
}
