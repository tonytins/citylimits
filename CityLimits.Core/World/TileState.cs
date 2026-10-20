namespace CityLimits.Core.World;

/// <summary>
/// Everything a tile knows about itself: its ground, its use, which part of a zone it is, and
/// how far its zone has developed. A zone's development is kept on its centre tile only, so the
/// map holds one copy of it.
/// </summary>
public readonly struct TileState : IEquatable<TileState>
{
    /// <summary>The most developed a zone can get.</summary>
    public const int MaxDevelopment = 3;

    /// <summary>Creates a state, rejecting a development level or a zone part the city does not have.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The development level is outside 0 to <see cref="MaxDevelopment"/>, or the zone part is
    /// outside 0 to 8.
    /// </exception>
    public TileState(TileKind kind, TileUse use = TileUse.None, int development = 0, int zonePart = ZoneLayout.CenterPart)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(development);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(development, MaxDevelopment);
        ArgumentOutOfRangeException.ThrowIfNegative(zonePart);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(zonePart, ZoneLayout.Parts);

        Kind = kind;
        Use = use;
        Development = development;

        // Only a zone has parts; any other tile keeps the default, so equal tiles compare equal.
        ZonePart = TileRules.IsZone(use) ? zonePart : ZoneLayout.CenterPart;
    }

    /// <summary>The ground the tile stands on.</summary>
    public TileKind Kind { get; }

    /// <summary>What has been built or zoned on the tile.</summary>
    public TileUse Use { get; }

    /// <summary>How far the zone has developed, from 0 to <see cref="MaxDevelopment"/>; kept on the zone's centre.</summary>
    public int Development { get; }

    /// <summary>Which part of its zone the tile is, 0 to 8 across and then down; 4 is the centre.</summary>
    public int ZonePart { get; }

    /// <summary>Tells whether the tile carries a zone.</summary>
    public bool IsZoned => TileRules.IsZone(Use);

    /// <summary>Tells whether the tile is the centre of a zone, where the simulation works on it.</summary>
    public bool IsZoneCenter => IsZoned && ZonePart == ZoneLayout.CenterPart;

    /// <summary>Tells whether the tile carries a road, over land or over water.</summary>
    public bool IsRoad => Use == TileUse.Road;

    /// <summary>Tells whether the tile is a road over water.</summary>
    public bool IsBridge => IsRoad && Kind == TileKind.Water;

    /// <summary>Tells whether two states are the same.</summary>
    public static bool operator ==(TileState left, TileState right) => left.Equals(right);

    /// <summary>Tells whether two states are different.</summary>
    public static bool operator !=(TileState left, TileState right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(TileState other) =>
        Kind == other.Kind && Use == other.Use && Development == other.Development && ZonePart == other.ZonePart;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TileState other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Use, Development, ZonePart);

    /// <inheritdoc />
    public override string ToString() => IsZoned ? $"{Kind} {Use} {Development} part {ZonePart}" : $"{Kind} {Use} {Development}";
}
