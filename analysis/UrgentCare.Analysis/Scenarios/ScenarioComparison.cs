namespace UrgentCare.Analysis.Scenarios;

/// <summary>
/// One row of a comparison table: the same measure taken from both runs, with the change
/// between them.
/// </summary>
/// <param name="Metric">Measure name, matching the export column it corresponds to.</param>
/// <param name="Baseline">Baseline value, or null when the run did not measure it.</param>
/// <param name="Variant">Variant value, or null when the run did not measure it.</param>
/// <param name="AbsoluteChange">Variant minus baseline, or null when either side is missing.</param>
/// <param name="PercentChange">
/// Change relative to the baseline, or null when the baseline is zero or either side is missing.
/// </param>
public sealed record MetricDelta(
    string Metric,
    double? Baseline,
    double? Variant,
    double? AbsoluteChange,
    double? PercentChange);

/// <summary>
/// A baseline-versus-variant table over the measures SC-002 compares scenarios on (FR-010).
/// </summary>
/// <remarks>
/// Every comparison carries the same rows in the same order, including rows whose values are
/// null because the run did not measure them. A table whose shape depended on what a particular
/// run happened to record could not be read alongside another one.
/// </remarks>
public sealed record ScenarioComparison(
    ScenarioResult Baseline,
    ScenarioResult Variant,
    IReadOnlyList<MetricDelta> Deltas)
{
    /// <summary>The measures compared, in the order they appear in every table.</summary>
    public static IReadOnlyList<string> ComparedMetrics { get; } =
    [
        "averageWaitTime",
        "p95WaitTime",
        "abandonmentRate",
        "utilization",
        "throughput",
        "averageQueueLength",
        "maxQueueLength"
    ];

    public static ScenarioComparison Compare(ScenarioResult baseline, ScenarioResult variant)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(variant);

        var b = baseline.Metrics;
        var v = variant.Metrics;

        List<MetricDelta> deltas =
        [
            Delta("averageWaitTime", b.AverageWaitTime, v.AverageWaitTime),
            Delta("p95WaitTime", b.P95WaitTime, v.P95WaitTime),
            Delta("abandonmentRate", b.AbandonmentRate, v.AbandonmentRate),
            Delta("utilization", b.Utilization, v.Utilization),
            Delta("throughput", b.Throughput, v.Throughput),
            Delta("averageQueueLength", b.AverageQueueLength, v.AverageQueueLength),
            Delta("maxQueueLength", b.MaxQueueLength, v.MaxQueueLength)
        ];

        return new ScenarioComparison(baseline, variant, deltas);
    }

    /// <summary>
    /// Builds one row. A percent change against a zero baseline is left null rather than
    /// reported as infinity: "the wait went from nothing to something" has no meaningful
    /// percentage, and a placeholder number would be read as one.
    /// </summary>
    private static MetricDelta Delta(string metric, double? baseline, double? variant)
    {
        if (baseline is not { } b || variant is not { } v)
        {
            return new MetricDelta(metric, baseline, variant, null, null);
        }

        var absolute = v - b;
        var percent = b == 0.0 ? (double?)null : absolute / b * 100.0;

        return new MetricDelta(metric, b, v, absolute, percent);
    }
}
