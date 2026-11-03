using UrgentCare.Core.Configuration;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// SC-005: a complete path from arrival to completion or abandonment for a single queue and a
/// single care resource. This is the MVP acceptance test.
/// </summary>
public class EndToEndFlowTests
{
    [Fact]
    public void PatientsArriveMoveThroughTreatmentAndLeave()
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 1, roomCount: 5);

        var (engine, log) = ScenarioBuilder.Run(config);

        Assert.NotEmpty(engine.Patients);
        Assert.Contains(log.Events, e => e.Type == SimulationEventType.PatientArrived);
        Assert.Contains(log.Events, e => e.Type == SimulationEventType.TreatmentStarted);
        Assert.Contains(log.Events, e => e.Type == SimulationEventType.TreatmentCompleted);
    }

    [Fact]
    public void EveryPatientEndsInAValidState()
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 2);

        var (engine, _) = ScenarioBuilder.Run(config);

        foreach (var patient in engine.Patients)
        {
            Assert.Contains(patient.State, new[]
            {
                PatientState.Waiting,
                PatientState.InTreatment,
                PatientState.Completed,
                PatientState.Abandoned
            });
        }
    }

    [Fact]
    public void TreatmentAlwaysStartsAfterArrival()
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 2);

        var (engine, _) = ScenarioBuilder.Run(config);

        foreach (var patient in engine.Patients.Where(p => p.TreatmentStartTime is not null))
        {
            Assert.True(patient.TreatmentStartTime >= patient.ArrivalTime);
        }
    }

    [Fact]
    public void CompletedPatientsHaveANonNegativeWaitAndADepartureTime()
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 2);

        var (engine, _) = ScenarioBuilder.Run(config);

        var completed = engine.Patients.Where(p => p.State == PatientState.Completed).ToList();
        Assert.NotEmpty(completed);

        foreach (var patient in completed)
        {
            Assert.NotNull(patient.WaitingTime);
            Assert.True(patient.WaitingTime >= 0);
            Assert.NotNull(patient.DepartureTime);
            Assert.True(patient.DepartureTime >= patient.TreatmentStartTime);
        }
    }

    [Fact]
    public void EventsAreEmittedInNonDecreasingTimeOrder()
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 2);

        var (_, log) = ScenarioBuilder.Run(config);

        for (var i = 1; i < log.Events.Count; i++)
        {
            Assert.True(log.Events[i].Timestamp >= log.Events[i - 1].Timestamp);
        }
    }

    [Fact]
    public void EveryEventCarriesItsCorrelationKeys()
    {
        var config = ScenarioBuilder.Scenario();

        var (engine, log) = ScenarioBuilder.Run(config);

        Assert.All(log.Events, e =>
        {
            Assert.Equal(engine.RunId, e.RunId);
            Assert.Equal("test", e.ScenarioId);
            Assert.Equal(0, e.ReplicationIndex);
        });
    }

    [Fact]
    public void NoEventFiresBeyondTheTimeHorizon()
    {
        var config = ScenarioBuilder.Scenario(timeHorizon: 300.0);

        var (_, log) = ScenarioBuilder.Run(config);

        Assert.All(log.Events, e => Assert.True(e.Timestamp <= config.TimeHorizon));
    }

    [Fact]
    public void MetricsDerivedFromTheEventLogAgreeWithThePatientRecords()
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 2);

        var (engine, log) = ScenarioBuilder.Run(config);
        var metrics = new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);

        Assert.Equal(engine.Patients.Count, metrics.Arrivals);
        Assert.Equal(engine.Patients.Count(p => p.State == PatientState.Completed), metrics.Completed);
        Assert.Equal(engine.Patients.Count(p => p.State == PatientState.Abandoned), metrics.Abandoned);
    }

    [Fact]
    public void AddingStaffReducesAverageWaitingTime()
    {
        // The headline claim of the whole simulator: staffing changes move the numbers.
        var lightlyStaffed = ScenarioBuilder.Scenario(resourceCount: 1, baseRatePerHour: 8.0);
        var wellStaffed = ScenarioBuilder.Scenario(resourceCount: 4, baseRatePerHour: 8.0);

        var thin = WaitFor(lightlyStaffed);
        var thick = WaitFor(wellStaffed);

        Assert.True(
            thick < thin,
            $"Expected more staff to shorten the wait, but got {thick:F1} with 4 resources " +
            $"versus {thin:F1} with 1.");
    }

    [Fact]
    public void SameSeedProducesAnIdenticalRun()
    {
        var config = ScenarioBuilder.Scenario(seed: 4711);

        var (_, firstLog) = ScenarioBuilder.Run(config);
        var (_, secondLog) = ScenarioBuilder.Run(config);

        Assert.Equal(firstLog.Events.Count, secondLog.Events.Count);
        for (var i = 0; i < firstLog.Events.Count; i++)
        {
            Assert.Equal(firstLog.Events[i].Timestamp, secondLog.Events[i].Timestamp);
            Assert.Equal(firstLog.Events[i].Type, secondLog.Events[i].Type);
            Assert.Equal(firstLog.Events[i].PatientId, secondLog.Events[i].PatientId);
        }
    }

    [Fact]
    public void DifferentSeedsProduceDifferentRuns()
    {
        var (_, first) = ScenarioBuilder.Run(ScenarioBuilder.Scenario(seed: 1));
        var (_, second) = ScenarioBuilder.Run(ScenarioBuilder.Scenario(seed: 2));

        var firstTimes = first.Events.Select(e => e.Timestamp).ToArray();
        var secondTimes = second.Events.Select(e => e.Timestamp).ToArray();

        Assert.NotEqual(firstTimes, secondTimes);
    }

    private static double WaitFor(ScenarioConfig config)
    {
        var (_, log) = ScenarioBuilder.Run(config);
        var metrics = new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);

        return metrics.AverageWaitTime ?? 0.0;
    }
}
