using UrgentCare.Core.Configuration;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Core.Tests;

/// <summary>
/// Queue-length aggregation (SC-002, FR-009). Average queue length is time-weighted, not a
/// plain mean over events: events cluster around busy moments, so averaging them directly
/// would over-report a queue that was briefly long and under-report one that sat long for
/// hours. The time-weighted figure is the queueing-theory L, the mean number waiting.
/// </summary>
public class StatisticsCollectorTests
{
    private static readonly Guid RunId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly StatisticsCollector _collector = new(new MetricsConfig());

    private static SimulationEvent QueueEvent(double timestamp, int queueLength) =>
        new(RunId, "test", 0, timestamp, SimulationEventType.PatientArrived, "P1", null, 3, queueLength);

    [Fact]
    public void EmptyRun_ReportsZeroQueueLength()
    {
        var metrics = _collector.Collect([], timeHorizonMinutes: 100.0, resourceCount: 1);

        Assert.Equal(0.0, metrics.AverageQueueLength);
        Assert.Equal(0, metrics.MaxQueueLength);
    }

    [Fact]
    public void MaxQueueLength_IsThePeakObserved()
    {
        SimulationEvent[] events =
        [
            QueueEvent(0.0, 1),
            QueueEvent(10.0, 7),
            QueueEvent(20.0, 2)
        ];

        var metrics = _collector.Collect(events, timeHorizonMinutes: 40.0, resourceCount: 1);

        Assert.Equal(7, metrics.MaxQueueLength);
    }

    [Fact]
    public void AverageQueueLength_IsWeightedByHowLongEachLengthHeld()
    {
        // Queue is 0 over [0,10), 5 over [10,30), then 1 over [30,40].
        // Weighted total = (0*10) + (5*20) + (1*10) = 110, over a 40-minute horizon = 2.75.
        // A plain mean of the three readings would give 2.0, so this pins the weighting.
        SimulationEvent[] events =
        [
            QueueEvent(0.0, 0),
            QueueEvent(10.0, 5),
            QueueEvent(30.0, 1)
        ];

        var metrics = _collector.Collect(events, timeHorizonMinutes: 40.0, resourceCount: 1);

        Assert.Equal(2.75, metrics.AverageQueueLength, precision: 6);
    }

    [Fact]
    public void ABrieflyLongQueue_DoesNotDominateTheAverage()
    {
        // One spike of 20 lasting a single minute, against an otherwise empty department.
        SimulationEvent[] events =
        [
            QueueEvent(0.0, 0),
            QueueEvent(50.0, 20),
            QueueEvent(51.0, 0)
        ];

        var metrics = _collector.Collect(events, timeHorizonMinutes: 100.0, resourceCount: 1);

        Assert.Equal(20, metrics.MaxQueueLength);
        Assert.Equal(0.2, metrics.AverageQueueLength, precision: 6);
    }

    [Fact]
    public void TheFinalQueueLength_HoldsUntilTheHorizon()
    {
        // Nothing happens after the last event, so the queue stays where it was left.
        SimulationEvent[] events = [QueueEvent(0.0, 0), QueueEvent(20.0, 4)];

        var metrics = _collector.Collect(events, timeHorizonMinutes: 40.0, resourceCount: 1);

        // 4 waiting over [20,40] = 80, over 40 minutes = 2.0.
        Assert.Equal(2.0, metrics.AverageQueueLength, precision: 6);
    }

    [Fact]
    public void ZeroHorizon_ReportsZeroRatherThanDividingByZero()
    {
        SimulationEvent[] events = [QueueEvent(0.0, 3)];

        var metrics = _collector.Collect(events, timeHorizonMinutes: 0.0, resourceCount: 1);

        Assert.Equal(0.0, metrics.AverageQueueLength);
        Assert.Equal(3, metrics.MaxQueueLength);
    }
}
