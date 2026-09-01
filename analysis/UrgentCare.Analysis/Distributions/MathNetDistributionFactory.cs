using UrgentCare.Core.Configuration;
using UrgentCare.Core.Distributions;
using UrgentCare.Core.Random;

// MathNet also defines IDistribution, so its types are aliased rather than imported wholesale.
// Inside this file IDistribution always means the Core abstraction.
using IContinuousDistribution = MathNet.Numerics.Distributions.IContinuousDistribution;
using MathNetCategorical = MathNet.Numerics.Distributions.Categorical;
using MathNetExponential = MathNet.Numerics.Distributions.Exponential;
using MathNetLogNormal = MathNet.Numerics.Distributions.LogNormal;
using MathNetNormal = MathNet.Numerics.Distributions.Normal;
using MathNetTriangular = MathNet.Numerics.Distributions.Triangular;

namespace UrgentCare.Analysis.Distributions;

/// <summary>
/// Builds sampleable distributions from configuration using MathNet.Numerics. This is the only
/// place MathNet is referenced, which is what keeps UrgentCare.Core dependency-free.
/// </summary>
/// <remarks>
/// Every distribution is constructed against the run's seeded <see cref="System.Random"/>, so
/// identical seeds reproduce identical sample streams regardless of family.
/// </remarks>
public sealed class MathNetDistributionFactory : IDistributionFactory
{
    public IDistribution Create(DistributionConfig config, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(random);

        if (string.IsNullOrWhiteSpace(config.Family))
        {
            throw new ArgumentException("Distribution family must be present.", nameof(config));
        }

        var systemRandom = random.AsSystemRandom();

        return config.Family.ToLowerInvariant() switch
        {
            "categorical" => CreateCategorical(config, systemRandom),
            "normal" => CreateNormal(config, systemRandom),
            "lognormal" => CreateLogNormal(config, systemRandom),
            "triangular" => CreateTriangular(config, systemRandom),
            "exponential" => CreateExponential(config, systemRandom),
            _ => throw new ArgumentException(
                $"Unsupported distribution family '{config.Family}'.", nameof(config))
        };
    }

    private static IDistribution CreateCategorical(DistributionConfig config, System.Random random)
    {
        var categories = Require(config.Categories, "categories");
        var weights = Require(config.Weights, "weights");

        if (categories.Count != weights.Count)
        {
            throw new ArgumentException(
                $"categorical requires categories and weights of equal length, " +
                $"but got {categories.Count} and {weights.Count}.");
        }

        var categorical = new MathNetCategorical([.. weights], random);
        return new CategoricalDistribution(categorical, [.. categories]);
    }

    private static IDistribution CreateNormal(DistributionConfig config, System.Random random)
    {
        var mean = Require(config.Mean, "mean");
        var standardDeviation = Require(config.StandardDeviation, "standardDeviation");

        var normal = new MathNetNormal(mean, standardDeviation, random);
        return new TruncatedDistribution(normal, config.Min, config.Max, mean);
    }

    private static IDistribution CreateLogNormal(DistributionConfig config, System.Random random)
    {
        var mu = Require(config.Mu, "mu");
        var sigma = Require(config.Sigma, "sigma");

        return new ContinuousDistributionAdapter(new MathNetLogNormal(mu, sigma, random));
    }

    private static IDistribution CreateTriangular(DistributionConfig config, System.Random random)
    {
        var min = Require(config.Min, "min");
        var mode = Require(config.Mode, "mode");
        var max = Require(config.Max, "max");

        if (!(min <= mode && mode <= max))
        {
            throw new ArgumentException(
                $"triangular requires min <= mode <= max, but got {min}, {mode}, {max}.");
        }

        // MathNet orders the parameters lower, upper, mode.
        return new ContinuousDistributionAdapter(new MathNetTriangular(min, max, mode, random));
    }

    private static IDistribution CreateExponential(DistributionConfig config, System.Random random)
    {
        // A rate of zero or absent means the caller supplies it per sample, as the arrival model does.
        if (config.Rate is not { } rate || rate <= 0)
        {
            return new DeferredRateExponential(random);
        }

        return new ContinuousDistributionAdapter(new MathNetExponential(rate, random));
    }

    private static T Require<T>(T? value, string parameterName) where T : class =>
        value ?? throw new ArgumentException($"Parameter '{parameterName}' is required for this family.");

    private static double Require(double? value, string parameterName) =>
        value ?? throw new ArgumentException($"Parameter '{parameterName}' is required for this family.");
}

/// <summary>Adapts any MathNet continuous distribution to the Core abstraction.</summary>
internal sealed class ContinuousDistributionAdapter : IDistribution
{
    private readonly IContinuousDistribution _distribution;

    public ContinuousDistributionAdapter(IContinuousDistribution distribution) =>
        _distribution = distribution;

    public double Sample() => _distribution.Sample();
}

/// <summary>
/// Maps a MathNet categorical draw (an index) onto the configured category values,
/// so triage acuity comes back as 1 to 5 rather than 0 to 4.
/// </summary>
internal sealed class CategoricalDistribution : IDistribution
{
    private readonly MathNetCategorical _categorical;
    private readonly double[] _categories;

    public CategoricalDistribution(MathNetCategorical categorical, double[] categories)
    {
        _categorical = categorical;
        _categories = categories;
    }

    public double Sample() => _categories[_categorical.Sample()];
}

/// <summary>
/// Restricts a distribution to a range by resampling. Used for age, which is normally
/// distributed but cannot be negative or implausibly large.
/// </summary>
internal sealed class TruncatedDistribution : IDistribution
{
    private const int MaxAttempts = 100;

    private readonly IContinuousDistribution _distribution;
    private readonly double? _min;
    private readonly double? _max;
    private readonly double _fallback;

    public TruncatedDistribution(IContinuousDistribution distribution, double? min, double? max, double fallback)
    {
        _distribution = distribution;
        _min = min;
        _max = max;
        _fallback = fallback;
    }

    public double Sample()
    {
        if (_min is null && _max is null)
        {
            return _distribution.Sample();
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var value = _distribution.Sample();
            if ((_min is null || value >= _min) && (_max is null || value <= _max))
            {
                return value;
            }
        }

        // Resampling failed to land in range; clamp rather than loop forever.
        return Math.Clamp(_fallback, _min ?? double.MinValue, _max ?? double.MaxValue);
    }
}

/// <summary>
/// Exponential distribution whose rate is supplied per sample rather than at construction.
/// The non-stationary arrival model needs this, because its rate varies with the time of day.
/// </summary>
public sealed class DeferredRateExponential : IDistribution
{
    private readonly System.Random _random;

    public DeferredRateExponential(System.Random random) => _random = random;

    public double Sample() => throw new InvalidOperationException(
        "This exponential distribution has no configured rate; call SampleWithRate instead.");

    /// <summary>Draws an interarrival time for the rate in force at this moment.</summary>
    public double SampleWithRate(double rate)
    {
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be greater than zero.");
        }

        return MathNetExponential.Sample(_random, rate);
    }
}
