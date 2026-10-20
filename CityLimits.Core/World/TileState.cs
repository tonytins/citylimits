namespace CityLimits.Core.World;

/// <summary>Everything a tile knows about itself: its ground, its use, and how far it has developed.</summary>
public readonly struct TileState : IEquatable<TileState>
{
    /// <summary>The most developed a zoned tile can get.</summary>
    public const int MaxDevelopment = 3;

    /// <summary>Creates a state, rejecting a development level the city does not have.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The development level is outside 0 to <see cref="MaxDevelopment"/>.</exception>
    public TileState(TileKind kind, TileUse use = TileUse.None, int development = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(development);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(development, MaxDevelopment);

        Kind = kind;
        Use = use;
        Development = development;
    }

    /// <summary>The ground the tile stands on.</summary>
    public TileKind Kind { get; }

    /// <summary>What has been built or zoned on the tile.</summary>
    public TileUse Use { get; }

    /// <summary>How far the tile has developed, from 0 to <see cref="MaxDevelopment"/>.</summary>
    public int Development { get; }

    /// <summary>Tells whether the tile carries a zone.</summary>
    public bool IsZoned => Use is TileUse.Residential or TileUse.Commercial or TileUse.Industrial;

    /// <summary>Tells whether the tile carries a road.</summary>
    public bool IsRoad => Use == TileUse.Road;

    /// <summary>Tells whether two states are the same.</summary>
    public static bool operator ==(TileState left, TileState right) => left.Equals(right);

    /// <summary>Tells whether two states are different.</summary>
    public static bool operator !=(TileState left, TileState right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(TileState other) =>
        Kind == other.Kind && Use == other.Use && Development == other.Development;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TileState other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Use, Development);

    /// <inheritdoc />
    public override string ToString() => $"{Kind} {Use} {Development}";
}
