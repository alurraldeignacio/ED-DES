namespace UrgentCare.Analysis.Scenarios;

/// <summary>
/// Derives one random seed per replication from the scenario's base seed (FR-011).
/// </summary>
/// <remarks>
/// Two properties matter. The sequence must be reproducible, so a batch re-run from the same
/// scenario file lands on exactly the same seeds months later. And consecutive replications must
/// get uncorrelated streams: seeding replication <c>n</c> with <c>baseSeed + n</c> would give
/// neighbouring runs adjacent states, which for many generators produces visibly similar output
/// and quietly understates the variation a replication batch exists to measure. Mixing the base
/// seed and the index through a bit-avalanche gives both properties without keeping any state.
/// </remarks>
public static class SeedSequence
{
    public static int SeedFor(int baseSeed, int replicationIndex)
    {
        if (replicationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(replicationIndex), replicationIndex, "replicationIndex must be non-negative.");
        }

        unchecked
        {
            // SplitMix64 finalizer: a well-tested avalanche that spreads one bit of input change
            // across the whole output.
            var z = ((ulong)(uint)baseSeed << 32) | (uint)replicationIndex;

            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            // Masked to stay non-negative: a negative seed is legal but awkward to read back
            // out of a run manifest.
            return (int)(z & 0x7FFFFFFF);
        }
    }

    /// <summary>The first <paramref name="count"/> seeds of the sequence, in replication order.</summary>
    public static IEnumerable<int> Take(int baseSeed, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "count must be non-negative.");
        }

        for (var i = 0; i < count; i++)
        {
            yield return SeedFor(baseSeed, i);
        }
    }
}
