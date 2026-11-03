using UrgentCare.Analysis.Distributions;
using UrgentCare.Core;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;
using UrgentCare.Web.Session;

namespace UrgentCare.Web.Tests;

/// <summary>
/// FR-012 and SC-002: a run watched in the browser ends with exactly the figures the CLI's
/// headless run reports for the same scenario and seed (contracts/visual-session.md).
/// </summary>
public class CliParityTests
{
    public static TheoryData<string, int?> Runs()
    {
        var data = new TheoryData<string, int?>();
        foreach (var scenario in new BundledScenarios().All)
        {
            data.Add(scenario.Name, null);
        }

        data.Add("default", 7);
        data.Add("default", 2024);
        return data;
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void FinalMetrics_MatchTheCliHeadlessRun(string scenarioName, int? seed)
    {
        var session = TestSessions.Create(scenarioName, seed);
        session.JumpToEnd();

        var config = session.Scenario.Config;
        var cliLog = new InMemoryEventLog();
        SimulationEngine.Create(config, new MathNetDistributionFactory(), cliLog, seed: session.Seed).Run();
        var cli = new StatisticsCollector(config.Metrics)
            .Collect(cliLog.Events, config.TimeHorizon, config.Department.ResourceCount);

        AssertSameMetrics(cli, session.Metrics);
    }

    private static void AssertSameMetrics(SimulationMetrics expected, SimulationMetrics actual)
    {
        Assert.Equal(expected.Arrivals, actual.Arrivals);
        Assert.Equal(expected.Completed, actual.Completed);
        Assert.Equal(expected.Abandoned, actual.Abandoned);
        Assert.Equal(expected.AverageWaitTime, actual.AverageWaitTime);
        Assert.Equal(expected.P95WaitTime, actual.P95WaitTime);
        Assert.Equal(expected.AbandonmentRate, actual.AbandonmentRate);
        Assert.Equal(expected.Utilization, actual.Utilization);
        Assert.Equal(expected.Throughput, actual.Throughput);
        Assert.Equal(expected.AverageQueueLength, actual.AverageQueueLength);
        Assert.Equal(expected.MaxQueueLength, actual.MaxQueueLength);

        // A record compares dictionaries by reference, so compare the contents.
        Assert.Equal(
            expected.UtilizationByResource?.OrderBy(kv => kv.Key),
            actual.UtilizationByResource?.OrderBy(kv => kv.Key));
    }
}
