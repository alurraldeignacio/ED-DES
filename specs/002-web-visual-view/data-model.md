# Data Model: Web Visual View

The view adds no domain entities. Everything below is presentation state derived from the existing engine (`SimulationEngine`, `Department`, `Patient`, `CareResource`) and its event log. Domain entities are documented in [001 data-model.md](../001-urgent-care-simulator/data-model.md).

## BundledScenario

A scenario configuration shipped with the app.

| Field | Type | Notes |
|---|---|---|
| Name | string | From the file name: `default-scenario.json` → `default` |
| Config | `ScenarioConfig` | Parsed with `ScenarioConfigLoader.Parse` and validated with `ScenarioConfigValidator` at start-up |

**Validation**: A file that fails parsing or validation is a build-time mistake in `shared/config/`; the app shows the error message on the page instead of the department.

## VisualSession

One run as seen in the view. Owns the engine and the event log for that run; holds no copy of simulation state.

| Field | Type | Notes |
|---|---|---|
| Scenario | `BundledScenario` | Selected scenario |
| Seed | int | Defaults to the scenario's seed; must be a whole number in `int` range |
| Engine | `SimulationEngine` | Built with `SimulationEngine.Create(config, new MathNetDistributionFactory(), log, seed: Seed)` |
| Log | `InMemoryEventLog` | Every event of the run; source of metrics |
| PlaybackState | enum `Idle`, `Playing`, `Paused`, `Complete` | See transitions below |
| Speed | double | Simulated minutes per real second; one of the presets |
| DisplayTime | double | Simulated time shown on screen; between the last processed event's time and the next event's time |

**State transitions**:

```text
Idle ──Play──▶ Playing ──Pause──▶ Paused ──Play──▶ Playing
  │               │                  │
  │             (completion rule     Step (one event; stays Paused,
  │              below holds)         or Complete if none left)
  │               ▼                  │
  └──JumpToEnd──▶ Complete ◀──JumpToEnd──┘

Restart (any state) ─▶ Idle with a fresh engine and log, same scenario and seed
Change scenario or seed (any state) ─▶ Idle with a fresh engine and log
```

- `Step` from `Idle` begins the run and processes one event (state becomes `Paused`).
- **Completion rule** (the only one): once the run has begun, the session is `Complete` exactly when no event remains within the horizon *and* `DisplayTime` has reached `TimeHorizon`. "No event remains" means `Engine.NextEventTime` is null, which is the same condition under which `RunOneEvent()` returns false.
  - During play, `Advance` stops processing events when `NextEventTime` is null but keeps moving `DisplayTime` at the playback speed. The session becomes `Complete` when `DisplayTime` reaches `TimeHorizon`, so patients still in the department stay visible as the clock runs to the end.
  - `Step` and `JumpToEnd` find no event left (`RunOneEvent()` returns false), set `DisplayTime` to `TimeHorizon`, and so meet the rule immediately.

**Rules**:
- Speed changes never rebuild the engine (FR-010).
- Each frame: `target = DisplayTime + Speed × min(elapsedSeconds, 0.25)`; process events while `Engine.NextEventTime <= target`; set `DisplayTime = min(target, TimeHorizon)`; then apply the completion rule.

## DepartmentSnapshot

The picture at `DisplayTime`. Immutable; rebuilt after each frame or step from the engine.

| Field | Type | Derived from |
|---|---|---|
| Time | double | `VisualSession.DisplayTime` |
| WaitingInServeOrder | list of `PatientView` (at most `MaxDrawnWaiting`) | Repeated `QueuePolicy.SelectNext` over `Department.WaitingArea` at `Time` |
| HiddenWaitingCount | int | `Department.QueueLength − WaitingInServeOrder.Count` (FR-013) |
| Staff | list of `StaffView` | `Department.CareResources` |
| RoomCount | int | `Department.RoomCount` |
| PatientsInRooms | list of `PatientView` | Treated patients for whom `Department.BypassesRooms` is false |
| PatientsBypassingRooms | list of `PatientView` | Treated patients for whom `Department.BypassesRooms` is true |
| RecentArrivals | list of `PatientView` | `PatientArrived` events within the last few simulated minutes, for the arrival area |
| CompletedCount, AbandonedCount | int | From the metrics |
| RecentExits | list of (`PatientView`, Completed/Abandoned) | Last few exit events, so leaving patients animate out |
| Metrics | `SimulationMetrics` | `StatisticsCollector.Collect(Log.Events, Time or TimeHorizon, ResourceCount)` |

`MaxDrawnWaiting` is a display constant (30).

### PatientView

| Field | Type | Notes |
|---|---|---|
| Id | string | Engine patient id (`P17`), used as the render key |
| Urgency | int | ESI 1–5, shown as text and colour |
| WaitedMinutes | double | `Time − ArrivalTime` for waiting patients |

### StaffView

| Field | Type | Notes |
|---|---|---|
| Id | string | `R1`, `R2`, … |
| Type | `CareResourceType` | Nurse, Doctor, Intake |
| State | `ResourceState` | Available, Busy, Offline |
| Patient | `PatientView?` | Set when Busy |
