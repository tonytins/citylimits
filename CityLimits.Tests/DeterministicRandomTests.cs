using CityLimits.Core.Randomness;

namespace CityLimits.Tests;

public class DeterministicRandomTests
{
    [Fact]
    public void TheSameSeedGivesTheSameSequence()
    {
        var first = new DeterministicRandom(1234);
        var second = new DeterministicRandom(1234);

        for (var i = 0; i < 32; i++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }
    }

    [Fact]
    public void DifferentSeedsGiveDifferentSequences()
    {
        var first = new DeterministicRandom(1);
        var second = new DeterministicRandom(2);

        Assert.NotEqual(first.NextUInt64(), second.NextUInt64());
    }

    [Fact]
    public void NextIntStaysInsideItsRange()
    {
        var random = new DeterministicRandom(99);

        for (var i = 0; i < 500; i++)
        {
            var value = random.NextInt(-3, 7);
            Assert.InRange(value, -3, 6);
        }
    }

    [Fact]
    public void NextIntCoversItsWholeRange()
    {
        var random = new DeterministicRandom(7);
        var seen = new HashSet<int>();

        for (var i = 0; i < 500; i++)
        {
            seen.Add(random.NextInt(0, 5));
        }

        Assert.Equal([0, 1, 2, 3, 4], seen.OrderBy(value => value));
    }

    [Fact]
    public void NextIntRejectsAnEmptyRange()
    {
        var random = new DeterministicRandom(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(6, 5));
    }

    [Fact]
    public void NextDoubleStaysBetweenZeroAndOne()
    {
        var random = new DeterministicRandom(42);

        for (var i = 0; i < 500; i++)
        {
            var value = random.NextDouble();
            Assert.InRange(value, 0.0, 0.9999999999999999);
        }
    }
}
