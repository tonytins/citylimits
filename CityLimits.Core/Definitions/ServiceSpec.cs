namespace CityLimits.Core.Definitions;

/// <summary>
/// What a kind of service building is, as data rather than code: its name, its price, and what
/// it costs to keep up. The base game's specs are Micropolis' numbers, and a mod can change them.
/// </summary>
/// <param name="Id">The id definitions and mods name the service by, such as <c>fire</c>.</param>
/// <param name="Use">The use the station's tiles carry.</param>
/// <param name="Name">The station's name, as the player sees it.</param>
/// <param name="Price">What one station costs to build.</param>
/// <param name="Upkeep">What one station costs to keep up each year, at full funding.</param>
public sealed record ServiceSpec(string Id, TileUse Use, string Name, int Price, int Upkeep);
