using UrgentCare.Core.Metrics;

namespace UrgentCare.Core.Tests;

/// <summary>
/// The one percentile implementation in the codebase. Core computes P95 within a run and the
/// analysis library computes P95 across replications; both call this, so the two figures are
/// always produced under the same interpolation rule rather than drifting apart.
/// </summary>
public class PercentileTests
{
    [Fact]
    public void EmptySample_ReturnsZero()
    {
        // Nobody waited, so there is no 95th percentile wait. Zero is the only sensible answer
        // that keeps the metric a number rather than a null the caller must special-case.
        Assert.Equal(0.0, Percentile.Of([], 0.95));
    }

    [Fact]
    public void SingleSample_ReturnsThatValue()
    {
        Assert.Equal(42.0, Percentile.Of([42.0], 0.95));
        Assert.Equal(42.0, Percentile.Of([42.0], 0.05));
    }

    [Fact]
    public void UnsortedInput_IsSortedBeforeReading()
    {
        var shuffled = new[] { 50.0, 10.0, 30.0, 20.0, 40.0 };

        Assert.Equal(30.0, Percentile.Of(shuffled, 0.5));
    }

    [Fact]
    public void ZerothPercentile_IsTheMinimum()
    {
        Assert.Equal(10.0, Percentile.Of([30.0, 10.0, 20.0], 0.0));
    }

    [Fact]
    public void HundredthPercentile_IsTheMaximum()
    {
        Assert.Equal(30.0, Percentile.Of([30.0, 10.0, 20.0], 1.0));
    }

    [Fact]
    public void APositionBetweenTwoSamples_IsInterpolatedLinearly()
    {
        // Four samples put the 50th percentile at position 0.5 * 3 = 1.5, halfway between the
        // second and third values.
        Assert.Equal(25.0, Percentile.Of([10.0, 20.0, 30.0, 40.0], 0.5), precision: 6);
    }

    [Fact]
    public void APositionLandingExactlyOnASample_ReturnsThatSample()
    {
        // Five samples put the 50th percentile at position 0.5 * 4 = 2, exactly the third value.
        Assert.Equal(30.0, Percentile.Of([10.0, 20.0, 30.0, 40.0, 50.0], 0.5), precision: 6);
    }

    [Fact]
    public void P95_OfAWideSpread_SitsNearTheTop()
    {
        var values = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();

        Assert.Equal(95.05, Percentile.Of(values, 0.95), precision: 6);
    }

    [Fact]
    public void IdenticalSamples_ReturnThatValueAtEveryPercentile()
    {
        double[] flat = [7.0, 7.0, 7.0, 7.0];

        Assert.Equal(7.0, Percentile.Of(flat, 0.0));
        Assert.Equal(7.0, Percentile.Of(flat, 0.5));
        Assert.Equal(7.0, Percentile.Of(flat, 0.95));
    }

    [Fact]
    public void TheInputCollectionIsNotReordered()
    {
        // The caller's list is theirs; sorting it in place would be a surprising side effect.
        var values = new List<double> { 30.0, 10.0, 20.0 };

        Percentile.Of(values, 0.5);

        Assert.Equal([30.0, 10.0, 20.0], values);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void APercentileOutsideZeroToOne_IsRejected(double percentile)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentile.Of([1.0, 2.0], percentile));
    }

    [Fact]
    public void NullInput_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Percentile.Of(null!, 0.95));
    }
}
