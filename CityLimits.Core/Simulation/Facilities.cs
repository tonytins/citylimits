using CityLimits.Core.Saving;

namespace CityLimits.Core.Simulation;

/// <summary>Which of the city's demands are held back, each until the city builds what it lacks.</summary>
[Flags]
public enum DemandCaps
{
    /// <summary>No demand is held back.</summary>
    None = 0,

    /// <summary>Residential demand can't rise above zero until the city has a stadium.</summary>
    Residential = 1,

    /// <summary>Commercial demand can't rise above zero until the city has an airport.</summary>
    Commercial = 2,

    /// <summary>Industrial demand can't rise above zero until the city has a seaport.</summary>
    Industrial = 4,
}

/// <summary>
/// The city's stadiums, seaports, and airports, as Micropolis has them. The scan counts each as it
/// passes its centre, and the count starts again every cycle. Once a big city lacks one, the demand
/// it serves is held at zero: residents want a stadium, industry a seaport, and commerce an
/// airport, past the cap each one's spec gives (Micropolis' 500, 70, and 100, in the census' own
/// units). Each is checked, and the city told, at its own count of Micropolis' 64.
/// </summary>
public sealed class Facilities
{
    /// <summary>The stadiums counted so far in this cycle's scan.</summary>
    public int Stadiums { get; private set; }

    /// <summary>The seaports counted so far in this cycle's scan.</summary>
    public int Seaports { get; private set; }

    /// <summary>The airports counted so far in this cycle's scan.</summary>
    public int Airports { get; private set; }

    /// <summary>The demands held back, as last checked.</summary>
    public DemandCaps Caps { get; private set; }

    /// <summary>Starts the count again, at the top of a cycle.</summary>
    internal void StartCount() => (Stadiums, Seaports, Airports) = (0, 0, 0);

    /// <summary>Counts a stadium, a seaport, or an airport, as the scan passes its centre.</summary>
    internal void Count(TileUse use)
    {
        switch (use)
        {
            case TileUse.Stadium:
                Stadiums++;
                break;
            case TileUse.Seaport:
                Seaports++;
                break;
            case TileUse.Airport:
                Airports++;
                break;
        }
    }

    /// <summary>Checks whether residents want a stadium, from the cycle's census; tells whether they do.</summary>
    internal bool CheckStadium(CityCensus census, int cap) => Hold(DemandCaps.Residential, census.Residential > cap && Stadiums == 0);

    /// <summary>Checks whether industry wants a seaport, from the cycle's census; tells whether it does.</summary>
    internal bool CheckSeaport(CityCensus census, int cap) => Hold(DemandCaps.Industrial, census.Industrial > cap && Seaports == 0);

    /// <summary>Checks whether commerce wants an airport, from the cycle's census; tells whether it does.</summary>
    internal bool CheckAirport(CityCensus census, int cap) => Hold(DemandCaps.Commercial, census.Commercial > cap && Airports == 0);

    internal FacilitiesSave Capture() => new()
    {
        Stadiums = Stadiums,
        Seaports = Seaports,
        Airports = Airports,
        ResidentialCapped = Caps.HasFlag(DemandCaps.Residential),
        CommercialCapped = Caps.HasFlag(DemandCaps.Commercial),
        IndustrialCapped = Caps.HasFlag(DemandCaps.Industrial),
        Buildings = [],
    };

    internal void Restore(FacilitiesSave save)
    {
        Stadiums = SaveChecks.InRange(save.Stadiums, 0, int.MaxValue, "facilities.stadiums");
        Seaports = SaveChecks.InRange(save.Seaports, 0, int.MaxValue, "facilities.seaports");
        Airports = SaveChecks.InRange(save.Airports, 0, int.MaxValue, "facilities.airports");
        Caps = (save.ResidentialCapped ? DemandCaps.Residential : DemandCaps.None)
            | (save.CommercialCapped ? DemandCaps.Commercial : DemandCaps.None)
            | (save.IndustrialCapped ? DemandCaps.Industrial : DemandCaps.None);
    }

    private bool Hold(DemandCaps cap, bool held)
    {
        Caps = held ? Caps | cap : Caps & ~cap;
        return held;
    }
}
