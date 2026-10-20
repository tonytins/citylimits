namespace CityLimits.Core.World;

/// <summary>What came of a piece of building work.</summary>
public enum BuildStatus
{
    /// <summary>The world changed.</summary>
    Built,

    /// <summary>The tile was already what the player asked for, so nothing changed.</summary>
    Unchanged,

    /// <summary>Something stopped the work.</summary>
    Refused,
}
