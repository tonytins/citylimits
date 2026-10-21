namespace CityLimits.Core.Simulation;

/// <summary>
/// The city's census, as in Micropolis. As the scan passes each zone it adds the zone's
/// population to a running count; the count starts again at the top of every cycle. Once a month
/// the counts are recorded, keeping ten years of history, and the city's population is worked out.
/// </summary>
public sealed class CityCensus
{
    /// <summary>How many months of history the census keeps.</summary>
    public const int HistoryMonths = 120;

    private readonly int[] _residentialHistory = new int[HistoryMonths];
    private readonly int[] _commercialHistory = new int[HistoryMonths];
    private readonly int[] _industrialHistory = new int[HistoryMonths];

    /// <summary>The residential population counted so far in this cycle's scan.</summary>
    public int Residential { get; private set; }

    /// <summary>The commercial population counted so far in this cycle's scan.</summary>
    public int Commercial { get; private set; }

    /// <summary>The industrial population counted so far in this cycle's scan.</summary>
    public int Industrial { get; private set; }

    /// <summary>
    /// The city's population as the player sees it, worked out once a month from the counts:
    /// residents, and eight for every commercial and industrial unit, all times twenty.
    /// </summary>
    public int CityPopulation { get; private set; }

    /// <summary>The residents' share of <see cref="CityPopulation"/>, as of the last monthly record.</summary>
    public int ResidentPopulation { get; private set; }

    /// <summary>Commerce's share of <see cref="CityPopulation"/>, as of the last monthly record.</summary>
    public int CommercialPopulation { get; private set; }

    /// <summary>Industry's share of <see cref="CityPopulation"/>, as of the last monthly record.</summary>
    public int IndustrialPopulation { get; private set; }

    /// <summary>The residential count recorded a number of months ago, in eighths, as Micropolis keeps it; 0 is the latest.</summary>
    public int ResidentialHistory(int monthsAgo) => _residentialHistory[monthsAgo];

    /// <summary>The commercial count recorded a number of months ago; 0 is the latest.</summary>
    public int CommercialHistory(int monthsAgo) => _commercialHistory[monthsAgo];

    /// <summary>The industrial count recorded a number of months ago; 0 is the latest.</summary>
    public int IndustrialHistory(int monthsAgo) => _industrialHistory[monthsAgo];

    /// <summary>Adds a zone's population to the count, as the scan passes it.</summary>
    public void Add(TileUse use, int population)
    {
        switch (use)
        {
            case TileUse.Residential:
                Residential += population;
                break;
            case TileUse.Commercial:
                Commercial += population;
                break;
            case TileUse.Industrial:
                Industrial += population;
                break;
        }
    }

    /// <summary>Starts the count again, at the top of a cycle.</summary>
    public void Clear()
    {
        Residential = 0;
        Commercial = 0;
        Industrial = 0;
    }

    /// <summary>Records this month's counts at the front of the history, moving the older months back one.</summary>
    public void RecordMonth()
    {
        Array.Copy(_residentialHistory, 0, _residentialHistory, 1, HistoryMonths - 1);
        Array.Copy(_commercialHistory, 0, _commercialHistory, 1, HistoryMonths - 1);
        Array.Copy(_industrialHistory, 0, _industrialHistory, 1, HistoryMonths - 1);
        _residentialHistory[0] = Residential / 8;
        _commercialHistory[0] = Commercial;
        _industrialHistory[0] = Industrial;
        ResidentPopulation = Residential * 20;
        CommercialPopulation = Commercial * 8 * 20;
        IndustrialPopulation = Industrial * 8 * 20;
        CityPopulation = ResidentPopulation + CommercialPopulation + IndustrialPopulation;
    }
}
