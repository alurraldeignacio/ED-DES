using UrgentCare.Analysis.Scenarios;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Analysis.Tests;

/// <summary>
/// FR-010: a baseline-versus-variant table. The table's shape is the contract here, because a
/// comparison whose rows change between runs cannot be read side by side.
/// </summary>
public class ScenarioComparisonTests
{
    private static ScenarioResult Result(
        string scenarioId,
        double averageWait,
        double throughput,
        double abandonmentRate,
        double utilization) =>
        new(scenarioId, "1.0.0", 12345, new SimulationMetrics
        {
            Arrivals = 100,
            Completed = 90,
            Abandoned = 10,
            AverageWaitTime = averageWait,
            P95WaitTime = averageWait * 2,
            AbandonmentRate = abandonmentRate,
            Utilization = utilization,
            Throughput = throughput,
            AverageQueueLength = 3.5,
            MaxQueueLength = 12
        });

    private static ScenarioComparison SampleComparison() =>
        ScenarioComparison.Compare(
            Result("baseline", averageWait: 40.0, throughput: 5.0, abandonmentRate: 0.20, utilization: 0.90),
            Result("variant", averageWait: 20.0, throughput: 6.0, abandonmentRate: 0.10, utilization: 0.75));

    [Fact]
    public void ComparisonKeepsBothScenariosIdentifiable()
    {
        var comparison = SampleComparison();

        Assert.Equal("baseline", comparison.Baseline.ScenarioId);
        Assert.Equal("variant", comparison.Variant.ScenarioId);
    }

    [Fact]
    public void EveryComparedMetricAppearsAsARow()
    {
        var comparison = SampleComparison();

        var metrics = comparison.Deltas.Select(d => d.Metric).ToList();

        Assert.Equal(ScenarioComparison.ComparedMetrics, metrics);
    }

    [Fact]
    public void TheRowOrderIsTheSameForEveryComparison()
    {
        // Two comparisons of different scenarios must line up row for row, or a reader cannot
        // scan a batch of them (FR-010).
        var first = SampleComparison();
        var second = ScenarioComparison.Compare(
            Result("a", 10.0, 1.0, 0.5, 0.5),
            Result("b", 90.0, 9.0, 0.1, 0.1));

        Assert.Equal(
            first.Deltas.Select(d => d.Metric),
            second.Deltas.Select(d => d.Metric));
    }

    [Fact]
    public void AbsoluteChangeIsVariantMinusBaseline()
    {
        var comparison = SampleComparison();

        var wait = comparison.Deltas.Single(d => d.Metric == "averageWaitTime");

        Assert.Equal(40.0, wait.Baseline);
        Assert.Equal(20.0, wait.Variant);
        Assert.Equal(-20.0, wait.AbsoluteChange);
    }

    [Fact]
    public void PercentChangeIsRelativeToTheBaseline()
    {
        var comparison = SampleComparison();

        var wait = comparison.Deltas.Single(d => d.Metric == "averageWaitTime");

        Assert.NotNull(wait.PercentChange);
        Assert.Equal(-50.0, wait.PercentChange!.Value, precision: 6);
    }

    [Fact]
    public void AZeroBaseline_LeavesPercentChangeUndefinedRatherThanInfinite()
    {
        var comparison = ScenarioComparison.Compare(
            Result("baseline", averageWait: 0.0, throughput: 0.0, abandonmentRate: 0.0, utilization: 0.0),
            Result("variant", averageWait: 5.0, throughput: 1.0, abandonmentRate: 0.1, utilization: 0.2));

        var wait = comparison.Deltas.Single(d => d.Metric == "averageWaitTime");

        Assert.Equal(5.0, wait.AbsoluteChange);
        Assert.Null(wait.PercentChange);
    }

    [Fact]
    public void ThroughputAndUtilizationAreCompared()
    {
        var comparison = SampleComparison();

        var throughput = comparison.Deltas.Single(d => d.Metric == "throughput");
        var utilization = comparison.Deltas.Single(d => d.Metric == "utilization");

        Assert.Equal(1.0, throughput.AbsoluteChange);
        Assert.Equal(-0.15, utilization.AbsoluteChange!.Value, precision: 6);
    }

    [Fact]
    public void QueueLengthIsCompared()
    {
        // SC-002 lists queue length among the measures a scenario comparison must carry.
        var comparison = SampleComparison();

        Assert.Contains(comparison.Deltas, d => d.Metric == "averageQueueLength");
        Assert.Contains(comparison.Deltas, d => d.Metric == "maxQueueLength");
    }
}
