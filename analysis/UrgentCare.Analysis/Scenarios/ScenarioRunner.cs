using UrgentCare.Analysis.Distributions;
using UrgentCare.Core;
using UrgentCare.Core.Configuration;
using UrgentCare.Core.Distributions;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Analysis.Scenarios;

/// <summary>
/// One scenario's outcome, carrying enough provenance to interpret the numbers later: which
/// scenario produced them, under which contract version, and from which seed.
/// </summary>
/// <param name="ScenarioId">Scenario that produced the result.</param>
/// <param name="SchemaVersion">Configuration contract version the run was executed under.</param>
/// <param name="Seed">Seed the run actually used, which may differ from the scenario file for a replication.</param>
/// <param name="Metrics">Outcomes derived from the run's event stream.</param>
public sealed record ScenarioResult(
    string ScenarioId,
    string SchemaVersion,
    int Seed,
    SimulationMetrics Metrics);

/// <summary>
/// Executes scenarios and collects their metrics. This is where a batch of configurations turns
/// into a batch of comparable results, so nothing above it needs to know how an engine is wired.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly IDistributionFactory _distributionFactory;

    /// <param name="distributionFactory">
    /// Sampling backend. Defaults to the MathNet implementation this library owns; tests
    /// override it when they need deterministic draws without a real distribution.
    /// </param>
    public ScenarioRunner(IDistributionFactory? distributionFactory = null) =>
        _distributionFactory = distributionFactory ?? new MathNetDistributionFactory();

    /// <summary>
    /// Runs one scenario to its time horizon and derives its metrics.
    /// </summary>
    /// <param name="seed">Overrides the scenario's own seed, for one replication of a batch.</param>
    public ScenarioResult Run(ScenarioConfig config, int? seed = null, int replicationIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(config);

        var log = new InMemoryEventLog();
        var engine = SimulationEngine.Create(
            config, _distributionFactory, log, runId: null, replicationIndex, seed);

        engine.Run();

        var metrics = new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);

        return new ScenarioResult(config.ScenarioId, config.SchemaVersion, seed ?? config.Seed, metrics);
    }

    /// <summary>
    /// Runs every scenario in the batch, in the order given. Results keep that order so a
    /// caller can pair them with the configurations they came from.
    /// </summary>
    public IReadOnlyList<ScenarioResult> RunAll(IEnumerable<ScenarioConfig> configs)
    {
        ArgumentNullException.ThrowIfNull(configs);

        return [.. configs.Select(config => Run(config))];
    }
}
