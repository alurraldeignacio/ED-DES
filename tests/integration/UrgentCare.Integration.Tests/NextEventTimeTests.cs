using UrgentCare.Core.Events;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// NextEventTime lets a view pace playback by simulated time: it must report exactly when the
/// next event will happen, respect the horizon the way RunOneEvent does, and never disturb the
/// run by being read (feature 002, research.md §3).
/// </summary>
public class NextEventTimeTests
{
    [Fact]
    public void IsNull_BeforeTheRunBegins()
    {
        var (engine, _) = ScenarioBuilder.Build(ScenarioBuilder.Scenario());

        Assert.Null(engine.NextEventTime);
    }

    [Fact]
    public void PredictsTheTimeTheNextEventMovesTheClockTo()
    {
        var (engine, _) = ScenarioBuilder.Build(ScenarioBuilder.Scenario());
        engine.Begin();

        for (var i = 0; i < 50; i++)
        {
            var predicted = engine.NextEventTime;

            Assert.NotNull(predicted);
            Assert.True(engine.RunOneEvent());
            Assert.Equal(predicted.Value, engine.Clock.CurrentTime);
        }
    }

    [Fact]
    public void IsNull_OnceNoEventRemainsWithinTheHorizon()
    {
        var config = ScenarioBuilder.Scenario(timeHorizon: 120.0);
        var (engine, _) = ScenarioBuilder.Build(config);
        engine.Begin();

        do
        {
            // Never reports an event the engine would refuse to run.
            if (engine.NextEventTime is { } next)
            {
                Assert.True(next <= config.TimeHorizon);
            }
        }
        while (engine.RunOneEvent());

        Assert.Null(engine.NextEventTime);
        Assert.True(engine.Clock.CurrentTime <= config.TimeHorizon);
    }

    [Fact]
    public void ReadingIt_NeverChangesTheEventSequence()
    {
        var config = ScenarioBuilder.Scenario();

        var (plain, plainLog) = ScenarioBuilder.Build(config);
        plain.Begin();
        while (plain.RunOneEvent())
        {
        }

        var (peeked, peekedLog) = ScenarioBuilder.Build(config);
        peeked.Begin();
        do
        {
            _ = peeked.NextEventTime;
            _ = peeked.NextEventTime;
        }
        while (peeked.RunOneEvent());

        Assert.Equal(Fingerprint(plainLog), Fingerprint(peekedLog));
    }

    /// <summary>The fields that define a run, without the per-run identifier.</summary>
    private static List<(double, SimulationEventType, string?, string?, int)> Fingerprint(InMemoryEventLog log) =>
        [.. log.Events.Select(e => (e.Timestamp, e.Type, e.PatientId, e.ResourceId, e.QueueLength))];
}
