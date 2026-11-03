using UrgentCare.Analysis.Distributions;
using UrgentCare.Analysis.Scenarios;
using UrgentCare.Cli;
using UrgentCare.Core;
using UrgentCare.Core.Configuration;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

try
{
    var options = CommandLineOptions.Parse(args);

    if (options.ShowHelp)
    {
        Console.WriteLine(CommandLineOptions.UsageText);
        return 0;
    }

    var loader = new ScenarioConfigLoader();
    var validator = new ScenarioConfigValidator();

    if (options.IsComparison)
    {
        RunComparison(loader, validator, options);
        return 0;
    }

    var config = loader.LoadByName(options.ConfigDirectory, options.Scenario);

    validator.ValidateOrThrow(config);

    // Rebuilt on demand so the view's reset key starts a genuinely fresh run from the same seed.
    (SimulationEngine Engine, InMemoryEventLog Log) BuildEngine()
    {
        var log = new InMemoryEventLog();
        var engine = SimulationEngine.Create(config, new MathNetDistributionFactory(), log);

        return (engine, log);
    }

    if (options.Replications > 1)
    {
        RunReplications(config, options.Replications);
        return 0;
    }

    if (options.Headless)
    {
        RunHeadless(config, options);
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

/// <summary>
/// Runs two scenarios and prints how the variant moved against the baseline (FR-010). Both are
/// validated before either runs, so a typo in the second scenario is reported before spending a
/// simulation on the first.
/// </summary>
static void RunComparison(
    ScenarioConfigLoader loader,
    ScenarioConfigValidator validator,
    CommandLineOptions options)
{
    var baselineConfig = loader.LoadByName(options.ConfigDirectory, options.CompareBaseline!);
    var variantConfig = loader.LoadByName(options.ConfigDirectory, options.CompareVariant!);

    validator.ValidateOrThrow(baselineConfig);
    validator.ValidateOrThrow(variantConfig);

    var runner = new ScenarioRunner();
    var comparison = ScenarioComparison.Compare(runner.Run(baselineConfig), runner.Run(variantConfig));

    Console.WriteLine($"Baseline   {comparison.Baseline.ScenarioId} (seed {comparison.Baseline.Seed})");
    Console.WriteLine($"Variant    {comparison.Variant.ScenarioId} (seed {comparison.Variant.Seed})");
    Console.WriteLine();
    Console.WriteLine($"{"Metric",-20}{"Baseline",14}{"Variant",14}{"Change",14}{"",10}");

    foreach (var delta in comparison.Deltas)
    {
        Console.WriteLine(
            $"{delta.Metric,-20}{Figure(delta.Baseline),14}{Figure(delta.Variant),14}"
            + $"{Figure(delta.AbsoluteChange),14}{Percent(delta.PercentChange),10}");
    }

    // A metric that was switched off shows as "n/a" rather than vanishing, so the table keeps
    // the same rows whatever the scenarios measured.
    static string Figure(double? value) => value is { } v ? v.ToString("0.00") : "n/a";

    static string Percent(double? value) => value is { } v ? $"{v:+0.0;-0.0;0.0}%" : "";
}

/// <summary>
/// Runs the scenario repeatedly under derived seeds and prints the aggregate (FR-011). Reporting
/// the spread alongside the mean is the point: a difference between two scenarios only means
/// something once it is larger than the variation a single scenario shows on its own.
/// </summary>
static void RunReplications(ScenarioConfig config, int replications)
{
    var started = System.Diagnostics.Stopwatch.StartNew();
    var results = new ReplicationRunner().Run(config, replications);
    started.Stop();

    var manifest = RunManifest.For(config, results);

    Console.WriteLine($"Scenario         {manifest.ScenarioId} (base seed {manifest.BaseSeed})");
    Console.WriteLine($"Replications     {manifest.Replications}");
    Console.WriteLine($"Horizon          {manifest.TimeHorizon:0} simulated minutes each");
    Console.WriteLine($"Staffing         {manifest.ResourceCount} x {manifest.ResourceType}");
    Console.WriteLine($"Rooms            {manifest.RoomCount} "
        + $"(bypass at urgency <= {manifest.RoomBypassUrgency})");
    Console.WriteLine($"Queue policy     {manifest.QueuePolicy}");
    Console.WriteLine($"Arrivals         {manifest.BaseRatePerHour:0.0}/hour, {manifest.ArrivalProfile}");
    Console.WriteLine();
    Console.WriteLine($"{"Metric",-20}{"Mean",12}{"P95",12}{"Std dev",12}{"95% interval",26}");

    foreach (var a in manifest.Aggregates)
    {
        var interval = $"[{a.ConfidenceIntervalLow:0.00}, {a.ConfidenceIntervalHigh:0.00}]";
        Console.WriteLine(
            $"{a.Metric,-20}{a.Mean,12:0.00}{a.P95,12:0.00}{a.StandardDeviation,12:0.00}{interval,26}");
    }

    Console.WriteLine();
    Console.WriteLine($"Wall-clock       {started.Elapsed.TotalSeconds:0.00} s");
}

/// <summary>
/// Runs to completion with no interactive view, then prints the metrics once. When an event
/// stream is requested it is written alongside the in-memory log the metrics derive from, so the
/// machine-readable telemetry and the human-facing summary never share a writer.
/// </summary>
static void RunHeadless(ScenarioConfig config, CommandLineOptions options)
{
    var log = new InMemoryEventLog();
    var runId = Guid.NewGuid();

    using var eventStream = OpenEventStream(config, options, runId);
    IEventSink sink = eventStream is null ? log : new CompositeEventSink(log, eventStream);

    var started = System.Diagnostics.Stopwatch.StartNew();
    var engine = SimulationEngine.Create(config, new MathNetDistributionFactory(), sink, runId);
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
    Console.WriteLine($"Avg queue        {metrics.AverageQueueLength:0.0} waiting");
    Console.WriteLine($"Peak queue       {metrics.MaxQueueLength} waiting");
    Console.WriteLine($"Events           {log.Events.Count}");
    Console.WriteLine($"Wall-clock       {started.Elapsed.TotalSeconds:0.00} s");

    if (options.EventStream is { } destination && !options.EventStreamToConsole)
    {
        Console.WriteLine($"Event stream     {destination}");
    }
}

/// <summary>
/// Opens the requested event stream, or returns null when none was asked for. The header is
/// written as the stream opens, so even an interrupted run produces a file that identifies its
/// format version and the run it came from.
/// </summary>
static JsonLinesEventSink? OpenEventStream(ScenarioConfig config, CommandLineOptions options, Guid runId)
{
    if (options.EventStream is not { } destination)
    {
        return null;
    }

    return options.EventStreamToConsole
        ? JsonLinesEventSink.To(Console.Out, config.SchemaVersion, config.ScenarioId, runId)
        : JsonLinesEventSink.ToFile(destination, config.SchemaVersion, config.ScenarioId, runId);
}

