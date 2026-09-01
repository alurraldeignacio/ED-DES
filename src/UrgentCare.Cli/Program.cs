using UrgentCare.Analysis.Distributions;
using UrgentCare.Cli;
using UrgentCare.Core;
using UrgentCare.Core.Configuration;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Events;
using UrgentCare.Core.Generation;
using UrgentCare.Core.Metrics;
using UrgentCare.Core.Queues;
using UrgentCare.Core.Random;

try
{
    var options = CommandLineOptions.Parse(args);

    if (options.ShowHelp)
    {
        Console.WriteLine(CommandLineOptions.UsageText);
        return 0;
    }

    var loader = new ScenarioConfigLoader();
    var config = loader.LoadByName(options.ConfigDirectory, options.Scenario);

    new ScenarioConfigValidator().ValidateOrThrow(config);

    // Rebuilt on demand so the view's reset key starts a genuinely fresh run from the same seed.
    (SimulationEngine Engine, InMemoryEventLog Log) BuildEngine()
    {
        var random = new SeededRandomSource(config.Seed);
        var generator = new PatientGenerator(config, new MathNetDistributionFactory(), random);
        var department = new Department(config.Department, config.ResourceProfile, ResolvePolicy(config));
        var log = new InMemoryEventLog();

        return (new SimulationEngine(config, department, generator, log), log);
    }

    if (options.Headless)
    {
        RunHeadless(config, BuildEngine);
        return 0;
    }

    var view = new ConsoleOperationalView(config, BuildEngine);
    view.Run();
    return 0;
}
catch (ScenarioConfigException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (InvalidOperationException ex)
{
    // Degenerate scenarios (no arrivals, no staffing, nobody admissible) land here.
    Console.Error.WriteLine(ex.Message);
    return 1;
}

/// <summary>Runs to completion with no interactive view, then prints the metrics once.</summary>
static void RunHeadless(
    ScenarioConfig config,
    Func<(SimulationEngine Engine, InMemoryEventLog Log)> buildEngine)
{
    var started = System.Diagnostics.Stopwatch.StartNew();
    var (engine, log) = buildEngine();
    engine.Run();
    started.Stop();

    var metrics = new StatisticsCollector(config.Metrics)
        .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);

    Console.WriteLine($"Scenario         {config.ScenarioId} (seed {config.Seed})");
    Console.WriteLine($"Horizon          {config.TimeHorizon:0} simulated minutes");
    Console.WriteLine($"Staffing         {config.Department.ResourceCount} x {config.Department.ResourceType}");
    Console.WriteLine($"Rooms            {config.Department.RoomCount} "
        + $"(bypass at urgency <= {config.Department.RoomBypassUrgency})");
    Console.WriteLine($"Queue policy     {config.Department.QueuePolicy}");
    Console.WriteLine();
    Console.WriteLine($"Arrivals         {metrics.Arrivals}");
    Console.WriteLine($"Completed        {metrics.Completed}");
    Console.WriteLine($"Abandoned        {metrics.Abandoned}");

    if (metrics.AverageWaitTime is { } averageWait)
    {
        Console.WriteLine($"Average wait     {averageWait:0.0} min");
    }

    if (metrics.P95WaitTime is { } p95)
    {
        Console.WriteLine($"P95 wait         {p95:0.0} min");
    }

    if (metrics.AbandonmentRate is { } abandonmentRate)
    {
        Console.WriteLine($"Abandonment      {abandonmentRate:P1}");
    }

    if (metrics.Utilization is { } utilization)
    {
        Console.WriteLine($"Utilization      {utilization:P1}");
    }

    Console.WriteLine($"Throughput       {metrics.Throughput:0.00} patients/hour");
    Console.WriteLine($"Events           {log.Events.Count}");
    Console.WriteLine($"Wall-clock       {started.Elapsed.TotalSeconds:0.00} s");
}

/// <summary>
/// Resolves the configured queue policy. Only UrgencyFirst exists in this release; the other
/// policies the contract allows arrive with US2, and failing loudly here is better than
/// silently running a scenario under the wrong rule.
/// </summary>
static IQueuePolicy ResolvePolicy(ScenarioConfig config) => config.Department.QueuePolicy switch
{
    "UrgencyFirst" => new UrgencyFirstPolicy(),
    var other => throw new NotSupportedException(
        $"Queue policy '{other}' is not implemented yet; only UrgencyFirst is available in this release.")
};
