# Data Model: Hospital Urgent Care Queue Simulator

## Entity Summary

The simulation domain is organized around a small set of entities with explicit ownership boundaries. The patient and care-resource models remain simple, while the hospital and simulation coordinator own event flow and state transitions.

## Entities

### Patient

| Field | Type | Description |
|---|---|---|
| id | string | Unique patient identifier |
| urgency | int | Triage acuity from 1 to 5 on the ESI scale, where 1 is most severe (resuscitation) and 5 is least severe (non-urgent) |
| age | int | Patient age in years |
| arrivalTime | double | Simulation time at which patient entered the system |
| waitingTime | double | Current cumulative waiting duration |
| abandonmentThreshold | double | Maximum tolerated waiting time |
| serviceDuration | double | Expected treatment duration |
| state | enum | Waiting, InTreatment, Completed, Abandoned |
| departureTime | double | Time at which the patient left the system if applicable |

**Relationships**:
- One Patient belongs to exactly one Department context.
- One Patient is assigned to at most one active CareResource at a time.
- One Patient may be in exactly one QueuePolicy ordering at a time.

**Validation rules**:
- urgency must be between 1 and 5, where a lower value means a more severe presentation
- abandonmentThreshold must be non-negative
- serviceDuration must be greater than zero if treatment is required
- completion and abandonment are terminal states

### CareResource

| Field | Type | Description |
|---|---|---|
| id | string | Unique identifier for clinician or treatment resource |
| type | enum | Nurse, Doctor, Intake, Room, etc. |
| experience | double | Skill rating in the range 0.0 to 1.0, configured per scenario |
| efficiency | double | Derived productivity multiplier, clamped to [minEfficiency, maxEfficiency] (defaults 0.5 to 1.5) |
| fatigue | double | Derived performance degradation in the range 0.0 to 1.0, accrued over minutes on shift |
| currentState | enum | Available, Busy, Offline |
| assignedPatient | Patient | Active patient if in treatment |
| shiftStart | double | Time when the resource begins duty |

**Relationships**:
- A CareResource belongs to one Department.
- A CareResource may serve zero or more Patients over a run.

**Validation rules**:
- efficiency must be greater than zero and must stay within [minEfficiency, maxEfficiency]
- fatigue must be within 0.0 and 1.0 inclusive
- experience must be within 0.0 and 1.0 inclusive
- assignedPatient is null when the resource is available
- a CareResource serves at most one patient at a time; there is no per-resource capacity. Concurrency is expressed by the number of CareResource instances in the department (`resourceCount`)

**Derived behavior** (parameters come from `resourceProfile` in contracts/simulation-config.md):

```text
fatigue(t)    = min(1.0, fatigueRatePerMinute * minutesSinceShiftStart)
efficiency(t) = clamp(baseEfficiency * (1 + experienceBonus * experience) * (1 - fatigue(t)),
                      minEfficiency, maxEfficiency)
effectiveServiceDuration = sampledServiceDuration / efficiency(t)
```

`efficiency` and `fatigue` are recomputed when a resource starts a treatment; both are derived state and are never configured directly.

### Department

| Field | Type | Description |
|---|---|---|
| id | string | Department identifier |
| name | string | Human-readable name |
| careResources | list | Nurses or treatment resources assigned to the department |
| waitingArea | Queue | Main waiting queue for arriving patients |
| rooms | list | Treatment rooms. A patient needs a free room as well as a free CareResource to enter treatment, unless the patient's urgency is at or below `roomBypassUrgency` |
| queuePolicy | QueuePolicy | Active queue ordering rule |
| config | ScenarioConfig | Current scenario and operational parameters |

**Relationships**:
- One Department owns many Patients and CareResources.
- One Department contains one or more Queues and one active QueuePolicy.

### QueuePolicy

| Field | Type | Description |
|---|---|---|
| id | string | Unique policy identifier |
| name | string | FIFO, UrgencyFirst, UrgencyPlusWait, etc. |
| priorityRules | list | Ordered rules for selecting the next patient |

**Relationships**:
- One Department holds one active QueuePolicy instance.
- A QueuePolicy orders one or many Patients in a queue.

### ArrivalModel

| Field | Type | Description |
|---|---|---|
| id | string | Unique identifier |
| rate | double | Baseline patient arrival rate |
| timeProfile | object | Hourly, daily, seasonal variation model |
| distribution | DistributionModel | Probability model for arrival spacing and patient attributes |

**Relationships**:
- One Department uses one or more ArrivalModel definitions depending on the scenario.

### StatisticsCollector

| Field | Type | Description |
|---|---|---|
| arrivals | int | Number of patients entering the system |
| completed | int | Number of patients treated and discharged |
| abandoned | int | Number of patients leaving due to wait threshold |
| averageWaitTime | double | Mean patient waiting time |
| p95WaitTime | double | 95th percentile wait time |
| throughput | double | Completed patients per time unit |
| utilization | map | Per-resource utilization and averages |

**Relationships**:
- One StatisticsCollector belongs to one Simulation run.

### Simulation

| Field | Type | Description |
|---|---|---|
| clock | SimulationClock | Current simulated time |
| department | Department | Active department under test |
| arrivalModel | ArrivalModel | Active generation logic |
| metrics | StatisticsCollector | Aggregated results |
| seed | int | Random seed for reproducibility |
| status | enum | Idle, Running, Paused, Complete |

**Relationships**:
- One Simulation owns one Department and one MetricsCollector.
- One Simulation can be re-run using a new scenario configuration or seed.

## State transitions

### Patient lifecycle
- Created -> Waiting -> InTreatment -> Completed
- Created -> Waiting -> Abandoned
- Waiting -> InTreatment may be entered only when a resource is available

### Resource lifecycle
- Available -> Busy -> Available
- Unavailable states can be introduced for offline or maintenance periods

### Simulation lifecycle
- Idle -> Running -> Paused -> Running -> Complete
- A reset operation returns the simulation to Idle with a fresh scenario snapshot
