namespace CityLimits.Core.Simulation;

/// <summary>
/// The city's hospitals and churches, as Micropolis has them. Nobody builds one: an empty
/// residential zone becomes one when the city needs it, and one the city has too many of now and
/// then turns back into an empty zone. Each month the city needs one of each for every 256
/// residents the census counted.
/// </summary>
/// <remarks>
/// Micropolis can make a hospital and then a church on the same roll, losing the hospital; the
/// owner chose to fix that, so a roll makes one building, a hospital first when one is needed.
/// </remarks>
public sealed class CommunityBuildings
{
    // The residents each hospital, and each church, serves.
    private const int ResidentsPerBuilding = 256;

    // One the city has too many of turns back into an empty zone one time in this many scans.
    private const int TooManyChance = 21;

    /// <summary>The hospitals the scan has passed this cycle.</summary>
    public int Hospitals { get; private set; }

    /// <summary>The churches the scan has passed this cycle.</summary>
    public int Churches { get; private set; }

    /// <summary>Whether the city needs another hospital (1), has too many (-1), or has the right number (0), as of the last month.</summary>
    public int HospitalNeed { get; private set; }

    /// <summary>Whether the city needs another church (1), has too many (-1), or has the right number (0), as of the last month.</summary>
    public int ChurchNeed { get; private set; }

    /// <summary>Starts a new cycle's count, as the census does.</summary>
    public void StartCount() => (Hospitals, Churches) = (0, 0);

    /// <summary>Works out, once a month, how many hospitals and churches the city's residents call for.</summary>
    public void UpdateNeeds(int residents)
    {
        var wanted = residents / ResidentsPerBuilding;
        HospitalNeed = Math.Sign(wanted - Hospitals);
        ChurchNeed = Math.Sign(wanted - Churches);
    }

    /// <summary>
    /// Counts the hospital or church whose centre the scan has reached, and, when the city has too
    /// many of its kind, now and then turns it back into an empty residential zone. With the city's
    /// fires, a building with any tile burning stays as it is, as a Micropolis zone can't be redrawn then.
    /// </summary>
    public void Visit(TileMap map, DeterministicRandom random, int x, int y, Fires? fires = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);

        var center = map.StateAt(x, y);
        var tooMany = center.Use switch
        {
            TileUse.Hospital => CountAndCheck(isHospital: true),
            TileUse.Church => CountAndCheck(isHospital: false),
            _ => throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is not a hospital or a church."),
        };

        if (tooMany && random.NextInt(0, TooManyChance) == 0)
        {
            Replace(map, fires, x, y, TileUse.Residential, center.Powered);
        }
    }

    /// <summary>
    /// Turns the empty residential zone centred on a tile into a hospital when the city needs one,
    /// or else a church when it needs one, and tells whether it did. A zone with any tile burning can't
    /// be redrawn, so it stays as it is.
    /// </summary>
    public bool TryBuildIn(TileMap map, int x, int y, Fires? fires = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        var powered = map.StateAt(x, y).Powered;
        if (HospitalNeed > 0 && Replace(map, fires, x, y, TileUse.Hospital, powered))
        {
            HospitalNeed = 0;
            return true;
        }

        if (ChurchNeed > 0 && Replace(map, fires, x, y, TileUse.Church, powered))
        {
            ChurchNeed = 0;
            return true;
        }

        return false;
    }

    private bool CountAndCheck(bool isHospital)
    {
        if (isHospital)
        {
            Hospitals++;
            return HospitalNeed < 0;
        }

        Churches++;
        return ChurchNeed < 0;
    }

    // The nine tiles become the new use, the centre keeping its power until the scan looks again;
    // a hole fire left is redrawn with the rest. Nothing changes while any of them burns.
    private static bool Replace(TileMap map, Fires? fires, int x, int y, TileUse use, bool powered)
    {
        if (fires?.AnyBurningIn(x, y) == true)
        {
            return false;
        }

        for (var part = 0; part < ZoneLayout.Parts; part++)
        {
            var (dx, dy) = ZoneLayout.OffsetFromCenter(part);
            map.SetState(x + dx, y + dy, new TileState(TileKind.Grass, use, zonePart: part, powered: part == ZoneLayout.CenterPart && powered));
        }

        return true;
    }

    internal CommunitySave Capture() => new()
    {
        Hospitals = Hospitals,
        Churches = Churches,
        HospitalNeed = HospitalNeed,
        ChurchNeed = ChurchNeed,
    };

    internal void Restore(CommunitySave save)
    {
        Hospitals = SaveChecks.InRange(save.Hospitals, 0, int.MaxValue, "community.hospitals");
        Churches = SaveChecks.InRange(save.Churches, 0, int.MaxValue, "community.churches");
        HospitalNeed = SaveChecks.InRange(save.HospitalNeed, -1, 1, "community.hospitalNeed");
        ChurchNeed = SaveChecks.InRange(save.ChurchNeed, -1, 1, "community.churchNeed");
    }
}
