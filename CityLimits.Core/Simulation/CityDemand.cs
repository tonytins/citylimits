namespace CityLimits.Core.Simulation;

/// <summary>
/// How much the city wants more residential, commercial, and industrial zones, as in Micropolis.
/// Each demand is a level that rises and falls: every second count, the census moves it by how
/// far each kind of population is from where the city's economy says it should be. A positive
/// demand helps that kind of zone grow; a negative one helps it decline.
/// </summary>
/// <remarks>
/// The tax rate pushes all three up or down, by Micropolis' own steps: a push of 200 at no tax,
/// none at the default 7%, and -600 at 20%. Until the systems they come from exist, two of
/// Micropolis' inputs are fixed: the difficulty is Easy, and the caps that stop demand without
/// a stadium, an airport, or a seaport are off.
/// </remarks>
public sealed class CityDemand
{
    /// <summary>How far residential demand can rise or fall.</summary>
    public const int ResidentialLimit = 2000;

    /// <summary>How far commercial and industrial demand can rise or fall.</summary>
    public const int BusinessLimit = 1500;

    // A year's births, as a share of the residents.
    private const double BirthRate = 0.02;

    // The most the labour available can tilt commerce and industry.
    private const double MostLabour = 1.3;

    // How many people support one unit of commerce.
    private const double PeoplePerCommerce = 3.7;

    // How much industry the economy wants at Easy difficulty, and the least it always wants.
    private const double IndustryAppetite = 1.2;
    private const double LeastIndustry = 5.0;

    // A population this many times what it should be, or more, counts as the most.
    private const double MostRatio = 2.0;

    // How strongly a ratio moves its demand.
    private const double Pull = 600.0;

    // With no residents yet, how much the city wants them.
    private const double EmptyCityResidentialRatio = 1.3;

    // How hard each tax rate, from 0% to 20%, pushes every demand: Micropolis' table, taken with
    // the owner's approval. Easy difficulty adds nothing to the rate before it is looked up.
    private static readonly int[] TaxPush =
    [
        200, 150, 120, 100, 80, 50, 30, 0, -10, -40, -100,
        -150, -200, -250, -300, -350, -400, -450, -500, -550, -600,
    ];

    /// <summary>Residential demand, from -<see cref="ResidentialLimit"/> to <see cref="ResidentialLimit"/>.</summary>
    public int Residential { get; private set; }

    /// <summary>Commercial demand, from -<see cref="BusinessLimit"/> to <see cref="BusinessLimit"/>.</summary>
    public int Commercial { get; private set; }

    /// <summary>Industrial demand, from -<see cref="BusinessLimit"/> to <see cref="BusinessLimit"/>.</summary>
    public int Industrial { get; private set; }

    /// <summary>
    /// The population taxes are worked out from, as of the last update: residents in eighths, plus
    /// commerce and industry, as Micropolis counts them.
    /// </summary>
    public int TaxablePopulation { get; private set; }

    /// <summary>
    /// How far residential demand moved from the economy at the last update, before the tax: up
    /// when the city has more jobs than residents, down when it has fewer.
    /// </summary>
    public int ResidentialPull { get; private set; }

    /// <summary>How far commercial demand moved from the economy at the last update, before the tax.</summary>
    public int CommercialPull { get; private set; }

    /// <summary>How far industrial demand moved from the economy at the last update, before the tax.</summary>
    public int IndustrialPull { get; private set; }

    /// <summary>How far the tax rate pushed every demand at the last update.</summary>
    public int TaxPushApplied { get; private set; }

    /// <summary>Last month's jobs for each resident at the last update; above 1, people move in.</summary>
    public double JobsPerResident { get; private set; } = 1.0;

    /// <summary>Last month's residents for each job at the last update, as far as it tilts commerce and industry.</summary>
    public double WorkersPerJob { get; private set; } = 1.0;

    /// <summary>
    /// Moves each demand by how far its population is from what the city's economy calls for, and
    /// by the push of the tax rate.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The tax rate is outside 0 to 20.</exception>
    public void Update(CityCensus census, int taxRate = CityBudget.DefaultTaxRate)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentOutOfRangeException.ThrowIfNegative(taxRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(taxRate, TaxPush.Length);

        // Residents are counted in eighths, as Micropolis counts them against jobs.
        var residents = census.Residential / 8.0;
        var commerce = (double)census.Commercial;
        var industry = (double)census.Industrial;
        var lastMonthsJobs = (double)(census.CommercialHistory(1) + census.IndustrialHistory(1));

        // People move in when there are more jobs than residents, and out when there are fewer.
        var employment = residents > 0 ? lastMonthsJobs / residents : 1.0;
        var projectedResidents = residents + (residents * (employment - 1)) + (residents * BirthRate);

        var labour = lastMonthsJobs > 0 ? census.ResidentialHistory(1) / lastMonthsJobs : 1.0;
        labour = Math.Clamp(labour, 0.0, MostLabour);
        var projectedCommerce = (residents + commerce + industry) / PeoplePerCommerce * labour;
        var projectedIndustry = Math.Max(industry * labour * IndustryAppetite, LeastIndustry);

        var residentialRatio = residents > 0 ? projectedResidents / residents : EmptyCityResidentialRatio;
        var commercialRatio = commerce > 0 ? projectedCommerce / commerce : projectedCommerce;
        var industrialRatio = industry > 0 ? projectedIndustry / industry : projectedIndustry;

        TaxablePopulation = (int)(residents + commerce + industry);
        JobsPerResident = employment;
        WorkersPerJob = labour;
        ResidentialPull = PullOf(residentialRatio);
        CommercialPull = PullOf(commercialRatio);
        IndustrialPull = PullOf(industrialRatio);
        var push = TaxPush[taxRate];
        TaxPushApplied = push;
        Residential = Move(Residential, residentialRatio, push, ResidentialLimit);
        Commercial = Move(Commercial, commercialRatio, push, BusinessLimit);
        Industrial = Move(Industrial, industrialRatio, push, BusinessLimit);
    }

    // A ratio above 1 raises demand, below 1 lowers it, and the tax pushes it as well.
    private static int Move(int demand, double ratio, int push, int limit)
    {
        var change = (int)(Economy(ratio) + push);
        return Math.Clamp(demand + change, -limit, limit);
    }

    private static double Economy(double ratio) => (Math.Min(ratio, MostRatio) - 1) * Pull;

    // For showing the player; the move itself adds the tax before it rounds, so the two together
    // can differ from it by one.
    private static int PullOf(double ratio) => (int)Math.Round(Economy(ratio));
}
