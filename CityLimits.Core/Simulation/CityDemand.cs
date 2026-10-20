namespace CityLimits.Core.Simulation;

/// <summary>
/// How much the city wants more residential, commercial, and industrial zones, as in Micropolis.
/// Each demand is a level that rises and falls: every second count, the census moves it by how
/// far each kind of population is from where the city's economy says it should be. A positive
/// demand helps that kind of zone grow; a negative one helps it decline.
/// </summary>
/// <remarks>
/// Until the systems they come from exist, three of Micropolis' inputs are fixed: the difficulty
/// is Easy, the tax has no effect (Micropolis' effect at its default 7% is none), and the caps
/// that stop demand without a stadium, an airport, or a seaport are off.
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

    /// <summary>Residential demand, from -<see cref="ResidentialLimit"/> to <see cref="ResidentialLimit"/>.</summary>
    public int Residential { get; private set; }

    /// <summary>Commercial demand, from -<see cref="BusinessLimit"/> to <see cref="BusinessLimit"/>.</summary>
    public int Commercial { get; private set; }

    /// <summary>Industrial demand, from -<see cref="BusinessLimit"/> to <see cref="BusinessLimit"/>.</summary>
    public int Industrial { get; private set; }

    /// <summary>Moves each demand by how far its population is from what the city's economy calls for.</summary>
    public void Update(CityCensus census)
    {
        ArgumentNullException.ThrowIfNull(census);

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

        Residential = Move(Residential, residentialRatio, ResidentialLimit);
        Commercial = Move(Commercial, commercialRatio, BusinessLimit);
        Industrial = Move(Industrial, industrialRatio, BusinessLimit);
    }

    // A ratio above 1 raises demand, below 1 lowers it; the tax would add its own push here.
    private static int Move(int demand, double ratio, int limit)
    {
        var change = (int)((Math.Min(ratio, MostRatio) - 1) * Pull);
        return Math.Clamp(demand + change, -limit, limit);
    }
}
