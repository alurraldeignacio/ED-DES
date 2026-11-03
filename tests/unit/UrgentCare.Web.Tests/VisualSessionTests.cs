using UrgentCare.Core.Metrics;
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

    // --- Controls (US2) ---

    [Fact]
    public void Step_FromIdle_BeginsTheRunAndAppliesExactlyOneEngineEvent()
    {
        var session = TestSessions.Create("default");

        // The same run driven straight through the engine, one event.
        var reference = TestSessions.Create("default");
        reference.Engine.Begin();
        reference.Engine.RunOneEvent();

        session.Step();

        Assert.Equal(PlaybackState.Paused, session.PlaybackState);
        Assert.Equal(TestSessions.Fingerprint(reference.Log), TestSessions.Fingerprint(session.Log));
        Assert.Equal(reference.Engine.Clock.CurrentTime, session.DisplayTime);
    }

    [Fact]
    public void Step_WhilePlaying_PausesThenSteps()
    {
        var session = TestSessions.Create("default");
        session.Play();
        session.Advance(0.1);
        var before = session.Log.Events.Count;

        session.Step();

        Assert.Equal(PlaybackState.Paused, session.PlaybackState);
        Assert.True(session.Log.Events.Count > before);
    }

    [Fact]
    public void Step_WithNoEventLeft_CompletesTheRun()
    {
        var session = TestSessions.PlayPastTheLastEvent("default");
        session.Pause();

        session.Step();

        Assert.Equal(PlaybackState.Complete, session.PlaybackState);
        Assert.Equal(session.Scenario.Config.TimeHorizon, session.DisplayTime);
    }

    [Fact]
    public void ChangingSpeedMidRun_KeepsTheEngine_AndDoesNotChangeTheOutcome()
    {
        var uninterrupted = TestSessions.Create("default");
        uninterrupted.JumpToEnd();

        var session = TestSessions.Create("default");
        var engine = session.Engine;
        session.Play();
        foreach (var speed in VisualSession.SpeedPresets)
        {
            session.Speed = speed;
            session.Advance(VisualSession.MaxFrameSeconds);
        }

        session.Speed = 1e9;
        session.Advance(VisualSession.MaxFrameSeconds);

        Assert.Same(engine, session.Engine);
        Assert.Equal(PlaybackState.Complete, session.PlaybackState);
        Assert.Equal(TestSessions.Fingerprint(uninterrupted.Log), TestSessions.Fingerprint(session.Log));
    }

    [Fact]
    public void JumpToEnd_CompletesAtTheHorizon()
    {
        var session = TestSessions.Create("default");

        session.JumpToEnd();

        Assert.Equal(PlaybackState.Complete, session.PlaybackState);
        Assert.Equal(session.Scenario.Config.TimeHorizon, session.DisplayTime);
        Assert.Null(session.Engine.NextEventTime);
    }

    [Fact]
    public void Restart_StartsOverWithTheSameSeed_AndReproducesTheRun()
    {
        var session = TestSessions.Create("default");
        session.JumpToEnd();
        var firstRun = TestSessions.Fingerprint(session.Log);
        var firstEngine = session.Engine;

        session.Restart();

        Assert.Equal(PlaybackState.Idle, session.PlaybackState);
        Assert.Equal(0.0, session.DisplayTime);
        Assert.Empty(session.Log.Events);
        Assert.NotSame(firstEngine, session.Engine);

        session.JumpToEnd();

        Assert.Equal(firstRun, TestSessions.Fingerprint(session.Log));
    }

    // --- Metrics and seed (US3) ---

    [Fact]
    public void MidRunMetrics_AreCollectedOverTheTimeShown()
    {
        var session = TestSessions.Create("default");
        session.Speed = 240;
        session.Play();
        for (var i = 0; i < 20; i++)
        {
            session.Advance(VisualSession.MaxFrameSeconds);
        }

        var config = session.Scenario.Config;
        var expected = new StatisticsCollector(config.Metrics)
            .Collect(session.Log.Events, session.DisplayTime, config.Department.ResourceCount);

        Assert.Equal(1200.0, session.DisplayTime, precision: 6);
        Assert.Equal(expected.Throughput, session.Metrics.Throughput);
        Assert.Equal(expected.Utilization, session.Metrics.Utilization);
        Assert.Equal(expected.AverageQueueLength, session.Metrics.AverageQueueLength);
        Assert.Equal(session.Metrics.Arrivals, session.Snapshot().Metrics.Arrivals);
    }

    [Fact]
    public void CompletedRunMetrics_AreCollectedOverTheHorizon()
    {
        var session = TestSessions.Create("default");
        session.JumpToEnd();

        var config = session.Scenario.Config;
        var expected = new StatisticsCollector(config.Metrics)
            .Collect(session.Log.Events, config.TimeHorizon, config.Department.ResourceCount);

        Assert.Equal(expected.Throughput, session.Metrics.Throughput);
        Assert.Equal(expected.AverageQueueLength, session.Metrics.AverageQueueLength);
    }

    [Theory]
    [InlineData("12345", 12345)]
    [InlineData(" 7 ", 7)]
    [InlineData("-3", -3)]
    [InlineData("2147483647", int.MaxValue)]
    public void SeedInput_AcceptsWholeNumbersInRange(string text, int expected)
    {
        Assert.True(SeedInput.TryParse(text, out var seed, out var error));
        Assert.Equal(expected, seed);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("99999999999")]
    public void SeedInput_RejectsAnythingElse_WithAMessage(string text)
    {
        Assert.False(SeedInput.TryParse(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
