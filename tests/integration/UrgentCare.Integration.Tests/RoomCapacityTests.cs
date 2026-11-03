using UrgentCare.Core.Events;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// Rooms cap how many patients can be treated at once, but a patient at or below
/// roomBypassUrgency is admitted even with every room full. That mirrors a real department
/// holding or clearing a resuscitation bay for a critical arrival.
/// </summary>
public class RoomCapacityTests
{
    [Fact]
    public void RoomsCapConcurrentTreatmentsWhenNobodyCanBypass()
    {
        // Six resources but only two rooms, and no bypass: rooms are the binding constraint.
        var config = ScenarioBuilder.Scenario(
            resourceCount: 6,
            roomCount: 2,
            roomBypassUrgency: 0,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (_, log) = ScenarioBuilder.Run(config);

        Assert.True(MaxConcurrentTreatments(log) <= 2);
    }

    [Fact]
    public void MoreRoomsAllowMoreConcurrentTreatments()
    {
        var narrow = ScenarioBuilder.Scenario(
            resourceCount: 6,
            roomCount: 1,
            roomBypassUrgency: 0,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var wide = ScenarioBuilder.Scenario(
            resourceCount: 6,
            roomCount: 4,
            roomBypassUrgency: 0,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (_, narrowLog) = ScenarioBuilder.Run(narrow);
        var (_, wideLog) = ScenarioBuilder.Run(wide);

        Assert.True(MaxConcurrentTreatments(wideLog) > MaxConcurrentTreatments(narrowLog));
    }

    [Fact]
    public void HighAcuityPatientsAreAdmittedWithEveryRoomOccupied()
    {
        // Rooms are scarce, but urgency 1 and 2 bypass them entirely.
        var config = ScenarioBuilder.Scenario(
            resourceCount: 6,
            roomCount: 1,
            roomBypassUrgency: 2,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (_, log) = ScenarioBuilder.Run(config);

        // Concurrency must exceed the room count, which is only possible via the bypass.
        Assert.True(
            MaxConcurrentTreatments(log) > 1,
            "High-acuity patients should be treated concurrently with the single room in use.");

        var bypassing = log.Events
            .Where(e => e.Type == SimulationEventType.TreatmentStarted && e.Urgency <= 2)
            .ToList();

        Assert.NotEmpty(bypassing);
    }

    [Fact]
    public void LowAcuityPatientsStillQueueBehindRoomsWhenABypassExists()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 6,
            roomCount: 1,
            roomBypassUrgency: 2,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (_, log) = ScenarioBuilder.Run(config);

        Assert.True(MaxConcurrentLowAcuityTreatments(log) <= 1);
    }

    [Fact]
    public void ABypassUrgencyOfZeroDisablesTheBypassEntirely()
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 6,
            roomCount: 2,
            roomBypassUrgency: 0,
            baseRatePerHour: 20.0,
            abandonmentThreshold: ScenarioBuilder.Fixed(100000.0));

        var (_, log) = ScenarioBuilder.Run(config);

        Assert.True(MaxConcurrentTreatments(log) <= 2);
    }

    /// <summary>Replays starts and completions to find the peak number of simultaneous treatments.</summary>
    private static int MaxConcurrentTreatments(InMemoryEventLog log) =>
        MaxConcurrent(log, _ => true);

    private static int MaxConcurrentLowAcuityTreatments(InMemoryEventLog log) =>
        MaxConcurrent(log, urgency => urgency > 2);

    private static int MaxConcurrent(InMemoryEventLog log, Func<int, bool> urgencyFilter)
    {
        var urgencyByPatient = new Dictionary<string, int>();
        var current = 0;
        var peak = 0;

        foreach (var e in log.Events)
        {
            if (e.PatientId is null)
            {
                continue;
            }

            switch (e.Type)
            {
                case SimulationEventType.TreatmentStarted when e.Urgency is { } urgency:
                    urgencyByPatient[e.PatientId] = urgency;
                    if (urgencyFilter(urgency))
                    {
                        current++;
                        peak = Math.Max(peak, current);
                    }

                    break;

                case SimulationEventType.TreatmentCompleted:
                    if (urgencyByPatient.TryGetValue(e.PatientId, out var completedUrgency)
                        && urgencyFilter(completedUrgency))
                    {
                        current--;
                    }

                    break;
            }
        }

        return peak;
    }
}
