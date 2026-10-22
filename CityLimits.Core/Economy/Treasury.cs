namespace CityLimits.Core.Economy;

/// <summary>The money the city has, and what it spends and takes back.</summary>
public sealed class Treasury
{
    /// <summary>The money a new city starts with.</summary>
    public const int StartingFunds = 20000;

    /// <summary>Creates a treasury holding the given money.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The money is negative.</exception>
    public Treasury(int funds = StartingFunds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(funds);
        Funds = funds;
    }

    /// <summary>The money the city has.</summary>
    public int Funds { get; private set; }

    /// <summary>Tells whether the city can pay the given amount.</summary>
    public bool CanAfford(int cost) => cost <= Funds;

    /// <summary>Spends money, and tells whether it could be paid.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public bool TrySpend(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (!CanAfford(amount))
        {
            return false;
        }

        Funds -= amount;
        return true;
    }

    /// <summary>
    /// Settles the year's taxes and upkeep: adds what came in and takes what went out. Unlike
    /// spending on building, this can take the city below zero, as in Micropolis.
    /// </summary>
    public void Settle(int cashFlow) => Funds += cashFlow;

    /// <summary>Takes money back into the treasury.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public void Refund(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Funds += amount;
    }

    // A saved city's money, which may be below zero.
    internal void Restore(int funds) => Funds = funds;
}
