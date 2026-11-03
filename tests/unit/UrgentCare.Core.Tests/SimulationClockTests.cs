using UrgentCare.Core.Clock;
using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Tests;

/// <summary>
/// FR-007: the clock supports start, pause, resume, reset and speed adjustment, and is
/// decoupled from real-time UI timing. Simulated time only ever moves forward, and only
/// while the clock is running.
/// </summary>
public class SimulationClockTests
{
    [Fact]
    public void NewClock_IsIdleAtTimeZero()
    {
        var clock = new SimulationClock();

        Assert.Equal(SimulationStatus.Idle, clock.Status);
        Assert.Equal(0.0, clock.CurrentTime);
        Assert.Equal(1.0, clock.SpeedMultiplier);
    }

    [Fact]
    public void Start_MovesToRunning()
    {
        var clock = new SimulationClock();

        clock.Start();

        Assert.Equal(SimulationStatus.Running, clock.Status);
    }

    [Fact]
    public void Pause_MovesToPausedAndKeepsTheCurrentTime()
    {
        var clock = new SimulationClock();
        clock.Start();
        clock.AdvanceTo(120.0);

        clock.Pause();

        Assert.Equal(SimulationStatus.Paused, clock.Status);
        Assert.Equal(120.0, clock.CurrentTime);
    }

    [Fact]
    public void Resume_ReturnsToRunningFromPaused()
    {
        var clock = new SimulationClock();
        clock.Start();
        clock.Pause();

        clock.Resume();

        Assert.Equal(SimulationStatus.Running, clock.Status);
    }

    [Fact]
    public void Reset_ReturnsToIdleAtTimeZeroWithDefaultSpeed()
    {
        var clock = new SimulationClock();
        clock.Start();
        clock.AdvanceTo(500.0);
        clock.SetSpeed(8.0);

        clock.Reset();

        Assert.Equal(SimulationStatus.Idle, clock.Status);
        Assert.Equal(0.0, clock.CurrentTime);
        Assert.Equal(1.0, clock.SpeedMultiplier);
    }

    [Fact]
    public void Complete_MovesToComplete()
    {
        var clock = new SimulationClock();
        clock.Start();

        clock.Complete();

        Assert.Equal(SimulationStatus.Complete, clock.Status);
    }

    [Fact]
    public void AdvanceTo_MovesSimulatedTimeForward()
    {
        var clock = new SimulationClock();
        clock.Start();

        clock.AdvanceTo(42.5);

        Assert.Equal(42.5, clock.CurrentTime);
    }

    [Fact]
    public void AdvanceTo_RejectsMovingBackwards()
    {
        var clock = new SimulationClock();
        clock.Start();
        clock.AdvanceTo(100.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTo(99.0));
    }

    [Fact]
    public void AdvanceTo_AllowsStayingAtTheSameInstant()
    {
        // Several events can share a timestamp, so advancing to the current time is valid.
        var clock = new SimulationClock();
        clock.Start();
        clock.AdvanceTo(100.0);

        clock.AdvanceTo(100.0);

        Assert.Equal(100.0, clock.CurrentTime);
    }

    [Fact]
    public void AdvanceTo_IsRejectedWhilePaused()
    {
        var clock = new SimulationClock();
        clock.Start();
        clock.Pause();

        Assert.Throws<InvalidOperationException>(() => clock.AdvanceTo(10.0));
    }

    [Fact]
    public void AdvanceTo_IsRejectedBeforeStarting()
    {
        var clock = new SimulationClock();

        Assert.Throws<InvalidOperationException>(() => clock.AdvanceTo(10.0));
    }

    [Fact]
    public void Pause_IsRejectedWhenNotRunning()
    {
        var clock = new SimulationClock();

        Assert.Throws<InvalidOperationException>(() => clock.Pause());
    }

    [Fact]
    public void Resume_IsRejectedWhenNotPaused()
    {
        var clock = new SimulationClock();
        clock.Start();

        Assert.Throws<InvalidOperationException>(() => clock.Resume());
    }

    [Fact]
    public void Start_IsRejectedWhenAlreadyRunning()
    {
        var clock = new SimulationClock();
        clock.Start();

        Assert.Throws<InvalidOperationException>(() => clock.Start());
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1.0)]
    [InlineData(16.0)]
    public void SetSpeed_AcceptsPositiveMultipliers(double multiplier)
    {
        var clock = new SimulationClock();

        clock.SetSpeed(multiplier);

        Assert.Equal(multiplier, clock.SpeedMultiplier);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    public void SetSpeed_RejectsNonPositiveMultipliers(double multiplier)
    {
        var clock = new SimulationClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed(multiplier));
    }

    [Fact]
    public void SpeedDoesNotAffectSimulatedTime()
    {
        // Speed is a presentation concern: it changes how fast a viewer sees the run,
        // never the simulated timeline itself (FR-007).
        var fast = new SimulationClock();
        fast.Start();
        fast.SetSpeed(16.0);
        fast.AdvanceTo(300.0);

        var slow = new SimulationClock();
        slow.Start();
        slow.SetSpeed(0.5);
        slow.AdvanceTo(300.0);

        Assert.Equal(fast.CurrentTime, slow.CurrentTime);
    }

    [Fact]
    public void ResetAfterComplete_AllowsStartingAgain()
    {
        var clock = new SimulationClock();
        clock.Start();
        clock.AdvanceTo(50.0);
        clock.Complete();

        clock.Reset();
        clock.Start();

        Assert.Equal(SimulationStatus.Running, clock.Status);
        Assert.Equal(0.0, clock.CurrentTime);
    }
}
