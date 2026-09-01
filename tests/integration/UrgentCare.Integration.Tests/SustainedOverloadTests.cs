using System.Diagnostics;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// spec.md edge case: more patients than available resources for extended periods. The run must
/// stay stable and finite, with pressure showing up as queue length and abandonment rather than
/// as a stalled engine or an event log that grows without bound.
/// </summary>
public class SustainedOverloadTests
{
    [Fact]
    public void SustainedOverloadCompletesWithoutStalling()
    {
        // One resource against roughly ten times the demand it can serve, for a full week.
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            roomCount: 2,
            baseRatePerHour: 40.0,
            timeHorizon: 10080.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var stopwatch = Stopwatch.StartNew();
        var (engine, log) = ScenarioBuilder.Run(config);
        stopwatch.Stop();

        Assert.Equal(SimulationStatus.Complete, engine.Clock.Status);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(30),
            $"Overloaded run took {stopwatch.Elapsed.TotalSeconds:F1}s, which suggests a stall.");
        Assert.NotEmpty(log.Events);
    }

    [Fact]
    public void OverloadShowsUpAsQueueGrowthRatherThanLostPatients()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 40.0,
            timeHorizon: 2880.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (engine, _) = ScenarioBuilder.Run(config);

        // Nobody abandons here, so every patient is accounted for in exactly one state.
        var total = engine.Patients.Count;
        var accounted = engine.Patients.Count(p =>
            p.State is PatientState.Waiting or PatientState.InTreatment or PatientState.Completed);

        Assert.Equal(total, accounted);
        Assert.True(engine.Department.QueueLength > 0, "An overloaded department should have a backlog.");
    }

    [Fact]
    public void OverloadRaisesAbandonmentWhenPatienceIsFinite()
    {
        var comfortable = ScenarioBuilder.Scenario(
            resourceCount: 4,
            baseRatePerHour: 4.0,
            timeHorizon: 2880.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(60.0));

        var overloaded = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 40.0,
            timeHorizon: 2880.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(60.0));

        Assert.True(AbandonmentRate(overloaded) > AbandonmentRate(comfortable));
    }

    [Fact]
    public void TheEventLogStaysProportionalToPatientVolume()
    {
        // Each patient can only produce a bounded number of events, so the log must not grow
        // faster than arrivals. This is the retention assumption InMemoryEventLog documents.
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 40.0,
            timeHorizon: 2880.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (engine, log) = ScenarioBuilder.Run(config);

        Assert.True(
            log.Events.Count <= engine.Patients.Count * 6,
            $"{log.Events.Count} events for {engine.Patients.Count} patients suggests unbounded growth.");
    }

    [Fact]
    public void ResourcesStayFullyUtilizedUnderOverload()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 40.0,
            timeHorizon: 2880.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (_, log) = ScenarioBuilder.Run(config);
        var metrics = new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);

        Assert.NotNull(metrics.Utilization);
        Assert.True(
            metrics.Utilization > 0.9,
            $"An overloaded single resource should be near-saturated, but utilization was {metrics.Utilization:P0}.");
        Assert.True(metrics.Utilization <= 1.0 + 1e-9);
    }

    private static double AbandonmentRate(UrgentCare.Core.Configuration.ScenarioConfig config)
    {
        var (_, log) = ScenarioBuilder.Run(config);
        var metrics = new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);

        return metrics.AbandonmentRate ?? 0.0;
    }
}
