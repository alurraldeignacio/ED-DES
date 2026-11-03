# Contract: Visual Session

**Contract version**: 1.1.0

1.1.0 (minor) adds `SimulationEngine.Patients` and `Patient.WaitingTime`, which the implementation reads to label tokens with minutes waited. Found by the T042 check of members used against members listed.

The web view's contract has two sides: what it consumes from the engine, and what it offers the user. Both are listed so that a change on either side is a visible contract change.

## Versioning

The version follows the same major.minor.patch scheme as the scenario file's `schemaVersion`.

- **Major**: a consumed engine member is removed, renamed, or changes meaning (for example `NextEventTime` stops respecting the horizon), or a user control changes behaviour (for example Restart stops reusing the seed).
- **Minor**: a consumed member or a user control is added without changing existing ones.
- **Patch**: wording or formatting changes with no behavioural effect.

A change to any member listed below must bump this version in the same commit.

## Consumed from UrgentCare.Core (engine boundary)

The view uses only these public members. Anything else is off limits to the web project.

| Member | Use |
|---|---|
| `SimulationEngine.Create(config, factory, sink, seed:)` | Build a run |
| `SimulationEngine.Begin()` | Prepare the run for event-by-event driving |
| `SimulationEngine.RunOneEvent()` | Advance exactly one event; false when finished |
| `SimulationEngine.NextEventTime` **(new)** | `double?`: time of the next pending event within the horizon, or null when none remains. Read-only; never changes state |
| `SimulationEngine.Clock.CurrentTime` | Time of the last processed event |
| `SimulationEngine.Patients` **(1.1.0)** | Looking up patients named in recent arrival and exit events |
| `SimulationEngine.Department` | `WaitingArea`, `QueuePolicy`, `CareResources`, `RoomCount`, `QueueLength` |
| `Department.BypassesRooms(Patient)` **(made public)** | Whether a patient's acuity bypasses the room pool. Unchanged logic |
| `IQueuePolicy.SelectNext(waiting, now)` | Ordering the drawn waiting line. Relies on the interface's documented read-only and deterministic guarantees |
| `CareResource.Id`, `.Type`, `.State`, `.AssignedPatient` | Staff panel |
| `Patient.Id`, `.Urgency`, `.ArrivalTime`, `.State`, `.WaitingTime` **(1.1.0)** | Patient tokens |
| `InMemoryEventLog` and `SimulationEvent` | Event log; arrivals and exits for animation |
| `StatisticsCollector.Collect(events, horizon, resourceCount)` | All displayed metrics |
| `new ScenarioConfigLoader().Parse(...)`, `new ScenarioConfigValidator().ValidateOrThrow(...)` (instance methods) | Bundled scenarios |
| `MathNetDistributionFactory` (UrgentCare.Analysis) | Distribution factory, the same one the CLI uses |

The view never calls a mutating member of `Department`, `Patient`, `CareResource`, or `SimulationClock`.

One consequence is intentional: the CLI's `Run()` ends with `Clock.Complete()`, but a session drives the engine through `Begin()` and `RunOneEvent()` and never calls `Complete()`, so a finished session's engine clock still reports `Running`. Completion is the session's `PlaybackState`, decided by the completion rule in data-model.md. Metrics are unaffected because `StatisticsCollector` reads only the event log.

## Offered to the user (UI contract)

| Control | Behaviour | Spec |
|---|---|---|
| Scenario dropdown | Lists every `*-scenario.json` bundled from `shared/config/`; selecting one resets to Idle | FR-003 |
| Seed input | Whole number in 32-bit range; Apply resets to Idle; invalid input shows a message and keeps the previous seed | FR-004, edge case |
| Play / Pause | Toggles Playing and Paused | FR-009 |
| Step | Processes exactly one engine event | FR-009, SC-004 |
| Speed | Presets 1, 10, 60, 240, 1440 simulated min/s; changing it never restarts the run | FR-009, FR-010 |
| Jump to end | Runs to completion and shows final metrics | FR-009, SC-003 |
| Restart | New engine, same scenario and seed, back to Idle | FR-009, SC-005 |

### Displayed metrics

Labels and format strings match the CLI headless output:

| Label | Source field | Format |
|---|---|---|
| Arrivals | `Arrivals` | integer |
| Completed | `Completed` | integer |
| Abandoned | `Abandoned` | integer |
| Average wait | `AverageWaitTime` | `0.0` min |
| P95 wait | `P95WaitTime` | `0.0` min |
| Abandonment | `AbandonmentRate` | `P1` |
| Utilization | `Utilization` | `P1` |
| Throughput | `Throughput` | `0.00` patients/hour |
| Queue now | `Department.QueueLength` | integer |
| Avg queue | `AverageQueueLength` | `0.0` |
| Peak queue | `MaxQueueLength` | integer |

Metrics switched off in the scenario's `metrics` block are hidden, as in the CLI.

### Parity guarantee

For every bundled scenario, with its own seed and with at least one overridden seed, the `SimulationMetrics` the session reports at `Complete` equals, field by field, the record the CLI headless path computes (`engine.Run()` then `StatisticsCollector.Collect(log.Events, config.TimeHorizon, resourceCount)`). Enforced by `CliParityTests`.
