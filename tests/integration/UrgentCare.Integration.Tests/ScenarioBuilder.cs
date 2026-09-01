using UrgentCare.Analysis.Distributions;
using UrgentCare.Core;
using UrgentCare.Core.Configuration;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Events;
using UrgentCare.Core.Generation;
using UrgentCare.Core.Queues;
using UrgentCare.Core.Random;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// Assembles a running engine from a scenario, the way the CLI does. Integration tests use the
/// real distribution factory and event log rather than stand-ins, so they exercise the actual
/// process boundaries (Constitution Principle V).
/// </summary>
internal static class ScenarioBuilder
{
    /// <summary>A small, fast scenario: one week is unnecessary when proving a behavior.</summary>
    public static ScenarioConfig Scenario(
        int resourceCount = 1,
        int roomCount = 5,
        int roomBypassUrgency = 0,
        double baseRatePerHour = 6.0,
        double timeHorizon = 480.0,
        int seed = 12345,
        string queuePolicy = "UrgencyFirst",
        DistributionConfig? serviceDuration = null,
        DistributionConfig? abandonmentThreshold = null,
        DistributionConfig? urgency = null) => new()
    {
        SchemaVersion = "1.0.0",
        ScenarioId = "test",
        Seed = seed,
        TimeHorizon = timeHorizon,
        ArrivalModel = new ArrivalModelConfig
        {
            BaseRatePerHour = baseRatePerHour,
            HourlyVariation = false,
            DailyVariation = false,
            SeasonalVariation = false,
            InterarrivalDistribution = new DistributionConfig { Family = "exponential" }
        },
        Department = new DepartmentConfig
        {
            Name = "TestDepartment",
            ResourceCount = resourceCount,
            ResourceType = "Nurse",
            QueuePolicy = queuePolicy,
            RoomCount = roomCount,
            RoomBypassUrgency = roomBypassUrgency
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
            UrgencyDistribution = urgency ?? new DistributionConfig
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
            ServiceDurationDistribution = serviceDuration ?? new DistributionConfig
            {
                Family = "lognormal",
                Mu = 2.9,
                Sigma = 0.5
            },
            AbandonmentThresholdDistribution = abandonmentThreshold ?? new DistributionConfig
            {
                Family = "triangular",
                Min = 30.0,
                Mode = 90.0,
                Max = 240.0
            }
        },
        Metrics = new MetricsConfig()
    };

    /// <summary>A distribution that always returns the same value, for tests that need a fixed input.</summary>
    public static DistributionConfig Fixed(double value) => new()
    {
        Family = "triangular",
        Min = value,
        Mode = value,
        Max = value
    };

    public static (SimulationEngine Engine, InMemoryEventLog Log) Build(ScenarioConfig config)
    {
        var random = new SeededRandomSource(config.Seed);
        var factory = new MathNetDistributionFactory();
        var generator = new PatientGenerator(config, factory, random);
        var policy = QueuePolicyFor(config.Department.QueuePolicy);
        var department = new Department(config.Department, config.ResourceProfile, policy);
        var log = new InMemoryEventLog();

        return (new SimulationEngine(config, department, generator, log), log);
    }

    /// <summary>Runs a scenario to completion and hands back the engine and its event log.</summary>
    public static (SimulationEngine Engine, InMemoryEventLog Log) Run(ScenarioConfig config)
    {
        var built = Build(config);
        built.Engine.Run();
        return built;
    }

    private static IQueuePolicy QueuePolicyFor(string name) => name switch
    {
        "UrgencyFirst" => new UrgencyFirstPolicy(),
        _ => new UrgencyFirstPolicy()
    };
}
