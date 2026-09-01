namespace UrgentCare.Core.Random;

/// <summary>
/// Deterministic random source. Two instances constructed with the same seed emit
/// identical sequences, which is what makes a scenario run reproducible.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private readonly System.Random _random;

    public SeededRandomSource(int seed)
    {
        Seed = seed;
        _random = new System.Random(seed);
    }

    public int Seed { get; }

    public double NextDouble() => _random.NextDouble();

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                $"maxExclusive ({maxExclusive}) must be greater than minInclusive ({minInclusive}).");
        }

        return _random.Next(minInclusive, maxExclusive);
    }

    public System.Random AsSystemRandom() => _random;
}
