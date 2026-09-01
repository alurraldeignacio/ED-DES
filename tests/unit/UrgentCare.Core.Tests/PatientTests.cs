using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Tests;

/// <summary>
/// Patient validation and the terminal-state rules from data-model.md. Completion and
/// abandonment are terminal, so a patient can never be resurrected into treatment.
/// </summary>
public class PatientTests
{
    private static Patient NewPatient(int urgency = 3, double arrivalTime = 0.0) =>
        new("P1", urgency, age: 42, arrivalTime, serviceDuration: 18.0, abandonmentThreshold: 90.0);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void UrgencyWithinOneToFive_IsAccepted(int urgency)
    {
        var patient = NewPatient(urgency);

        Assert.Equal(urgency, patient.Urgency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void UrgencyOutsideOneToFive_IsRejected(int urgency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Patient("P1", urgency, 42, 0.0, 18.0, 90.0));
    }

    [Fact]
    public void NegativeAbandonmentThreshold_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Patient("P1", 3, 42, 0.0, 18.0, abandonmentThreshold: -1.0));
    }

    [Fact]
    public void ZeroAbandonmentThreshold_IsAccepted()
    {
        // A threshold of zero is a patient who will not wait at all, which is valid.
        var patient = new Patient("P1", 3, 42, 0.0, 18.0, abandonmentThreshold: 0.0);

        Assert.Equal(0.0, patient.AbandonmentThreshold);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void NonPositiveServiceDuration_IsRejected(double serviceDuration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Patient("P1", 3, 42, 0.0, serviceDuration, 90.0));
    }

    [Fact]
    public void MissingId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Patient("", 3, 42, 0.0, 18.0, 90.0));
    }

    [Fact]
    public void NewPatient_StartsWaiting()
    {
        var patient = NewPatient();

        Assert.Equal(PatientState.Waiting, patient.State);
        Assert.Null(patient.TreatmentStartTime);
        Assert.Null(patient.DepartureTime);
    }

    [Fact]
    public void StartTreatment_MovesToInTreatmentAndRecordsWaitingTime()
    {
        var patient = NewPatient(arrivalTime: 10.0);

        patient.StartTreatment(35.0);

        Assert.Equal(PatientState.InTreatment, patient.State);
        Assert.Equal(35.0, patient.TreatmentStartTime);
        Assert.Equal(25.0, patient.WaitingTime);
    }

    [Fact]
    public void Complete_MovesToCompletedAndRecordsDeparture()
    {
        var patient = NewPatient(arrivalTime: 10.0);
        patient.StartTreatment(35.0);

        patient.Complete(53.0);

        Assert.Equal(PatientState.Completed, patient.State);
        Assert.Equal(53.0, patient.DepartureTime);
        Assert.Equal(25.0, patient.WaitingTime);
    }

    [Fact]
    public void Abandon_MovesToAbandonedAndCountsTheFullWait()
    {
        var patient = NewPatient(arrivalTime: 10.0);

        patient.Abandon(105.0);

        Assert.Equal(PatientState.Abandoned, patient.State);
        Assert.Equal(105.0, patient.DepartureTime);
        Assert.Equal(95.0, patient.WaitingTime);
    }

    [Fact]
    public void CompletedIsTerminal_FurtherTransitionsAreRejected()
    {
        var patient = NewPatient();
        patient.StartTreatment(5.0);
        patient.Complete(25.0);

        Assert.Throws<InvalidOperationException>(() => patient.StartTreatment(30.0));
        Assert.Throws<InvalidOperationException>(() => patient.Abandon(30.0));
        Assert.Throws<InvalidOperationException>(() => patient.Complete(30.0));
    }

    [Fact]
    public void AbandonedIsTerminal_FurtherTransitionsAreRejected()
    {
        var patient = NewPatient();
        patient.Abandon(95.0);

        Assert.Throws<InvalidOperationException>(() => patient.StartTreatment(100.0));
        Assert.Throws<InvalidOperationException>(() => patient.Complete(100.0));
        Assert.Throws<InvalidOperationException>(() => patient.Abandon(100.0));
    }

    [Fact]
    public void CompletingWithoutTreatment_IsRejected()
    {
        var patient = NewPatient();

        Assert.Throws<InvalidOperationException>(() => patient.Complete(20.0));
    }

    [Fact]
    public void AbandoningDuringTreatment_IsRejected()
    {
        // Abandonment means leaving the queue; a patient already being treated cannot abandon.
        var patient = NewPatient();
        patient.StartTreatment(5.0);

        Assert.Throws<InvalidOperationException>(() => patient.Abandon(10.0));
    }

    [Fact]
    public void HasWaitedTooLong_IsFalseBeforeTheThresholdIsExceeded()
    {
        var patient = NewPatient(arrivalTime: 0.0);

        Assert.False(patient.HasWaitedTooLongAt(89.0));
        Assert.False(patient.HasWaitedTooLongAt(90.0));
    }

    [Fact]
    public void HasWaitedTooLong_IsTrueOnceTheThresholdIsExceeded()
    {
        var patient = NewPatient(arrivalTime: 0.0);

        Assert.True(patient.HasWaitedTooLongAt(90.1));
    }

    [Fact]
    public void HasWaitedTooLong_IsFalseOnceTreatmentStarted()
    {
        var patient = NewPatient(arrivalTime: 0.0);
        patient.StartTreatment(10.0);

        Assert.False(patient.HasWaitedTooLongAt(500.0));
    }
}
