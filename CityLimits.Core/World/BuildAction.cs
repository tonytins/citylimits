namespace CityLimits.Core.World;

/// <summary>The work a tool asks the city to do on a tile.</summary>
public enum BuildAction
{
    /// <summary>Lay a road.</summary>
    Road,

    /// <summary>Zone the tile for homes.</summary>
    ZoneResidential,

    /// <summary>Zone the tile for shops and offices.</summary>
    ZoneCommercial,

    /// <summary>Zone the tile for factories.</summary>
    ZoneIndustrial,

    /// <summary>Clear whatever is on the tile.</summary>
    Bulldoze,

    /// <summary>Build a coal power plant, centred on the tile.</summary>
    CoalPowerPlant,

    /// <summary>Build a nuclear power plant, centred on the tile.</summary>
    NuclearPowerPlant,

    /// <summary>String a power line.</summary>
    PowerLine,
}
