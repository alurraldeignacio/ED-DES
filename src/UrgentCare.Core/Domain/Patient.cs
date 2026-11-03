namespace UrgentCare.Core.Domain;

/// <summary>
/// A patient moving through the department. Attributes are sampled by the patient generator
/// and handed in, never drawn inside the entity (FR-006).
/// </summary>
public sealed class Patient
{
    /// <param name="id">Unique patient identifier.</param>
    /// <param name="urgency">Triage acuity on the ESI scale, 1 (most severe) to 5 (least severe).</param>
    /// <param name="age">Patient age in years.</param>
    /// <param name="arrivalTime">Simulated minute at which the patient entered the system.</param>
    /// <param name="serviceDuration">Sampled treatment duration in minutes, before efficiency is applied.</param>
    /// <param name="abandonmentThreshold">Maximum tolerated waiting time in minutes.</param>
    public Patient(
        string id,
        int urgency,
        int age,
        double arrivalTime,
        double serviceDuration,
        double abandonmentThreshold)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Patient id must be present.", nameof(id));
        }

        if (urgency is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(urgency), urgency, "Urgency must be between 1 and 5 on the ESI scale.");
        }

        if (serviceDuration <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serviceDuration), serviceDuration, "Service duration must be greater than zero.");
        }

        if (abandonmentThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(abandonmentThreshold),
                abandonmentThreshold,
                "Abandonment threshold must be non-negative.");
        }

        Id = id;
        Urgency = urgency;
        Age = age;
        ArrivalTime = arrivalTime;
        ServiceDuration = serviceDuration;
        AbandonmentThreshold = abandonmentThreshold;
        State = PatientState.Waiting;
    }

    public string Id { get; }

    /// <summary>Triage acuity, 1 (most severe) to 5 (least severe).</summary>
    public int Urgency { get; }

    public int Age { get; }

    public double ArrivalTime { get; }

    /// <summary>Sampled treatment duration in minutes, before resource efficiency is applied.</summary>
    public double ServiceDuration { get; }

    public double AbandonmentThreshold { get; }

    public PatientState State { get; private set; }

    /// <summary>Minute treatment began, or null while the patient is still waiting.</summary>
    public double? TreatmentStartTime { get; private set; }

    /// <summary>Minute the patient left the system, or null while still present.</summary>
    public double? DepartureTime { get; private set; }

    /// <summary>
    /// Time spent in the waiting area, known once the patient has left it either into
    /// treatment or by abandoning. Null while the patient is still waiting.
    /// </summary>
    public double? WaitingTime => State switch
    {
        PatientState.Waiting => null,
        PatientState.Abandoned => DepartureTime - ArrivalTime,
        _ => TreatmentStartTime - ArrivalTime
    };

    /// <summary>True when a still-waiting patient has exceeded their tolerance at this moment.</summary>
    public bool HasWaitedTooLongAt(double currentTime) =>
        State == PatientState.Waiting && currentTime - ArrivalTime > AbandonmentThreshold;

    public void StartTreatment(double currentTime)
    {
        RequireState(PatientState.Waiting, "start treatment");

        State = PatientState.InTreatment;
        TreatmentStartTime = currentTime;
    }

    public void Complete(double currentTime)
    {
        RequireState(PatientState.InTreatment, "complete treatment");

        State = PatientState.Completed;
        DepartureTime = currentTime;
    }

    public void Abandon(double currentTime)
    {
        RequireState(PatientState.Waiting, "abandon");

        State = PatientState.Abandoned;
        DepartureTime = currentTime;
    }

    private void RequireState(PatientState required, string action)
    {
        if (State != required)
        {
            throw new InvalidOperationException(
                $"Patient '{Id}' cannot {action} from state {State}; {required} is required.");
        }
    }
}
