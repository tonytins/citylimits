namespace CityLimits.Game;

/// <summary>The tools the player can hold.</summary>
public enum Tool
{
    /// <summary>Look at a tile without changing it.</summary>
    Inspect,

    /// <summary>Draw a road.</summary>
    Road,

    /// <summary>Zone land as residential.</summary>
    Residential,

    /// <summary>Zone land as commercial.</summary>
    Commercial,

    /// <summary>Zone land as industrial.</summary>
    Industrial,
}
