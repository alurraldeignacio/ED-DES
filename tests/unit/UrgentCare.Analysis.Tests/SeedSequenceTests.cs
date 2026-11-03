using UrgentCare.Analysis.Scenarios;

namespace UrgentCare.Analysis.Tests;

/// <summary>
/// FR-011: repeated replications must be reproducible and must not repeat each other. Each
/// replication needs its own random stream, and that stream has to be derivable from the
/// scenario seed alone so a batch can be re-run months later and land on the same numbers.
/// </summary>
public class SeedSequenceTests
{
    [Fact]
    public void TheSameBaseSeedAndIndex_AlwaysYieldTheSameSeed()
    {
        Assert.Equal(SeedSequence.SeedFor(12345, 7), SeedSequence.SeedFor(12345, 7));
    }

    [Fact]
    public void DifferentReplications_GetDifferentSeeds()
    {
        var seeds = Enumerable.Range(0, 100)
            .Select(i => SeedSequence.SeedFor(12345, i))
            .ToList();

        Assert.Equal(seeds.Count, seeds.Distinct().Count());
    }

    [Fact]
    public void DifferentBaseSeeds_ProduceDifferentSequences()
    {
        var first = SeedSequence.Take(1, 20).ToList();
        var second = SeedSequence.Take(2, 20).ToList();

        Assert.Empty(first.Intersect(second));
    }

    [Fact]
    public void ConsecutiveIndices_DoNotProduceConsecutiveSeeds()
    {
        // A seed sequence that simply counts upward would give neighbouring replications
        // strongly correlated streams, which defeats the point of replicating.
        var a = SeedSequence.SeedFor(12345, 0);
        var b = SeedSequence.SeedFor(12345, 1);

        Assert.NotEqual(a + 1, b);
    }

    [Fact]
    public void SeedsAreNonNegative()
    {
        // A negative seed is legal but awkward to read in a manifest, and some sources reject it.
        foreach (var seed in SeedSequence.Take(int.MinValue + 1, 50))
        {
            Assert.True(seed >= 0, $"Expected a non-negative seed but got {seed}.");
        }
    }

    [Fact]
    public void TakeReturnsTheRequestedCountInIndexOrder()
    {
        var seeds = SeedSequence.Take(999, 5).ToList();

        Assert.Equal(5, seeds.Count);
        Assert.Equal(SeedSequence.SeedFor(999, 0), seeds[0]);
        Assert.Equal(SeedSequence.SeedFor(999, 4), seeds[4]);
    }

    [Fact]
    public void TakeIsRepeatable()
    {
        Assert.Equal(SeedSequence.Take(555, 10), SeedSequence.Take(555, 10));
    }

    [Fact]
    public void ANegativeReplicationIndex_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SeedSequence.SeedFor(1, -1));
    }

    [Fact]
    public void ANegativeCount_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SeedSequence.Take(1, -1).ToList());
    }
}
