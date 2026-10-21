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

    /// <summary>Clear a tile, or a zone or plant through its centre.</summary>
    Bulldozer,

    /// <summary>Build a coal power plant.</summary>
    CoalPowerPlant,

    /// <summary>Build a nuclear power plant.</summary>
    NuclearPowerPlant,

    /// <summary>String a power line.</summary>
    PowerLine,

    /// <summary>Build a fire station.</summary>
    FireStation,

    /// <summary>Build a police station.</summary>
    PoliceStation,

    /// <summary>Lay parks.</summary>
    Park,
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
        Tool.CoalPowerPlant => BuildAction.CoalPowerPlant,
        Tool.NuclearPowerPlant => BuildAction.NuclearPowerPlant,
        Tool.PowerLine => BuildAction.PowerLine,
        Tool.FireStation => BuildAction.FireStation,
        Tool.PoliceStation => BuildAction.PoliceStation,
        Tool.Park => BuildAction.Park,
        _ => null,
    };
}
