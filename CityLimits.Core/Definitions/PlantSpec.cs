namespace CityLimits.Core.Definitions;

/// <summary>
/// What a kind of power plant is, as data rather than code: its name, its price, and how much
/// it powers. The base game's specs are Micropolis' numbers, and a mod can change them.
/// </summary>
/// <param name="Id">The id definitions and mods name the plant by, such as <c>coal</c>.</param>
/// <param name="Use">The use the plant's tiles carry.</param>
/// <param name="Name">The plant's name, as the player sees it.</param>
/// <param name="Price">What one plant costs to build.</param>
/// <param name="Capacity">
/// How many tiles the plant can power. The city's plants pool their capacity, connected to the
/// grid or not, as in Micropolis.
/// </param>
public sealed record PlantSpec(string Id, TileUse Use, string Name, int Price, int Capacity);
