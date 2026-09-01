using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Generation;

/// <summary>
/// Produces arrival timing and patient attributes from configured distributions. Sampling lives
/// here rather than inside Patient, so distributions can change without touching the entity (FR-006).
/// </summary>
public interface IPatientGenerator
{
    /// <summary>Minutes until the next arrival, measured from <paramref name="currentTime"/>.</summary>
    double NextInterarrivalTime(double currentTime);

    /// <summary>Creates the patient arriving at this moment, with every attribute sampled.</summary>
    Patient GeneratePatient(string id, double arrivalTime);
}
