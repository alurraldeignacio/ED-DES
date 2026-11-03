namespace UrgentCare.Core.Metrics;

/// <summary>
/// The single percentile implementation in the codebase, using linear interpolation between the
/// two neighbouring samples.
/// </summary>
/// <remarks>
/// This lives in Core, not in the analysis library, because both sides need it and the
/// dependency only runs one way. <see cref="StatisticsCollector"/> uses it for the P95 wait
/// within a run, and the analysis library uses it for the P95 across replications. There are
/// several accepted definitions of a percentile that disagree on where a position between two
/// samples lands; keeping one implementation means those two figures can never be computed
/// under different rules and quietly disagree.
/// </remarks>
public static class Percentile
{
    /// <summary>
    /// The value at the given percentile of the sample.
    /// </summary>
    /// <param name="values">
    /// The sample, in any order. The collection is not modified: sorting a caller's list in
    /// place would be a surprising side effect.
    /// </param>
    /// <param name="percentile">Position in the range 0.0 to 1.0 inclusive.</param>
    /// <returns>
    /// The interpolated value, or 0 for an empty sample, which is the only sensible answer when
    /// nobody ever waited.
    /// </returns>
    public static double Of(IReadOnlyList<double> values, double percentile)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (percentile is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentile), percentile, "percentile must be between 0.0 and 1.0 inclusive.");
        }

        if (values.Count == 0)
        {
            return 0.0;
        }

        if (values.Count == 1)
        {
            return values[0];
        }

        var sorted = values.OrderBy(v => v).ToArray();
        var position = percentile * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return sorted[lower];
        }

        return sorted[lower] + ((position - lower) * (sorted[upper] - sorted[lower]));
    }
}
