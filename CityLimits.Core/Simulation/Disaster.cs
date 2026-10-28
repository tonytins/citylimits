namespace CityLimits.Core.Simulation;

/// <summary>A disaster that can strike the city on purpose, as a scenario's events strike it.</summary>
public enum Disaster
{
    /// <summary>A fire breaks out on something that can burn.</summary>
    Fire,

    /// <summary>The water rises from a shore onto the land beside it.</summary>
    Flood,

    /// <summary>An earthquake shakes the city.</summary>
    Earthquake,

    /// <summary>A nuclear plant melts down, leaving radioactive ground around it.</summary>
    Meltdown,

    /// <summary>The plane in the air comes down where it flies.</summary>
    PlaneCrash,

    /// <summary>The ship at sea runs aground where it sails.</summary>
    Shipwreck,
}
