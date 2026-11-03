using UrgentCare.Analysis.Distributions;
using UrgentCare.Core.Events;
using UrgentCare.Web.Session;

namespace UrgentCare.Web.Tests;

/// <summary>Builds sessions on the real bundled scenarios and real distribution factory.</summary>
internal static class TestSessions
{
    private static readonly BundledScenarios Bundled = new();

    public static VisualSession Create(string scenarioName, int? seed = null)
    {
        var scenario = Bundled.All.Single(s => s.Name == scenarioName);
        return new VisualSession(scenario, seed ?? scenario.Config.Seed, new MathNetDistributionFactory());
    }

    /// <summary>
    /// Plays in small frames until the condition holds. Returns false if the run completed
    /// first, so a test can fail loudly instead of checking a state it never reached.
    /// </summary>
    public static bool PlayUntil(VisualSession session, Func<VisualSession, bool> condition, double minutesPerFrame = 0.25)
    {
        session.Speed = minutesPerFrame / VisualSession.MaxFrameSeconds;
        session.Play();

        while (session.PlaybackState == PlaybackState.Playing)
        {
            if (condition(session))
            {
                return true;
            }

            session.Advance(VisualSession.MaxFrameSeconds);
        }

        return condition(session);
    }

    /// <summary>The fields that define a run's event sequence, without the per-run identifier.</summary>
    public static List<(double, SimulationEventType, string?, string?)> Fingerprint(InMemoryEventLog log) =>
        [.. log.Events.Select(e => (e.Timestamp, e.Type, e.PatientId, e.ResourceId))];

    /// <summary>
    /// Plays one frame that stops between the run's last event and its horizon, so the session is
    /// still Playing with nothing left to process.
    /// </summary>
    public static VisualSession PlayPastTheLastEvent(string scenarioName)
    {
        var probe = Create(scenarioName);
        probe.JumpToEnd();
        // The clock, not the log: the last event processed may be a stale check that emits nothing.
        var lastEvent = probe.Engine.Clock.CurrentTime;
        var horizon = probe.Scenario.Config.TimeHorizon;

        var session = Create(scenarioName);
        session.Speed = ((lastEvent + horizon) / 2) / VisualSession.MaxFrameSeconds;
        session.Play();
        session.Advance(VisualSession.MaxFrameSeconds);
        return session;
    }
}
