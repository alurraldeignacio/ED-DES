using UrgentCare.Analysis.Distributions;
using UrgentCare.Core.Configuration;
using UrgentCare.Core.Random;

namespace UrgentCare.Analysis.Tests;

/// <summary>
/// Each supported family must sample in range and reproduce its stream under a fixed seed.
/// These are the transformations the whole simulation samples from, so they are unit-tested
/// rather than exercised only through a full run (Constitution Principle V).
/// </summary>
public class MathNetDistributionFactoryTests
{
    private readonly MathNetDistributionFactory _factory = new();

    [Fact]
    public void Categorical_SamplesOnlyConfiguredCategories()
    {
        var config = new DistributionConfig
        {
            Family = "categorical",
            Categories = [1, 2, 3, 4, 5],
            Weights = [0.05, 0.15, 0.40, 0.30, 0.10]
        };

        var distribution = _factory.Create(config, new SeededRandomSource(1));

        for (var i = 0; i < 500; i++)
        {
            var sample = distribution.Sample();
            Assert.Contains(sample, new double[] { 1, 2, 3, 4, 5 });
        }
    }

    [Fact]
    public void Categorical_FavorsTheHeaviestWeight()
    {
        // Urgency 3 carries 40 percent of the mass, so it must dominate a large sample.
        var config = new DistributionConfig
        {
            Family = "categorical",
            Categories = [1, 2, 3, 4, 5],
            Weights = [0.05, 0.15, 0.40, 0.30, 0.10]
        };

        var distribution = _factory.Create(config, new SeededRandomSource(1));
        var samples = Enumerable.Range(0, 5000).Select(_ => distribution.Sample()).ToArray();

        var mostCommon = samples.GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key;

        Assert.Equal(3, mostCommon);
    }

    [Fact]
    public void Normal_RespectsTruncationBounds()
    {
        var config = new DistributionConfig
        {
            Family = "normal",
            Mean = 42.0,
            StandardDeviation = 21.0,
            Min = 0,
            Max = 105
        };

        var distribution = _factory.Create(config, new SeededRandomSource(2));

        for (var i = 0; i < 2000; i++)
        {
            Assert.InRange(distribution.Sample(), 0.0, 105.0);
        }
    }

    [Fact]
    public void LogNormal_IsAlwaysPositive()
    {
        var config = new DistributionConfig { Family = "lognormal", Mu = 2.9, Sigma = 0.5 };

        var distribution = _factory.Create(config, new SeededRandomSource(3));

        for (var i = 0; i < 1000; i++)
        {
            Assert.True(distribution.Sample() > 0);
        }
    }

    [Fact]
    public void Triangular_StaysWithinMinAndMax()
    {
        var config = new DistributionConfig
        {
            Family = "triangular",
            Min = 30.0,
            Mode = 90.0,
            Max = 240.0
        };

        var distribution = _factory.Create(config, new SeededRandomSource(4));

        for (var i = 0; i < 2000; i++)
        {
            Assert.InRange(distribution.Sample(), 30.0, 240.0);
        }
    }

    [Fact]
    public void Exponential_WithConfiguredRate_IsAlwaysPositive()
    {
        var config = new DistributionConfig { Family = "exponential", Rate = 0.1 };

        var distribution = _factory.Create(config, new SeededRandomSource(5));

        for (var i = 0; i < 1000; i++)
        {
            Assert.True(distribution.Sample() > 0);
        }
    }

    [Fact]
    public void Exponential_WithoutRate_DefersToTheCaller()
    {
        var config = new DistributionConfig { Family = "exponential" };

        var distribution = _factory.Create(config, new SeededRandomSource(6));

        var deferred = Assert.IsType<DeferredRateExponential>(distribution);
        Assert.Throws<InvalidOperationException>(() => deferred.Sample());
        Assert.True(deferred.SampleWithRate(0.1) > 0);
    }

    [Theory]
    [InlineData("categorical")]
    [InlineData("normal")]
    [InlineData("lognormal")]
    [InlineData("triangular")]
    public void EverySupportedFamily_IsDeterministicUnderAFixedSeed(string family)
    {
        var config = ConfigFor(family);

        var first = _factory.Create(config, new SeededRandomSource(777));
        var second = _factory.Create(config, new SeededRandomSource(777));

        var firstSamples = Enumerable.Range(0, 100).Select(_ => first.Sample()).ToArray();
        var secondSamples = Enumerable.Range(0, 100).Select(_ => second.Sample()).ToArray();

        Assert.Equal(firstSamples, secondSamples);
    }

    [Fact]
    public void UnknownFamily_IsRejected()
    {
        var config = new DistributionConfig { Family = "weibull" };

        var exception = Assert.Throws<ArgumentException>(
            () => _factory.Create(config, new SeededRandomSource(1)));

        Assert.Contains("weibull", exception.Message);
    }

    [Fact]
    public void MissingRequiredParameter_IsRejected()
    {
        // lognormal without sigma
        var config = new DistributionConfig { Family = "lognormal", Mu = 2.9 };

        var exception = Assert.Throws<ArgumentException>(
            () => _factory.Create(config, new SeededRandomSource(1)));

        Assert.Contains("sigma", exception.Message);
    }

    [Fact]
    public void TriangularWithModeOutsideBounds_IsRejected()
    {
        var config = new DistributionConfig
        {
            Family = "triangular",
            Min = 30.0,
            Mode = 500.0,
            Max = 240.0
        };

        var exception = Assert.Throws<ArgumentException>(
            () => _factory.Create(config, new SeededRandomSource(1)));

        Assert.Contains("min <= mode <= max", exception.Message);
    }

    private static DistributionConfig ConfigFor(string family) => family switch
    {
        "categorical" => new DistributionConfig
        {
            Family = "categorical",
            Categories = [1, 2, 3, 4, 5],
            Weights = [0.05, 0.15, 0.40, 0.30, 0.10]
        },
        "normal" => new DistributionConfig
        {
            Family = "normal",
            Mean = 42.0,
            StandardDeviation = 21.0
        },
        "lognormal" => new DistributionConfig { Family = "lognormal", Mu = 2.9, Sigma = 0.5 },
        "triangular" => new DistributionConfig { Family = "triangular", Min = 30, Mode = 90, Max = 240 },
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };
}
