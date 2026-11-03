using UrgentCare.Core;
using UrgentCare.Core.Distributions;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Web.Session;

public enum PlaybackState
{
    Idle,
    Playing,
    Paused,
    Complete
}

/// <summary>
/// One run as the browser shows it. It owns the engine and the event log for that run but keeps
/// no copy of simulation state: the picture and the metrics are always read back from the
/// engine and the log (data-model.md, contracts/visual-session.md).
/// </summary>
public sealed class VisualSession
{
    /// <summary>
    /// The longest real time one frame may account for. A throttled background tab resumes
    /// where it was instead of bursting through hours of events (research.md §10).
    /// </summary>
    public const double MaxFrameSeconds = 0.25;

    /// <summary>Playback speeds offered to the user, in simulated minutes per real second.</summary>
    public static IReadOnlyList<double> SpeedPresets { get; } = [1, 10, 60, 240, 1440];

    private readonly IDistributionFactory _distributionFactory;
    private bool _begun;
    private double _speed = 60.0;

    public VisualSession(BundledScenario scenario, int seed, IDistributionFactory distributionFactory)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(distributionFactory);

        Scenario = scenario;
        Seed = seed;
        _distributionFactory = distributionFactory;
        (Engine, Log) = BuildRun();
    }

    public BundledScenario Scenario { get; }

    public int Seed { get; }

    public SimulationEngine Engine { get; private set; }

    public InMemoryEventLog Log { get; private set; }

    public PlaybackState PlaybackState { get; private set; } = PlaybackState.Idle;

    /// <summary>Simulated minutes shown on screen; may sit between two events while playing.</summary>
    public double DisplayTime { get; private set; }

    /// <summary>
    /// Simulated minutes per real second. Changing it never rebuilds the engine, so the run's
    /// outcome does not depend on how it was watched (FR-010).
    /// </summary>
    public double Speed
    {
        get => _speed;
        set
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Speed must be a positive number.");
            }

            _speed = value;
        }
    }

    private double TimeHorizon => Scenario.Config.TimeHorizon;

    /// <summary>
    /// Metrics so far, recomputed from the event log by the same collector the CLI uses. Once the
    /// run is complete this is exactly the CLI's call, over the full horizon (FR-012,
    /// research.md §6).
    /// </summary>
    public SimulationMetrics Metrics =>
        new StatisticsCollector(Scenario.Config.Metrics).Collect(
            Log.Events,
            PlaybackState == PlaybackState.Complete ? TimeHorizon : DisplayTime,
            Scenario.Config.Department.ResourceCount);

    public void Play()
    {
        if (PlaybackState is PlaybackState.Idle or PlaybackState.Paused)
        {
            EnsureBegun();
            PlaybackState = PlaybackState.Playing;
        }
    }

    public void Pause()
    {
        if (PlaybackState == PlaybackState.Playing)
        {
            PlaybackState = PlaybackState.Paused;
        }
    }

    /// <summary>
    /// Moves the run forward by one frame of real time: every event up to the target time is
    /// processed, and the clock shows the target rather than the last event, so time flows
    /// evenly between events (data-model.md).
    /// </summary>
    public void Advance(double elapsedSeconds)
    {
        if (PlaybackState != PlaybackState.Playing)
        {
            return;
        }

        var target = DisplayTime + (Speed * Math.Clamp(elapsedSeconds, 0.0, MaxFrameSeconds));

        while (Engine.NextEventTime is { } next && next <= target)
        {
            Engine.RunOneEvent();
        }

        DisplayTime = Math.Min(target, TimeHorizon);

        if (Engine.NextEventTime is null && DisplayTime >= TimeHorizon)
        {
            PlaybackState = PlaybackState.Complete;
        }
    }

    /// <summary>Processes exactly one engine event and leaves the run paused (FR-009, SC-004).</summary>
    public void Step()
    {
        if (PlaybackState == PlaybackState.Complete)
        {
            return;
        }

        if (ProcessOneEvent())
        {
            PlaybackState = PlaybackState.Paused;
        }
    }

    /// <summary>Runs every remaining event and shows the final state.</summary>
    public void JumpToEnd()
    {
        while (PlaybackState != PlaybackState.Complete)
        {
            ProcessOneEvent();
        }
    }

    /// <summary>
    /// Starts the same scenario and seed again from time zero. The engine and log are rebuilt
    /// rather than rewound, so the new run cannot inherit anything from the old one.
    /// </summary>
    public void Restart()
    {
        (Engine, Log) = BuildRun();
        _begun = false;
        DisplayTime = 0.0;
        PlaybackState = PlaybackState.Idle;
    }

    public DepartmentSnapshot Snapshot() => DepartmentSnapshot.From(this);

    private (SimulationEngine Engine, InMemoryEventLog Log) BuildRun()
    {
        var log = new InMemoryEventLog();
        var engine = SimulationEngine.Create(Scenario.Config, _distributionFactory, log, seed: Seed);
        return (engine, log);
    }

    /// <summary>
    /// Runs exactly one engine event. When none is left the session completes and the clock
    /// jumps to the horizon (the completion rule in data-model.md).
    /// </summary>
    private bool ProcessOneEvent()
    {
        EnsureBegun();

        if (Engine.RunOneEvent())
        {
            DisplayTime = Engine.Clock.CurrentTime;
            return true;
        }

        DisplayTime = TimeHorizon;
        PlaybackState = PlaybackState.Complete;
        return false;
    }

    private void EnsureBegun()
    {
        if (!_begun)
        {
            Engine.Begin();
            _begun = true;
        }
    }
}
