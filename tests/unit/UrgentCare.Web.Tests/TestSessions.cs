using UrgentCare.Analysis.Distributions;
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
}
