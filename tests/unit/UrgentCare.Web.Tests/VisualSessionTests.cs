using UrgentCare.Web.Session;

namespace UrgentCare.Web.Tests;

/// <summary>
/// Playback pacing: speed is simulated minutes per real second, a frame never runs an event
/// past its target time, and the completion rule in data-model.md decides when a run ends.
/// </summary>
public class VisualSessionTests
{
    [Fact]
    public void Advance_RunsExactlyTheEventsUpToTheTarget_AndShowsTheTarget()
    {
        var session = TestSessions.Create("default");
        session.Speed = 60;
        session.Play();

        session.Advance(0.2); // target = 12 simulated minutes

        Assert.Equal(12.0, session.DisplayTime, precision: 9);
        Assert.All(session.Log.Events, e => Assert.True(e.Timestamp <= 12.0));
        Assert.True(session.Engine.NextEventTime is null or > 12.0);
    }

    [Fact]
    public void Advance_CapsALongFrame_SoAHiddenTabDoesNotBurst()
    {
        var session = TestSessions.Create("default");
        session.Speed = 60;
        session.Play();

        session.Advance(10.0);

        Assert.Equal(60 * VisualSession.MaxFrameSeconds, session.DisplayTime, precision: 9);
    }

    [Fact]
    public void Advance_DoesNothingWhileIdle()
    {
        var session = TestSessions.Create("default");

        session.Advance(1.0);

        Assert.Equal(PlaybackState.Idle, session.PlaybackState);
        Assert.Equal(0.0, session.DisplayTime);
        Assert.Empty(session.Log.Events);
    }

    [Fact]
    public void Advance_DoesNothingWhilePaused()
    {
        var session = TestSessions.Create("default");
        session.Speed = 240;
        session.Play();
        session.Advance(0.25);
        session.Pause();

        var time = session.DisplayTime;
        var events = session.Log.Events.Count;
        session.Advance(1.0);

        Assert.Equal(PlaybackState.Paused, session.PlaybackState);
        Assert.Equal(time, session.DisplayTime);
        Assert.Equal(events, session.Log.Events.Count);
    }

    [Fact]
    public void AfterTheLastEvent_ClockRunsOnToTheHorizon_ThenTheRunCompletes()
    {
        var horizon = TestSessions.Create("default").Scenario.Config.TimeHorizon;

        // Find when the last event happens.
        var probe = TestSessions.Create("default");
        probe.Speed = 1e9;
        probe.Play();
        probe.Advance(VisualSession.MaxFrameSeconds);
        var lastEvent = probe.Engine.Clock.CurrentTime;
        Assert.True(lastEvent < horizon);

        // Stop a frame between the last event and the horizon.
        var session = TestSessions.Create("default");
        var between = (lastEvent + horizon) / 2;
        session.Speed = between / VisualSession.MaxFrameSeconds;
        session.Play();
        session.Advance(VisualSession.MaxFrameSeconds);

        Assert.Null(session.Engine.NextEventTime);
        Assert.Equal(PlaybackState.Playing, session.PlaybackState);
        Assert.Equal(between, session.DisplayTime, precision: 6);

        session.Advance(VisualSession.MaxFrameSeconds);

        Assert.Equal(PlaybackState.Complete, session.PlaybackState);
        Assert.Equal(horizon, session.DisplayTime);
    }
}
