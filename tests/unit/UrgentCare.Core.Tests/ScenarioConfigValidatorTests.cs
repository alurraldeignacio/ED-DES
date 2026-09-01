using UrgentCare.Core.Configuration;

namespace UrgentCare.Core.Tests;

/// <summary>
/// Covers the rules in contracts/simulation-config.md. Each test breaks exactly one rule
/// so a failure names the rule that regressed.
/// </summary>
public class ScenarioConfigValidatorTests
{
    private readonly ScenarioConfigValidator _validator = new();

    /// <summary>A scenario matching shared/config/default-scenario.json, used as the valid baseline.</summary>
    private static ScenarioConfig ValidConfig() => new()
    {
        SchemaVersion = "1.0.0",
        ScenarioId = "default",
        Seed = 12345,
        TimeHorizon = 10080,
        ArrivalModel = new ArrivalModelConfig
        {
            BaseRatePerHour = 6.0,
            HourlyVariation = true,
            DailyVariation = true,
            InterarrivalDistribution = new DistributionConfig { Family = "exponential" }
        },
        Department = new DepartmentConfig
        {
            Name = "UrgentCare",
            ResourceCount = 3,
            ResourceType = "Nurse",
            QueuePolicy = "UrgencyFirst",
            RoomCount = 2,
            RoomBypassUrgency = 2
        },
        ResourceProfile = new ResourceProfileConfig
        {
            BaseEfficiency = 1.0,
            Experience = 0.8,
            ExperienceBonus = 0.25,
            ShiftLengthMinutes = 480,
            FatigueRatePerMinute = 0.000625,
            MinEfficiency = 0.5,
            MaxEfficiency = 1.5
        },
        PatientModel = new PatientModelConfig
        {
            UrgencyDistribution = new DistributionConfig
            {
                Family = "categorical",
                Categories = [1, 2, 3, 4, 5],
                Weights = [0.05, 0.15, 0.40, 0.30, 0.10]
            },
            AgeDistribution = new DistributionConfig
            {
                Family = "normal",
                Mean = 42.0,
                StandardDeviation = 21.0,
                Min = 0,
                Max = 105
            },
            ServiceDurationDistribution = new DistributionConfig
            {
                Family = "lognormal",
                Mu = 2.9,
                Sigma = 0.5
            },
            AbandonmentThresholdDistribution = new DistributionConfig
            {
                Family = "triangular",
                Min = 30.0,
                Mode = 90.0,
                Max = 240.0
            }
        },
        Metrics = new MetricsConfig()
    };

    [Fact]
    public void ValidConfiguration_PassesWithNoErrors()
    {
        var result = _validator.Validate(ValidConfig());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MissingSchemaVersion_IsRejected()
    {
        var config = ValidConfig() with { SchemaVersion = "" };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schemaVersion must be present"));
    }

    [Fact]
    public void UnsupportedMajorSchemaVersion_IsRejected()
    {
        var config = ValidConfig() with { SchemaVersion = "2.0.0" };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("major version 2"));
    }

    [Fact]
    public void SameMajorDifferentMinorSchemaVersion_IsAccepted()
    {
        var config = ValidConfig() with { SchemaVersion = "1.4.2" };

        var result = _validator.Validate(config);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NonSemanticSchemaVersion_IsRejected()
    {
        var config = ValidConfig() with { SchemaVersion = "one" };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not semantic versioning"));
    }

    [Fact]
    public void MissingArrivalRate_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            ArrivalModel = config.ArrivalModel with { BaseRatePerHour = 0 }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("baseRatePerHour"));
    }

    [Fact]
    public void NegativeStaffing_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            Department = config.Department with { ResourceCount = -1 }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("resourceCount must be non-negative"));
    }

    [Fact]
    public void NegativeRoomCount_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            Department = config.Department with { RoomCount = -2 }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("roomCount must be non-negative"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void RoomBypassUrgencyOutsideZeroToFive_IsRejected(int bypass)
    {
        var config = ValidConfig();
        config = config with
        {
            Department = config.Department with { RoomBypassUrgency = bypass }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("roomBypassUrgency"));
    }

    [Fact]
    public void UnsupportedQueuePolicy_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            Department = config.Department with { QueuePolicy = "ShortestJobFirst" }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("queuePolicy"));
    }

    [Fact]
    public void NonPositiveTimeHorizon_IsRejected()
    {
        var config = ValidConfig() with { TimeHorizon = 0 };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("timeHorizon"));
    }

    [Fact]
    public void UnknownDistributionFamily_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            PatientModel = config.PatientModel with
            {
                ServiceDurationDistribution = new DistributionConfig { Family = "weibull" }
            }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("weibull"));
    }

    [Fact]
    public void DistributionMissingRequiredParameter_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            PatientModel = config.PatientModel with
            {
                // lognormal without sigma
                ServiceDurationDistribution = new DistributionConfig { Family = "lognormal", Mu = 2.9 }
            }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("sigma"));
    }

    [Fact]
    public void CategoricalWithMismatchedWeights_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            PatientModel = config.PatientModel with
            {
                UrgencyDistribution = new DistributionConfig
                {
                    Family = "categorical",
                    Categories = [1, 2, 3, 4, 5],
                    Weights = [0.5, 0.5]
                }
            }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("same length"));
    }

    [Fact]
    public void TriangularWithModeOutsideBounds_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            PatientModel = config.PatientModel with
            {
                AbandonmentThresholdDistribution = new DistributionConfig
                {
                    Family = "triangular",
                    Min = 30.0,
                    Mode = 500.0,
                    Max = 240.0
                }
            }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("min <= mode <= max"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ExperienceOutsideZeroToOne_IsRejected(double experience)
    {
        var config = ValidConfig();
        config = config with
        {
            ResourceProfile = config.ResourceProfile with { Experience = experience }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("experience"));
    }

    [Fact]
    public void MinEfficiencyAboveMaxEfficiency_IsRejected()
    {
        var config = ValidConfig();
        config = config with
        {
            ResourceProfile = config.ResourceProfile with { MinEfficiency = 2.0, MaxEfficiency = 1.5 }
        };

        var result = _validator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must not exceed"));
    }

    [Fact]
    public void ValidateOrThrow_ReportsEveryBrokenRuleAtOnce()
    {
        var config = ValidConfig() with { SchemaVersion = "", ScenarioId = "", TimeHorizon = -1 };

        var exception = Assert.Throws<ScenarioConfigException>(() => _validator.ValidateOrThrow(config));

        Assert.Contains("schemaVersion", exception.Message);
        Assert.Contains("scenarioId", exception.Message);
        Assert.Contains("timeHorizon", exception.Message);
    }
}
