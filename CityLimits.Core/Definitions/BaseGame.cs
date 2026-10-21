namespace CityLimits.Core.Definitions;

/// <summary>
/// The base game's definitions, written as a definitions file. They live in code so they can't go
/// missing, and they are read by <see cref="DefinitionLoader"/>, the same loader a mod's files go
/// through. The numbers are Micropolis'.
/// </summary>
public static class BaseGame
{
    /// <summary>The base game's definitions file.</summary>
    public const string Json = """
        {
          "format": 1,
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
