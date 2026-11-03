using UrgentCare.Core;
using UrgentCare.Core.Configuration;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Cli;

/// <summary>
/// Live operational view of the department: the clock, the waiting area by acuity, each
/// resource's state, and running metrics (FR-008). It also carries the interactive clock
/// controls, which are
/// the only user-facing surface for FR-007 until a visual layer exists.
/// </summary>
public sealed class ConsoleOperationalView
{
    /// <summary>Real milliseconds spent per simulated minute at speed 1.</summary>
    private const double BaseMillisecondsPerSimulatedMinute = 4.0;

    private readonly ScenarioConfig _config;
    private readonly Func<(SimulationEngine Engine, InMemoryEventLog Log)> _engineFactory;

    private readonly bool _canPaint = ConsoleSupportsLiveView();

    private SimulationEngine _engine;
    private InMemoryEventLog _log;
    private double _lastRenderedTime = double.NegativeInfinity;

    public ConsoleOperationalView(
        ScenarioConfig config,
        Func<(SimulationEngine Engine, InMemoryEventLog Log)> engineFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(engineFactory);

        _config = config;
        _engineFactory = engineFactory;
        (_engine, _log) = engineFactory();
    }

    /// <summary>
    /// Drives the run one event at a time, painting between events and honoring the keyboard
    /// controls. Returns the metrics for the run that finished.
    /// </summary>
    public SimulationMetrics Run()
    {
        // Without a real console (redirected output, or a CI log) there is nothing to animate:
        // run straight through and report, rather than failing on cursor operations.
        if (!_canPaint)
        {
            return RunWithoutLiveView();
        }

        TrySetCursorVisible(false);
        try
        {
            _engine.Begin();
            Console.Clear();

            var wallClock = System.Diagnostics.Stopwatch.StartNew();
            var lastSimTime = 0.0;

            while (true)
            {
                if (!HandleInput(out var quit))
                {
                    // Paused: keep painting so the view stays responsive, but advance nothing.
                    Render(force: true);
                    Thread.Sleep(50);
                    continue;
                }

                if (quit)
                {
                    break;
                }

                if (!_engine.RunOneEvent())
                {
                    break;
                }

                // Pace the run so a viewer can follow it, scaled by the clock's speed multiplier.
                var advanced = _engine.Clock.CurrentTime - lastSimTime;
                lastSimTime = _engine.Clock.CurrentTime;

                var delay = advanced * BaseMillisecondsPerSimulatedMinute / _engine.Clock.SpeedMultiplier;
                if (delay >= 1.0)
                {
                    Thread.Sleep((int)Math.Min(delay, 250));
                }

                Render(force: false);
            }

            wallClock.Stop();
            Render(force: true);

            var metrics = CollectMetrics();
            RenderSummary(metrics, wallClock.Elapsed);
            return metrics;
        }
        finally
        {
            TrySetCursorVisible(true);
        }
    }

    /// <summary>Runs to completion with no animation, for redirected or non-interactive consoles.</summary>
    private SimulationMetrics RunWithoutLiveView()
    {
        var wallClock = System.Diagnostics.Stopwatch.StartNew();
        _engine.Run();
        wallClock.Stop();

        var metrics = CollectMetrics();
        RenderSummary(metrics, wallClock.Elapsed);
        return metrics;
    }

    /// <summary>
    /// True only when a real interactive console is attached. Redirected streams and hosts
    /// without a console handle cannot be painted into.
    /// </summary>
    private static bool ConsoleSupportsLiveView()
    {
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            return false;
        }

        try
        {
            return Console.WindowWidth > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void TrySetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch (IOException)
        {
            // No console handle; nothing to hide or restore.
        }
    }

    public SimulationMetrics CollectMetrics() =>
        new StatisticsCollector(_config.Metrics)
            .Collect(_log.Events, _config.TimeHorizon, _config.Department.ResourceCount);

    /// <summary>
    /// Reads a pending keypress. Returns false while the run is paused, and sets
    /// <paramref name="quit"/> when the viewer asked to stop.
    /// </summary>
    private bool HandleInput(out bool quit)
    {
        quit = false;

        while (!Console.IsInputRedirected && Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Spacebar:
                    if (_engine.Clock.Status == SimulationStatus.Running)
                    {
                        _engine.Clock.Pause();
                    }
                    else if (_engine.Clock.Status == SimulationStatus.Paused)
                    {
                        _engine.Clock.Resume();
                    }

                    break;

                case ConsoleKey.R:
                    Restart();
                    break;

                case ConsoleKey.Q:
                    quit = true;
                    return true;

                case ConsoleKey.Add or ConsoleKey.OemPlus:
                    _engine.Clock.SetSpeed(Math.Min(_engine.Clock.SpeedMultiplier * 2, 256));
                    break;

                case ConsoleKey.Subtract or ConsoleKey.OemMinus:
                    _engine.Clock.SetSpeed(Math.Max(_engine.Clock.SpeedMultiplier / 2, 0.125));
                    break;
            }
        }

        return _engine.Clock.Status == SimulationStatus.Running;
    }

    private void Restart()
    {
        (_engine, _log) = _engineFactory();
        _engine.Begin();
        _lastRenderedTime = double.NegativeInfinity;
        Console.Clear();
    }

    /// <summary>Repaints, throttled to roughly one frame per simulated 10 minutes.</summary>
    private void Render(bool force)
    {
        var now = _engine.Clock.CurrentTime;
        if (!force && now - _lastRenderedTime < 10.0)
        {
            return;
        }

        _lastRenderedTime = now;

        var department = _engine.Department;
        var waiting = department.WaitingArea;
        var patients = _engine.Patients;

        var lines = new List<string>
        {
            $"  URGENT CARE  |  {_config.ScenarioId}  |  seed {_config.Seed}",
            "  " + new string('-', 66),
            $"  Time {FormatSimTime(now)} of {FormatSimTime(_config.TimeHorizon)}"
                + $"   {_engine.Clock.Status,-8}  speed x{_engine.Clock.SpeedMultiplier:0.###}",
            "",
            $"  Waiting room: {waiting.Count,3} patients        Rooms: "
                + $"{department.OccupiedRooms}/{department.RoomCount} occupied",
            "  " + AcuityBar(waiting),
            "",
            "  Care resources"
        };

        foreach (var resource in department.CareResources)
        {
            var patient = resource.AssignedPatient;
            var detail = patient is null
                ? "idle"
                : $"treating {patient.Id} (urgency {patient.Urgency})";

            lines.Add(
                $"    {resource.Id,-4} {resource.State,-9} {detail,-34}"
                + $" efficiency {resource.EfficiencyAt(now):0.00}");
        }

        lines.Add("");
        lines.Add(
            $"  Arrived {patients.Count,4}   "
            + $"In treatment {patients.Count(p => p.State == PatientState.InTreatment),3}   "
            + $"Completed {patients.Count(p => p.State == PatientState.Completed),4}   "
            + $"Abandoned {patients.Count(p => p.State == PatientState.Abandoned),4}");
        lines.Add("");
        lines.Add("  [space] pause  [r] reset  [+/-] speed  [q] quit");

        Paint(lines);
    }

    /// <summary>A simple histogram of the waiting room by triage acuity.</summary>
    private static string AcuityBar(IReadOnlyList<Patient> waiting)
    {
        var parts = new List<string>();

        for (var urgency = 1; urgency <= 5; urgency++)
        {
            var count = waiting.Count(p => p.Urgency == urgency);
            parts.Add($"ESI{urgency} {new string('#', Math.Min(count, 12)),-12} {count,3}");
        }

        return string.Join("  ", parts[..2]) + Environment.NewLine
            + "  " + string.Join("  ", parts[2..]);
    }

    private static string FormatSimTime(double minutes)
    {
        var span = TimeSpan.FromMinutes(minutes);
        return $"day {(int)span.TotalDays + 1} {span.Hours:00}:{span.Minutes:00}";
    }

    /// <summary>
    /// Repaints in place when the console supports it, so the view updates rather than scrolling.
    /// Falls back to plain appended output when redirected.
    /// </summary>
    private static void Paint(IEnumerable<string> lines)
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        Console.SetCursorPosition(0, 0);

        foreach (var line in lines)
        {
            foreach (var physical in line.Split(Environment.NewLine))
            {
                Console.WriteLine(physical.PadRight(Math.Max(Console.WindowWidth - 1, 0))[
                    ..Math.Max(Console.WindowWidth - 1, 0)]);
            }
        }
    }

    private void RenderSummary(SimulationMetrics metrics, TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.WriteLine("  Run complete");
        Console.WriteLine("  " + new string('-', 66));
        Console.WriteLine($"  Arrivals            {metrics.Arrivals}");
        Console.WriteLine($"  Completed           {metrics.Completed}");
        Console.WriteLine($"  Abandoned           {metrics.Abandoned}");

        if (metrics.AverageWaitTime is { } averageWait)
        {
            Console.WriteLine($"  Average wait        {averageWait:0.0} min");
        }

        if (metrics.P95WaitTime is { } p95)
        {
            Console.WriteLine($"  P95 wait            {p95:0.0} min");
        }

        if (metrics.AbandonmentRate is { } abandonmentRate)
        {
            Console.WriteLine($"  Abandonment rate    {abandonmentRate:P1}");
        }

        if (metrics.Utilization is { } utilization)
        {
            Console.WriteLine($"  Utilization         {utilization:P1}");
        }

        Console.WriteLine($"  Throughput          {metrics.Throughput:0.00} patients/hour");
        Console.WriteLine($"  Wall-clock time     {elapsed.TotalSeconds:0.00} s");
    }
}
