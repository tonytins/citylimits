namespace CityLimits.Core.World;

/// <summary>
/// Everything a tile knows about itself: its ground, its use, which part of a zone it is, and how
/// its zone has grown. As in Micropolis, a zone's building is kept on its centre tile: its
/// development level and its land-value class. A residential zone with no building yet grows
/// single houses instead, and each house is kept on the outer tile it stands on.
/// </summary>
public readonly struct TileState : IEquatable<TileState>
{
    /// <summary>The most building levels any zone has; each zone's own number is in its spec.</summary>
    public const int MaxDevelopment = 5;

    /// <summary>The most land-value classes any zone has; each zone's own number is in its spec.</summary>
    public const int MaxValueClass = 3;

    /// <summary>The number of single-house drawings, three for each land-value class.</summary>
    public const int HouseDrawings = 12;

    /// <summary>Creates a state, rejecting a level, a class, a house, or a zone part the city does not have.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The development level is outside 0 to <see cref="MaxDevelopment"/>, the value class outside
    /// 0 to <see cref="MaxValueClass"/>, the house outside 0 to <see cref="HouseDrawings"/>, or the
    /// zone part outside 0 to 8.
    /// </exception>
    public TileState(
        TileKind kind,
        TileUse use = TileUse.None,
        int development = 0,
        int zonePart = ZoneLayout.CenterPart,
        int valueClass = 0,
        int house = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(development);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(development, MaxDevelopment);
        ArgumentOutOfRangeException.ThrowIfNegative(zonePart);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(zonePart, ZoneLayout.Parts);
        ArgumentOutOfRangeException.ThrowIfNegative(valueClass);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(valueClass, MaxValueClass);
        ArgumentOutOfRangeException.ThrowIfNegative(house);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(house, HouseDrawings);

        Kind = kind;
        Use = use;

        // Only a zone has parts, a building, or houses; any other tile keeps the defaults, so
        // equal tiles compare equal.
        var zoned = TileRules.IsZone(use);
        ZonePart = zoned ? zonePart : ZoneLayout.CenterPart;
        Development = zoned ? development : 0;
        ValueClass = zoned ? valueClass : 0;
        House = zoned && zonePart != ZoneLayout.CenterPart ? house : 0;
    }

    /// <summary>The ground the tile stands on.</summary>
    public TileKind Kind { get; }

    /// <summary>What has been built or zoned on the tile.</summary>
    public TileUse Use { get; }

    /// <summary>
    /// The zone's building level, kept on its centre: 0 when it has no building yet, then 1 up to
    /// the number of densities its spec has.
    /// </summary>
    public int Development { get; }

    /// <summary>The land-value class of the zone's building, kept on its centre: 0 is the lowest.</summary>
    public int ValueClass { get; }

    /// <summary>The single house on an outer tile of a residential zone: 0 for none, or 1 to 12 for its drawing.</summary>
    public int House { get; }

    /// <summary>Which part of its zone the tile is, 0 to 8 across and then down; 4 is the centre.</summary>
    public int ZonePart { get; }

    /// <summary>Tells whether the tile carries a zone.</summary>
    public bool IsZoned => TileRules.IsZone(Use);

    /// <summary>Tells whether the tile is the centre of a zone, where the simulation works on it.</summary>
    public bool IsZoneCenter => IsZoned && ZonePart == ZoneLayout.CenterPart;

    /// <summary>Tells whether the tile is an outer tile of a zone with a house on it.</summary>
    public bool HasHouse => House > 0;

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
        Kind == other.Kind && Use == other.Use && Development == other.Development &&
        ZonePart == other.ZonePart && ValueClass == other.ValueClass && House == other.House;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TileState other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Use, Development, ZonePart, ValueClass, House);

    /// <inheritdoc />
    public override string ToString() => IsZoned
        ? $"{Kind} {Use} {Development} part {ZonePart} class {ValueClass} house {House}"
        : $"{Kind} {Use}";
}
