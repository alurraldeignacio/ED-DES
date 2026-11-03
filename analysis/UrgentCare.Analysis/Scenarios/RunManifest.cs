using UrgentCare.Analysis.Metrics;
using UrgentCare.Core.Configuration;

namespace UrgentCare.Analysis.Scenarios;

/// <summary>
/// The assumptions a batch was run under, recorded next to what it observed (US3 AS3).
/// </summary>
/// <remarks>
/// A wait of 40 minutes means nothing on its own. It means something next to the staffing, the
/// arrival rate, the queue policy and the seed that produced it. This record pairs the two so a
/// result can be reviewed, and re-run, without hunting for the configuration that made it.
/// </remarks>
public sealed record RunManifest
{
    public required string ScenarioId { get; init; }

    /// <summary>Configuration contract version the batch was executed under.</summary>
    public required string SchemaVersion { get; init; }

    /// <summary>Scenario seed the per-replication seeds were derived from.</summary>
    public required int BaseSeed { get; init; }

    public required int Replications { get; init; }

    /// <summary>Length of each run in minutes of simulated time.</summary>
    public required double TimeHorizon { get; init; }

    public required int ResourceCount { get; init; }

    public required string ResourceType { get; init; }

    public required string QueuePolicy { get; init; }

    public required int RoomCount { get; init; }

    public required int RoomBypassUrgency { get; init; }

    public required double BaseRatePerHour { get; init; }

    /// <summary>Whether the arrival rate varied by hour, day, or season.</summary>
    public required string ArrivalProfile { get; init; }

    /// <summary>Seed actually used by each replication, in order.</summary>
    public required IReadOnlyList<int> ReplicationSeeds { get; init; }

    public required IReadOnlyList<AggregateStatistic> Aggregates { get; init; }

    public static RunManifest For(ScenarioConfig config, IReadOnlyList<ScenarioResult> results)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(results);

        return new RunManifest
        {
            ScenarioId = config.ScenarioId,
            SchemaVersion = config.SchemaVersion,
            BaseSeed = config.Seed,
            Replications = results.Count,
            TimeHorizon = config.TimeHorizon,
            ResourceCount = config.Department.ResourceCount,
            ResourceType = config.Department.ResourceType,
            QueuePolicy = config.Department.QueuePolicy,
            RoomCount = config.Department.RoomCount,
            RoomBypassUrgency = config.Department.RoomBypassUrgency,
            BaseRatePerHour = config.ArrivalModel.BaseRatePerHour,
            ArrivalProfile = DescribeProfile(config.ArrivalModel),
            ReplicationSeeds = [.. results.Select(r => r.Seed)],
            Aggregates = MetricsAggregator.Aggregate(results)
        };
    }

    /// <summary>
    /// Names the variation switches that were on, because "6 arrivals per hour, flat" and
    /// "6 arrivals per hour with a daily peak" produce very different departments.
    /// </summary>
    private static string DescribeProfile(ArrivalModelConfig arrivalModel)
    {
        List<string> active = [];

        if (arrivalModel.HourlyVariation)
        {
            active.Add("hourly");
        }

        if (arrivalModel.DailyVariation)
        {
            active.Add("daily");
        }

        if (arrivalModel.SeasonalVariation)
        {
            active.Add("seasonal");
        }

        return active.Count == 0 ? "stationary" : string.Join("+", active);
    }
}
