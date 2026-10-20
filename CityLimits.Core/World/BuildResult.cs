namespace CityLimits.Core.World;

/// <summary>What came of a piece of building work, and what it cost.</summary>
/// <param name="Status">Whether the world changed.</param>
/// <param name="Reason">Why the work did not happen.</param>
/// <param name="Cost">The money the work cost the city.</param>
/// <param name="TilesChanged">How many tiles the work changed.</param>
public readonly record struct BuildResult(BuildStatus Status, BuildRefusal Reason, int Cost = 0, int TilesChanged = 0)
{
    /// <summary>Tells whether the world changed.</summary>
    public bool Changed => Status == BuildStatus.Built;

    /// <summary>The result of work that was refused.</summary>
    public static BuildResult Refused(BuildRefusal reason, int cost = 0) =>
        new(BuildStatus.Refused, reason, cost);

    /// <summary>The result of work that found the tile already as it should be.</summary>
    public static BuildResult Unchanged(BuildRefusal reason) => new(BuildStatus.Unchanged, reason);

    /// <summary>The result of work that changed the world.</summary>
    public static BuildResult Built(int cost = 0, int tilesChanged = 1) =>
        new(BuildStatus.Built, BuildRefusal.None, cost, tilesChanged);
}
