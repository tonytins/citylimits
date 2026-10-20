namespace CityLimits.Core.World;

/// <summary>Why a piece of building work did not happen.</summary>
public enum BuildRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>The tile is not on the map.</summary>
    OffTheMap,

    /// <summary>The ground, or what is already on it, does not allow the work.</summary>
    CannotBuildHere,

    /// <summary>The tile is already what the player asked for.</summary>
    AlreadyDone,

    /// <summary>The city cannot pay for the work.</summary>
    NotEnoughFunds,

    /// <summary>There was nothing on the tile to clear.</summary>
    NothingToClear,

    /// <summary>The tile is part of a zone, which the bulldozer clears only through its centre.</summary>
    PartOfAZone,
}
