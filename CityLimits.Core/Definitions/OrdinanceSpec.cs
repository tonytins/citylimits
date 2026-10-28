namespace CityLimits.Core.Definitions;

/// <summary>
/// A city ordinance, after SimCity 3000's: a rule the player enacts or repeals at any time, which
/// earns or costs the city money each year by its population and nudges what the city does, such
/// as its demand, its crime, or its fires. The ordinances are the base game's, by id; a mod can
/// change what each one does, but not add others, since the engine decides when each is asked for.
/// </summary>
/// <param name="Id">The id the ordinance is known by, in spec files and saves, such as <c>salesTax</c>.</param>
/// <param name="Name">Its name, as the player sees it.</param>
/// <param name="Money">What it earns each year for every 1,000 people in the city, or, below zero, what it costs.</param>
/// <param name="Effects">What it does while it is enacted, each kind at most once.</param>
public sealed record OrdinanceSpec(string Id, string Name, int Money, IReadOnlyList<OrdinanceEffectSpec> Effects)
{
    /// <summary>How much the ordinance changes one thing, or 0 when it doesn't.</summary>
    public int AmountOf(OrdinanceEffect effect)
    {
        foreach (var each in Effects)
        {
            if (each.Effect == effect)
            {
                return each.Amount;
            }
        }

        return 0;
    }

    /// <summary>What the ordinance earns, or costs below zero, in a year, for a city of the population given.</summary>
    public long YearlyMoney(int population) => (long)Math.Max(0, population) * Money / 1000;
}

/// <summary>One thing an ordinance changes, and by how much.</summary>
/// <param name="Effect">What it changes.</param>
/// <param name="Amount">By how much, in the units the effect names.</param>
public sealed record OrdinanceEffectSpec(OrdinanceEffect Effect, int Amount);

/// <summary>What an ordinance can change: the things the city already works out, each in its own units.</summary>
public enum OrdinanceEffect
{
    /// <summary>Residential demand, added each time demand moves, as the tax rate's push is.</summary>
    ResidentialDemand,

    /// <summary>Commercial demand, added each time demand moves.</summary>
    CommercialDemand,

    /// <summary>Industrial demand, added each time demand moves.</summary>
    IndustrialDemand,

    /// <summary>Crime everywhere, added where police coverage takes it away.</summary>
    Crime,

    /// <summary>The pollution industry gives off, in percent: -30 leaves 70%.</summary>
    IndustrialPollution,

    /// <summary>The chance of a fire breaking out by itself, in percent: -50 halves it.</summary>
    Fires,

    /// <summary>What the power plants can power, in percent: 15 makes it 115%.</summary>
    PowerCapacity,

    /// <summary>The traffic each trip leaves on the roads, in percent: -10 leaves 90%.</summary>
    Traffic,
}
