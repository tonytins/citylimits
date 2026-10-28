namespace CityLimits.Core.Economy;

/// <summary>
/// The city's taxes and upkeep, worked out as Micropolis does. Every count, the year's tax income
/// and upkeep are estimated from the city as it stands, and a 48th of each is set aside; at the
/// year's end the city collects what was set aside and pays it out. Upkeep is paid at its funding
/// level, roads first, then fire, then police, and whatever the money can't cover is cut.
/// </summary>
/// <remarks>
/// The budget is always automatic, a liberty the owner chose: every request is paid at its funding
/// level when the money allows. Until difficulty can be chosen, the factors are Easy's.
/// </remarks>
public sealed class CityBudget
{
    /// <summary>The tax rate a new city starts with, in percent.</summary>
    public const int DefaultTaxRate = 7;

    /// <summary>The highest tax rate, in percent.</summary>
    public const int MaxTaxRate = 20;

    /// <summary>The funding level each kind of upkeep starts at, in percent.</summary>
    public const int FullFunding = 100;

    /// <summary>The counts in a year; a year's estimate is set aside a 48th at a time.</summary>
    public const int CountsPerYear = 48;

    /// <summary>What funding gives road upkeep at full, and what it scales down from.</summary>
    public const int FullRoadEffect = 32;

    /// <summary>What funding gives fire and police coverage at full, and what it scales down from.</summary>
    public const int FullServiceEffect = 1000;


    // Tax income is population times average land value over this, times the tax rate, times
    // Easy's factor; road upkeep is the road count times Easy's factor. They are Micropolis'.
    private const int LandValueDivisor = 120;
    private const double EasyTaxFactor = 1.4;
    private const double EasyRoadFactor = 0.7;

    // What one fire station and one police station cost to keep up each year, from the services spec.
    private readonly int _fireStationUpkeep;
    private readonly int _policeStationUpkeep;

    // The year so far, in full-year estimates added up once a count.
    private long _taxFund;
    private long _roadFund;
    private long _fireFund;
    private long _policeFund;
    private long _ordinanceIncomeFund;
    private long _ordinanceCostFund;
    private long _dealIncomeFund;
    private long _dealCostFund;
    private bool _cut;

    /// <summary>Creates a budget whose stations cost the given upkeep a year: the base game's, $100 each, when none is given.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An upkeep is negative.</exception>
    public CityBudget(int fireStationUpkeep = 100, int policeStationUpkeep = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fireStationUpkeep);
        ArgumentOutOfRangeException.ThrowIfNegative(policeStationUpkeep);
        (_fireStationUpkeep, _policeStationUpkeep) = (fireStationUpkeep, policeStationUpkeep);
    }

    /// <summary>The tax rate, in percent, from 0 to <see cref="MaxTaxRate"/>.</summary>
    public int TaxRate { get; private set; } = DefaultTaxRate;

    /// <summary>How much of the roads' upkeep the city pays, in percent, from 0 to <see cref="FullFunding"/>.</summary>
    public int RoadFunding { get; private set; } = FullFunding;

    /// <summary>How much of the fire stations' upkeep the city pays, in percent.</summary>
    public int FireFunding { get; private set; } = FullFunding;

    /// <summary>How much of the police stations' upkeep the city pays, in percent.</summary>
    public int PoliceFunding { get; private set; } = FullFunding;

    /// <summary>The tax rate as of the last count, which is the one demand feels.</summary>
    public int TaxEffect { get; private set; } = DefaultTaxRate;

    /// <summary>How well the roads were funded at the last count, from 0 to <see cref="FullRoadEffect"/>.</summary>
    public int RoadEffect { get; private set; } = FullRoadEffect;

    /// <summary>How well the fire stations were funded at the last count, from 0 to <see cref="FullServiceEffect"/>.</summary>
    public int FireEffect { get; private set; } = FullServiceEffect;

    /// <summary>How well the police stations were funded at the last count, from 0 to <see cref="FullServiceEffect"/>.</summary>
    public int PoliceEffect { get; private set; } = FullServiceEffect;

    /// <summary>The last year the city settled, or null before the first.</summary>
    public BudgetYear? LastYear { get; private set; }

    /// <summary>The year's budget as the last count estimated it, with what each kind of upkeep asks; null before the first count.</summary>
    public BudgetYear? Estimated { get; private set; }

    /// <summary>Sets the tax rate.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The rate is outside 0 to <see cref="MaxTaxRate"/>.</exception>
    public void SetTaxRate(int rate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rate, MaxTaxRate);
        TaxRate = rate;
    }

    /// <summary>Sets how much of each kind of upkeep the city pays, in percent.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A level is outside 0 to <see cref="FullFunding"/>.</exception>
    public void SetFunding(int roads, int fire, int police)
    {
        foreach (var level in (int[])[roads, fire, police])
        {
            ArgumentOutOfRangeException.ThrowIfNegative(level);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(level, FullFunding);
        }

        (RoadFunding, FireFunding, PoliceFunding) = (roads, fire, police);
    }

    /// <summary>
    /// Works out the year's budget from the city as it stands, sets a 48th of it aside, and
    /// updates the effects of funding and the tax.
    /// </summary>
    /// <param name="population">The city's population as Micropolis taxes it: residents in eighths, plus commerce and industry.</param>
    /// <param name="landValueAverage">The average land value of the blocks with something built on them.</param>
    /// <param name="roadTiles">The road tiles the scan counted, a bridge counting five, a road in heavy traffic one more, and each rail tile two.</param>
    /// <param name="fireStations">The fire stations the scan counted.</param>
    /// <param name="policeStations">The police stations the scan counted.</param>
    /// <param name="funds">The money the city has now, which caps what upkeep can be paid.</param>
    /// <param name="ordinanceIncome">What the city's ordinances earn in a year.</param>
    /// <param name="ordinanceCost">What the city's ordinances cost in a year, paid after the police.</param>
    /// <param name="dealIncome">What the city's deals with its neighbours earn in a year.</param>
    /// <param name="dealCost">What the city's deals with its neighbours cost in a year, paid last.</param>
    public BudgetYear Estimate(int population, int landValueAverage, int roadTiles, int fireStations, int policeStations, int funds, int ordinanceIncome = 0, int ordinanceCost = 0, int dealIncome = 0, int dealCost = 0)
    {
        // As in Micropolis, the population and land value are divided as whole numbers first.
        var taxIncome = Round((long)population * landValueAverage / LandValueDivisor * TaxRate * EasyTaxFactor);
        var roadRequest = Round(roadTiles * EasyRoadFactor);
        var fireRequest = _fireStationUpkeep * fireStations;
        var policeRequest = _policeStationUpkeep * policeStations;

        var roadFunded = Round(roadRequest * RoadFunding / 100.0);
        var fireFunded = Round(fireRequest * FireFunding / 100.0);
        var policeFunded = Round(policeRequest * PoliceFunding / 100.0);

        // The money there is pays roads first, then fire, then police; the rest goes unpaid.
        // The ordinances and the deals, CityLimits' own, earn with the taxes and are paid last.
        var available = Math.Max(0L, (long)funds + taxIncome + ordinanceIncome + dealIncome);
        var cut = false;
        (roadFunded, available, cut) = Pay(roadFunded, available, cut);
        (fireFunded, available, cut) = Pay(fireFunded, available, cut);
        (policeFunded, available, cut) = Pay(policeFunded, available, cut);
        (var ordinancesPaid, available, cut) = Pay(ordinanceCost, available, cut);
        (var dealsPaid, _, cut) = Pay(dealCost, available, cut);

        return new BudgetYear(TaxRate, taxIncome, roadRequest, roadFunded, fireRequest, fireFunded, policeRequest, policeFunded, cut, ordinanceIncome, ordinancesPaid, dealIncome, dealsPaid);
    }

    /// <summary>
    /// Takes one count's estimate: sets a 48th of the year aside, and updates the effects of
    /// funding and the tax, as Micropolis does each count.
    /// </summary>
    public void Accrue(BudgetYear estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        _taxFund += estimate.TaxIncome;
        _roadFund += estimate.RoadFunded;
        _fireFund += estimate.FireFunded;
        _policeFund += estimate.PoliceFunded;
        _ordinanceIncomeFund += estimate.OrdinanceIncome;
        _ordinanceCostFund += estimate.OrdinanceCost;
        _dealIncomeFund += estimate.DealIncome;
        _dealCostFund += estimate.DealCost;
        _cut |= estimate.Cut;
        Estimated = estimate;

        TaxEffect = estimate.TaxRate;
        RoadEffect = Effect(estimate.RoadFunded, estimate.RoadRequest, FullRoadEffect);
        FireEffect = Effect(estimate.FireFunded, estimate.FireRequest, FullServiceEffect);
        PoliceEffect = Effect(estimate.PoliceFunded, estimate.PoliceRequest, FullServiceEffect);
    }

    /// <summary>
    /// Closes the year: collects the taxes set aside, pays the upkeep set aside, and starts the
    /// next year. As in Micropolis, the money can go below zero.
    /// </summary>
    public BudgetYear Settle(Treasury treasury)
    {
        ArgumentNullException.ThrowIfNull(treasury);

        var year = new BudgetYear(
            TaxEffect,
            (int)(_taxFund / CountsPerYear),
            0,
            (int)(_roadFund / CountsPerYear),
            0,
            (int)(_fireFund / CountsPerYear),
            0,
            (int)(_policeFund / CountsPerYear),
            _cut,
            (int)(_ordinanceIncomeFund / CountsPerYear),
            (int)(_ordinanceCostFund / CountsPerYear),
            (int)(_dealIncomeFund / CountsPerYear),
            (int)(_dealCostFund / CountsPerYear));
        treasury.Settle(year.CashFlow);

        (_taxFund, _roadFund, _fireFund, _policeFund, _cut) = (0, 0, 0, 0, false);
        (_ordinanceIncomeFund, _ordinanceCostFund, _dealIncomeFund, _dealCostFund) = (0, 0, 0, 0);
        LastYear = year;
        return year;
    }

    internal BudgetSave Capture() => new()
    {
        TaxRate = TaxRate,
        RoadFunding = RoadFunding,
        FireFunding = FireFunding,
        PoliceFunding = PoliceFunding,
        TaxEffect = TaxEffect,
        RoadEffect = RoadEffect,
        FireEffect = FireEffect,
        PoliceEffect = PoliceEffect,
        TaxFund = _taxFund,
        RoadFund = _roadFund,
        FireFund = _fireFund,
        PoliceFund = _policeFund,
        Cut = _cut,
        LastYear = SaveOf(LastYear),
        Estimated = SaveOf(Estimated),
    };

    internal void Restore(BudgetSave save)
    {
        TaxRate = SaveChecks.InRange(save.TaxRate, 0, MaxTaxRate, "budget.taxRate");
        RoadFunding = SaveChecks.InRange(save.RoadFunding, 0, FullFunding, "budget.roadFunding");
        FireFunding = SaveChecks.InRange(save.FireFunding, 0, FullFunding, "budget.fireFunding");
        PoliceFunding = SaveChecks.InRange(save.PoliceFunding, 0, FullFunding, "budget.policeFunding");
        TaxEffect = SaveChecks.InRange(save.TaxEffect, 0, MaxTaxRate, "budget.taxEffect");
        RoadEffect = SaveChecks.InRange(save.RoadEffect, 0, FullRoadEffect, "budget.roadEffect");
        FireEffect = SaveChecks.InRange(save.FireEffect, 0, FullServiceEffect, "budget.fireEffect");
        PoliceEffect = SaveChecks.InRange(save.PoliceEffect, 0, FullServiceEffect, "budget.policeEffect");
        _taxFund = SaveChecks.InRange(save.TaxFund, 0, long.MaxValue, "budget.taxFund");
        _roadFund = SaveChecks.InRange(save.RoadFund, 0, long.MaxValue, "budget.roadFund");
        _fireFund = SaveChecks.InRange(save.FireFund, 0, long.MaxValue, "budget.fireFund");
        _policeFund = SaveChecks.InRange(save.PoliceFund, 0, long.MaxValue, "budget.policeFund");
        _cut = save.Cut;
        LastYear = YearOf(save.LastYear);
        Estimated = YearOf(save.Estimated);
    }

    // The ordinances' money is CityLimits' own, so it is saved in the ordinances' section, not the
    // budget's, which keeps Micropolis' shape for older versions.
    internal (long IncomeFund, long CostFund, int[]? LastYear, int[]? Estimated) CaptureOrdinances() =>
        (_ordinanceIncomeFund, _ordinanceCostFund, MoneyOf(LastYear), MoneyOf(Estimated));

    internal void RestoreOrdinances(long incomeFund, long costFund, int[]? lastYear, int[]? estimated, string at)
    {
        _ordinanceIncomeFund = SaveChecks.InRange(incomeFund, 0, long.MaxValue, $"{at}.incomeFund");
        _ordinanceCostFund = SaveChecks.InRange(costFund, 0, long.MaxValue, $"{at}.costFund");
        LastYear = WithMoney(LastYear, lastYear, $"{at}.lastYear");
        Estimated = WithMoney(Estimated, estimated, $"{at}.estimated");
    }

    // The deals' money, saved in the connections' section, as the ordinances' is in theirs.
    internal (long IncomeFund, long CostFund, int[]? LastYear, int[]? Estimated) CaptureDeals() =>
        (_dealIncomeFund, _dealCostFund, DealMoneyOf(LastYear), DealMoneyOf(Estimated));

    internal void RestoreDeals(long incomeFund, long costFund, int[]? lastYear, int[]? estimated, string at)
    {
        _dealIncomeFund = SaveChecks.InRange(incomeFund, 0, long.MaxValue, $"{at}.incomeFund");
        _dealCostFund = SaveChecks.InRange(costFund, 0, long.MaxValue, $"{at}.costFund");
        LastYear = WithDealMoney(LastYear, lastYear, $"{at}.lastYear");
        Estimated = WithDealMoney(Estimated, estimated, $"{at}.estimated");
    }

    private static int[]? MoneyOf(BudgetYear? year) => year is null ? null : [year.OrdinanceIncome, year.OrdinanceCost];

    private static int[]? DealMoneyOf(BudgetYear? year) => year is null ? null : [year.DealIncome, year.DealCost];

    private static BudgetYear? WithMoney(BudgetYear? year, int[]? money, string at) =>
        Pair(year, money, at) is { } pair ? year! with { OrdinanceIncome = pair.Income, OrdinanceCost = pair.Cost } : year;

    private static BudgetYear? WithDealMoney(BudgetYear? year, int[]? money, string at) =>
        Pair(year, money, at) is { } pair ? year! with { DealIncome = pair.Income, DealCost = pair.Cost } : year;

    private static (int Income, int Cost)? Pair(BudgetYear? year, int[]? money, string at)
    {
        if (year is null || money is null)
        {
            return null;
        }

        if (money.Length != 2 || money[0] < 0 || money[1] < 0)
        {
            throw new InvalidSave($"{at}: needs the income and the cost, each 0 or more.");
        }

        return (money[0], money[1]);
    }

    private static BudgetYearSave? SaveOf(BudgetYear? year) => year is null ? null : new()
    {
        TaxRate = year.TaxRate,
        TaxIncome = year.TaxIncome,
        RoadRequest = year.RoadRequest,
        RoadFunded = year.RoadFunded,
        FireRequest = year.FireRequest,
        FireFunded = year.FireFunded,
        PoliceRequest = year.PoliceRequest,
        PoliceFunded = year.PoliceFunded,
        Cut = year.Cut,
    };

    private static BudgetYear? YearOf(BudgetYearSave? save) => save is null ? null : new(
        SaveChecks.InRange(save.TaxRate, 0, MaxTaxRate, "budget year: taxRate"),
        save.TaxIncome,
        save.RoadRequest,
        save.RoadFunded,
        save.FireRequest,
        save.FireFunded,
        save.PoliceRequest,
        save.PoliceFunded,
        save.Cut);

    private static (int Paid, long Left, bool Cut) Pay(int wanted, long available, bool cut) =>
        cut ? (0, 0, true) : wanted <= available ? (wanted, available - wanted, false) : ((int)available, 0, true);

    private static int Effect(int funded, int request, int full) =>
        request == 0 ? full : (int)Math.Floor((double)full * funded / request);

    // Micropolis rounds halves up.
    private static int Round(double value) => (int)Math.Floor(value + 0.5);
}

/// <summary>
/// A year's budget: the tax rate, the tax income, and for each kind of upkeep what was asked and
/// what was paid. A year's estimate also says what was asked; a settled year only what was paid.
/// </summary>
/// <param name="TaxRate">The tax rate, in percent.</param>
/// <param name="TaxIncome">The taxes collected.</param>
/// <param name="RoadRequest">The roads' upkeep at full funding.</param>
/// <param name="RoadFunded">The roads' upkeep paid.</param>
/// <param name="FireRequest">The fire stations' upkeep at full funding.</param>
/// <param name="FireFunded">The fire stations' upkeep paid.</param>
/// <param name="PoliceRequest">The police stations' upkeep at full funding.</param>
/// <param name="PoliceFunded">The police stations' upkeep paid.</param>
/// <param name="Cut">Whether funding had to be cut because the money ran out.</param>
/// <param name="OrdinanceIncome">What the ordinances earned.</param>
/// <param name="OrdinanceCost">What the ordinances cost, as paid.</param>
/// <param name="DealIncome">What the deals with neighbours earned.</param>
/// <param name="DealCost">What the deals with neighbours cost, as paid.</param>
public sealed record BudgetYear(
    int TaxRate,
    int TaxIncome,
    int RoadRequest,
    int RoadFunded,
    int FireRequest,
    int FireFunded,
    int PoliceRequest,
    int PoliceFunded,
    bool Cut,
    int OrdinanceIncome = 0,
    int OrdinanceCost = 0,
    int DealIncome = 0,
    int DealCost = 0)
{
    /// <summary>What the upkeep, the ordinances, and the deals paid came to.</summary>
    public int Expenses => RoadFunded + FireFunded + PoliceFunded + OrdinanceCost + DealCost;

    /// <summary>What came in: the taxes, and what the ordinances and the deals earned.</summary>
    public int Income => TaxIncome + OrdinanceIncome + DealIncome;

    /// <summary>What the year added to the treasury, or took from it.</summary>
    public int CashFlow => Income - Expenses;

    /// <summary>Whether what was paid came to more than what came in.</summary>
    public bool IsDeficit => Expenses > Income;

    /// <summary>
    /// Whether spending outpaced income: a deficit, or funding cut because the money ran out.
    /// The budget panel opens by itself for such a year.
    /// </summary>
    public bool NeedsAttention => IsDeficit || Cut;
}
