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
            { "id": "residential", "name": "Residential zone", "price": 100, "populations": [16, 24, 32, 40], "valueClasses": 4, "houses": 8 },
            { "id": "commercial", "name": "Commercial zone", "price": 100, "populations": [1, 2, 3, 4, 5], "valueClasses": 4, "houses": 0 },
            { "id": "industrial", "name": "Industrial zone", "price": 100, "populations": [1, 2, 3, 4], "valueClasses": 2, "houses": 0 }
          ]
        }
        """;
}
