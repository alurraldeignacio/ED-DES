namespace UrgentCare.Core.Domain;

/// <summary>
/// Lifecycle of a patient inside the department. Completed and Abandoned are terminal.
/// </summary>
public enum PatientState
{
    Waiting,
    InTreatment,
    Completed,
    Abandoned
}

/// <summary>
/// Availability of a care resource. A resource treats at most one patient at a time.
/// </summary>
public enum ResourceState
{
    Available,
    Busy,
    Offline
}

/// <summary>
/// Kind of care resource staffing the department.
/// </summary>
public enum CareResourceType
{
    Nurse,
    Doctor,
    Intake
}

/// <summary>
/// Lifecycle of a simulation run.
/// </summary>
public enum SimulationStatus
{
    Idle,
    Running,
    Paused,
    Complete
}
