using UrgentCare.Core.Configuration;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// US2 AS1: change staffing, run, and compare the result against a baseline. The point of the
/// story is that the comparison is possible at all, so these tests check both that the two runs
/// report the same set of measures and that the measures move in the direction staffing implies.
/// </summary>
public class StaffingComparisonTests
{
    private static SimulationMetrics RunWithStaffing(int resourceCount)
    {
        // A busy department with one week of demand, so staffing genuinely binds.
        var config = ScenarioBuilder.Scenario(
            resourceCount: resourceCount,
            roomCount: 10,
            baseRatePerHour: 6.0,
            timeHorizon: 2880.0,
            seed: 4242);

        var (_, log) = ScenarioBuilder.Run(config);

        return new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, resourceCount);
    }

    [Fact]
    public void BothStaffingLevels_ReportTheSameSetOfMetrics()
    {
        var lean = RunWithStaffing(2);
        var generous = RunWithStaffing(6);

        // Comparability is the requirement: every figure present for one run is present for the
        // other, so a baseline-versus-variant table never has a hole in it (SC-002).
        Assert.NotNull(lean.AverageWaitTime);
        Assert.NotNull(generous.AverageWaitTime);
        Assert.NotNull(lean.P95WaitTime);
        Assert.NotNull(generous.P95WaitTime);
        Assert.NotNull(lean.AbandonmentRate);
        Assert.NotNull(generous.AbandonmentRate);
        Assert.NotNull(lean.Utilization);
        Assert.NotNull(generous.Utilization);
    }

    [Fact]
    public void MoreStaff_ReducesAverageWaitingTime()
    {
        var lean = RunWithStaffing(2);
        var generous = RunWithStaffing(6);

        Assert.True(
            generous.AverageWaitTime < lean.AverageWaitTime,
            $"Expected more staff to cut the wait, but 6 staff waited "
            + $"{generous.AverageWaitTime:0.0} min against {lean.AverageWaitTime:0.0} min for 2.");
    }

    [Fact]
    public void MoreStaff_RaisesThroughput()
    {
        var lean = RunWithStaffing(2);
        var generous = RunWithStaffing(6);

        Assert.True(
            generous.Throughput > lean.Throughput,
            $"Expected more staff to complete more patients per hour, but got "
            + $"{generous.Throughput:0.00} against {lean.Throughput:0.00}.");
    }

    [Fact]
    public void MoreStaff_LowersAbandonment()
    {
        var lean = RunWithStaffing(2);
        var generous = RunWithStaffing(6);

        Assert.True(
            generous.AbandonmentRate <= lean.AbandonmentRate,
            $"Expected more staff to reduce walkouts, but got {generous.AbandonmentRate:P1} "
            + $"against {lean.AbandonmentRate:P1}.");
    }

    [Fact]
    public void LeanStaffing_IsBusierPerResource()
    {
        var lean = RunWithStaffing(2);
        var generous = RunWithStaffing(6);

        Assert.True(
            lean.Utilization > generous.Utilization,
            $"Expected a lean team to be busier, but got {lean.Utilization:P1} "
            + $"against {generous.Utilization:P1}.");
    }

    [Fact]
    public void TheSameStaffingAndSeed_ReproducesTheSameComparison()
    {
        // Without this, a comparison between two runs could not be attributed to the staffing
        // change rather than to run-to-run noise.
        var first = RunWithStaffing(3);
        var second = RunWithStaffing(3);

        Assert.Equal(first.Arrivals, second.Arrivals);
        Assert.Equal(first.Completed, second.Completed);
        Assert.Equal(first.AverageWaitTime, second.AverageWaitTime);
        Assert.Equal(first.Throughput, second.Throughput);
    }
}
