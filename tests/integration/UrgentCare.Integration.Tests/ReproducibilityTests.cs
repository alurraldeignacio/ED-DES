using UrgentCare.Analysis.Scenarios;
using UrgentCare.Core.Events;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// US3 AS1 and FR-011: a fixed seed and configuration reproduce identical results. Without this
/// property no scenario comparison means anything, because a difference between two runs could
/// always be noise rather than the change under test.
/// </summary>
public class ReproducibilityTests
{
    [Fact]
    public void TheSameSeedAndConfiguration_ProduceIdenticalMetrics()
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 2, timeHorizon: 1440.0, seed: 8080);
        var runner = new ScenarioRunner();

        var first = runner.Run(config).Metrics;
        var second = runner.Run(config).Metrics;

        // Compared field by field rather than with record equality: SimulationMetrics carries a
        // dictionary, and a record compares that member by reference, so two runs holding the
        // same numbers in different dictionary instances would never test equal.
        Assert.Equal(first.Arrivals, second.Arrivals);
        Assert.Equal(first.Completed, second.Completed);
        Assert.Equal(first.Abandoned, second.Abandoned);
        Assert.Equal(first.AverageWaitTime, second.AverageWaitTime);
        Assert.Equal(first.P95WaitTime, second.P95WaitTime);
        Assert.Equal(first.AbandonmentRate, second.AbandonmentRate);
        Assert.Equal(first.Utilization, second.Utilization);
        Assert.Equal(first.Throughput, second.Throughput);
        Assert.Equal(first.AverageQueueLength, second.AverageQueueLength);
        Assert.Equal(first.MaxQueueLength, second.MaxQueueLength);
        Assert.Equal(first.UtilizationByResource, second.UtilizationByResource);
    }

    [Fact]
    public void TheSameSeedAndConfiguration_ProduceTheIdenticalEventStream()
    {
        // Metrics are derived from events, so matching event streams is the stronger claim:
        // it says the runs were the same simulation, not merely that they averaged out alike.
        var config = ScenarioBuilder.Scenario(resourceCount: 2, timeHorizon: 1440.0, seed: 8080);

        var (_, firstLog) = ScenarioBuilder.Run(config);
        var (_, secondLog) = ScenarioBuilder.Run(config);

        Assert.Equal(firstLog.Events.Count, secondLog.Events.Count);

        for (var i = 0; i < firstLog.Events.Count; i++)
        {
            var a = firstLog.Events[i];
            var b = secondLog.Events[i];

            Assert.Equal(a.Timestamp, b.Timestamp);
            Assert.Equal(a.Type, b.Type);
            Assert.Equal(a.PatientId, b.PatientId);
            Assert.Equal(a.ResourceId, b.ResourceId);
            Assert.Equal(a.Urgency, b.Urgency);
            Assert.Equal(a.QueueLength, b.QueueLength);
        }
    }

    [Fact]
    public void ADifferentSeed_ProducesADifferentRun()
    {
        var runner = new ScenarioRunner();

        var first = runner.Run(ScenarioBuilder.Scenario(timeHorizon: 1440.0, seed: 1));
        var second = runner.Run(ScenarioBuilder.Scenario(timeHorizon: 1440.0, seed: 2));

        Assert.NotEqual(first.Metrics.AverageWaitTime, second.Metrics.AverageWaitTime);
    }

    [Fact]
    public void OverridingTheSeed_ChangesTheRunWithoutTouchingTheScenarioFile()
    {
        // This is how a replication batch varies a run: the configuration is untouched and only
        // the seed moves, so every other assumption is provably held constant.
        var config = ScenarioBuilder.Scenario(timeHorizon: 1440.0, seed: 500);
        var runner = new ScenarioRunner();

        var asWritten = runner.Run(config);
        var overridden = runner.Run(config, seed: 999);

        Assert.Equal(500, asWritten.Seed);
        Assert.Equal(999, overridden.Seed);
        Assert.NotEqual(asWritten.Metrics.AverageWaitTime, overridden.Metrics.AverageWaitTime);
    }

    [Fact]
    public void EveryRunRecordsTheScenarioAndContractVersionThatProducedIt()
    {
        var config = ScenarioBuilder.Scenario(timeHorizon: 480.0);

        var result = new ScenarioRunner().Run(config);

        Assert.Equal(config.ScenarioId, result.ScenarioId);
        Assert.Equal(config.SchemaVersion, result.SchemaVersion);
    }

    [Fact]
    public void EventsCarryTheReplicationIndexTheyWereRunUnder()
    {
        // Correlation keys are what keep a batch of replications distinguishable once their
        // events are pooled (Constitution Principle VI).
        var config = ScenarioBuilder.Scenario(timeHorizon: 480.0);
        var log = new InMemoryEventLog();

        var engine = UrgentCare.Core.SimulationEngine.Create(
            config,
            new UrgentCare.Analysis.Distributions.MathNetDistributionFactory(),
            log,
            runId: null,
            replicationIndex: 3);

        engine.Run();

        Assert.NotEmpty(log.Events);
        Assert.All(log.Events, e => Assert.Equal(3, e.ReplicationIndex));
        Assert.All(log.Events, e => Assert.Equal(engine.RunId, e.RunId));
    }
}
