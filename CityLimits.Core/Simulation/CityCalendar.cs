namespace CityLimits.Core.Simulation;

/// <summary>
/// How the city's time is counted, as in Micropolis. The calendar counts 48ths of a year,
/// four to a month, and each count is a cycle of 16 simulation steps, or phases.
/// </summary>
public static class CityCalendar
{
    /// <summary>The year the calendar counts from, and the year a city starts in unless it is told another.</summary>
    public const int StartYear = 1900;

    /// <summary>The latest year a city can start in, the last the calendar's four-figure years show.</summary>
    public const int LatestStartYear = 9999;

    /// <summary>The calendar counts in a month.</summary>
    public const int CountsPerMonth = 4;

    /// <summary>The calendar counts in a year.</summary>
    public const int CountsPerYear = CountsPerMonth * 12;

    /// <summary>The simulation steps in one count of the calendar: one for each phase of the cycle.</summary>
    public const int StepsPerCount = 16;

    /// <summary>The vertical bands the map is scanned in, one band per phase.</summary>
    public const int ScanBands = 8;

    /// <summary>The simulation steps in a month.</summary>
    public const int StepsPerMonth = StepsPerCount * CountsPerMonth;

    /// <summary>The simulation steps in a year.</summary>
    public const int StepsPerYear = StepsPerCount * CountsPerYear;

    /// <summary>The month the calendar shows after the given number of counts.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    public static GameDate DateAt(long cityTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cityTime);

        var year = StartYear + (cityTime / CountsPerYear);
        var month = (int)(cityTime % CountsPerYear / CountsPerMonth) + 1;
        return new GameDate((int)Math.Min(year, int.MaxValue), month);
    }

    /// <summary>The columns a band of the scan covers: an equal share of the map, with the last band taking what is left over.</summary>
    public static (int First, int End) BandColumns(int band, int mapWidth)
    {
        var width = mapWidth / ScanBands;
        var first = band * width;
        return (first, band == ScanBands - 1 ? mapWidth : first + width);
    }
}
