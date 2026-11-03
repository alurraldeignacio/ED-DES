using UrgentCare.Core.Clock;
using UrgentCare.Core.Configuration;
using UrgentCare.Core.Distributions;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Events;
using UrgentCare.Core.Generation;
using UrgentCare.Core.Queues;
using UrgentCare.Core.Random;

namespace UrgentCare.Core;

/// <summary>
/// The discrete-event loop. It advances the clock to each scheduled event in turn, dispatches
/// it, and emits structured telemetry; every metric is derived from that event stream rather
/// than from counters kept here (Constitution Principle VI).
/// </summary>
public sealed class SimulationEngine
{
    private readonly ScenarioConfig _config;
    private readonly Department _department;
    private readonly IPatientGenerator _generator;
    private readonly IEventSink _eventSink;
    private readonly FutureEventList _futureEvents = new();
    private readonly Dictionary<string, Patient> _patients = [];

    private int _patientCounter;

    public SimulationEngine(
        ScenarioConfig config,
        Department department,
        IPatientGenerator generator,
        IEventSink eventSink,
        Guid? runId = null,
        int replicationIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(eventSink);

        _config = config;
        _department = department;
        _generator = generator;
        _eventSink = eventSink;

        RunId = runId ?? Guid.NewGuid();
        ReplicationIndex = replicationIndex;
        Clock = new SimulationClock();
    }

    /// <summary>
    /// Builds an engine from configuration alone: seed, queue policy, arrival profile, staffing
    /// and rooms all come from the scenario, so changing behavior is a configuration edit rather
    /// than a code edit (FR-012). The distribution factory is injected because it lives in the
    /// analysis library, which Core must not reference (Constitution Principle III).
    /// </summary>
    /// <param name="seed">
    /// Overrides the scenario seed, which is how a replication batch gives each run its own
    /// stream while leaving the scenario file untouched.
    /// </param>
    public static SimulationEngine Create(
        ScenarioConfig config,
        IDistributionFactory distributionFactory,
        IEventSink eventSink,
        Guid? runId = null,
        int replicationIndex = 0,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(distributionFactory);
        ArgumentNullException.ThrowIfNull(eventSink);

        var random = new SeededRandomSource(seed ?? config.Seed);
        var generator = new PatientGenerator(config, distributionFactory, random);
        var policy = QueuePolicyFactory.Create(config.Department.QueuePolicy);
        var department = new Department(config.Department, config.ResourceProfile, policy);

        return new SimulationEngine(config, department, generator, eventSink, runId, replicationIndex);
    }

    public Guid RunId { get; }

    public int ReplicationIndex { get; }

    public SimulationClock Clock { get; }

    public Department Department => _department;

    /// <summary>Every patient the run has produced, in arrival order.</summary>
    public IReadOnlyCollection<Patient> Patients => _patients.Values;

    /// <summary>
    /// When the next event will happen, or null when <see cref="RunOneEvent"/> would return
    /// false. A view uses it to advance by simulated time without overshooting. Reading it
    /// never changes the run.
    /// </summary>
    public double? NextEventTime
    {
        get
        {
            if (Clock.Status != SimulationStatus.Running)
            {
                return null;
            }

            if (!_futureEvents.TryPeek(out var next) || next.Time > _config.TimeHorizon)
            {
                return null;
            }

            return next.Time;
        }
    }

    /// <summary>
    /// Runs to the configured time horizon. Degenerate scenarios are rejected up front rather
    /// than producing a silently empty result.
    /// </summary>
    public void Run()
    {
        GuardAgainstDegenerateScenario();

        if (Clock.Status == SimulationStatus.Idle)
        {
            Clock.Start();
        }

        ScheduleFirstArrival();

        while (RunOneEvent())
        {
            // Each iteration dispatches exactly one event, so a caller driving the loop
            // manually can pause, render a frame, or stop between events.
        }

        Clock.Complete();
    }

    /// <summary>
    /// Dispatches the next pending event. Returns false when the run is finished, which lets a
    /// view drive the loop one event at a time and paint between steps.
    /// </summary>
    public bool RunOneEvent()
    {
        if (Clock.Status != SimulationStatus.Running)
        {
            return false;
        }

        if (!_futureEvents.TryPeek(out var next))
        {
            return false;
        }

        if (next.Time > _config.TimeHorizon)
        {
            return false;
        }

        _futureEvents.Dequeue();
        Clock.AdvanceTo(next.Time);

        switch (next.Type)
        {
            case ScheduledEventType.Arrival:
                HandleArrival(next.Time);
                break;

            case ScheduledEventType.TreatmentCompletion:
                HandleTreatmentCompletion(next);
                break;

            case ScheduledEventType.AbandonmentCheck:
                HandleAbandonmentCheck(next);
                break;
        }

        return true;
    }

    /// <summary>Prepares the run so a view can drive it event by event via <see cref="RunOneEvent"/>.</summary>
    public void Begin()
    {
        GuardAgainstDegenerateScenario();

        if (Clock.Status == SimulationStatus.Idle)
        {
            Clock.Start();
        }

        ScheduleFirstArrival();
    }

    private void ScheduleFirstArrival()
    {
        if (_futureEvents.IsEmpty)
        {
            ScheduleNextArrival(Clock.CurrentTime);
        }
    }

    private void ScheduleNextArrival(double fromTime)
    {
        var arrivalTime = fromTime + _generator.NextInterarrivalTime(fromTime);
        if (arrivalTime <= _config.TimeHorizon)
        {
            _futureEvents.Schedule(arrivalTime, ScheduledEventType.Arrival);
        }
    }

    /// <summary>
    /// A patient arrives, joins the waiting area, and the following arrival is scheduled.
    /// An abandonment check is queued for the moment their tolerance runs out.
    /// </summary>
    private void HandleArrival(double now)
    {
        var patient = _generator.GeneratePatient($"P{++_patientCounter}", now);
        _patients[patient.Id] = patient;
        _department.Enqueue(patient);

        Emit(SimulationEventType.PatientArrived, now, patient, resource: null);

        // The patient walks out the moment their tolerance is exceeded, unless treatment
        // starts first; the check is a no-op if they have already left the queue.
        _futureEvents.Schedule(
            now + patient.AbandonmentThreshold,
            ScheduledEventType.AbandonmentCheck,
            patientId: patient.Id);

        ScheduleNextArrival(now);
        TryStartTreatments(now);
    }

    /// <summary>
    /// Fills every free resource that can legally take the next waiting patient. A patient needs
    /// a free resource and either a free room or an acuity that bypasses the room pool.
    /// </summary>
    private void TryStartTreatments(double now)
    {
        while (true)
        {
            var resource = _department.FirstAvailableResource();
            if (resource is null)
            {
                return;
            }

            var patient = SelectAdmissiblePatient(now);
            if (patient is null)
            {
                return;
            }

            StartTreatment(resource, patient, now);
        }
    }

    /// <summary>
    /// Asks the policy for the next patient, skipping any the room pool cannot admit. A blocked
    /// low-acuity patient must not stall a high-acuity one who can bypass rooms.
    /// </summary>
    private Patient? SelectAdmissiblePatient(double now)
    {
        var candidates = _department.WaitingArea.ToList();

        while (candidates.Count > 0)
        {
            var patient = _department.QueuePolicy.SelectNext(candidates, now);
            if (patient is null)
            {
                return null;
            }

            if (_department.CanAdmit(patient))
            {
                return patient;
            }

            candidates.Remove(patient);
        }

        return null;
    }

    private void StartTreatment(CareResource resource, Patient patient, double now)
    {
        _department.Remove(patient);
        _department.OccupyRoom(patient);

        patient.StartTreatment(now);
        resource.Assign(patient, now);

        Emit(SimulationEventType.TreatmentStarted, now, patient, resource);
        Emit(SimulationEventType.ResourceStateChanged, now, patient, resource);

        // Effective duration, not the raw sample: a fatigued resource takes longer (FR-004).
        var duration = resource.EffectiveServiceDuration(patient, now);

        _futureEvents.Schedule(
            now + duration,
            ScheduledEventType.TreatmentCompletion,
            patientId: patient.Id,
            resourceId: resource.Id);
    }

    private void HandleTreatmentCompletion(ScheduledEvent scheduled)
    {
        var now = scheduled.Time;

        if (scheduled.PatientId is null || !_patients.TryGetValue(scheduled.PatientId, out var patient))
        {
            return;
        }

        var resource = _department.CareResources.FirstOrDefault(r => r.Id == scheduled.ResourceId);
        if (resource is null)
        {
            return;
        }

        patient.Complete(now);
        resource.Release(now);
        _department.ReleaseRoom(patient);

        Emit(SimulationEventType.TreatmentCompleted, now, patient, resource);
        Emit(SimulationEventType.ResourceStateChanged, now, patient: null, resource);

        TryStartTreatments(now);
    }

    /// <summary>
    /// Removes a patient whose waiting time has passed their tolerance. Patients already in
    /// treatment or already gone are left alone, so a stale check is harmless.
    /// </summary>
    private void HandleAbandonmentCheck(ScheduledEvent scheduled)
    {
        var now = scheduled.Time;

        if (scheduled.PatientId is null || !_patients.TryGetValue(scheduled.PatientId, out var patient))
        {
            return;
        }

        if (patient.State != PatientState.Waiting)
        {
            return;
        }

        if (!patient.HasWaitedTooLongAt(now) && now - patient.ArrivalTime < patient.AbandonmentThreshold)
        {
            return;
        }

        patient.Abandon(now);
        _department.Remove(patient);

        Emit(SimulationEventType.PatientAbandoned, now, patient, resource: null);
    }

    private void Emit(SimulationEventType type, double now, Patient? patient, CareResource? resource)
    {
        _eventSink.Emit(new SimulationEvent(
            RunId,
            _config.ScenarioId,
            ReplicationIndex,
            now,
            type,
            patient?.Id,
            resource?.Id,
            patient?.Urgency,
            _department.QueueLength));
    }

    /// <summary>
    /// Rejects configurations that cannot produce a meaningful run, so the cause is obvious
    /// rather than surfacing as an empty result.
    /// </summary>
    private void GuardAgainstDegenerateScenario()
    {
        if (_config.ArrivalModel.BaseRatePerHour <= 0)
        {
            throw new InvalidOperationException(
                "Scenario has no arrivals: arrivalModel.baseRatePerHour must be greater than zero.");
        }

        if (_department.CareResources.Count == 0)
        {
            throw new InvalidOperationException(
                "Scenario has no staffing: department.resourceCount must be greater than zero, " +
                "otherwise no patient can ever be treated.");
        }

        if (_department.RoomCount == 0 && _department.RoomBypassUrgency == 0)
        {
            throw new InvalidOperationException(
                "Scenario admits nobody: department.roomCount is zero and roomBypassUrgency is 0, " +
                "so no patient can ever enter treatment. Raise roomCount, or set roomBypassUrgency " +
                "to let high-acuity patients bypass the room constraint.");
        }

        if (_config.TimeHorizon <= 0)
        {
            throw new InvalidOperationException(
                "Scenario has no duration: timeHorizon must be greater than zero.");
        }
    }
}
