namespace CityLimits.Core.Simulation;

/// <summary>
/// The ordinances a city has enacted, after SimCity 3000's: any of them may be enacted or
/// repealed at any time. While enacted, each earns or costs money by the city's population, which
/// the budget collects with the taxes, and changes what its spec says, which the city reads from
/// here (<see cref="Effect"/>). Each quarter, the city can ask for the ordinance that would help it
/// most, as SimCity 3000's ticker carries petitions: one it hasn't enacted, for its brownouts,
/// crime, pollution, fires, money, or slack business, in that order.
/// </summary>
public sealed class CityOrdinances
{
    /// <summary>How many people a city needs before anyone asks for an ordinance.</summary>
    public const int PetitionPopulation = 2000;

    // The revenue ordinances a city in the red is asked for, the least painful first.
    private static readonly string[] Revenue = ["salesTax", "incomeTax", "parkingFines", "legalizedGambling"];

    private const int SlackDemand = -500;
    private const int HighCrime = 100;
    private const int HighPollution = 60;

    private readonly CityDefinitions _definitions;
    private readonly bool[] _enacted;
    private readonly int[] _effects = new int[Enum.GetValues<OrdinanceEffect>().Length];

    /// <summary>Creates a city's ordinances, none enacted, from the definitions it runs on.</summary>
    public CityOrdinances(CityDefinitions definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = definitions;
        _enacted = new bool[definitions.Ordinances.Count];
    }

    /// <summary>Every ordinance, in the order the player is shown them.</summary>
    public IReadOnlyList<OrdinanceSpec> All => _definitions.Ordinances;

    /// <summary>The ids of the ordinances enacted, in the order of <see cref="All"/>.</summary>
    public IEnumerable<string> Enacted => All.Where((_, i) => _enacted[i]).Select(ordinance => ordinance.Id);

    /// <summary>Tells whether an ordinance is enacted; one that doesn't exist isn't.</summary>
    public bool IsEnacted(string id) => IndexOf(id) is var i && i >= 0 && _enacted[i];

    /// <summary>What the enacted ordinances change one thing by, together, in its own units.</summary>
    public int Effect(OrdinanceEffect effect) => _effects[(int)effect];

    /// <summary>A value changed by a percent effect: 100 plus the ordinances' percent of it, never below none.</summary>
    public int Scaled(OrdinanceEffect effect, int value) => (int)Math.Max(0L, (long)value * (100 + Effect(effect)) / 100);

    /// <summary>
    /// What the enacted ordinances earn and cost in a year, for a city of the population given,
    /// each as a sum of its own, the cost as a positive number.
    /// </summary>
    public (int Income, int Cost) YearlyMoney(int population)
    {
        long income = 0;
        long cost = 0;
        for (var i = 0; i < _enacted.Length; i++)
        {
            if (!_enacted[i])
            {
                continue;
            }

            var money = All[i].YearlyMoney(population);
            if (money > 0)
            {
                income += money;
            }
            else
            {
                cost -= money;
            }
        }

        return ((int)Math.Min(income, int.MaxValue), (int)Math.Min(cost, int.MaxValue));
    }

    /// <summary>Enacts or repeals an ordinance. Tells whether that changed anything.</summary>
    /// <exception cref="ArgumentException">There is no ordinance with that id.</exception>
    public bool Set(string id, bool enacted)
    {
        var i = IndexOf(id);
        if (i < 0)
        {
            throw new ArgumentException($"'{id}' is not an ordinance.", nameof(id));
        }

        if (_enacted[i] == enacted)
        {
            return false;
        }

        _enacted[i] = enacted;
        Sum();
        return true;
    }

    /// <summary>
    /// The ordinance the city most wants and hasn't enacted, from how it stands, or null when it
    /// wants none, or is too small to ask.
    /// </summary>
    public OrdinanceSpec? MostWanted(CitySimulation city)
    {
        ArgumentNullException.ThrowIfNull(city);
        if (city.Census.CityPopulation < PetitionPopulation)
        {
            return null;
        }

        if (city.Power.IsBrownedOut && Wanted("energyConservation") is { } power)
        {
            return power;
        }

        if (city.Maps.CrimeAverage > HighCrime && Wanted("neighbourhoodWatch") is { } watch)
        {
            return watch;
        }

        if (city.Maps.PollutionAverage > HighPollution && Wanted("pollutionControls") is { } controls)
        {
            return controls;
        }

        if (city.Fires.Count > 0 && Wanted("smokeDetectors") is { } detectors)
        {
            return detectors;
        }

        if (city.Budget.LastYear is { NeedsAttention: true })
        {
            foreach (var id in Revenue)
            {
                if (Wanted(id) is { } revenue)
                {
                    return revenue;
                }
            }
        }

        if (city.Demand.Commercial < SlackDemand && Wanted("tourismPromotion") is { } tourism)
        {
            return tourism;
        }

        return city.Demand.Industrial < SlackDemand ? Wanted("industrialPromotion") : null;
    }

    internal string[] Capture() => [.. Enacted];

    // A saved id the definitions don't have is refused, as a city's buildings are when its mods are missing.
    internal void Restore(string[]? enacted)
    {
        Array.Clear(_enacted);
        foreach (var id in enacted ?? [])
        {
            var i = IndexOf(id);
            if (i < 0)
            {
                throw new InvalidSave($"ordinances.enacted: '{id}' is not an ordinance.");
            }

            _enacted[i] = true;
        }

        Sum();
    }

    private OrdinanceSpec? Wanted(string id) => IndexOf(id) is var i && i >= 0 && !_enacted[i] ? All[i] : null;

    private int IndexOf(string id)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void Sum()
    {
        Array.Clear(_effects);
        for (var i = 0; i < _enacted.Length; i++)
        {
            if (!_enacted[i])
            {
                continue;
            }

            foreach (var effect in All[i].Effects)
            {
                _effects[(int)effect.Effect] += effect.Amount;
            }
        }
    }
}

/// <summary>What happened to an ordinance, for the ticker to say.</summary>
public enum OrdinanceNewsKind
{
    /// <summary>The city asks for it.</summary>
    Petition,

    /// <summary>The player enacted it.</summary>
    Enacted,

    /// <summary>The player repealed it.</summary>
    Repealed,
}

/// <summary>News of an ordinance: which one, and what happened to it.</summary>
public sealed record OrdinanceNews(OrdinanceSpec Ordinance, OrdinanceNewsKind Kind);
