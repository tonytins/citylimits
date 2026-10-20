namespace CityLimits.Core.Simulation;

/// <summary>A month in the city's calendar.</summary>
public readonly record struct GameDate
{
    /// <summary>Creates a date, rejecting months outside the calendar.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The year is negative or the month is outside 1 to 12.</exception>
    public GameDate(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(year);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        Year = year;
        Month = month;
    }

    /// <summary>The year.</summary>
    public int Year { get; }

    /// <summary>The month, from 1 to 12.</summary>
    public int Month { get; }

    /// <summary>Returns the date a number of months later, carrying over into later years.</summary>
    public GameDate AddMonths(int months)
    {
        var total = ((Year * 12) + (Month - 1)) + (long)months;
        var year = (int)(total / 12);
        var monthIndex = (int)(total % 12);

        if (monthIndex < 0)
        {
            monthIndex += 12;
            year -= 1;
        }

        return new GameDate(year, monthIndex + 1);
    }

    /// <summary>Returns the date in the form YYYY-MM.</summary>
    public override string ToString() => $"{Year:0000}-{Month:00}";
}
