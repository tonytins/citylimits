using System.Globalization;
using CityLimits.Core.Simulation;

namespace CityLimits.Core.News;

/// <summary>
/// Picks the headlines that keep the news ticker running when nothing else is waiting: a milestone
/// the city's population has just passed, or a new year; otherwise, half the time, news of how the
/// city is doing, and the rest of the time flavour. It reads the city and never changes it, and
/// it picks with its own generator, never the simulation's, so the news can't change what the city
/// does, and isn't saved.
/// </summary>
public sealed class NewsDesk
{
    // A reactive line, once shown, rests for this many headlines before it can be news again.
    private const int ReactiveRest = 12;

    private readonly CitySimulation _city;
    private readonly Random _random;
    private readonly int[] _resting;
    private readonly int[] _flavourBag;
    private int _flavourNext;
    private int _milestonesPassed;
    private int _newYearShown;

    /// <summary>
    /// Creates the desk for a city. The milestones the city has already passed and the year it is
    /// in aren't news, so opening a city doesn't announce them.
    /// </summary>
    public NewsDesk(CitySimulation city, Random random)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(random);
        _city = city;
        _random = random;
        _resting = new int[Headlines.Reactive.Length];
        _flavourBag = [.. Enumerable.Range(0, Headlines.Flavour.Length)];
        _flavourNext = _flavourBag.Length;
        _milestonesPassed = MilestonesPassed(city.Census.CityPopulation);
        _newYearShown = city.Date.Year;
    }

    /// <summary>The next headline for the ticker.</summary>
    public NewsItem Next()
    {
        for (var i = 0; i < _resting.Length; i++)
        {
            _resting[i] = Math.Max(0, _resting[i] - 1);
        }

        var passed = MilestonesPassed(_city.Census.CityPopulation);
        if (passed > _milestonesPassed)
        {
            _milestonesPassed = passed;
            var count = Headlines.PopulationMilestones[passed - 1].ToString("N0", CultureInfo.InvariantCulture);
            return Headline(Headlines.PopulationMilestone.Replace("{count}", count, StringComparison.Ordinal));
        }

        var date = _city.Date;
        if (date.Month == 1 && date.Year > _newYearShown)
        {
            _newYearShown = date.Year;
            return Headline(Headlines.NewYear);
        }

        if (_random.Next(2) == 0 && TryReactive(out var reactive))
        {
            return reactive;
        }

        return Headline(NextFlavour());
    }

    // One of the lines whose state the city is in, and which hasn't been news lately.
    private bool TryReactive(out NewsItem item)
    {
        var eligible = 0;
        for (var i = 0; i < Headlines.Reactive.Length; i++)
        {
            if (_resting[i] == 0 && Headlines.Reactive[i].When(_city))
            {
                eligible++;
            }
        }

        if (eligible == 0)
        {
            item = default;
            return false;
        }

        var pick = _random.Next(eligible);
        for (var i = 0; i < Headlines.Reactive.Length; i++)
        {
            if (_resting[i] != 0 || !Headlines.Reactive[i].When(_city) || pick-- > 0)
            {
                continue;
            }

            _resting[i] = ReactiveRest;
            var lines = Headlines.Reactive[i].Lines;
            item = Headline(lines[_random.Next(lines.Length)]);
            return true;
        }

        item = default;
        return false;
    }

    // Every flavour line once, in a shuffled order, before any comes round again.
    private string NextFlavour()
    {
        if (_flavourNext >= _flavourBag.Length)
        {
            _random.Shuffle(_flavourBag);
            _flavourNext = 0;
        }

        return Headlines.Flavour[_flavourBag[_flavourNext++]];
    }

    private NewsItem Headline(string line) => new(
        line.Replace("{city}", _city.Name, StringComparison.Ordinal)
            .Replace("{year}", _city.Date.Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
        NewsKind.Headline);

    private static int MilestonesPassed(int population)
    {
        var passed = 0;
        while (passed < Headlines.PopulationMilestones.Length && population >= Headlines.PopulationMilestones[passed])
        {
            passed++;
        }

        return passed;
    }
}
