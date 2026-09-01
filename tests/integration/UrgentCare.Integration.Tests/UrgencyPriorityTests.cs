using UrgentCare.Core.Events;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// spec.md AS2: a more acute arrival is treated before less acute patients already waiting.
/// Urgency follows the ESI scale, so "more acute" means a lower urgency value.
/// </summary>
public class UrgencyPriorityTests
{
    [Fact]
    public void MoreAcutePatientsAreTreatedBeforeLessAcuteOnesAlreadyWaiting()
    {
        // One resource and heavy demand guarantee a real queue, so ordering actually matters.
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 12.0,
            timeHorizon: 480.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (engine, log) = ScenarioBuilder.Run(config);

        var arrivalsByPatient = log.Events
            .Where(e => e.Type == SimulationEventType.PatientArrived && e.PatientId is not null)
            .ToDictionary(e => e.PatientId!, e => e.Timestamp);

        var treatmentOrder = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted && e.PatientId is not null)
            .ToList();

        var overtakes = 0;

        for (var i = 0; i < treatmentOrder.Count; i++)
        {
            var treated = treatmentOrder[i];

            // Anyone who had already arrived when this patient was chosen but was treated later.
            var passedOver = treatmentOrder
                .Skip(i + 1)
                .Where(later => arrivalsByPatient[later.PatientId!] <= treated.Timestamp)
                .ToList();

            foreach (var later in passedOver)
            {
                // A patient may only be passed over by someone at least as acute.
                Assert.True(
                    treated.Urgency <= later.Urgency,
                    $"Patient {treated.PatientId} (urgency {treated.Urgency}) was treated before " +
                    $"{later.PatientId} (urgency {later.Urgency}), which inverts ESI priority.");

                if (treated.Urgency < later.Urgency &&
                    arrivalsByPatient[later.PatientId!] < arrivalsByPatient[treated.PatientId!])
                {
                    overtakes++;
                }
            }
        }

        // The scenario must actually exercise the rule, not pass vacuously on an empty queue.
        Assert.True(overtakes > 0, "No more-acute patient ever overtook a less acute one.");
    }

    [Fact]
    public void AllPatientsAtOneAcuityAreServedInArrivalOrder()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 10.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0),
            urgency: ScenarioBuilder.Fixed(3.0));

        var (_, log) = ScenarioBuilder.Run(config);

        var arrivals = log.Events
            .Where(e => e.Type == SimulationEventType.PatientArrived && e.PatientId is not null)
            .ToDictionary(e => e.PatientId!, e => e.Timestamp);

        var starts = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted && e.PatientId is not null)
            .Select(e => arrivals[e.PatientId!])
            .ToList();

        // With a single acuity band, FIFO by arrival is the expected order.
        Assert.Equal(starts.OrderBy(t => t), starts);
    }

    [Fact]
    public void TheMostAcutePatientsAreNeverTheLongestWaiters()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 1,
            baseRatePerHour: 12.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (engine, _) = ScenarioBuilder.Run(config);

        var treated = engine.Patients.Where(p => p.WaitingTime is not null).ToList();
        Assert.NotEmpty(treated);

        var mostAcute = treated.Where(p => p.Urgency == 1).ToList();
        var leastAcute = treated.Where(p => p.Urgency == 5).ToList();

        if (mostAcute.Count > 0 && leastAcute.Count > 0)
        {
            Assert.True(
                mostAcute.Average(p => p.WaitingTime!.Value) <
                leastAcute.Average(p => p.WaitingTime!.Value),
                "Urgency 1 patients should wait less on average than urgency 5 patients.");
        }
    }
}
