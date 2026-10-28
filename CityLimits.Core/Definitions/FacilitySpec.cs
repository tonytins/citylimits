namespace CityLimits.Core.Definitions;

/// <summary>
/// What a stadium, a seaport, or an airport is, as data rather than code: its name, its price, how
/// big the demand it serves may grow before the city wants one, and the pollution each of its
/// tiles gives off. The base game's specs are Micropolis' numbers, and a mod can change them.
/// </summary>
/// <param name="Id">The id definitions and mods name the facility by, such as <c>stadium</c>.</param>
/// <param name="Use">The use the facility's tiles carry.</param>
/// <param name="Name">The facility's name, as the player sees it.</param>
/// <param name="Price">What one costs to build.</param>
/// <param name="DemandCap">
/// How big the demand it serves may grow, in the census' units, before a city without one holds
/// that demand at zero: residents for a stadium, industry for a seaport, and commerce for an airport.
/// </param>
/// <param name="Pollution">The pollution each of its tiles gives off.</param>
/// <param name="Influences">What a powered one does to the land around it, each effect at most once; none when null.</param>
public sealed record FacilitySpec(string Id, TileUse Use, string Name, int Price, int DemandCap, int Pollution, IReadOnlyList<InfluenceSpec>? Influences = null)
{
    /// <summary>What a powered one does to the land around it.</summary>
    public IReadOnlyList<InfluenceSpec> Influences { get; init; } = Influences ?? [];

    /// <summary>Its influence on one of the city's maps, or null when it has none.</summary>
    public InfluenceSpec? InfluenceOn(InfluenceEffect effect) => Influences.FirstOrDefault(influence => influence.Effect == effect);
}

/// <summary>Which of the city's maps an influence changes.</summary>
public enum InfluenceEffect
{
    /// <summary>The land value, from 1 to 250 where something is built.</summary>
    LandValue,

    /// <summary>The crime, from 0 to 250, as police coverage lowers it.</summary>
    Crime,

    /// <summary>The pollution, from 0 to 255.</summary>
    Pollution,
}

/// <summary>
/// What a building does to the land around it, as a fire or police station's coverage does: its
/// strength goes to the 8 by 8 tile block it stands in and spreads to the blocks around it,
/// fading, once for each step of its reach. A negative strength lowers the map, such as an
/// airport's noise lowering land value.
/// </summary>
/// <param name="Effect">Which map it changes.</param>
/// <param name="Strength">How much it changes it, in the map's own units, at the building's block before it spreads.</param>
/// <param name="Reach">How many times it spreads to the blocks around.</param>
public sealed record InfluenceSpec(InfluenceEffect Effect, int Strength, int Reach);

/// <summary>
/// What the city's infrastructure costs, as data rather than code: each tile of road, bridge, rail,
/// tunnel, power line, power line under water, and park, the bulldozer's work, and clearing trees
/// or rubble before building. The base game's prices are Micropolis', and a mod can change them.
/// It also says what carries power besides lines and buildings: roads join the buildings along
/// them in the base game; rail doesn't, for now.
/// </summary>
/// <param name="Road">What one tile of road costs.</param>
/// <param name="Bridge">What one tile of road over water costs.</param>
/// <param name="Rail">What one tile of rail costs.</param>
/// <param name="Tunnel">What one tile of rail under water costs.</param>
/// <param name="PowerLine">What one tile of power line costs.</param>
/// <param name="UnderwaterPowerLine">What one tile of power line under water costs.</param>
/// <param name="Park">What one tile of park costs.</param>
/// <param name="Bulldozer">What the bulldozer costs for each tile it clears, or each zone or plant it knocks down.</param>
/// <param name="Clearing">What a tool pays to clear a tile of trees or rubble before it builds there.</param>
/// <param name="RoadCarriesPower">Whether a road network joins the zones and buildings along it, so one powered building powers the rest, without the road being powered itself.</param>
/// <param name="RailCarriesPower">Whether a rail network joins the zones and buildings along it, as a road does.</param>
public sealed record InfrastructureSpec(
    int Road,
    int Bridge,
    int Rail,
    int Tunnel,
    int PowerLine,
    int UnderwaterPowerLine,
    int Park,
    int Bulldozer,
    int Clearing,
    bool RoadCarriesPower = false,
    bool RailCarriesPower = false);

/// <summary>
/// What a new city starts with, as data rather than code: its money and its tax rate. The base
/// game's are Micropolis', and a mod can change them. A scenario sets its own money.
/// </summary>
/// <param name="StartingFunds">The money a new city starts with.</param>
/// <param name="TaxRate">The tax rate a new city starts with, in percent.</param>
public sealed record EconomySpec(int StartingFunds, int TaxRate);
