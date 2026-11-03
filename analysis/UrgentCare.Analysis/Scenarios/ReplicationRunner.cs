using UrgentCare.Core.Configuration;

namespace UrgentCare.Analysis.Scenarios;

/// <summary>
/// Runs one scenario repeatedly under different seeds (FR-011).
/// </summary>
/// <remarks>
/// A single run of a stochastic model is one draw. Replicating holds every assumption fixed and
/// varies only the random stream, which is what separates the effect of a scenario change from
/// the variation the model produces on its own. The scenario file is never modified: each
/// replication receives its seed as an override, so the configuration on disk stays the record
/// of what was assumed.
/// </remarks>
public sealed class ReplicationRunner
{
    private readonly ScenarioRunner _runner;

    public ReplicationRunner(ScenarioRunner? runner = null) => _runner = runner ?? new ScenarioRunner();

    public IReadOnlyList<ScenarioResult> Run(ScenarioConfig config, int replications)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (replications < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(replications), replications, "replications must be at least 1.");
        }

        var results = new List<ScenarioResult>(replications);

        for (var i = 0; i < replications; i++)
        {
            results.Add(_runner.Run(config, SeedSequence.SeedFor(config.Seed, i), replicationIndex: i));
        }

        return results;
    }
}
