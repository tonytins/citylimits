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

    /// <summary>The tile carries a power line: on poles over land, or under water.</summary>
    PowerLine,

    /// <summary>The tile carries a road running across, east to west, over a power line running down.</summary>
    RoadAcrossPowerLine,

    /// <summary>The tile carries a road running down, north to south, over a power line running across.</summary>
    RoadDownPowerLine,

    /// <summary>The tile is part of a fire station, 3 by 3.</summary>
    FireStation,

    /// <summary>The tile is part of a police station, 3 by 3.</summary>
    PoliceStation,

    /// <summary>The tile is a park: woods, or a fountain.</summary>
    Park,

    /// <summary>The tile is part of a hospital, 3 by 3, which a residential zone became.</summary>
    Hospital,

    /// <summary>The tile is part of a church, 3 by 3, which a residential zone became.</summary>
    Church,

    /// <summary>The tile is rail: track over land, or a tunnel under water.</summary>
    Rail,

    /// <summary>The tile is rail running across, east to west, over a road running down.</summary>
    RailAcrossRoad,

    /// <summary>The tile is rail running down, north to south, over a road running across.</summary>
    RailDownRoad,

    /// <summary>The tile is rail running across over a power line running down.</summary>
    RailAcrossPowerLine,

    /// <summary>The tile is rail running down over a power line running across.</summary>
    RailDownPowerLine,

    /// <summary>The tile is part of a stadium, 4 by 4, which residents want once the city is big.</summary>
    Stadium,

    /// <summary>The tile is part of a seaport, 4 by 4, which industry wants once it is big.</summary>
    Seaport,

    /// <summary>The tile is part of an airport, 6 by 6, which commerce wants once it is big.</summary>
    Airport,
}
