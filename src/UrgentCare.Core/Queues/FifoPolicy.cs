using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Queues;

/// <summary>
/// Serves patients in the order they arrived, ignoring acuity entirely. This is the baseline
/// a triage policy is measured against: it is what a waiting room does with no clinical
/// prioritisation at all, so the difference it makes is the value triage adds (FR-005).
/// </summary>
public sealed class FifoPolicy : IQueuePolicy
{
    public string Name => "FIFO";

    public Patient? SelectNext(IReadOnlyList<Patient> waiting, double currentTime)
    {
        ArgumentNullException.ThrowIfNull(waiting);

        Patient? best = null;

        foreach (var patient in waiting)
        {
            if (patient.State != PatientState.Waiting)
            {
                continue;
            }

            if (best is null || IsEarlier(patient, best))
            {
                best = patient;
            }
        }

        return best;
    }

    /// <summary>
    /// Earlier arrival wins; then id, so two patients who arrived at the same instant are
    /// still ordered the same way on every run.
    /// </summary>
    private static bool IsEarlier(Patient candidate, Patient incumbent)
    {
        if (candidate.ArrivalTime != incumbent.ArrivalTime)
        {
            return candidate.ArrivalTime < incumbent.ArrivalTime;
        }

        return string.CompareOrdinal(candidate.Id, incumbent.Id) < 0;
    }
}
