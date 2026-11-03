using UrgentCare.Core.Random;

namespace UrgentCare.Core.Tests;

/// <summary>
/// Reproducibility rests entirely on this behavior: identical seeds must produce identical
/// streams, or FR-011 and every replication comparison become meaningless.
/// </summary>
public class SeededRandomSourceTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalDoubleSequences()
    {
        var first = new SeededRandomSource(12345);
        var second = new SeededRandomSource(12345);

        var firstSequence = Enumerable.Range(0, 200).Select(_ => first.NextDouble()).ToArray();
        var secondSequence = Enumerable.Range(0, 200).Select(_ => second.NextDouble()).ToArray();

        Assert.Equal(firstSequence, secondSequence);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalIntegerSequences()
    {
        var first = new SeededRandomSource(999);
        var second = new SeededRandomSource(999);

        var firstSequence = Enumerable.Range(0, 200).Select(_ => first.NextInt(1, 6)).ToArray();
        var secondSequence = Enumerable.Range(0, 200).Select(_ => second.NextInt(1, 6)).ToArray();

        Assert.Equal(firstSequence, secondSequence);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var first = new SeededRandomSource(1);
        var second = new SeededRandomSource(2);

        var firstSequence = Enumerable.Range(0, 50).Select(_ => first.NextDouble()).ToArray();
        var secondSequence = Enumerable.Range(0, 50).Select(_ => second.NextDouble()).ToArray();

        Assert.NotEqual(firstSequence, secondSequence);
    }

    [Fact]
    public void NextDouble_StaysInUnitInterval()
    {
        var random = new SeededRandomSource(42);

        for (var i = 0; i < 1000; i++)
        {
            var value = random.NextDouble();
            Assert.InRange(value, 0.0, 1.0);
            Assert.NotEqual(1.0, value);
        }
    }

    [Fact]
    public void NextInt_StaysWithinRequestedRange()
    {
        var random = new SeededRandomSource(7);

        for (var i = 0; i < 1000; i++)
        {
            var value = random.NextInt(1, 6);
            Assert.InRange(value, 1, 5);
        }
    }

    [Fact]
    public void NextInt_RejectsAnEmptyRange()
    {
        var random = new SeededRandomSource(7);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(5, 5));
    }

    [Fact]
    public void SeedIsExposedForRunManifests()
    {
        var random = new SeededRandomSource(4711);

        Assert.Equal(4711, random.Seed);
    }

    [Fact]
    public void AsSystemRandom_DrawsFromTheSameSeededStream()
    {
        // MathNet samples through the System.Random, so it must be the same instance
        // that NextDouble advances, or determinism would depend on call ordering.
        var first = new SeededRandomSource(2024);
        var second = new SeededRandomSource(2024);

        var viaInterface = Enumerable.Range(0, 20).Select(_ => first.NextDouble()).ToArray();
        var viaSystemRandom = Enumerable.Range(0, 20).Select(_ => second.AsSystemRandom().NextDouble()).ToArray();

        Assert.Equal(viaInterface, viaSystemRandom);
    }
}
