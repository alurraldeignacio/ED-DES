---

description: "Task list for the web visual view"
---

# Tasks: Web Visual View

**Input**: Design documents from `/specs/002-web-visual-view/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/visual-session.md](./contracts/visual-session.md), [quickstart.md](./quickstart.md)

**Tests**: Included. Constitution Principle V requires tests for transformation logic, and the plan names three test files. Razor components are not unit-tested; they are verified by the quickstart walkthrough.

**Organization**: Tasks are grouped by user story so each story can be built and checked on its own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: The user story the task belongs to (US1, US2, US3)

## Path Conventions

- Web app: `src/UrgentCare.Web/`
- Web tests: `tests/unit/UrgentCare.Web.Tests/`
- Core changes: `src/UrgentCare.Core/`, tested in `tests/unit/UrgentCare.Core.Tests/` and `tests/integration/UrgentCare.Integration.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the two new projects and wire them into the solution, CI and VS Code.

- [X] T001 Create the Blazor WebAssembly project `src/UrgentCare.Web/UrgentCare.Web.csproj` (`Microsoft.NET.Sdk.BlazorWebAssembly`, `net10.0`, `ImplicitUsings` and `Nullable` enabled), referencing `src/UrgentCare.Core` and `analysis/UrgentCare.Analysis`, with package references to `Microsoft.AspNetCore.Components.WebAssembly` and `Microsoft.AspNetCore.Components.WebAssembly.DevServer` (PrivateAssets all) at the 10.0.x version matching the installed SDK
- [X] T002 In `src/UrgentCare.Web/UrgentCare.Web.csproj`, embed `..\..\shared\config\*-scenario.json` as `EmbeddedResource` items with `LogicalName` `scenarios/%(Filename)%(Extension)`, and set `<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>` plus `<TrimmerRootAssembly Include="UrgentCare.Core" />` (research.md §7, §8)
- [X] T003 Create the host shell: `src/UrgentCare.Web/Program.cs` (WebAssemblyHostBuilder, root component `App` at `#app`), `src/UrgentCare.Web/App.razor` (Router with a single page), `src/UrgentCare.Web/_Imports.razor`, `src/UrgentCare.Web/wwwroot/index.html` (loads `_framework/blazor.webassembly.js` and `css/app.css`, no other script), and an empty `src/UrgentCare.Web/wwwroot/css/app.css`
- [X] T004 [P] Create the test project `tests/unit/UrgentCare.Web.Tests/UrgentCare.Web.Tests.csproj` mirroring `tests/unit/UrgentCare.Core.Tests/UrgentCare.Core.Tests.csproj` (same xunit, test SDK and coverlet versions, global `using Xunit`), referencing `src/UrgentCare.Web` and `analysis/UrgentCare.Analysis`
- [X] T005 Add both new projects to `UrgentCare.sln` (Web under the `src` folder, Web.Tests under `tests/unit`) with `dotnet sln add`; verify `dotnet build UrgentCare.sln` succeeds with 0 warnings and `dotnet run --project src/UrgentCare.Web` serves a blank page
- [X] T006 [P] Add a `run-web` task to `.vscode/tasks.json` running `dotnet run --project src/UrgentCare.Web/UrgentCare.Web.csproj`, matching the style of the existing `run` task
- [X] T007 [P] Add `002-web-visual-view` to the `push.branches` list in `.github/workflows/ci.yml`

**Checkpoint**: Solution builds and the web project serves an empty page; existing 280 tests still pass.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The two read-only Core additions, scenario loading, and the session skeleton every story needs.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T008 [P] Write tests for `Department.BypassesRooms` in new `tests/unit/UrgentCare.Core.Tests/DepartmentTests.cs`: true for urgency at and below `RoomBypassUrgency`, false above it, false for every urgency when `RoomBypassUrgency` is 0
- [X] T009 [P] Write tests for `SimulationEngine.NextEventTime` in new `tests/integration/UrgentCare.Integration.Tests/NextEventTimeTests.cs` using `ScenarioBuilder`: null before `Begin()`; after `Begin()` equals the time the following `RunOneEvent()` advances the clock to, for the first 50 events; null once `RunOneEvent()` returns false; reading it never changes the event sequence (compare event logs of two seeded runs, one reading the property before every step)
- [X] T010 Make `BypassesRooms(Patient)` public in `src/UrgentCare.Core/Domain/Department.cs` with an XML doc comment; no logic change. T008 passes
- [X] T011 Add `public double? NextEventTime` to `src/UrgentCare.Core/SimulationEngine.cs`: returns null unless the clock is Running, otherwise the time from `_futureEvents.TryPeek` when it is within `_config.TimeHorizon`, else null (mirrors the guards in `RunOneEvent`). T009 passes
- [X] T012 Implement `BundledScenarios` in `src/UrgentCare.Web/Session/BundledScenarios.cs`: enumerates the assembly's `scenarios/*-scenario.json` manifest resources, parses each with `ScenarioConfigLoader.Parse` and validates with `ScenarioConfigValidator.ValidateOrThrow`, exposes `IReadOnlyList<BundledScenario>` sorted with `default` first; `BundledScenario` is a record of `Name` (file name minus `-scenario.json`) and `Config`
- [X] T013 [P] Write `tests/unit/UrgentCare.Web.Tests/BundledScenariosTests.cs`: both `default` and `lean` are present, `default` is first, each `Config` equals what `ScenarioConfigLoader.LoadByName` returns for the file in `shared/config` (compare `ScenarioId`, `Seed`, `TimeHorizon`, `Department`)
- [X] T014 Implement the `VisualSession` skeleton in `src/UrgentCare.Web/Session/VisualSession.cs` per data-model.md: constructor takes `BundledScenario`, `int seed`, `IDistributionFactory`; builds `InMemoryEventLog` and engine via `SimulationEngine.Create(config, factory, log, seed: seed)`; exposes `Scenario`, `Seed`, `Engine`, `Log`, `PlaybackState` (enum `Idle`, `Playing`, `Paused`, `Complete` in the same file), `DisplayTime`; and a private `ProcessOneEvent()` that calls `Begin()` on first use, runs `RunOneEvent()`, sets `DisplayTime` to the engine clock, and, when it returns false, sets `DisplayTime = TimeHorizon` and moves to `Complete` (the completion rule in data-model.md)
- [X] T015 Register `BundledScenarios` and `MathNetDistributionFactory` as singletons in `src/UrgentCare.Web/Program.cs`

**Checkpoint**: Core additions tested; the web app can load the bundled scenarios and build a session.

---

## Phase 3: User Story 1 - Watch a simulation run in the browser (Priority: P1) 🎯 MVP

**Goal**: Open the page, press Play on the default scenario, and watch patients arrive, wait in serve order, get treated in rooms or under the bypass, and leave as completed or abandoned.

**Independent Test**: Quickstart steps 1 to 4: run the default scenario at 60 min/s and see arrivals, a policy-ordered waiting line with ESI numbers, nurses with their patients, room and bypass areas, and both exit counters rising.

### Tests for User Story 1

- [X] T016 [P] [US1] Write `tests/unit/UrgentCare.Web.Tests/DepartmentSnapshotTests.cs` against a real session of the `default` scenario stepped to various points: waiting list equals the order produced by calling the scenario's `QueuePolicy.SelectNext` repeatedly; for the drawing cap, step a `lean` session (seed 12345, peak queue 33 per the CLI) until `Department.QueueLength > 30`, first asserting that this point is reached before Complete so the test fails loudly if the scenario changes, then check that exactly 30 patients are drawn and `HiddenWaitingCount` equals `QueueLength − 30`; every busy staff member's patient appears in exactly one of `PatientsInRooms` or `PatientsBypassingRooms`, matching `Department.BypassesRooms`; `PatientsInRooms.Count` equals `Department.OccupiedRooms`; building a snapshot leaves `Engine.Department.WaitingArea` and the event log unchanged
- [X] T017 [P] [US1] Write play-pacing tests in `tests/unit/UrgentCare.Web.Tests/VisualSessionTests.cs`: `Advance(elapsedSeconds)` while Playing processes exactly the events with time ≤ `DisplayTime + Speed × min(elapsed, 0.25)`; `DisplayTime` lands on the target, not on the last event; an elapsed of 10 s is capped to 0.25 s; `Advance` does nothing while Paused or Idle; once `NextEventTime` is null, further `Advance` calls process no events but keep moving `DisplayTime`, and the state stays Playing until `DisplayTime` reaches `TimeHorizon`, then becomes Complete (completion rule in data-model.md)

### Implementation for User Story 1

- [X] T018 [US1] Implement `DepartmentSnapshot` in `src/UrgentCare.Web/Session/DepartmentSnapshot.cs` per data-model.md, with nested `PatientView` and `StaffView` records and a static `From(VisualSession)` builder: waiting line in serve order via repeated `QueuePolicy.SelectNext` on a copy (capped at `MaxDrawnWaiting = 30`), `HiddenWaitingCount`, staff from `CareResources`, room and bypass lists via `Department.BypassesRooms`, recent arrivals and recent exits from the last events in `Log`, completed and abandoned counts from the log. T016 passes
- [X] T019 [US1] Add playback to `src/UrgentCare.Web/Session/VisualSession.cs`: `Speed` (default 60), `Play()`, `Pause()`, and `Advance(double elapsedSeconds)` implementing the pacing rule with `Engine.NextEventTime` followed by the completion rule from data-model.md; add `Snapshot()` returning `DepartmentSnapshot.From(this)`. T017 passes
- [X] T020 [P] [US1] Create `src/UrgentCare.Web/Components/PatientToken.razor`: a positioned element showing the ESI number as text, class `esi-1` to `esi-5` for colour, heavier border for ESI 1 and 2 (FR-007, research.md §11), title attribute with id and minutes waited
- [X] T021 [US1] Create `src/UrgentCare.Web/Components/DepartmentView.razor` taking a `DepartmentSnapshot`: arrival area, waiting line left to right in serve order with a "+N more" label when `HiddenWaitingCount > 0`, one card per staff member showing state and current patient, a row of `RoomCount` room slots filled from `PatientsInRooms`, a "No room (bypass)" area, and completed and abandoned exits with counters. Every patient token is keyed by patient id (`@key`) and placed by a CSS transform so moves animate (research.md §9)
- [X] T022 [US1] Write `src/UrgentCare.Web/wwwroot/css/app.css`: layout of the department areas, ESI colour scale red (1) to blue (5) with sufficient contrast, staff state styles (available, busy, offline), `transition: transform 300ms` on patient tokens, distinct styling for the completed and abandoned exits; colour tokens declared on `:root`
- [X] T023 [US1] Create `src/UrgentCare.Web/Pages/Home.razor` (route `/`): builds a `VisualSession` for the first bundled scenario with its own seed, shows a Play/Pause button and the current simulated time (day and hh:mm), runs a 50 ms `PeriodicTimer` loop while Playing that measures elapsed time with a `Stopwatch`, calls `Advance`, and re-renders `DepartmentView` from `Snapshot()`; disposes the timer with the component
- [X] T024 [US1] Validate quickstart steps 1 to 4 in the browser and record any deviation in `specs/002-web-visual-view/quickstart.md`

**Checkpoint**: MVP. The default scenario plays in the browser and the department picture is correct.

---

## Phase 4: User Story 2 - Control the pace of the run (Priority: P2)

**Goal**: Pause, step one event, change speed mid-run, jump to the end, and restart.

**Independent Test**: Quickstart steps 5, 6 and 8: each of three steps processes exactly one engine event (which may move zero, one or two patients); speed changes do not restart the run; Restart then Jump to end reproduces the same metrics.

### Tests for User Story 2

- [X] T025 [P] [US2] Add control tests to `tests/unit/UrgentCare.Web.Tests/VisualSessionTests.cs`: `Step()` from Idle begins the run and adds exactly the events of one `RunOneEvent()` to the log, leaving state Paused; `Step()` when no events remain moves to Complete; changing `Speed` while Playing keeps the same engine instance and the final event log equals an uninterrupted run's; `JumpToEnd()` reaches Complete with `DisplayTime == TimeHorizon`; `Restart()` returns to Idle with a new engine and empty log, and a subsequent `JumpToEnd()` produces an event sequence identical (type, time, patient, resource) to the first run

### Implementation for User Story 2

- [X] T026 [US2] Add `Step()`, `JumpToEnd()`, `Restart()` and the `SpeedPresets` constant (1, 10, 60, 240, 1440) to `src/UrgentCare.Web/Session/VisualSession.cs` following the transitions in data-model.md; `Restart()` rebuilds engine and log from the same scenario and seed. T025 passes
- [X] T027 [US2] Create `src/UrgentCare.Web/Components/ControlBar.razor` with Play/Pause, Step, a speed selector over `SpeedPresets` (label "min/s"), Jump to end and Restart; buttons disabled where the state makes them meaningless (Step and Play disabled when Complete); raises callbacks to the page
- [X] T028 [US2] Replace the Play/Pause button in `src/UrgentCare.Web/Pages/Home.razor` with `ControlBar`, wiring each callback to the session and re-rendering after Step, Jump to end and Restart
- [X] T029 [US2] Validate quickstart steps 5, 6 and 8 in the browser

**Checkpoint**: Full pace control on top of the MVP.

---

## Phase 5: User Story 3 - Read live metrics and choose scenario and seed (Priority: P3)

**Goal**: Live metrics next to the picture, scenario and seed selection, and proven parity with the CLI.

**Independent Test**: Quickstart steps 7 and 9, plus `CliParityTests` passing.

### Tests for User Story 3

- [X] T030 [P] [US3] Write `tests/unit/UrgentCare.Web.Tests/CliParityTests.cs`: for each bundled scenario with its own seed, and for `default` with seeds 7 and 2024, a session run with `JumpToEnd()` reports `SimulationMetrics` equal, field by field (including `UtilizationByResource` contents), to the CLI headless computation: `SimulationEngine.Create(config, new MathNetDistributionFactory(), log, seed: s).Run()` then `new StatisticsCollector(config.Metrics).Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount)`
- [X] T031 [P] [US3] Add metrics and seed tests to `tests/unit/UrgentCare.Web.Tests/VisualSessionTests.cs`: mid-run `Metrics` equals `Collect(log.Events, DisplayTime, resourceCount)`; at Complete it uses `TimeHorizon`; `SeedInput.TryParse` accepts whole numbers in `int` range and rejects `abc`, `1.5`, empty and `99999999999` with a message

### Implementation for User Story 3

- [X] T032 [US3] Add a `Metrics` property to `src/UrgentCare.Web/Session/VisualSession.cs` (collect over `DisplayTime`, or `TimeHorizon` when Complete) and expose it on `DepartmentSnapshot` in `src/UrgentCare.Web/Session/DepartmentSnapshot.cs`; add a static `SeedInput.TryParse(string, out int, out string? error)` in `src/UrgentCare.Web/Session/SeedInput.cs`. T030 and T031 pass
- [X] T033 [P] [US3] Create `src/UrgentCare.Web/Components/MetricsPanel.razor`: the scenario's settings (horizon, staffing, rooms, bypass threshold, queue policy) and the metrics table from contracts/visual-session.md with the same labels and format strings as the CLI, hiding metrics the scenario switched off and marking mid-run figures "so far"
- [X] T034 [US3] Add a scenario dropdown (all `BundledScenarios`) and a seed text box with Apply to `src/UrgentCare.Web/Components/ControlBar.razor`; on change, `src/UrgentCare.Web/Pages/Home.razor` builds a new `VisualSession` (Idle); an invalid seed shows the `SeedInput` message and keeps the previous seed
- [X] T035 [US3] Place `MetricsPanel` beside `DepartmentView` in `src/UrgentCare.Web/Pages/Home.razor` and style it in `src/UrgentCare.Web/wwwroot/css/app.css`
- [X] T036 [US3] Validate quickstart steps 7 and 9 in the browser against `dotnet run --project src/UrgentCare.Cli -- --scenario default --headless` and `--scenario lean --headless`

**Checkpoint**: All three stories work; parity with the CLI is proven by test and by hand.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T037 [P] Update `README.md`: add the web view to the project description and the run command next to the CLI command
- [ ] T038 [P] Update `unity/README.md` to say the web view (feature 002) is the project's visual front end and Unity remains optional and deferred (FR-014)
- [ ] T039 [P] Add the `dotnet run --project src/UrgentCare.Web` row to the command table in `specs/001-urgent-care-simulator/quickstart.md` so all commands stay listed in one place (Principle X)
- [ ] T040 Verify `dotnet publish src/UrgentCare.Web -c Release` succeeds and the published `wwwroot` loads a scenario and plays (confirms the trimming settings from T002); record the result in `specs/002-web-visual-view/quickstart.md`
- [ ] T041 Measure jump-to-end time for the default scenario in the browser (target under 1 s, SC-003) and the full-week playback time at 1440 min/s (target under 2 min); record both in `specs/002-web-visual-view/quickstart.md`
- [ ] T042 Run `dotnet build UrgentCare.sln` (0 warnings) and `dotnet test UrgentCare.sln` (all pass), then re-check the Constitution Check in `specs/002-web-visual-view/plan.md` and add a post-implementation note; confirm the engine members the web project actually uses match the list in `specs/002-web-visual-view/contracts/visual-session.md`, bumping its contract version if they differ

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none. T001 → T002, T003 → T005; T004 needs T001; T006 and T007 are independent.
- **Foundational (Phase 2)**: needs Setup. T008 → T010; T009 → T011; T012 → T013; T014 needs T011 and T012; T015 needs T012.
- **US1 (Phase 3)**: needs Foundational.
- **US2 (Phase 4)**: needs Foundational; its UI tasks (T027, T028) build on `Home.razor` from US1, so in practice it follows US1.
- **US3 (Phase 5)**: needs Foundational. `CliParityTests` (T030) and T032 only need `JumpToEnd()` from T026; the UI tasks build on US1 and US2.
- **Polish (Phase 6)**: after the stories you intend to ship.

### Within Each User Story

- Tests first, and they fail before the implementation task they name.
- Session logic before components; components before the page wiring.

### Parallel Opportunities

- Setup: T004, T006, T007 alongside T002 and T003.
- Foundational: T008 and T009 together; T012 alongside T010 and T011.
- US1: T016 and T017 together; T020 alongside T018 and T019.
- US3: T030 and T031 together; T033 alongside T032.
- Polish: T037, T038, T039 together.

---

## Parallel Example: User Story 1

```text
Task: "Write DepartmentSnapshotTests in tests/unit/UrgentCare.Web.Tests/DepartmentSnapshotTests.cs"   (T016)
Task: "Write play-pacing tests in tests/unit/UrgentCare.Web.Tests/VisualSessionTests.cs"            (T017)

Then:
Task: "Implement DepartmentSnapshot in src/UrgentCare.Web/Session/DepartmentSnapshot.cs"            (T018)
Task: "Create PatientToken in src/UrgentCare.Web/Components/PatientToken.razor"                     (T020)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup, Phase 2 Foundational.
2. Phase 3 US1.
3. **Stop and validate**: quickstart steps 1 to 4. The default scenario plays in the browser.

### Incremental Delivery

1. Setup + Foundational: Core additions tested, scenarios load.
2. US1: watchable department (MVP).
3. US2: step, speed, jump, restart.
4. US3: metrics, scenario and seed choice, CLI parity.
5. Polish: docs, publish check, timings, constitution re-check.

---

## Notes

- [P] tasks touch different files and do not depend on an unfinished task.
- The web project must never call a mutating member of `Department`, `Patient`, `CareResource` or `SimulationClock` (contracts/visual-session.md).
- Commit after each phase checkpoint.
