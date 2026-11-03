namespace UrgentCare.Core.Events;

/// <summary>Kinds of work the engine schedules for a future simulated moment.</summary>
public enum ScheduledEventType
{
    /// <summary>A patient arrives at the department.</summary>
    Arrival,

    /// <summary>A treatment in progress finishes.</summary>
    TreatmentCompletion,

    /// <summary>A waiting patient reaches the limit of their tolerance.</summary>
    AbandonmentCheck
}

/// <summary>
/// One entry in the future event list.
/// </summary>
/// <param name="Time">Simulated minute at which the event fires.</param>
/// <param name="Type">What should happen.</param>
/// <param name="Sequence">
/// Insertion order, used to break ties between events sharing a timestamp. Without it the
/// dispatch order of simultaneous events would be arbitrary and runs would stop being reproducible.
/// </param>
/// <param name="PatientId">Patient the event concerns, when applicable.</param>
/// <param name="ResourceId">Care resource the event concerns, when applicable.</param>
public readonly record struct ScheduledEvent(
    double Time,
    ScheduledEventType Type,
    long Sequence,
    string? PatientId = null,
    string? ResourceId = null);
