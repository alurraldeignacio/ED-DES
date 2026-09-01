namespace UrgentCare.Core.Configuration;

/// <summary>Outcome of validating a scenario against the configuration contract.</summary>
/// <param name="IsValid">True when no rule was broken.</param>
/// <param name="Errors">One message per broken rule, in declaration order.</param>
public readonly record struct ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success() => new(true, []);

    public static ValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Enforces the rules in contracts/simulation-config.md. Every rule is checked so a caller
/// sees all problems at once rather than fixing them one run at a time.
/// </summary>
public sealed class ScenarioConfigValidator
{
    /// <summary>Contract major version this build understands.</summary>
    public const int SupportedMajorVersion = 1;

    private static readonly string[] SupportedQueuePolicies =
        ["FIFO", "UrgencyFirst", "UrgencyPlusWait"];

    private static readonly string[] SupportedFamilies =
        ["categorical", "normal", "lognormal", "triangular", "exponential"];

    public ValidationResult Validate(ScenarioConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        ValidateSchemaVersion(config.SchemaVersion, errors);

        if (string.IsNullOrWhiteSpace(config.ScenarioId))
        {
            errors.Add("scenarioId must be present and non-empty.");
        }

        if (config.TimeHorizon <= 0)
        {
            errors.Add($"timeHorizon must be greater than zero, but was {config.TimeHorizon}.");
        }

        ValidateDepartment(config.Department, errors);
        ValidateArrivalModel(config.ArrivalModel, errors);
        ValidateResourceProfile(config.ResourceProfile, errors);
        ValidatePatientModel(config.PatientModel, errors);

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    /// <summary>Validates and throws, for callers that cannot continue with a bad scenario.</summary>
    public void ValidateOrThrow(ScenarioConfig config)
    {
        var result = Validate(config);
        if (!result.IsValid)
        {
            var detail = string.Join(Environment.NewLine, result.Errors.Select(e => "  - " + e));
            throw new ScenarioConfigException(
                "Scenario configuration is invalid:" + Environment.NewLine + detail);
        }
    }

    private static void ValidateSchemaVersion(string schemaVersion, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(schemaVersion))
        {
            errors.Add("schemaVersion must be present.");
            return;
        }

        var majorText = schemaVersion.Split('.')[0];
        if (!int.TryParse(majorText, out var major))
        {
            errors.Add($"schemaVersion '{schemaVersion}' is not semantic versioning.");
            return;
        }

        if (major != SupportedMajorVersion)
        {
            errors.Add(
                $"schemaVersion '{schemaVersion}' has major version {major}, " +
                $"but this build supports major version {SupportedMajorVersion}.");
        }
    }

    private static void ValidateDepartment(DepartmentConfig department, List<string> errors)
    {
        if (!SupportedQueuePolicies.Contains(department.QueuePolicy, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(
                $"queuePolicy '{department.QueuePolicy}' is not supported. " +
                $"Supported policies: {string.Join(", ", SupportedQueuePolicies)}.");
        }

        if (department.ResourceCount < 0)
        {
            errors.Add($"resourceCount must be non-negative, but was {department.ResourceCount}.");
        }

        if (department.RoomCount < 0)
        {
            errors.Add($"roomCount must be non-negative, but was {department.RoomCount}.");
        }

        if (department.RoomBypassUrgency is < 0 or > 5)
        {
            errors.Add(
                $"roomBypassUrgency must be between 0 and 5, but was {department.RoomBypassUrgency}.");
        }
    }

    private static void ValidateArrivalModel(ArrivalModelConfig arrivalModel, List<string> errors)
    {
        if (arrivalModel.BaseRatePerHour <= 0)
        {
            errors.Add(
                $"arrivalModel.baseRatePerHour must be greater than zero, but was {arrivalModel.BaseRatePerHour}.");
        }

        ValidateDistribution(arrivalModel.InterarrivalDistribution, "arrivalModel.interarrivalDistribution", errors);
    }

    private static void ValidateResourceProfile(ResourceProfileConfig profile, List<string> errors)
    {
        if (profile.Experience is < 0.0 or > 1.0)
        {
            errors.Add($"resourceProfile.experience must be between 0.0 and 1.0, but was {profile.Experience}.");
        }

        if (profile.BaseEfficiency <= 0)
        {
            errors.Add($"resourceProfile.baseEfficiency must be greater than zero, but was {profile.BaseEfficiency}.");
        }

        if (profile.MinEfficiency <= 0)
        {
            errors.Add($"resourceProfile.minEfficiency must be greater than zero, but was {profile.MinEfficiency}.");
        }

        if (profile.MaxEfficiency <= 0)
        {
            errors.Add($"resourceProfile.maxEfficiency must be greater than zero, but was {profile.MaxEfficiency}.");
        }

        if (profile.MinEfficiency > profile.MaxEfficiency)
        {
            errors.Add(
                $"resourceProfile.minEfficiency ({profile.MinEfficiency}) must not exceed " +
                $"maxEfficiency ({profile.MaxEfficiency}).");
        }

        if (profile.ShiftLengthMinutes <= 0)
        {
            errors.Add(
                $"resourceProfile.shiftLengthMinutes must be greater than zero, but was {profile.ShiftLengthMinutes}.");
        }

        if (profile.FatigueRatePerMinute < 0)
        {
            errors.Add(
                $"resourceProfile.fatigueRatePerMinute must be non-negative, but was {profile.FatigueRatePerMinute}.");
        }
    }

    private static void ValidatePatientModel(PatientModelConfig patientModel, List<string> errors)
    {
        ValidateDistribution(patientModel.UrgencyDistribution, "patientModel.urgencyDistribution", errors);
        ValidateDistribution(patientModel.AgeDistribution, "patientModel.ageDistribution", errors);
        ValidateDistribution(
            patientModel.ServiceDurationDistribution,
            "patientModel.serviceDurationDistribution",
            errors);
        ValidateDistribution(
            patientModel.AbandonmentThresholdDistribution,
            "patientModel.abandonmentThresholdDistribution",
            errors);
    }

    private static void ValidateDistribution(DistributionConfig distribution, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(distribution.Family))
        {
            errors.Add($"{path}.family must be present.");
            return;
        }

        var family = distribution.Family.ToLowerInvariant();
        if (!SupportedFamilies.Contains(family))
        {
            errors.Add(
                $"{path}.family '{distribution.Family}' is not supported. " +
                $"Supported families: {string.Join(", ", SupportedFamilies)}.");
            return;
        }

        switch (family)
        {
            case "categorical":
                ValidateCategorical(distribution, path, errors);
                break;

            case "normal":
                RequireParameter(distribution.Mean, $"{path}.mean", errors);
                RequireParameter(distribution.StandardDeviation, $"{path}.standardDeviation", errors);
                if (distribution.StandardDeviation is <= 0)
                {
                    errors.Add($"{path}.standardDeviation must be greater than zero.");
                }

                break;

            case "lognormal":
                RequireParameter(distribution.Mu, $"{path}.mu", errors);
                RequireParameter(distribution.Sigma, $"{path}.sigma", errors);
                if (distribution.Sigma is <= 0)
                {
                    errors.Add($"{path}.sigma must be greater than zero.");
                }

                break;

            case "triangular":
                ValidateTriangular(distribution, path, errors);
                break;

            case "exponential":
                // The rate is optional here: the arrival model supplies it for interarrival times.
                if (distribution.Rate is <= 0)
                {
                    errors.Add($"{path}.rate must be greater than zero when present.");
                }

                break;
        }
    }

    private static void ValidateCategorical(DistributionConfig distribution, string path, List<string> errors)
    {
        if (distribution.Categories is not { Count: > 0 })
        {
            errors.Add($"{path}.categories must be a non-empty array.");
            return;
        }

        if (distribution.Weights is not { Count: > 0 })
        {
            errors.Add($"{path}.weights must be a non-empty array.");
            return;
        }

        if (distribution.Categories.Count != distribution.Weights.Count)
        {
            errors.Add(
                $"{path}.categories ({distribution.Categories.Count}) and weights " +
                $"({distribution.Weights.Count}) must have the same length.");
            return;
        }

        if (distribution.Weights.Any(w => w < 0))
        {
            errors.Add($"{path}.weights must not contain negative values.");
        }

        if (distribution.Weights.Sum() <= 0)
        {
            errors.Add($"{path}.weights must sum to more than zero.");
        }
    }

    private static void ValidateTriangular(DistributionConfig distribution, string path, List<string> errors)
    {
        RequireParameter(distribution.Min, $"{path}.min", errors);
        RequireParameter(distribution.Mode, $"{path}.mode", errors);
        RequireParameter(distribution.Max, $"{path}.max", errors);

        if (distribution.Min is { } min && distribution.Mode is { } mode && distribution.Max is { } max
            && !(min <= mode && mode <= max))
        {
            errors.Add($"{path} requires min <= mode <= max, but was {min}, {mode}, {max}.");
        }
    }

    private static void RequireParameter(double? value, string path, List<string> errors)
    {
        if (value is null)
        {
            errors.Add($"{path} is required for this distribution family.");
        }
    }
}
