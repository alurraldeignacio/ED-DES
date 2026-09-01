using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Queues;

/// <summary>
/// Serves the most acute patient first. Urgency follows the ESI scale, where 1 is
/// resuscitation and 5 is non-urgent, so ordering is by <em>ascending</em> urgency value.
/// Ties break by arrival time, which keeps the policy fair within an acuity band.
/// </summary>
public sealed class UrgencyFirstPolicy : IQueuePolicy
{
    public string Name => "UrgencyFirst";

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

            if (best is null || IsHigherPriority(patient, best))
            {
                best = patient;
            }
        }

        return best;
    }

    /// <summary>
    /// Lower urgency value wins; then earlier arrival; then id, so the choice stays stable
    /// even for patients who are identical on both counts.
    /// </summary>
    private static bool IsHigherPriority(Patient candidate, Patient incumbent)
    {
        if (candidate.Urgency != incumbent.Urgency)
        {
            return candidate.Urgency < incumbent.Urgency;
        }

        if (candidate.ArrivalTime != incumbent.ArrivalTime)
        {
            return candidate.ArrivalTime < incumbent.ArrivalTime;
        }

        return string.CompareOrdinal(candidate.Id, incumbent.Id) < 0;
    }
}
