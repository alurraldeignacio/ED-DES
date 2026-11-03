using UrgentCare.Analysis.Metrics;
using UrgentCare.Analysis.Scenarios;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Analysis.Tests;

/// <summary>
/// FR-011: a batch of replications is summarised into one figure per measure, with a spread.
/// A single run of a stochastic model is one draw; the aggregate and its confidence interval
/// are what make a scenario comparison defensible rather than anecdotal.
/// </summary>
public class MetricsAggregatorTests
{
    private static ScenarioResult ResultWithWait(double averageWait, int seed = 1) =>
        new("scenario", "1.0.0", seed, new SimulationMetrics
        {
            Arrivals = 100,
            Completed = 90,
            Abandoned = 10,
            AverageWaitTime = averageWait,
            P95WaitTime = averageWait * 2,
            AbandonmentRate = 0.1,
            Utilization = 0.8,
            Throughput = 5.0,
            AverageQueueLength = 2.0,
            MaxQueueLength = 9
        });

    private static AggregateStatistic WaitStatistic(params double[] waits)
    {
        var results = waits.Select((w, i) => ResultWithWait(w, i)).ToList();

        return MetricsAggregator.Aggregate(results).Single(s => s.Metric == "averageWaitTime");
    }

    [Fact]
    public void MeanIsTheArithmeticMeanAcrossReplications()
    {
        var stat = WaitStatistic(10.0, 20.0, 30.0);

        Assert.Equal(3, stat.Samples);
        Assert.Equal(20.0, stat.Mean, precision: 6);
    }

    [Fact]
    public void StandardDeviationUsesTheSampleFormula()
    {
        // Values 10, 20, 30: deviations -10, 0, 10; sum of squares 200; divided by n-1 = 2
        // gives a variance of 100 and a standard deviation of 10. Dividing by n would give
        // 8.165, so this pins the sample (n-1) convention.
        var stat = WaitStatistic(10.0, 20.0, 30.0);

        Assert.Equal(10.0, stat.StandardDeviation, precision: 6);
    }

    [Fact]
    public void P95IsComputedAcrossTheReplicationResults()
    {
        var stat = WaitStatistic(10.0, 20.0, 30.0, 40.0, 50.0);

        // Shares Core's percentile helper: position 0.95 * 4 = 3.8, interpolated to 48.
        Assert.Equal(48.0, stat.P95, precision: 6);
    }

    [Fact]
    public void TheConfidenceIntervalIsCentredOnTheMean()
    {
        var stat = WaitStatistic(10.0, 20.0, 30.0);

        var midpoint = (stat.ConfidenceIntervalLow + stat.ConfidenceIntervalHigh) / 2.0;

        Assert.Equal(stat.Mean, midpoint, precision: 6);
    }

    [Fact]
    public void TheConfidenceIntervalWidensWithSpread()
    {
        var tight = WaitStatistic(20.0, 20.5, 19.5, 20.0);
        var loose = WaitStatistic(5.0, 40.0, 12.0, 33.0);

        var tightWidth = tight.ConfidenceIntervalHigh - tight.ConfidenceIntervalLow;
        var looseWidth = loose.ConfidenceIntervalHigh - loose.ConfidenceIntervalLow;

        Assert.True(looseWidth > tightWidth);
    }

    [Fact]
    public void IdenticalReplications_HaveNoSpreadAndACollapsedInterval()
    {
        var stat = WaitStatistic(25.0, 25.0, 25.0, 25.0);

        Assert.Equal(25.0, stat.Mean, precision: 6);
        Assert.Equal(0.0, stat.StandardDeviation, precision: 6);
        Assert.Equal(25.0, stat.ConfidenceIntervalLow, precision: 6);
        Assert.Equal(25.0, stat.ConfidenceIntervalHigh, precision: 6);
    }

    [Fact]
    public void ASingleReplication_ReportsNoSpreadRatherThanFailing()
    {
        // One sample has no sample standard deviation. Reporting zero with the interval
        // collapsed onto the mean is honest: it says "one draw, no spread measured".
        var stat = WaitStatistic(33.0);

        Assert.Equal(1, stat.Samples);
        Assert.Equal(33.0, stat.Mean, precision: 6);
        Assert.Equal(0.0, stat.StandardDeviation, precision: 6);
    }

    [Fact]
    public void EveryComparedMetricIsAggregated()
    {
        var results = new[] { ResultWithWait(10.0), ResultWithWait(20.0) };

        var metrics = MetricsAggregator.Aggregate(results).Select(s => s.Metric).ToList();

        Assert.Equal(ScenarioComparison.ComparedMetrics, metrics);
    }

    [Fact]
    public void AnEmptyBatch_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => MetricsAggregator.Aggregate([]));
    }

    [Fact]
    public void AMetricThatNoReplicationMeasured_ReportsZeroSamples()
    {
        var unmeasured = new ScenarioResult("scenario", "1.0.0", 1, new SimulationMetrics
        {
            Arrivals = 10,
            Throughput = 1.0,
            AverageWaitTime = null,
            P95WaitTime = null,
            AbandonmentRate = null,
            Utilization = null
        });

        var stat = MetricsAggregator.Aggregate([unmeasured])
            .Single(s => s.Metric == "averageWaitTime");

        Assert.Equal(0, stat.Samples);
        Assert.Equal(0.0, stat.Mean);
    }
}
