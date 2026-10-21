namespace CityLimits.Core.Simulation;

/// <summary>Something the city tells the player, as Micropolis' messages do.</summary>
public enum CityMessage
{
    /// <summary>There are more than 10 zones and no power plant.</summary>
    NeedPower,

    /// <summary>Fewer than 7 in 10 of the zones and plants have power.</summary>
    Blackouts,

    /// <summary>The plants ran out of capacity before power reached everything connected to them.</summary>
    Brownouts,
}
