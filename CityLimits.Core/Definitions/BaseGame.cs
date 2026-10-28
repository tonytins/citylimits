namespace CityLimits.Core.Definitions;

/// <summary>
/// The base game's definitions. They live in code so they can't go missing: its zones, power plants,
/// and services as a definitions file, read by <see cref="DefinitionLoader"/>, the same loader a mod's
/// files go through, and its facilities, infrastructure, and economy as specs. The game writes them
/// out as files in <c>common/</c>, for players to see and mods to follow, and reads them back from
/// there (<see cref="BaseFiles"/>). The numbers are Micropolis'.
/// </summary>
public static class BaseGame
{
    /// <summary>The base game's stadium, seaport, and airport: Micropolis' prices, demand caps, and pollution.</summary>
    public static readonly IReadOnlyList<FacilitySpec> Facilities =
    [
        // Seaports and airports pollute as a coal plant does, as in Micropolis. Their influences
        // are CityLimits' own, as Tony Bark chose: a stadium lifts the land around it, and a
        // seaport's and an airport's noise lowers it.
        new("stadium", TileUse.Stadium, "Stadium", TileCosts.Stadium, 500, 0, [new(InfluenceEffect.LandValue, 20, 2)]),
        new("seaport", TileUse.Seaport, "Seaport", TileCosts.Seaport, 70, 100, [new(InfluenceEffect.LandValue, -20, 2)]),
        new("airport", TileUse.Airport, "Airport", TileCosts.Airport, 100, 100, [new(InfluenceEffect.LandValue, -30, 3)]),
    ];

    /// <summary>The base game's infrastructure: Micropolis' prices, and power along roads.</summary>
    public static readonly InfrastructureSpec Infrastructure = new(
        Road: TileCosts.Road,
        Bridge: TileCosts.Bridge,
        Rail: TileCosts.Rail,
        Tunnel: TileCosts.Tunnel,
        PowerLine: TileCosts.PowerLine,
        UnderwaterPowerLine: TileCosts.UnderwaterPowerLine,
        Park: TileCosts.Park,
        Bulldozer: TileCosts.Bulldoze,
        Clearing: TileCosts.AutoClear,

        // CityLimits' own: power reaches the buildings along a road without a line beside every
        // street, as Tony Bark chose; Micropolis needs lines.
        RoadCarriesPower: true,
        RailCarriesPower: false);

    /// <summary>What a new city starts with in the base game: Micropolis' $20,000 and 7% tax.</summary>
    public static readonly EconomySpec Economy = new(Treasury.StartingFunds, CityBudget.DefaultTaxRate);

    /// <summary>The base game's definitions file.</summary>
    public const string Json = """
        {
          "format": "1.0",
          "zones": [
            { "id": "residential", "name": "Residential zone", "price": 100, "populations": [16, 24, 32, 40], "valueClasses": 4, "houses": 8, "trips": ["commercial", "industrial"] },
            { "id": "commercial", "name": "Commercial zone", "price": 100, "populations": [1, 2, 3, 4, 5], "valueClasses": 4, "houses": 0, "trips": ["residential", "commercial", "industrial"] },
            { "id": "industrial", "name": "Industrial zone", "price": 100, "populations": [1, 2, 3, 4], "valueClasses": 2, "houses": 0, "trips": ["residential"] }
          ],
          "plants": [
            { "id": "coal", "name": "Coal power plant", "price": 3000, "capacity": 700 },
            { "id": "nuclear", "name": "Nuclear power plant", "price": 5000, "capacity": 2000 }
          ],
          "services": [
            { "id": "fire", "name": "Fire station", "price": 500, "upkeep": 100 },
            { "id": "police", "name": "Police station", "price": 500, "upkeep": 100 }
          ]
        }
        """;
}
