using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Queues;

/// <summary>
/// Serves the most acute patient first, and lets accumulated waiting time promote a patient
/// within the non-critical band. Pure acuity ordering can starve a non-urgent patient
/// indefinitely whenever sicker arrivals keep coming; real departments escalate someone who has
/// waited too long, and this policy models that (FR-005).
/// </summary>
/// <remarks>
/// <para>
/// The queue is two tiers, and the split is the clinically important part. Patients at or below
/// <c>protectedUrgency</c> — ESI 1 (resuscitation) and 2 (emergent) by default — are ordered on
/// acuity alone and are always served before anyone else, however long the others have waited.
/// No amount of waiting promotes a patient past a resuscitation case, because in a real
/// department that patient cannot wait at all.
/// </para>
/// <para>
/// Escalation applies only within the remaining band (ESI 3 to 5), where the priority score is
/// <c>urgency - waitingTime / escalationMinutes</c> and the lowest score is served first. One
/// full escalation interval of waiting is worth one step on the ESI scale, so at 30 minutes a
/// patient who has waited an hour is treated as though they were two levels more acute — but
/// still never ahead of the protected tier.
/// </para>
/// <para>
/// The protection is structural rather than arithmetic: the two tiers are selected separately,
/// so no combination of escalation interval and waiting time can produce a score that jumps a
/// critical patient.
/// </para>
/// </remarks>
public sealed class UrgencyPlusWaitPolicy : IQueuePolicy
{
    private readonly double _escalationMinutes;
    private readonly int _protectedUrgency;

    /// <param name="escalationMinutes">
    /// Minutes of waiting worth one step of acuity, within the escalating band. Lower values
    /// make the policy more wait-driven; a very large value degenerates to plain UrgencyFirst.
    /// </param>
    /// <param name="protectedUrgency">
    /// Patients at or below this urgency are never overtaken by a waiting patient. Defaults to 2,
    /// covering ESI 1 and 2. Zero disables the protection, matching the convention used by
    /// <c>roomBypassUrgency</c>.
    /// </param>
    public UrgencyPlusWaitPolicy(double escalationMinutes = 30.0, int protectedUrgency = 2)
    {
        if (escalationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(escalationMinutes), escalationMinutes,
                "escalationMinutes must be greater than zero, otherwise waiting time would "
                + "dominate acuity without bound.");
        }

        if (protectedUrgency is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(protectedUrgency), protectedUrgency,
                "protectedUrgency must be an ESI value from 0 to 5, where 0 disables protection.");
        }

        _escalationMinutes = escalationMinutes;
        _protectedUrgency = protectedUrgency;
    }

    public string Name => "UrgencyPlusWait";

    public Patient? SelectNext(IReadOnlyList<Patient> waiting, double currentTime)
    {
        ArgumentNullException.ThrowIfNull(waiting);

        Patient? bestProtected = null;
        Patient? bestEscalating = null;
        var bestScore = double.MaxValue;

        foreach (var patient in waiting)
        {
            if (patient.State != PatientState.Waiting)
            {
                continue;
            }

            if (IsProtected(patient))
            {
                if (bestProtected is null || IsMoreAcute(patient, bestProtected))
                {
                    bestProtected = patient;
                }

                continue;
            }

            var score = PriorityScore(patient, currentTime);

            if (bestEscalating is null
                || score < bestScore
                || (score == bestScore && IsEarlier(patient, bestEscalating)))
            {
                bestEscalating = patient;
                bestScore = score;
            }
        }

        // The protected tier is drained before anyone in the escalating band is considered.
        return bestProtected ?? bestEscalating;
    }

    private bool IsProtected(Patient patient) =>
        _protectedUrgency > 0 && patient.Urgency <= _protectedUrgency;

    private double PriorityScore(Patient patient, double currentTime)
    {
        // A negative wait would mean the clock ran backwards; clamp so a stale currentTime
        // cannot demote a patient below their own acuity.
        var waited = Math.Max(0.0, currentTime - patient.ArrivalTime);
        return patient.Urgency - (waited / _escalationMinutes);
    }

    /// <summary>Ordering inside the protected tier: acuity, then arrival, then id.</summary>
    private static bool IsMoreAcute(Patient candidate, Patient incumbent)
    {
        if (candidate.Urgency != incumbent.Urgency)
        {
            return candidate.Urgency < incumbent.Urgency;
        }

        return IsEarlier(candidate, incumbent);
    }

    /// <summary>Tie-break for equal scores: earlier arrival, then id, so the choice is stable.</summary>
    private static bool IsEarlier(Patient candidate, Patient incumbent)
    {
        if (candidate.ArrivalTime != incumbent.ArrivalTime)
        {
            return candidate.ArrivalTime < incumbent.ArrivalTime;
        }

        return string.CompareOrdinal(candidate.Id, incumbent.Id) < 0;
    }
}
