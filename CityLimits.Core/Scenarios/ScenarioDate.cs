using System.Diagnostics.CodeAnalysis;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.Scenarios;

/// <summary>
/// A moment on the city's calendar as a scenario names it: a month, and the week of it, from 1 to
/// 4, the calendar's quarter-months. Week 1, the default, is the month's first step.
/// </summary>
public readonly record struct ScenarioDate(int Year, int Month, int Week = 1)
{
    /// <summary>How many weeks the calendar has in a month: its counts.</summary>
    public const int WeeksPerMonth = CityCalendar.CountsPerMonth;

    /// <summary>The calendar's count at the date: 48ths of a year since January 1900.</summary>
    public long Count => ((long)(Year - CityCalendar.StartYear) * CityCalendar.CountsPerYear) + ((Month - 1) * CityCalendar.CountsPerMonth) + (Week - 1);

    /// <summary>The step the date begins on, counted from January 1900.</summary>
    public long Step => Count * CityCalendar.StepsPerCount;

    /// <summary>The date a calendar count falls in.</summary>
    public static ScenarioDate AtCount(long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var month = CityCalendar.DateAt(count);
        return new ScenarioDate(month.Year, month.Month, (int)(count % CityCalendar.CountsPerMonth) + 1);
    }

    /// <summary>
    /// Reads a month as a scenario writes it, <c>YYYY-MM</c>, in a year from 1900 to 9999, with the
    /// week it is given; anything else is refused.
    /// </summary>
    public static bool TryParse(string? text, int week, [NotNullWhen(true)] out ScenarioDate? date)
    {
        date = null;
        if (text is not { Length: 7 } || text[4] != '-' || !text.Where((_, i) => i != 4).All(char.IsAsciiDigit))
        {
            return false;
        }

        var year = int.Parse(text.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);
        var month = int.Parse(text.AsSpan(5, 2), System.Globalization.CultureInfo.InvariantCulture);
        if (year is < CityCalendar.StartYear or > CityCalendar.LatestStartYear || month is < 1 or > 12 || week is < 1 or > WeeksPerMonth)
        {
            return false;
        }

        date = new ScenarioDate(year, month, week);
        return true;
    }

    /// <summary>The date as a scenario writes it: <c>YYYY-MM</c>, and the week when it isn't the first.</summary>
    public override string ToString() => Week == 1 ? $"{Year:0000}-{Month:00}" : $"{Year:0000}-{Month:00} week {Week}";
}
