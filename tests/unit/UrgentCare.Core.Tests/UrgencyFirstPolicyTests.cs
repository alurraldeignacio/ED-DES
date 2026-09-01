using UrgentCare.Core.Domain;
using UrgentCare.Core.Queues;

namespace UrgentCare.Core.Tests;

/// <summary>
/// UrgencyFirstPolicy orders by ascending urgency value: the ESI scale puts 1 at
/// resuscitation and 5 at non-urgent, so a lower number is served first. Ties break by
/// arrival time, which keeps the policy fair within an acuity band.
/// </summary>
public class UrgencyFirstPolicyTests
{
    private readonly UrgencyFirstPolicy _policy = new();

    private static Patient PatientWith(string id, int urgency, double arrivalTime) =>
        new(id, urgency, 42, arrivalTime, 18.0, 90.0);

    [Fact]
    public void PolicyExposesItsName()
    {
        Assert.Equal("UrgencyFirst", _policy.Name);
    }

    [Fact]
    public void MostAcutePatientIsServedFirst()
    {
        var waiting = new List<Patient>
        {
            PatientWith("P-low", 5, 0.0),
            PatientWith("P-critical", 1, 10.0),
            PatientWith("P-mid", 3, 5.0)
        };

        var next = _policy.SelectNext(waiting, currentTime: 20.0);

        Assert.NotNull(next);
        Assert.Equal("P-critical", next.Id);
    }

    [Fact]
    public void AMoreAcuteLateArrival_OvertakesLessAcutePatientsAlreadyWaiting()
    {
        // spec.md AS2, expressed at the policy level.
        var waiting = new List<Patient>
        {
            PatientWith("P-waiting-ages", 4, 0.0),
            PatientWith("P-just-arrived", 2, 90.0)
        };

        var next = _policy.SelectNext(waiting, currentTime: 90.0);

        Assert.NotNull(next);
        Assert.Equal("P-just-arrived", next.Id);
    }

    [Fact]
    public void EqualUrgency_BreaksTiesByEarliestArrival()
    {
        var waiting = new List<Patient>
        {
            PatientWith("P-second", 3, 50.0),
            PatientWith("P-first", 3, 10.0),
            PatientWith("P-third", 3, 90.0)
        };

        var next = _policy.SelectNext(waiting, currentTime: 100.0);

        Assert.NotNull(next);
        Assert.Equal("P-first", next.Id);
    }

    [Fact]
    public void EmptyQueue_ReturnsNull()
    {
        var next = _policy.SelectNext([], currentTime: 0.0);

        Assert.Null(next);
    }

    [Fact]
    public void SingleWaitingPatient_IsSelected()
    {
        var waiting = new List<Patient> { PatientWith("P-only", 4, 0.0) };

        var next = _policy.SelectNext(waiting, currentTime: 5.0);

        Assert.NotNull(next);
        Assert.Equal("P-only", next.Id);
    }

    [Fact]
    public void PatientsAreServedInStrictAcuityOrder()
    {
        var waiting = new List<Patient>
        {
            PatientWith("P5", 5, 0.0),
            PatientWith("P4", 4, 1.0),
            PatientWith("P3", 3, 2.0),
            PatientWith("P2", 2, 3.0),
            PatientWith("P1", 1, 4.0)
        };

        var served = new List<string>();
        while (waiting.Count > 0)
        {
            var next = _policy.SelectNext(waiting, currentTime: 10.0);
            Assert.NotNull(next);
            served.Add(next.Id);
            waiting.Remove(next);
        }

        Assert.Equal(["P1", "P2", "P3", "P4", "P5"], served);
    }

    [Fact]
    public void SelectionIsStableForIdenticalInput()
    {
        // Determinism matters: the same waiting room must yield the same choice every time,
        // or seeded runs stop being reproducible.
        var waiting = new List<Patient>
        {
            PatientWith("P-a", 3, 10.0),
            PatientWith("P-b", 3, 10.0)
        };

        var first = _policy.SelectNext(waiting, currentTime: 20.0);
        var second = _policy.SelectNext(waiting, currentTime: 20.0);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void PatientsNoLongerWaiting_AreNotSelected()
    {
        var treated = PatientWith("P-in-treatment", 1, 0.0);
        treated.StartTreatment(5.0);

        var waiting = new List<Patient> { treated, PatientWith("P-waiting", 4, 1.0) };

        var next = _policy.SelectNext(waiting, currentTime: 10.0);

        Assert.NotNull(next);
        Assert.Equal("P-waiting", next.Id);
    }
}
