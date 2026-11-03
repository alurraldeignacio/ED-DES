using UrgentCare.Core.Configuration;

namespace UrgentCare.Web.Session;

/// <summary>A scenario shipped with the app, named after its file: default-scenario.json is "default".</summary>
public sealed record BundledScenario(string Name, ScenarioConfig Config);

/// <summary>
/// The scenario files from shared/config, compiled into this assembly so the browser needs no
/// file access. They go through the same loader and validator as the CLI, so both hosts run
/// exactly the same configuration (research.md §7).
/// </summary>
public sealed class BundledScenarios
{
    private const string Prefix = "scenarios/";
    private const string Suffix = "-scenario.json";

    public BundledScenarios()
    {
        var assembly = typeof(BundledScenarios).Assembly;
        var loader = new ScenarioConfigLoader();
        var validator = new ScenarioConfigValidator();

        All = [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)
                && name.EndsWith(Suffix, StringComparison.Ordinal))
            .Select(resource =>
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);
                var config = loader.Parse(reader.ReadToEnd(), resource);
                validator.ValidateOrThrow(config);

                return new BundledScenario(resource[Prefix.Length..^Suffix.Length], config);
            })
            .OrderBy(s => s.Name == "default" ? 0 : 1)
            .ThenBy(s => s.Name, StringComparer.Ordinal)];
    }

    /// <summary>Every bundled scenario, with "default" first.</summary>
    public IReadOnlyList<BundledScenario> All { get; }
}
