namespace CityLimits.Game;

/// <summary>The tools the player can hold.</summary>
public enum Tool
{
    /// <summary>Look at a tile without changing it.</summary>
    Inspect,

    /// <summary>Draw a road.</summary>
    Road,

    /// <summary>Zone land as residential.</summary>
    Residential,

    /// <summary>Zone land as commercial.</summary>
    Commercial,

    /// <summary>Zone land as industrial.</summary>
    Industrial,

    /// <summary>Clear a tile and take part of the money back.</summary>
    Bulldozer,
}

/// <summary>The work each tool asks the city to do.</summary>
public static class ToolActions
{
    /// <summary>Returns the work a tool does, or null when the tool only looks.</summary>
    public static BuildAction? ActionFor(Tool tool) => tool switch
    {
        Tool.Road => BuildAction.Road,
        Tool.Residential => BuildAction.ZoneResidential,
        Tool.Commercial => BuildAction.ZoneCommercial,
        Tool.Industrial => BuildAction.ZoneIndustrial,
        Tool.Bulldozer => BuildAction.Bulldoze,
        _ => null,
    };
}
