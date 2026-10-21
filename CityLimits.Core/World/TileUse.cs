namespace CityLimits.Core.World;

/// <summary>What a tile is used for, on top of the ground it stands on.</summary>
public enum TileUse
{
    /// <summary>Nothing has been built on the tile.</summary>
    None,

    /// <summary>The tile carries a road.</summary>
    Road,

    /// <summary>The tile is zoned for homes.</summary>
    Residential,

    /// <summary>The tile is zoned for shops and offices.</summary>
    Commercial,

    /// <summary>The tile is zoned for factories.</summary>
    Industrial,

    /// <summary>The tile is part of a coal power plant, 4 by 4.</summary>
    CoalPowerPlant,

    /// <summary>The tile is part of a nuclear power plant, 4 by 4.</summary>
    NuclearPowerPlant,
}
