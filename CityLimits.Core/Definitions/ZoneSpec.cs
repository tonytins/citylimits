namespace CityLimits.Core.Definitions;

/// <summary>
/// What a kind of zone is, as data rather than code: its name and its price, and the growth
/// rules that come with population and demand. The base game's specs are Micropolis' numbers,
/// and a mod can change them.
/// </summary>
/// <param name="Id">The id definitions and mods name the zone by, such as <c>residential</c>.</param>
/// <param name="Use">The use the zone's tiles carry.</param>
/// <param name="Name">The zone's name, as the player sees it.</param>
/// <param name="Price">What one 3 by 3 zone costs to lay.</param>
public sealed record ZoneSpec(string Id, TileUse Use, string Name, int Price);
