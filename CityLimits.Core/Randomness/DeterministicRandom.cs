namespace CityLimits.Core.Randomness;

/// <summary>
/// A seeded pseudo-random number generator with a fixed algorithm, so the
/// same seed produces the same sequence on every machine and every runtime.
/// </summary>
public sealed class DeterministicRandom
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong MixA = 0xBF58476D1CE4E5B9UL;
    private const ulong MixB = 0x94D049BB133111EBUL;
    private const double Inverse53Bits = 1.0 / 9007199254740992.0;

    private ulong _state;

    /// <summary>Creates a generator that starts from the given seed.</summary>
    public DeterministicRandom(int seed) => _state = unchecked((ulong)(long)seed) + GoldenGamma;

    /// <summary>Returns the next value in the sequence.</summary>
    public ulong NextUInt64()
    {
        _state += GoldenGamma;
        var z = _state;
        z = (z ^ (z >> 30)) * MixA;
        z = (z ^ (z >> 27)) * MixB;
        return z ^ (z >> 31);
    }

    /// <summary>Returns a value in the range from <paramref name="minInclusive"/> up to, but not including, <paramref name="maxExclusive"/>.</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minInclusive, maxExclusive);

        var range = (ulong)((long)maxExclusive - minInclusive);
        return (int)(minInclusive + (long)(NextUInt64() % range));
    }

    /// <summary>Returns a value in the range [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * Inverse53Bits;

    /// <summary>Returns a value in the range [0, 1).</summary>
    public float NextFloat() => (float)NextDouble();

    /// <summary>The generator's whole state, which a save keeps so a loaded city draws the same numbers.</summary>
    internal ulong State
    {
        get => _state;
        set => _state = value;
    }
}
