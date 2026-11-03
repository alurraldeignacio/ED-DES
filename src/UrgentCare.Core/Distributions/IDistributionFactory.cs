using UrgentCare.Core.Configuration;
using UrgentCare.Core.Random;

namespace UrgentCare.Core.Distributions;

/// <summary>
/// A configured probability distribution bound to a random source. Core defines the
/// abstraction so it stays free of MathNet; the concrete factory lives in UrgentCare.Analysis.
/// </summary>
public interface IDistribution
{
    /// <summary>Draws one sample from the distribution's own random stream.</summary>
    double Sample();
}

/// <summary>
/// Builds distributions from configuration. Implementations must reject an unknown family
/// or a family whose required parameters are missing.
/// </summary>
public interface IDistributionFactory
{
    /// <summary>
    /// Creates a distribution bound to <paramref name="random"/>, so every sample it produces
    /// comes from the run's seeded stream.
    /// </summary>
    IDistribution Create(DistributionConfig config, IRandomSource random);
}
