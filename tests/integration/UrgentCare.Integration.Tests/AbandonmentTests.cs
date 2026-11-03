using UrgentCare.Core.Domain;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// spec.md AS3: a patient who waits past their tolerance leaves the queue and is counted as
/// abandoned. This is the LWBS ("left without being seen") behavior of a real department.
/// </summary>
public class AbandonmentTests
{
    [Fact]
    public void PatientsWhoWaitPastTheirThresholdAbandon()
    {
        // Deliberately overloaded: one resource, heavy arrivals, and a short fuse.
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 20.0,
            timeHorizon: 480.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(20.0));

        var (engine, log) = ScenarioBuilder.Run(config);

        Assert.Contains(log.Events, e => e.Type == SimulationEventType.PatientAbandoned);
        Assert.Contains(engine.Patients, p => p.State == PatientState.Abandoned);
    }

    [Fact]
    public void AnAbandoningPatientWaitedAtLeastTheirThreshold()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(20.0));

        var (engine, _) = ScenarioBuilder.Run(config);

        var abandoned = engine.Patients.Where(p => p.State == PatientState.Abandoned).ToList();
        Assert.NotEmpty(abandoned);

        foreach (var patient in abandoned)
        {
            Assert.NotNull(patient.WaitingTime);
            Assert.True(
                patient.WaitingTime >= patient.AbandonmentThreshold - 1e-9,
                $"Patient {patient.Id} abandoned after {patient.WaitingTime} minutes but " +
                $"tolerated {patient.AbandonmentThreshold}.");
        }
    }

    [Fact]
    public void AbandonedPatientsAreRemovedFromTheWaitingArea()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(20.0));

        var (engine, _) = ScenarioBuilder.Run(config);

        Assert.DoesNotContain(
            engine.Department.WaitingArea,
            p => p.State == PatientState.Abandoned);
    }

    [Fact]
    public void AbandonedPatientsAreNeverTreated()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(20.0));

        var (engine, log) = ScenarioBuilder.Run(config);

        var abandonedIds = engine.Patients
            .Where(p => p.State == PatientState.Abandoned)
            .Select(p => p.Id)
            .ToHashSet();

        var treatedIds = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted && e.PatientId is not null)
            .Select(e => e.PatientId!)
            .ToHashSet();

        Assert.Empty(abandonedIds.Intersect(treatedIds));
    }

    [Fact]
    public void AbandonmentIsCountedInTheMetrics()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(20.0));

        var (engine, log) = ScenarioBuilder.Run(config);
        var metrics = new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);

        Assert.Equal(engine.Patients.Count(p => p.State == PatientState.Abandoned), metrics.Abandoned);
        Assert.NotNull(metrics.AbandonmentRate);
        Assert.InRange(metrics.AbandonmentRate.Value, 0.0, 1.0);
        Assert.True(metrics.AbandonmentRate > 0);
    }

    [Fact]
    public void PatientsWithAmpleToleranceDoNotAbandon()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 3,
            baseRatePerHour: 4.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (engine, log) = ScenarioBuilder.Run(config);

        Assert.DoesNotContain(log.Events, e => e.Type == SimulationEventType.PatientAbandoned);
        Assert.DoesNotContain(engine.Patients, p => p.State == PatientState.Abandoned);
    }

    [Fact]
    public void ShorterToleranceProducesMoreAbandonment()
    {
        var patient = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(90.0));

        var impatient = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(15.0));

        Assert.True(AbandonedCount(impatient) > AbandonedCount(patient));
    }

    private static int AbandonedCount(UrgentCare.Core.Configuration.ScenarioConfig config)
    {
        var (engine, _) = ScenarioBuilder.Run(config);
        return engine.Patients.Count(p => p.State == PatientState.Abandoned);
    }
}
