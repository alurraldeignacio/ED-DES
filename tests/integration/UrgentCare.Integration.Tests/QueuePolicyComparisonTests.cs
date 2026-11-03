using UrgentCare.Core.Events;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// US2 AS2: switching the queue policy changes who gets treated first, with everything else
/// held constant. The seed is fixed across both runs, so any difference in treatment order is
/// attributable to the policy and not to a different sequence of patients.
/// </summary>
public class QueuePolicyComparisonTests
{
    /// <summary>
    /// One resource against steady demand, so a real queue forms. Without contention every
    /// policy behaves identically and the comparison proves nothing.
    /// </summary>
    private static (IReadOnlyList<string> Order, IReadOnlyList<int> Urgencies) TreatmentOrderUnder(
        string queuePolicy)
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            roomCount: 10,
            baseRatePerHour: 8.0,
            timeHorizon: 1440.0,
            seed: 20260902,
            queuePolicy: queuePolicy);

        var (_, log) = ScenarioBuilder.Run(config);

        var started = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted)
            .ToList();

        return (
            started.Select(e => e.PatientId!).ToList(),
            started.Select(e => e.Urgency!.Value).ToList());
    }

    [Fact]
    public void SwitchingFromFifoToUrgencyFirst_ChangesTheTreatmentOrder()
    {
        var fifo = TreatmentOrderUnder("FIFO");
        var urgencyFirst = TreatmentOrderUnder("UrgencyFirst");

        Assert.NotEmpty(fifo.Order);
        Assert.NotEmpty(urgencyFirst.Order);
        Assert.NotEqual(fifo.Order, urgencyFirst.Order);
    }

    [Fact]
    public void UnderFifo_PatientsAreTreatedInArrivalOrder()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            roomCount: 10,
            baseRatePerHour: 8.0,
            timeHorizon: 1440.0,
            seed: 20260902,
            queuePolicy: "FIFO");

        var (_, log) = ScenarioBuilder.Run(config);

        var arrivalOrder = log.Events
            .Where(e => e.Type == SimulationEventType.PatientArrived)
            .Select(e => e.PatientId!)
            .ToList();

        var treatmentOrder = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted)
            .Select(e => e.PatientId!)
            .ToList();

        // Treated patients are a subsequence of the arrival order: some patients abandon and are
        // never treated, but nobody overtakes anybody under FIFO.
        var positions = treatmentOrder.Select(id => arrivalOrder.IndexOf(id)).ToList();

        Assert.Equal(positions.OrderBy(p => p), positions);
    }

    [Fact]
    public void UrgencyFirst_TreatsMoreAcutePatientsEarlierThanFifo()
    {
        var fifo = TreatmentOrderUnder("FIFO");
        var urgencyFirst = TreatmentOrderUnder("UrgencyFirst");

        // Over the first quarter of treatments the acuity-aware policy should have taken the
        // sicker patients; averaged urgency is lower because ESI 1 is the most severe.
        var window = Math.Min(fifo.Urgencies.Count, urgencyFirst.Urgencies.Count) / 4;
        Assert.True(window > 0, "Scenario produced too few treatments to compare.");

        var fifoAcuity = fifo.Urgencies.Take(window).Average();
        var urgencyAcuity = urgencyFirst.Urgencies.Take(window).Average();

        Assert.True(
            urgencyAcuity < fifoAcuity,
            $"Expected UrgencyFirst to front-load acute patients, but its mean urgency was "
            + $"{urgencyAcuity:0.00} against FIFO's {fifoAcuity:0.00} (lower is more acute).");
    }

    [Fact]
    public void UrgencyPlusWait_IsAcuityLedButDoesNotStarveTheLeastAcute()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            roomCount: 10,
            baseRatePerHour: 8.0,
            timeHorizon: 1440.0,
            seed: 20260902,
            queuePolicy: "UrgencyPlusWait");

        var (_, log) = ScenarioBuilder.Run(config);

        var treatedUrgencies = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted)
            .Select(e => e.Urgency!.Value)
            .ToList();

        Assert.NotEmpty(treatedUrgencies);

        // The escalation term must let low-acuity patients through eventually (FR-005).
        Assert.Contains(treatedUrgencies, u => u >= 4);
    }

    [Fact]
    public void UnderUrgencyPlusWait_CriticalPatientsStillWaitLessThanNonUrgentOnes()
    {
        // The policy-level invariant is unit-tested; this checks it survives the engine's
        // admissibility loop, which skips room-blocked candidates and re-asks the policy.
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            roomCount: 20,
            baseRatePerHour: 8.0,
            timeHorizon: 2880.0,
            seed: 20260902,
            queuePolicy: "UrgencyPlusWait");

        var (_, log) = ScenarioBuilder.Run(config);

        var arrivedAt = log.Events
            .Where(e => e.Type == SimulationEventType.PatientArrived)
            .ToDictionary(e => e.PatientId!, e => e.Timestamp);

        var waitsByBand = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted)
            .Select(e => (Band: e.Urgency!.Value <= 2 ? "protected" : "escalating",
                          Wait: e.Timestamp - arrivedAt[e.PatientId!]))
            .GroupBy(x => x.Band)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Wait));

        Assert.True(waitsByBand.ContainsKey("protected"), "Scenario produced no ESI 1-2 patients.");
        Assert.True(waitsByBand.ContainsKey("escalating"), "Scenario produced no ESI 3-5 patients.");

        Assert.True(
            waitsByBand["protected"] < waitsByBand["escalating"],
            $"Expected protected patients to wait less, but ESI 1-2 averaged "
            + $"{waitsByBand["protected"]:0.0} min against {waitsByBand["escalating"]:0.0} min "
            + "for ESI 3-5.");
    }

    [Fact]
    public void TheSamePolicyAndSeed_ProducesTheSameOrderTwice()
    {
        var first = TreatmentOrderUnder("UrgencyFirst");
        var second = TreatmentOrderUnder("UrgencyFirst");

        Assert.Equal(first.Order, second.Order);
    }
}
