using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Clock;

/// <summary>
/// Tracks simulated time and run status. Deliberately decoupled from real-time UI timing:
/// <see cref="SpeedMultiplier"/> is a presentation concern that a view uses to decide how long
/// to sleep between frames, and never changes the simulated timeline itself (FR-007).
/// </summary>
public sealed class SimulationClock
{
    public double CurrentTime { get; private set; }

    public SimulationStatus Status { get; private set; } = SimulationStatus.Idle;

    /// <summary>How fast a viewer sees the run. Purely presentational.</summary>
    public double SpeedMultiplier { get; private set; } = 1.0;

    public void Start()
    {
        if (Status != SimulationStatus.Idle)
        {
            throw new InvalidOperationException($"Cannot start a clock that is {Status}.");
        }

        Status = SimulationStatus.Running;
    }

    public void Pause()
    {
        if (Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException($"Cannot pause a clock that is {Status}.");
        }

        Status = SimulationStatus.Paused;
    }

    public void Resume()
    {
        if (Status != SimulationStatus.Paused)
        {
            throw new InvalidOperationException($"Cannot resume a clock that is {Status}.");
        }

        Status = SimulationStatus.Running;
    }

    public void Complete()
    {
        Status = SimulationStatus.Complete;
    }

    /// <summary>Returns the clock to a fresh scenario snapshot.</summary>
    public void Reset()
    {
        CurrentTime = 0.0;
        SpeedMultiplier = 1.0;
        Status = SimulationStatus.Idle;
    }

    /// <summary>
    /// Moves simulated time to the timestamp of the next event. Time never runs backwards,
    /// though several events may share one instant.
    /// </summary>
    public void AdvanceTo(double time)
    {
        if (Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException($"Cannot advance a clock that is {Status}.");
        }

        if (time < CurrentTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(time), time, $"Simulated time cannot move backwards from {CurrentTime}.");
        }

        CurrentTime = time;
    }

    public void SetSpeed(double multiplier)
    {
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier), multiplier, "Speed multiplier must be greater than zero.");
        }

        SpeedMultiplier = multiplier;
    }
}
