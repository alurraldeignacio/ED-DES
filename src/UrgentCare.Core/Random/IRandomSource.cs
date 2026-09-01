namespace UrgentCare.Core.Random;

/// <summary>
/// Source of pseudo-random numbers. Injected rather than static so a run is reproducible
/// from its seed alone (Constitution Principle III).
/// </summary>
public interface IRandomSource
{
    /// <summary>Seed this source was constructed with.</summary>
    int Seed { get; }

    /// <summary>Returns a uniform sample in [0, 1).</summary>
    double NextDouble();

    /// <summary>Returns a uniform integer in [minInclusive, maxExclusive).</summary>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>
    /// The underlying <see cref="System.Random"/>, for libraries that require one directly.
    /// Sampling through this instance draws from the same seeded stream, so determinism holds.
    /// </summary>
    System.Random AsSystemRandom();
}
