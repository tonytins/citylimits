namespace CityLimits.Core.Definitions;

/// <summary>
/// What a kind of service building is, as data rather than code: its name, its price, what it
/// costs to keep up, and how far and how strongly it covers the city. The base game's specs are
/// Micropolis' numbers, and a mod can change them.
/// </summary>
/// <param name="Id">The id definitions and mods name the service by, such as <c>fire</c>.</param>
/// <param name="Use">The use the station's tiles carry.</param>
/// <param name="Name">The station's name, as the player sees it.</param>
/// <param name="Price">What one station costs to build.</param>
/// <param name="Upkeep">What one station costs to keep up each year, at full funding.</param>
/// <param name="Strength">
/// How strongly a station covers its block, as a percentage of Micropolis' station: 100 is its
/// funding's effect, as Micropolis has it.
/// </param>
/// <param name="Reach">
/// How far a station's cover spreads: how many times its coverage map is smoothed each cycle, each
/// pass spreading it a block further and fainter; Micropolis smooths 3 times.
/// </param>
public sealed record ServiceSpec(string Id, TileUse Use, string Name, int Price, int Upkeep, int Strength = ServiceSpec.MicropolisStrength, int Reach = ServiceSpec.MicropolisReach)
{
    /// <summary>A station's strength in Micropolis, as a percentage.</summary>
    public const int MicropolisStrength = 100;

    /// <summary>How many times Micropolis smooths a coverage map.</summary>
    public const int MicropolisReach = 3;
}
