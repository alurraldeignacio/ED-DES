using UrgentCare.Analysis.Metrics;
using UrgentCare.Analysis.Scenarios;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// US3 AS2 and FR-011: run one scenario N times and aggregate. This is what turns a stochastic
/// model into something you can draw a conclusion from, because it separates the effect of a
/// scenario change from the run-to-run variation the model produces on its own.
/// </summary>
public class ReplicationAggregationTests
{
    private static readonly Core.Configuration.ScenarioConfig Scenario =
        ScenarioBuilder.Scenario(resourceCount: 2, timeHorizon: 1440.0, seed: 31415);

    [Fact]
    public void ReplicationsProduceTheRequestedNumberOfResults()
    {
        var results = new ReplicationRunner().Run(Scenario, replications: 8);

        Assert.Equal(8, results.Count);
    }

    [Fact]
    public void EachReplicationUsesItsOwnSeed()
    {
        var results = new ReplicationRunner().Run(Scenario, replications: 10);

        var seeds = results.Select(r => r.Seed).ToList();

        Assert.Equal(seeds.Count, seeds.Distinct().Count());
    }

    [Fact]
    public void ReplicationsDifferFromEachOther()
    {
        // If every replication returned the same numbers the batch would be measuring nothing.
        var results = new ReplicationRunner().Run(Scenario, replications: 10);

        var waits = results.Select(r => r.Metrics.AverageWaitTime).Distinct().ToList();

        Assert.True(waits.Count > 1, "Expected replications to vary, but every run gave the same wait.");
    }

    [Fact]
    public void TheWholeBatchIsReproducible()
    {
        var first = new ReplicationRunner().Run(Scenario, replications: 6);
        var second = new ReplicationRunner().Run(Scenario, replications: 6);

        Assert.Equal(first.Select(r => r.Seed), second.Select(r => r.Seed));
        Assert.Equal(
            first.Select(r => r.Metrics.AverageWaitTime),
            second.Select(r => r.Metrics.AverageWaitTime));
    }

    [Fact]
    public void AggregatingABatch_SummarisesEveryComparedMetric()
    {
        var results = new ReplicationRunner().Run(Scenario, replications: 12);

        var aggregates = MetricsAggregator.Aggregate(results);

        Assert.Equal(ScenarioComparison.ComparedMetrics, aggregates.Select(a => a.Metric).ToList());
        Assert.All(aggregates, a => Assert.Equal(12, a.Samples));
    }

    [Fact]
    public void TheAggregateMeanSitsInsideTheObservedRange()
    {
        var results = new ReplicationRunner().Run(Scenario, replications: 12);

        var waits = results.Select(r => r.Metrics.AverageWaitTime!.Value).ToList();
        var stat = MetricsAggregator.Aggregate(results).Single(a => a.Metric == "averageWaitTime");

        Assert.InRange(stat.Mean, waits.Min(), waits.Max());
    }

    [Fact]
    public void MoreReplications_TightenTheConfidenceInterval()
    {
        // The interval scales with 1/sqrt(n), so a larger batch should say more precisely where
        // the true mean sits. This is the practical reason to replicate at all.
        var few = MetricsAggregator.Aggregate(new ReplicationRunner().Run(Scenario, replications: 4))
            .Single(a => a.Metric == "averageWaitTime");

        var many = MetricsAggregator.Aggregate(new ReplicationRunner().Run(Scenario, replications: 40))
            .Single(a => a.Metric == "averageWaitTime");

        var fewWidth = few.ConfidenceIntervalHigh - few.ConfidenceIntervalLow;
        var manyWidth = many.ConfidenceIntervalHigh - many.ConfidenceIntervalLow;

        Assert.True(
            manyWidth < fewWidth,
            $"Expected 40 replications to be more precise than 4, but the interval widths were "
            + $"{manyWidth:0.000} and {fewWidth:0.000}.");
    }

    [Fact]
    public void ARunManifest_RecordsTheAssumptionsAlongsideTheOutcomes()
    {
        // US3 AS3: the numbers are only reviewable next to the assumptions that produced them.
        var results = new ReplicationRunner().Run(Scenario, replications: 5);

        var manifest = RunManifest.For(Scenario, results);

        Assert.Equal(Scenario.ScenarioId, manifest.ScenarioId);
        Assert.Equal(Scenario.SchemaVersion, manifest.SchemaVersion);
        Assert.Equal(Scenario.Seed, manifest.BaseSeed);
        Assert.Equal(5, manifest.Replications);
        Assert.Equal(Scenario.Department.QueuePolicy, manifest.QueuePolicy);
        Assert.Equal(Scenario.Department.ResourceCount, manifest.ResourceCount);
        Assert.Equal(Scenario.TimeHorizon, manifest.TimeHorizon);
        Assert.NotEmpty(manifest.Aggregates);
    }

    [Fact]
    public void ZeroReplications_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReplicationRunner().Run(Scenario, replications: 0));
    }
}
