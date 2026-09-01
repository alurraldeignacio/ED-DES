using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Queues;

/// <summary>
/// Decides which waiting patient is treated next. Policies read patients but never modify
/// them, so a new policy can be introduced without touching the Patient entity (FR-005).
/// </summary>
public interface IQueuePolicy
{
    /// <summary>Name as it appears in scenario configuration.</summary>
    string Name { get; }

    /// <summary>
    /// Returns the next patient to treat, or null when nobody is waiting. Implementations must
    /// be deterministic: the same waiting room at the same instant always yields the same choice,
    /// or seeded runs stop being reproducible.
    /// </summary>
    Patient? SelectNext(IReadOnlyList<Patient> waiting, double currentTime);
}
