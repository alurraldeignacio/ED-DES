namespace UrgentCare.Core.Configuration;

/// <summary>
/// External scenario configuration, mirroring contracts/simulation-config.md.
/// All simulated durations are in minutes; the one exception is named explicitly
/// (<see cref="ArrivalModelConfig.BaseRatePerHour"/>).
/// </summary>
public sealed record ScenarioConfig
{
    /// <summary>Semantic version of the configuration contract this file was written against.</summary>
    public string SchemaVersion { get; init; } = string.Empty;

    public string ScenarioId { get; init; } = string.Empty;

    public int Seed { get; init; }

    /// <summary>Length of the run in minutes of simulated time (10080 = 7 days).</summary>
    public double TimeHorizon { get; init; }

    public ArrivalModelConfig ArrivalModel { get; init; } = new();

    public DepartmentConfig Department { get; init; } = new();

    public ResourceProfileConfig ResourceProfile { get; init; } = new();

    public PatientModelConfig PatientModel { get; init; } = new();

    public MetricsConfig Metrics { get; init; } = new();
}

/// <summary>Arrival intensity and its non-stationary profile.</summary>
public sealed record ArrivalModelConfig
{
    /// <summary>Expected arrivals per hour; the engine converts to a per-minute rate.</summary>
    public double BaseRatePerHour { get; init; }

    public bool HourlyVariation { get; init; }

    public bool DailyVariation { get; init; }

    public bool SeasonalVariation { get; init; }

    public DistributionConfig InterarrivalDistribution { get; init; } = new();
}

/// <summary>Staffing, rooms, and the active queue policy.</summary>
public sealed record DepartmentConfig
{
    public string Name { get; init; } = string.Empty;

    public int ResourceCount { get; init; }

    public string ResourceType { get; init; } = "Nurse";

    public string QueuePolicy { get; init; } = "UrgencyFirst";

    public int RoomCount { get; init; }

    /// <summary>
    /// Patients whose urgency is at or below this value bypass the room constraint,
    /// as a real department holds or clears a bay for a critical arrival. Zero disables the bypass.
    /// </summary>
    public int RoomBypassUrgency { get; init; }
}

/// <summary>Parameters driving time-dependent care-resource productivity.</summary>
public sealed record ResourceProfileConfig
{
    public double BaseEfficiency { get; init; } = 1.0;

    /// <summary>Skill rating from 0.0 to 1.0.</summary>
    public double Experience { get; init; }

    public double ExperienceBonus { get; init; }

    public double ShiftLengthMinutes { get; init; } = 480.0;

    public double FatigueRatePerMinute { get; init; }

    public double MinEfficiency { get; init; } = 0.5;

    public double MaxEfficiency { get; init; } = 1.5;
}

/// <summary>Distributions from which patient attributes are sampled.</summary>
public sealed record PatientModelConfig
{
    public DistributionConfig UrgencyDistribution { get; init; } = new();

    public DistributionConfig AgeDistribution { get; init; } = new();

    public DistributionConfig ServiceDurationDistribution { get; init; } = new();

    public DistributionConfig AbandonmentThresholdDistribution { get; init; } = new();
}

/// <summary>
/// A distribution declared as a family discriminator plus that family's parameters.
/// Unused parameters stay null so a missing required parameter is detectable.
/// </summary>
public sealed record DistributionConfig
{
    public string Family { get; init; } = string.Empty;

    // categorical
    public IReadOnlyList<double>? Categories { get; init; }
    public IReadOnlyList<double>? Weights { get; init; }

    // normal (Min and Max optionally truncate)
    public double? Mean { get; init; }
    public double? StandardDeviation { get; init; }

    // lognormal (parameters of the underlying normal)
    public double? Mu { get; init; }
    public double? Sigma { get; init; }

    // triangular, and truncation bounds for normal
    public double? Min { get; init; }
    public double? Mode { get; init; }
    public double? Max { get; init; }

    // exponential
    public double? Rate { get; init; }
}

/// <summary>
/// Which outcomes the run reports. A disabled metric is reported as null rather than omitted,
/// so the export keeps the same columns across runs. The four flags below are required by
/// SC-002 and are rejected by ScenarioConfigValidator if switched off.
/// </summary>
public sealed record MetricsConfig
{
    public bool ReportAverageWait { get; init; } = true;

    public bool ReportP95Wait { get; init; } = true;

    public bool ReportAbandonmentRate { get; init; } = true;

    public bool ReportUtilization { get; init; } = true;
}
