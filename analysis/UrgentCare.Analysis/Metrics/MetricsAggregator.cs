using UrgentCare.Analysis.Scenarios;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Analysis.Metrics;

/// <summary>
/// One measure summarised across a batch of replications.
/// </summary>
/// <param name="Metric">Measure name, matching the export column it corresponds to.</param>
/// <param name="Samples">How many replications actually reported this measure.</param>
/// <param name="Mean">Arithmetic mean across the replications.</param>
/// <param name="P95">95th percentile across the replications.</param>
/// <param name="StandardDeviation">Sample standard deviation, zero when there is a single sample.</param>
/// <param name="ConfidenceIntervalLow">Lower bound of the 95% interval for the mean.</param>
/// <param name="ConfidenceIntervalHigh">Upper bound of the 95% interval for the mean.</param>
public sealed record AggregateStatistic(
    string Metric,
    int Samples,
    double Mean,
    double P95,
    double StandardDeviation,
    double ConfidenceIntervalLow,
    double ConfidenceIntervalHigh);

/// <summary>
/// Summarises a replication batch into one row per measure (FR-011).
/// </summary>
/// <remarks>
/// The confidence interval is the normal approximation, mean ± 1.96 · s/√n. At the replication
/// counts this tool is built for that is close enough, and it has the property that matters
/// operationally: the interval narrows as the batch grows, so a reader can see when they have
/// run enough replications to tell two scenarios apart.
/// </remarks>
public static class MetricsAggregator
{
    /// <summary>Normal-approximation multiplier for a 95% interval.</summary>
    private const double NormalQuantile95 = 1.959963984540054;

    public static IReadOnlyList<AggregateStatistic> Aggregate(IReadOnlyList<ScenarioResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            throw new ArgumentException(
                "Cannot aggregate an empty batch: there is nothing to summarise.", nameof(results));
        }

        return [.. ScenarioComparison.ComparedMetrics.Select(
            metric => Summarise(metric, Samples(results, metric)))];
    }

    /// <summary>
    /// Pulls one measure out of every replication that reported it. A metric switched off in
    /// MetricsConfig comes back null and is skipped rather than counted as zero, which would
    /// drag the mean toward a value nobody observed.
    /// </summary>
    private static List<double> Samples(IReadOnlyList<ScenarioResult> results, string metric)
    {
        var samples = new List<double>(results.Count);

        foreach (var result in results)
        {
            var m = result.Metrics;

            double? value = metric switch
            {
                "averageWaitTime" => m.AverageWaitTime,
                "p95WaitTime" => m.P95WaitTime,
                "abandonmentRate" => m.AbandonmentRate,
                "utilization" => m.Utilization,
                "throughput" => m.Throughput,
                "averageQueueLength" => m.AverageQueueLength,
                "maxQueueLength" => m.MaxQueueLength,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(metric), metric, "Metric is not part of the aggregated set.")
            };

            if (value is { } sample)
            {
                samples.Add(sample);
            }
        }

        return samples;
    }

    private static AggregateStatistic Summarise(string metric, List<double> samples)
    {
        if (samples.Count == 0)
        {
            return new AggregateStatistic(metric, 0, 0.0, 0.0, 0.0, 0.0, 0.0);
        }

        var mean = samples.Average();
        var p95 = Percentile.Of(samples, 0.95);

        // A single sample has no sample standard deviation. Reporting zero with the interval
        // collapsed onto the mean says "one draw, no spread measured", which is honest; a
        // fabricated interval would read as precision the batch does not have.
        if (samples.Count == 1)
        {
            return new AggregateStatistic(metric, 1, mean, p95, 0.0, mean, mean);
        }

        var variance = samples.Sum(v => (v - mean) * (v - mean)) / (samples.Count - 1);
        var standardDeviation = Math.Sqrt(variance);
        var margin = NormalQuantile95 * standardDeviation / Math.Sqrt(samples.Count);

        return new AggregateStatistic(
            metric,
            samples.Count,
            mean,
            p95,
            standardDeviation,
            mean - margin,
            mean + margin);
    }
}
