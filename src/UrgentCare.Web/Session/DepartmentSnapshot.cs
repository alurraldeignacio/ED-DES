using UrgentCare.Core.Domain;
using UrgentCare.Core.Events;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Web.Session;

/// <summary>A patient as drawn: id for the render key, ESI urgency, and minutes spent waiting.</summary>
public sealed record PatientView(string Id, int Urgency, double WaitedMinutes);

/// <summary>A care resource as drawn, with the patient it is treating when busy.</summary>
public sealed record StaffView(string Id, CareResourceType Type, ResourceState State, PatientView? Patient);

public enum ExitKind
{
    Completed,
    Abandoned
}

public sealed record ExitView(PatientView Patient, ExitKind Kind);

/// <summary>
/// The department at one moment, derived entirely from the engine and its event log. Building
/// it only reads: the policy is asked for an order on a copy of the waiting area, and nothing
/// in the run changes (contracts/visual-session.md).
/// </summary>
public sealed class DepartmentSnapshot
{
    /// <summary>Waiting patients drawn; the rest are counted (FR-013).</summary>
    public const int MaxDrawnWaiting = 30;

    /// <summary>How long a new arrival is listed in the arrival area, in simulated minutes.</summary>
    public const double RecentArrivalMinutes = 10.0;

    /// <summary>How many of the latest completed and of the latest abandoned patients stay drawn at the exits.</summary>
    public const int RecentExitsPerKind = 5;

    private DepartmentSnapshot()
    {
    }

    public double Time { get; private init; }

    public IReadOnlyList<PatientView> WaitingInServeOrder { get; private init; } = [];

    public int HiddenWaitingCount { get; private init; }

    public IReadOnlyList<StaffView> Staff { get; private init; } = [];

    public int RoomCount { get; private init; }

    public IReadOnlyList<PatientView> PatientsInRooms { get; private init; } = [];

    public IReadOnlyList<PatientView> PatientsBypassingRooms { get; private init; } = [];

    public IReadOnlyList<PatientView> RecentArrivals { get; private init; } = [];

    public IReadOnlyList<ExitView> RecentExits { get; private init; } = [];

    public int ArrivalCount { get; private init; }

    public int CompletedCount { get; private init; }

    public int AbandonedCount { get; private init; }

    /// <summary>Patients waiting right now, drawn or not.</summary>
    public int QueueLength { get; private init; }

    public SimulationMetrics Metrics { get; private init; } = new();

    public static DepartmentSnapshot From(VisualSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var now = session.DisplayTime;
        var department = session.Engine.Department;
        var patients = session.Engine.Patients.ToDictionary(p => p.Id);

        PatientView View(Patient p) => new(p.Id, p.Urgency, p.WaitingTime ?? now - p.ArrivalTime);

        var waiting = InServeOrder(department, now).Select(View).ToList();

        var staff = department.CareResources
            .Select(r => new StaffView(
                r.Id,
                r.Type,
                r.State,
                r.AssignedPatient is { } assigned ? View(assigned) : null))
            .ToList();

        var treated = department.CareResources
            .Select(r => r.AssignedPatient)
            .OfType<Patient>()
            .ToList();

        var (arrivals, exits, arrivalCount, completed, abandoned) = ReadLog(session.Log, now);

        return new DepartmentSnapshot
        {
            Time = now,
            WaitingInServeOrder = waiting,
            HiddenWaitingCount = department.QueueLength - waiting.Count,
            Staff = staff,
            RoomCount = department.RoomCount,
            PatientsInRooms = [.. treated.Where(p => !department.BypassesRooms(p)).Select(View)],
            PatientsBypassingRooms = [.. treated.Where(department.BypassesRooms).Select(View)],
            RecentArrivals = [.. arrivals.Select(id => View(patients[id]))],
            RecentExits = [.. exits.Select(e => new ExitView(View(patients[e.Id]), e.Kind))],
            ArrivalCount = arrivalCount,
            CompletedCount = completed,
            AbandonedCount = abandoned,
            QueueLength = department.QueueLength,
            Metrics = session.Metrics
        };
    }

    /// <summary>
    /// The policy's own order: ask it for the next patient, set that patient aside, ask again.
    /// Policies only read patients, so this is the order the engine would serve them now
    /// (research.md §5).
    /// </summary>
    private static List<Patient> InServeOrder(Department department, double now)
    {
        var remaining = department.WaitingArea.ToList();
        var ordered = new List<Patient>(Math.Min(remaining.Count, MaxDrawnWaiting));

        while (remaining.Count > 0 && ordered.Count < MaxDrawnWaiting)
        {
            var next = department.QueuePolicy.SelectNext(remaining, now);
            if (next is null)
            {
                break;
            }

            ordered.Add(next);
            remaining.Remove(next);
        }

        return ordered;
    }

    /// <summary>
    /// One pass over the log: totals, plus the newest arrivals and exits, newest last so the
    /// exits stack in the order patients left.
    /// </summary>
    private static (List<string> Arrivals, List<(string Id, ExitKind Kind)> Exits, int ArrivalCount, int Completed, int Abandoned)
        ReadLog(InMemoryEventLog log, double now)
    {
        var arrivals = new List<string>();
        var completedIds = new List<string>();
        var abandonedIds = new List<string>();
        var arrivalCount = 0;

        foreach (var e in log.Events)
        {
            if (e.PatientId is not { } id)
            {
                continue;
            }

            switch (e.Type)
            {
                case SimulationEventType.PatientArrived:
                    arrivalCount++;
                    if (e.Timestamp > now - RecentArrivalMinutes)
                    {
                        arrivals.Add(id);
                    }

                    break;

                case SimulationEventType.TreatmentCompleted:
                    completedIds.Add(id);
                    break;

                case SimulationEventType.PatientAbandoned:
                    abandonedIds.Add(id);
                    break;
            }
        }

        var exits = completedIds.TakeLast(RecentExitsPerKind).Select(id => (id, ExitKind.Completed))
            .Concat(abandonedIds.TakeLast(RecentExitsPerKind).Select(id => (id, ExitKind.Abandoned)))
            .ToList();

        return (arrivals, exits, arrivalCount, completedIds.Count, abandonedIds.Count);
    }
}
