using UrgentCare.Core.Configuration;

namespace UrgentCare.Core.Domain;

/// <summary>
/// A clinician or treatment resource. A resource serves at most one patient at a time;
/// concurrency is expressed by the number of resources, not by a per-resource capacity.
/// </summary>
public sealed class CareResource
{
    private readonly ResourceProfileConfig _profile;

    public CareResource(string id, CareResourceType type, double shiftStart, ResourceProfileConfig profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Care resource id must be present.", nameof(id));
        }

        if (profile.BaseEfficiency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile), profile.BaseEfficiency, "baseEfficiency must be greater than zero.");
        }

        if (profile.Experience is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile), profile.Experience, "experience must be between 0.0 and 1.0.");
        }

        if (profile.MinEfficiency <= 0 || profile.MaxEfficiency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile), "minEfficiency and maxEfficiency must be greater than zero.");
        }

        if (profile.MinEfficiency > profile.MaxEfficiency)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile), "minEfficiency must not exceed maxEfficiency.");
        }

        if (profile.ShiftLengthMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile), profile.ShiftLengthMinutes, "shiftLengthMinutes must be greater than zero.");
        }

        if (profile.FatigueRatePerMinute < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile), profile.FatigueRatePerMinute, "fatigueRatePerMinute must be non-negative.");
        }

        Id = id;
        Type = type;
        ShiftStart = shiftStart;
        _profile = profile;
        State = ResourceState.Available;
    }

    public string Id { get; }

    public CareResourceType Type { get; }

    /// <summary>Skill rating from 0.0 to 1.0.</summary>
    public double Experience => _profile.Experience;

    /// <summary>Simulated minute at which this resource began duty.</summary>
    public double ShiftStart { get; }

    public ResourceState State { get; private set; }

    /// <summary>The patient under treatment, or null when the resource is available.</summary>
    public Patient? AssignedPatient { get; private set; }

    /// <summary>
    /// Performance degradation at this moment, from 0.0 (fresh) to 1.0 (spent). Fatigue accrues
    /// across the current shift and resets when the next one begins, so a long run does not pin
    /// every resource at the efficiency floor forever.
    /// </summary>
    public double FatigueAt(double currentTime)
    {
        var elapsed = currentTime - ShiftStart;
        if (elapsed <= 0)
        {
            return 0.0;
        }

        var intoShift = elapsed % _profile.ShiftLengthMinutes;

        // An exact multiple of the shift length is the end of that shift, not the start of the next.
        if (intoShift == 0)
        {
            intoShift = _profile.ShiftLengthMinutes;
        }

        return Math.Min(1.0, _profile.FatigueRatePerMinute * intoShift);
    }

    /// <summary>
    /// Productivity multiplier at this moment. Above 1.0 shortens treatment, below 1.0 lengthens it.
    /// </summary>
    public double EfficiencyAt(double currentTime)
    {
        var raw = _profile.BaseEfficiency
            * (1.0 + (_profile.ExperienceBonus * _profile.Experience))
            * (1.0 - FatigueAt(currentTime));

        return Math.Clamp(raw, _profile.MinEfficiency, _profile.MaxEfficiency);
    }

    /// <summary>
    /// How long this resource actually takes over the patient's sampled service duration,
    /// given its current efficiency (FR-004).
    /// </summary>
    public double EffectiveServiceDuration(Patient patient, double currentTime)
    {
        ArgumentNullException.ThrowIfNull(patient);

        return patient.ServiceDuration / EfficiencyAt(currentTime);
    }

    public void Assign(Patient patient, double currentTime)
    {
        ArgumentNullException.ThrowIfNull(patient);

        if (State != ResourceState.Available)
        {
            throw new InvalidOperationException(
                $"Care resource '{Id}' is {State} and cannot take patient '{patient.Id}'.");
        }

        AssignedPatient = patient;
        State = ResourceState.Busy;
    }

    public void Release(double currentTime)
    {
        if (State != ResourceState.Busy || AssignedPatient is null)
        {
            throw new InvalidOperationException(
                $"Care resource '{Id}' has no assigned patient to release.");
        }

        AssignedPatient = null;
        State = ResourceState.Available;
    }
}
