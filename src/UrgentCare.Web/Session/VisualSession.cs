using UrgentCare.Core;
using UrgentCare.Core.Distributions;
using UrgentCare.Core.Events;

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

    private bool _begun;
    private double _speed = 60.0;

    public VisualSession(BundledScenario scenario, int seed, IDistributionFactory distributionFactory)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(distributionFactory);

        Scenario = scenario;
        Seed = seed;
        Log = new InMemoryEventLog();
        Engine = SimulationEngine.Create(scenario.Config, distributionFactory, Log, seed: seed);
    }

    public BundledScenario Scenario { get; }

    public int Seed { get; }

    public SimulationEngine Engine { get; }

    public InMemoryEventLog Log { get; }

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

    public DepartmentSnapshot Snapshot() => DepartmentSnapshot.From(this);

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
