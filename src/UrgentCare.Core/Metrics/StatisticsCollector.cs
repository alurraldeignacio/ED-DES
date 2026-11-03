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

    /// <summary>
    /// Mean number of patients waiting, weighted by how long each queue length held. This is
    /// the queueing-theory L: a plain mean over events would over-weight busy moments, because
    /// events cluster exactly where the queue is changing fastest.
    /// </summary>
    public double AverageQueueLength { get; init; }

    /// <summary>Longest the waiting area ever got during the run.</summary>
    public int MaxQueueLength { get; init; }
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
        var (averageQueueLength, maxQueueLength) = QueueLength(events, timeHorizonMinutes);

        return new SimulationMetrics
        {
            Arrivals = arrivals,
            Completed = completed,
            Abandoned = abandoned,
            Throughput = hours > 0 ? completed / hours : 0.0,
            AverageWaitTime = _metricsConfig.ReportAverageWait
                ? waitTimes.Count > 0 ? waitTimes.Average() : 0.0
                : null,
            P95WaitTime = _metricsConfig.ReportP95Wait ? Percentile.Of(waitTimes, 0.95) : null,
            AbandonmentRate = _metricsConfig.ReportAbandonmentRate
                ? arrivals > 0 ? (double)abandoned / arrivals : 0.0
                : null,
            Utilization = _metricsConfig.ReportUtilization
                ? OverallUtilization(busyMinutesByResource, timeHorizonMinutes, resourceCount)
                : null,
            UtilizationByResource = _metricsConfig.ReportUtilization
                ? UtilizationByResource(busyMinutesByResource, timeHorizonMinutes)
                : null,
            AverageQueueLength = averageQueueLength,
            MaxQueueLength = maxQueueLength
        };
    }

    /// <summary>
    /// Time-weighted mean queue length and the peak, read off the QueueLength carried by every
    /// event. Each event records the waiting-area size at the instant it was emitted, so the
    /// length between two consecutive events is the one the earlier event reported, and it held
    /// for exactly that gap. The final reading holds until the horizon.
    /// </summary>
    private static (double Average, int Max) QueueLength(
        IReadOnlyList<SimulationEvent> events,
        double timeHorizonMinutes)
    {
        var previousTime = 0.0;
        var previousLength = 0;
        var weighted = 0.0;
        var max = 0;

        foreach (var e in events)
        {
            weighted += previousLength * (e.Timestamp - previousTime);
            previousTime = e.Timestamp;
            previousLength = e.QueueLength;

            if (e.QueueLength > max)
            {
                max = e.QueueLength;
            }
        }

        if (timeHorizonMinutes <= 0)
        {
            return (0.0, max);
        }

        weighted += previousLength * (timeHorizonMinutes - previousTime);

        return (weighted / timeHorizonMinutes, max);
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
