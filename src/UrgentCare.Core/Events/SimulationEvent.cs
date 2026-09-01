namespace UrgentCare.Core.Events;

/// <summary>
/// Kinds of event the engine emits. Every metric is derived from this stream
/// rather than from ad hoc counters (Constitution Principle VI).
/// </summary>
public enum SimulationEventType
{
    PatientArrived,
    TreatmentStarted,
    TreatmentCompleted,
    PatientAbandoned,
    ResourceStateChanged
}

/// <summary>
/// One structured telemetry record. The runId, scenarioId and replicationIndex correlation
/// keys keep events distinguishable across batched scenarios and repeated replications.
/// </summary>
/// <param name="RunId">Identifier unique to a single engine run.</param>
/// <param name="ScenarioId">Scenario that produced the event.</param>
/// <param name="ReplicationIndex">Zero-based replication number within a batch.</param>
/// <param name="Timestamp">Simulated time in minutes.</param>
/// <param name="Type">What happened.</param>
/// <param name="PatientId">Patient involved, when the event concerns one.</param>
/// <param name="ResourceId">Care resource involved, when the event concerns one.</param>
/// <param name="Urgency">Triage acuity of the patient involved, 1 (most severe) to 5.</param>
/// <param name="QueueLength">Waiting-area length at the moment the event was emitted.</param>
public readonly record struct SimulationEvent(
    Guid RunId,
    string ScenarioId,
    int ReplicationIndex,
    double Timestamp,
    SimulationEventType Type,
    string? PatientId,
    string? ResourceId,
    int? Urgency,
    int QueueLength);
