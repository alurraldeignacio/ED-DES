using UrgentCare.Core.Configuration;
using UrgentCare.Core.Events;

namespace UrgentCare.Core.Metrics;

/// <summary>
/// Outcomes of one run. A metric disabled in MetricsConfig comes back null rather than zero,
/// so a reader can tell "not measured" from "measured as none".
/// </summary>
public sealed record SimulationMetrics
{
    public int Arrivals { get; init; }

    public int Completed { get; init; }

    public int Abandoned { get; init; }

    public double? AverageWaitTime { get; init; }

    public double? P95WaitTime { get; init; }

    public double? AbandonmentRate { get; init; }

    public double? Utilization { get; init; }

    /// <summary>Completed patients per hour of simulated time.</summary>
    public double Throughput { get; init; }

    /// <summary>Utilization per care resource, when reporting is enabled.</summary>
    public IReadOnlyDictionary<string, double>? UtilizationByResource { get; init; }
}

/// <summary>
/// Derives run metrics from the structured event stream rather than from counters kept during
/// the run (FR-009, Constitution Principle VI). Because the events are the source of truth, the
/// same numbers can be recomputed later from an exported log.
/// </summary>
public sealed class StatisticsCollector
{
    private readonly MetricsConfig _metricsConfig;

    public StatisticsCollector(MetricsConfig metricsConfig)
    {
        ArgumentNullException.ThrowIfNull(metricsConfig);
        _metricsConfig = metricsConfig;
    }

    public SimulationMetrics Collect(
        IReadOnlyList<SimulationEvent> events,
        double timeHorizonMinutes,
        int resourceCount)
    {
        ArgumentNullException.ThrowIfNull(events);

        var arrivalTimes = new Dictionary<string, double>();
        var waitTimes = new List<double>();
        var busyMinutesByResource = new Dictionary<string, double>();
        var treatmentStarts = new Dictionary<string, (double Time, string ResourceId)>();

        var arrivals = 0;
        var completed = 0;
        var abandoned = 0;

        foreach (var e in events)
        {
            switch (e.Type)
            {
                case SimulationEventType.PatientArrived when e.PatientId is not null:
                    arrivals++;
                    arrivalTimes[e.PatientId] = e.Timestamp;
                    break;

                case SimulationEventType.TreatmentStarted when e.PatientId is not null:
                    if (arrivalTimes.TryGetValue(e.PatientId, out var arrivedAt))
                    {
                        waitTimes.Add(e.Timestamp - arrivedAt);
                    }

                    if (e.ResourceId is not null)
                    {
                        treatmentStarts[e.PatientId] = (e.Timestamp, e.ResourceId);
                    }

                    break;

                case SimulationEventType.TreatmentCompleted when e.PatientId is not null:
                    completed++;
                    if (treatmentStarts.Remove(e.PatientId, out var start))
                    {
                        busyMinutesByResource.TryGetValue(start.ResourceId, out var busy);
                        busyMinutesByResource[start.ResourceId] = busy + (e.Timestamp - start.Time);
                    }

                    break;

                case SimulationEventType.PatientAbandoned when e.PatientId is not null:
                    abandoned++;
                    if (arrivalTimes.TryGetValue(e.PatientId, out var waitedFrom))
                    {
                        waitTimes.Add(e.Timestamp - waitedFrom);
                    }

                    break;
            }
        }

        var hours = timeHorizonMinutes / 60.0;

        return new SimulationMetrics
        {
            Arrivals = arrivals,
            Completed = completed,
            Abandoned = abandoned,
            Throughput = hours > 0 ? completed / hours : 0.0,
            AverageWaitTime = _metricsConfig.ReportAverageWait
                ? waitTimes.Count > 0 ? waitTimes.Average() : 0.0
                : null,
            P95WaitTime = _metricsConfig.ReportP95Wait ? Percentile(waitTimes, 0.95) : null,
            AbandonmentRate = _metricsConfig.ReportAbandonmentRate
                ? arrivals > 0 ? (double)abandoned / arrivals : 0.0
                : null,
            Utilization = _metricsConfig.ReportUtilization
                ? OverallUtilization(busyMinutesByResource, timeHorizonMinutes, resourceCount)
                : null,
            UtilizationByResource = _metricsConfig.ReportUtilization
                ? UtilizationByResource(busyMinutesByResource, timeHorizonMinutes)
                : null
        };
    }

    /// <summary>
    /// Linear-interpolated percentile. Returns 0 for an empty sample, which is the only
    /// sensible answer when nobody ever waited.
    /// </summary>
    internal static double Percentile(IReadOnlyList<double> values, double percentile)
    {
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

    private static double OverallUtilization(
        Dictionary<string, double> busyMinutes,
        double timeHorizonMinutes,
        int resourceCount)
    {
        if (resourceCount <= 0 || timeHorizonMinutes <= 0)
        {
            return 0.0;
        }

        return busyMinutes.Values.Sum() / (timeHorizonMinutes * resourceCount);
    }

    private static IReadOnlyDictionary<string, double> UtilizationByResource(
        Dictionary<string, double> busyMinutes,
        double timeHorizonMinutes)
    {
        if (timeHorizonMinutes <= 0)
        {
            return new Dictionary<string, double>();
        }

        return busyMinutes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / timeHorizonMinutes);
    }
}
