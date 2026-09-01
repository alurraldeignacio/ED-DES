---

description: "Task list for Hospital Urgent Care Queue Simulator"
---

# Tasks: Hospital Urgent Care Queue Simulator

**Input**: Design documents from `/specs/001-urgent-care-simulator/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/simulation-config.md

**Tests**: Test tasks ARE included. They are not speculative: plan.md declares xUnit/NUnit for simulation logic plus integration tests around scenario results and queue rules, and Constitution Principle V requires unit tests over pure transformation logic and integration tests over process boundaries.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and demoed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story the task belongs to (US1, US2, US3)
- Every task names an exact file path

## Conventions

- **Stack (constitution v2.1.0)**: C# on .NET 10, MathNet.Numerics for distributions, optional Unity for presentation, Visual Studio Code as editor. No other language runtime.
- **From scratch**: Nothing is carried over from earlier exploratory work. No code, project file, or naming is migrated in.
- **English only**: All namespaces, types, members, locals, comments, commit messages, config keys, and documentation are written in English.
- **Git**: The repository root is the `ED-DES` directory (remote `alurraldeignacio/ED-DES`, working branch `001-urgent-care-simulator`), and it is the single project root. **Every path in this document is relative to that root** — do not prefix paths with `ED-DES/`. Source, `specs/`, and the `.specify/` tooling all live inside it, so the feature documents are version-controlled alongside the code.
- **Out of scope**: `Simularg/` and `SimulargPython/` are prior exploratory work. They stay on disk untouched and are not referenced, built, or migrated by any task here.

## Path Conventions

```text
ED-DES/                              # git repository; all source lives here
├── UrgentCare.sln
├── src/UrgentCare.Core/             # simulation engine: domain, clock, events, queues, metrics
├── src/UrgentCare.Cli/              # console and headless runner, operational view
├── analysis/UrgentCare.Analysis/    # probability, scenario, and aggregation library
├── shared/config/                   # scenario JSON files
├── shared/schemas/                  # config JSON schema
├── unity/                           # optional visual layer (deferred)
└── tests/unit/, tests/integration/
```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the .NET 10 solution, projects, and VS Code tooling

- [X] T001 Create .NET 10 solution at UrgentCare.sln via `dotnet new sln`
- [X] T002 [P] Create net10.0 class library src/UrgentCare.Core/UrgentCare.Core.csproj with nullable enabled, and add it to UrgentCare.sln
- [X] T003 [P] Create net10.0 class library analysis/UrgentCare.Analysis/UrgentCare.Analysis.csproj referencing UrgentCare.Core, and add it to UrgentCare.sln
- [X] T004 [P] Create net10.0 console app src/UrgentCare.Cli/UrgentCare.Cli.csproj referencing UrgentCare.Core and UrgentCare.Analysis, and add it to UrgentCare.sln
- [X] T005 [P] Create xUnit project tests/unit/UrgentCare.Core.Tests/UrgentCare.Core.Tests.csproj referencing UrgentCare.Core, and add it to UrgentCare.sln
- [X] T005a [P] Create xUnit project tests/unit/UrgentCare.Analysis.Tests/UrgentCare.Analysis.Tests.csproj referencing UrgentCare.Analysis, and add it to UrgentCare.sln
- [X] T006 [P] Create xUnit project tests/integration/UrgentCare.Integration.Tests/UrgentCare.Integration.Tests.csproj referencing UrgentCare.Core, UrgentCare.Analysis, and UrgentCare.Cli, and add it to UrgentCare.sln
- [X] T007 Add MathNet.Numerics 5.0.0 PackageReference to analysis/UrgentCare.Analysis/UrgentCare.Analysis.csproj (Analysis only, so Core stays dependency-free)
- [X] T008 [P] Add .editorconfig with C# formatting rules and analyzer severities
- [X] T009 [P] Add .vscode/tasks.json and .vscode/launch.json exposing restore, build, test, and run as named commands (Constitution Principle X)
- [X] T009a [P] Add .github/workflows/ci.yml running the same `dotnet restore`, `dotnet build`, and `dotnet test` commands against UrgentCare.sln that T009 and quickstart.md expose locally (Constitution Principle X: the local command and the CI command must be the same)
- [X] T010 [P] Add .gitignore covering bin/, obj/, .vs/, Unity Library/Temp artifacts, and the out-of-scope Simularg/ and SimulargPython/ directories so they stay on disk but out of the repository. **Already satisfied**: the root .gitignore exists and covers all of these; re-verify rather than recreate

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Contracts, determinism, and telemetry primitives that every user story depends on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T011 [P] Define PatientState, ResourceState, CareResourceType, and SimulationStatus enums in src/UrgentCare.Core/Domain/Enums.cs per data-model.md state transitions
- [X] T012 [P] Define IRandomSource in src/UrgentCare.Core/Random/IRandomSource.cs (injected, never static, per Constitution Principle III)
- [X] T013 Implement SeededRandomSource in src/UrgentCare.Core/Random/SeededRandomSource.cs producing identical streams for identical seeds
- [X] T014 [P] Define IDistribution and IDistributionFactory abstractions in src/UrgentCare.Core/Distributions/IDistributionFactory.cs so Core stays free of MathNet
- [X] T015 Implement MathNetDistributionFactory in analysis/UrgentCare.Analysis/Distributions/MathNetDistributionFactory.cs building parameterized distributions from the `family` discriminator plus that family's required parameters (categorical, normal with optional truncation, lognormal, triangular, exponential) per contracts/simulation-config.md, rejecting an unknown family or missing parameters
- [X] T016 [P] Define ScenarioConfig (including the required `schemaVersion` field), ArrivalModelConfig, DepartmentConfig (including roomCount and roomBypassUrgency), ResourceProfileConfig, PatientModelConfig, DistributionConfig, and MetricsConfig records in src/UrgentCare.Core/Configuration/ScenarioConfig.cs mirroring contracts/simulation-config.md
- [X] T017 Implement ScenarioConfigLoader using System.Text.Json in src/UrgentCare.Core/Configuration/ScenarioConfigLoader.cs
- [X] T018 Implement ScenarioConfigValidator in src/UrgentCare.Core/Configuration/ScenarioConfigValidator.cs enforcing the contract rules: present and supported `schemaVersion` (reject an unsupported major version), unique scenarioId, integer seed, timeHorizon greater than zero, supported queuePolicy, non-negative resourceCount and roomCount, roomBypassUrgency within 0 to 5, a supported distribution `family` with its required parameters present, experience within 0.0 to 1.0, and minEfficiency not greater than maxEfficiency
- [X] T019 [P] Define the SimulationEvent telemetry record (runId, scenarioId, replicationIndex, timestamp, event type, patientId, resourceId, urgency, queueLength) in src/UrgentCare.Core/Events/SimulationEvent.cs. The correlation keys are required so events from batched scenarios (T061) and repeated replications (T072) stay distinguishable (Constitution Principle VI)
- [X] T020 [P] Define IEventSink in src/UrgentCare.Core/Events/IEventSink.cs
- [X] T021 Implement InMemoryEventLog in src/UrgentCare.Core/Events/InMemoryEventLog.cs as the event source all metrics derive from, and document its retention limit: it holds every event for the run, which is acceptable at the default scale (roughly 1000 patients per simulated week) but must be replaced by a streaming or windowed sink before long horizons or large replication batches
- [X] T022 [P] Add the default scenario file shared/config/default-scenario.json matching the example object in contracts/simulation-config.md, including `schemaVersion`
- [X] T023 [P] Add shared/schemas/simulation-config.schema.json describing the configuration contract, with `schemaVersion` as a required property
- [X] T024 [P] Unit tests for configuration validation, including a missing or unsupported schemaVersion, missing arrival rates, and negative staffing, in tests/unit/UrgentCare.Core.Tests/ScenarioConfigValidatorTests.cs
- [X] T025 [P] Unit test asserting two SeededRandomSource instances with the same seed emit identical sequences in tests/unit/UrgentCare.Core.Tests/SeededRandomSourceTests.cs
- [X] T025a [P] Unit tests for MathNetDistributionFactory asserting each supported family (categorical, normal, lognormal, triangular, exponential) samples in range and is deterministic under a fixed IRandomSource, in tests/unit/UrgentCare.Analysis.Tests/MathNetDistributionFactoryTests.cs

**Checkpoint**: Configuration contract, deterministic randomness, and the event stream exist, so user stories can begin

---

## Phase 3: User Story 1 - Run a live urgent-care simulation (Priority: P1) 🎯 MVP

**Goal**: A configured department runs end to end. Patients arrive, queue by urgency, are treated by a care resource, and complete or abandon, with metrics and an operational view.

**Independent Test**: Start a run from `shared/config/default-scenario.json`, watch patients enter and leave, and confirm queue length and waiting time change when staffing or arrival rate changes.

### Tests for User Story 1

> Write these first and confirm they FAIL before implementing.

- [X] T026 [P] [US1] Integration test covering arrival through completion for one queue and one care resource (SC-005) in tests/integration/UrgentCare.Integration.Tests/EndToEndFlowTests.cs
- [X] T027 [P] [US1] Integration test asserting a more acute arrival (a lower urgency value on the ESI scale) is treated before less acute patients already waiting (AS2) in tests/integration/UrgentCare.Integration.Tests/UrgencyPriorityTests.cs
- [X] T028 [P] [US1] Integration test asserting a patient exceeding abandonmentThreshold leaves the queue and is counted as abandoned (AS3) in tests/integration/UrgentCare.Integration.Tests/AbandonmentTests.cs
- [X] T028a [P] [US1] Integration test asserting rooms cap concurrent treatments for low-acuity patients while a patient at or below `roomBypassUrgency` is admitted with all rooms occupied, in tests/integration/UrgentCare.Integration.Tests/RoomCapacityTests.cs
- [X] T029 [P] [US1] Unit tests for start, pause, resume, reset, and speed adjustment in tests/unit/UrgentCare.Core.Tests/SimulationClockTests.cs
- [X] T030 [P] [US1] Unit tests for Patient validation and terminal-state rules in tests/unit/UrgentCare.Core.Tests/PatientTests.cs
- [X] T031 [P] [US1] Unit tests for CareResource validation (efficiency above zero, bounded fatigue, null assignedPatient when available, and rejection of a second concurrent assignment) in tests/unit/UrgentCare.Core.Tests/CareResourceTests.cs
- [X] T031a [P] [US1] Unit tests for the fatigue and efficiency formula (fatigue accrues over a shift, efficiency clamps at minEfficiency and maxEfficiency, effective duration lengthens as efficiency falls) in tests/unit/UrgentCare.Core.Tests/ResourceEfficiencyTests.cs
- [X] T032 [P] [US1] Unit tests for UrgencyFirstPolicy ordering, asserting ascending urgency value is served first (1 before 5) in tests/unit/UrgentCare.Core.Tests/UrgencyFirstPolicyTests.cs

### Implementation for User Story 1

- [X] T033 [P] [US1] Implement Patient in src/UrgentCare.Core/Domain/Patient.cs with the data-model.md fields and validation rules (urgency 1 to 5 on the ESI scale where 1 is most severe, non-negative threshold, positive serviceDuration)
- [X] T034 [P] [US1] Implement CareResource in src/UrgentCare.Core/Domain/CareResource.cs with experience, efficiency, fatigue, and shiftStart, serving at most one patient at a time (no per-resource capacity field), enforcing the numeric bounds in data-model.md
- [X] T034a [US1] Implement the fatigue and efficiency dynamics in src/UrgentCare.Core/Domain/CareResource.cs per the formula in data-model.md, so effective service duration is the sampled duration divided by the resource's current efficiency (FR-004 time-dependent clause)
- [X] T035 [US1] Implement Department in src/UrgentCare.Core/Domain/Department.cs owning care resources, the waiting area, rooms as a finite occupancy pool, and the active queue policy (depends on T033, T034)
- [X] T036 [P] [US1] Implement SimulationClock in src/UrgentCare.Core/Clock/SimulationClock.cs, decoupled from real-time UI timing (FR-007)
- [X] T037 [P] [US1] Implement the ScheduledEvent record in src/UrgentCare.Core/Events/ScheduledEvent.cs
- [X] T038 [US1] Implement FutureEventList as a time-ordered priority queue in src/UrgentCare.Core/Events/FutureEventList.cs
- [X] T039 [P] [US1] Define IQueuePolicy in src/UrgentCare.Core/Queues/IQueuePolicy.cs so new policies never require changes to Patient (FR-005)
- [X] T040 [US1] Implement UrgencyFirstPolicy in src/UrgentCare.Core/Queues/UrgencyFirstPolicy.cs ordering by ascending urgency value (ESI: 1 is most severe), breaking ties by arrival time
- [X] T041 [P] [US1] Define IPatientGenerator in src/UrgentCare.Core/Generation/IPatientGenerator.cs, keeping sampling outside the Patient entity (FR-006)
- [X] T042 [US1] Implement PatientGenerator in src/UrgentCare.Core/Generation/PatientGenerator.cs sampling interarrival time, urgency, age, serviceDuration, and abandonmentThreshold from the configured parameterized distribution objects via IDistributionFactory and IRandomSource, with no distribution parameters hard-coded in the generator (FR-012)
- [X] T043 [US1] Implement the SimulationEngine loop (advance clock, pop and dispatch events, honor Idle/Running/Paused/Complete) in src/UrgentCare.Core/SimulationEngine.cs
- [X] T044 [US1] Implement arrival handling (generate the next patient, enqueue via the active policy, schedule the following arrival) in src/UrgentCare.Core/SimulationEngine.cs
- [X] T045 [US1] Implement resource assignment, treatment start, and treatment completion with Available/Busy transitions in src/UrgentCare.Core/SimulationEngine.cs, scheduling completion at the effective service duration returned by T034a rather than the raw sampled duration, and admitting a patient only when a care resource is free and either a room is free or the patient's urgency is at or below `roomBypassUrgency`
- [X] T046 [US1] Implement the abandonment check that removes a waiting patient once waitingTime exceeds abandonmentThreshold in src/UrgentCare.Core/SimulationEngine.cs
- [X] T047 [US1] Emit SimulationEvent telemetry to IEventSink at every arrival, treatment start, completion, abandonment, and resource state change in src/UrgentCare.Core/SimulationEngine.cs
- [X] T048 [US1] Implement StatisticsCollector in src/UrgentCare.Core/Metrics/StatisticsCollector.cs deriving arrivals, completed, abandoned, averageWaitTime, throughput, and utilization from the event log rather than from ad hoc counters, honoring the MetricsConfig flags so a disabled metric is not reported (FR-009)
- [X] T049 [US1] Implement the console operational view showing queue state, patient locations, and resource status over time in src/UrgentCare.Cli/ConsoleOperationalView.cs (FR-008; the Unity view is deferred)
- [X] T049a [US1] Implement interactive clock controls in the console view (space to pause and resume, r to reset, plus and minus to adjust speed, q to quit) bound to the SimulationClock operations from T036, in src/UrgentCare.Cli/ConsoleOperationalView.cs (FR-007 control surface)
- [X] T050 [US1] Implement the `--scenario <name>` and `--headless` CLI entry points loading from shared/config/ in src/UrgentCare.Cli/Program.cs and src/UrgentCare.Cli/CommandLineOptions.cs. `--headless` skips the interactive input loop from T049a so batch runs, `--compare`, and CI stay non-interactive
- [X] T051 [US1] Add explicit guards and messages for degenerate scenarios (no arrivals, no staffing, and zero rooms combined with a `roomBypassUrgency` of 0, which admits nobody) in src/UrgentCare.Core/SimulationEngine.cs
- [X] T051a [US1] Integration test for sustained overload: arrivals exceeding service capacity for an extended period must keep the run stable, with queue length and abandonment growing rather than the engine stalling or the event log growing without bound (spec.md Edge Cases) in tests/integration/UrgentCare.Integration.Tests/SustainedOverloadTests.cs

**Checkpoint**: User Story 1 is fully functional and demoable on its own. This is the MVP.

---

## Phase 4: User Story 2 - Compare staffing and triage scenarios (Priority: P2)

**Goal**: Change staffing, queue policy, or arrival settings and compare outcomes against a baseline without touching engine code.

**Independent Test**: Alter one parameter, run the scenario, and compare waiting time, throughput, and utilization against the baseline run.

### Tests for User Story 2

> Write these first and confirm they FAIL before implementing.

- [ ] T052 [P] [US2] Integration test comparing two staffing levels and asserting comparable reported metrics (AS1) in tests/integration/UrgentCare.Integration.Tests/StaffingComparisonTests.cs
- [ ] T053 [P] [US2] Integration test asserting treatment order changes when the policy switches from FIFO to UrgencyFirst under a fixed seed (AS2) in tests/integration/UrgentCare.Integration.Tests/QueuePolicyComparisonTests.cs
- [ ] T054 [P] [US2] Integration test asserting different arrival patterns change queue length and abandonment (AS3) in tests/integration/UrgentCare.Integration.Tests/ArrivalPatternTests.cs
- [ ] T055 [P] [US2] Unit tests for FifoPolicy and UrgencyPlusWaitPolicy ordering in tests/unit/UrgentCare.Core.Tests/QueuePolicyTests.cs
- [ ] T055a [P] [US2] Unit tests for ScenarioComparison output shape and ResultExporter schema stability across runs in tests/unit/UrgentCare.Analysis.Tests/ScenarioComparisonTests.cs and tests/unit/UrgentCare.Analysis.Tests/ResultExporterTests.cs

### Implementation for User Story 2

- [ ] T056 [P] [US2] Implement FifoPolicy in src/UrgentCare.Core/Queues/FifoPolicy.cs
- [ ] T057 [P] [US2] Implement UrgencyPlusWaitPolicy in src/UrgentCare.Core/Queues/UrgencyPlusWaitPolicy.cs
- [ ] T058 [US2] Implement QueuePolicyFactory resolving the config queuePolicy string to a policy instance in src/UrgentCare.Core/Queues/QueuePolicyFactory.cs
- [X] T059 [US2] Implement the non-stationary arrival time profile (hourly, daily, seasonal variation) over `baseRatePerHour` in src/UrgentCare.Core/Generation/ArrivalModel.cs (FR-001)
- [ ] T060 [US2] Wire queuePolicy and arrivalModel selection from ScenarioConfig into engine construction in src/UrgentCare.Core/SimulationEngine.cs so behavior changes without code edits (FR-012)
- [ ] T061 [US2] Implement ScenarioRunner executing a batch of scenario configurations in analysis/UrgentCare.Analysis/Scenarios/ScenarioRunner.cs
- [ ] T062 [US2] Implement ScenarioComparison producing a baseline-versus-variant table of waiting time, throughput, abandonment, and utilization in analysis/UrgentCare.Analysis/Scenarios/ScenarioComparison.cs (FR-010)
- [ ] T063 [US2] Implement ResultExporter writing JSON and CSV with a schema stable across runs, stamping each export with the `schemaVersion` of the configuration that produced it and including only the metrics enabled by MetricsConfig, in analysis/UrgentCare.Analysis/Metrics/ResultExporter.cs
- [ ] T064 [US2] Add the `--compare <baseline> <variant>` CLI command in src/UrgentCare.Cli/Program.cs

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Review statistical outcomes and reproducibility (Priority: P3)

**Goal**: Run repeated replications under a fixed seed, aggregate results, and review scenario assumptions against observed outcomes.

**Independent Test**: Run several replications of one scenario, aggregate them, and compare average wait, P95 wait, throughput, and abandonment rate.

### Tests for User Story 3

> Write these first and confirm they FAIL before implementing.

- [ ] T065 [P] [US3] Integration test asserting a fixed seed and configuration reproduce identical results across runs (AS1) in tests/integration/UrgentCare.Integration.Tests/ReproducibilityTests.cs
- [ ] T066 [P] [US3] Integration test aggregating metrics across N replications (AS2) in tests/integration/UrgentCare.Integration.Tests/ReplicationAggregationTests.cs
- [ ] T067 [P] [US3] Unit tests for P95 and percentile edge cases (empty set, single sample) in tests/unit/UrgentCare.Analysis.Tests/PercentileCalculatorTests.cs
- [ ] T068 [P] [US3] Unit test asserting the per-replication seed sequence is deterministic and non-repeating in tests/unit/UrgentCare.Analysis.Tests/SeedSequenceTests.cs
- [ ] T068a [P] [US3] Unit tests for MetricsAggregator (mean, P95, standard deviation, and confidence interval across replications) in tests/unit/UrgentCare.Analysis.Tests/MetricsAggregatorTests.cs

### Implementation for User Story 3

- [ ] T069 [US3] Implement PercentileCalculator in analysis/UrgentCare.Analysis/Metrics/PercentileCalculator.cs
- [ ] T070 [US3] Add p95WaitTime and the per-resource utilization breakdown to src/UrgentCare.Core/Metrics/StatisticsCollector.cs per data-model.md. T048 already produces department-wide aggregate utilization; this task adds only the per-resource map and the P95 figure on top of it
- [ ] T071 [US3] Implement deterministic per-replication seed derivation in analysis/UrgentCare.Analysis/Scenarios/SeedSequence.cs
- [ ] T072 [US3] Implement ReplicationRunner executing N replications of one scenario in analysis/UrgentCare.Analysis/Scenarios/ReplicationRunner.cs (FR-011)
- [ ] T073 [US3] Implement MetricsAggregator computing mean, P95, standard deviation, and confidence interval across replications in analysis/UrgentCare.Analysis/Metrics/MetricsAggregator.cs
- [ ] T074 [US3] Implement RunManifest recording scenario assumptions alongside observed outcomes for review (AS3) in analysis/UrgentCare.Analysis/Scenarios/RunManifest.cs
- [ ] T075 [US3] Extend ResultExporter with an aggregated multi-run summary export in analysis/UrgentCare.Analysis/Metrics/ResultExporter.cs
- [ ] T076 [US3] Add the `--replications <n>` CLI option in src/UrgentCare.Cli/Program.cs and src/UrgentCare.Cli/CommandLineOptions.cs

**Checkpoint**: All three user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T077 [P] Rewrite README.md in English with a Stack section (C#, .NET 10, Unity optional, VS Code) and a short project description
- [ ] T078 [P] Add unity/README.md recording that the Unity presentation layer is deferred and must not own simulation state (research.md decision), and recording the runtime constraint: Unity 2022 LTS cannot reference a net10.0 assembly, so a future visual layer needs either a netstandard2.1 interop surface over UrgentCare.Core or a Unity version with a newer scripting runtime
- [ ] T079 [P] Document the full command set in one place in specs/001-urgent-care-simulator/quickstart.md so local commands match CI (Constitution Principle X)
- [ ] T080 Add structured logging configuration bridging IEventSink to console and file output in src/UrgentCare.Cli/Program.cs. This is the machine-readable event stream and is distinct from T049, which renders the human-facing operational view; the two must not share a writer
- [ ] T081 Verify that a 10080-minute (168-hour) horizon on the default scenario completes in under 10 seconds, and record the measured timing in specs/001-urgent-care-simulator/quickstart.md
- [ ] T082 Verify every namespace, type, member, and comment across src/, analysis/, and tests/ is written in English
- [ ] T083 Run the full quickstart.md validation workflow end to end and fix any drift between documented and actual commands
- [ ] T084 Run `dotnet test UrgentCare.sln` and confirm all unit and integration tests pass
- [ ] T085 Re-check the design against .specify/memory/constitution.md (no hidden global state, metrics derived from events, explicit dependencies) and record the result in the Constitution Check section of specs/001-urgent-care-simulator/plan.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies, start immediately
- **Foundational (Phase 2)**: Depends on Setup, and BLOCKS all user stories
- **User Stories (Phases 3 to 5)**: All depend on Foundational, then run in parallel or sequentially by priority (P1, P2, P3)
- **Polish (Phase 6)**: Depends on the desired stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational. No dependency on other stories.
- **US2 (P2)**: Depends on Foundational. Reuses the US1 engine and StatisticsCollector but is independently testable, adding policies, arrival profiles, and comparison on top.
- **US3 (P3)**: Depends on Foundational. Reuses the US1 engine, and is independently testable via replication and aggregation.

Note: US2 and US3 both invoke the engine built in US1. If they are staffed in parallel with US1, agree on the SimulationEngine and StatisticsCollector signatures first (T043, T048).

### Within Each User Story

- Tests are written and failing before implementation
- Domain entities before the engine
- Engine before CLI and views
- Core before Analysis (Analysis references Core, never the reverse)
- Analysis code is unit-tested in tests/unit/UrgentCare.Analysis.Tests; UrgentCare.Core.Tests references Core only and cannot see Analysis types

### Parallel Opportunities

- Setup: T002 to T006 (including T005a) and T008 to T010 (including T009a) in parallel
- Foundational: T011, T012, T014, T016, T019, T020, T022, T023 in parallel; tests T024, T025, and T025a in parallel
- US1: all test tasks T026 to T032 (including T028a, T031a and T051a) in parallel; entity tasks T033, T034, T036, T037, T039, T041 in parallel (T034a follows T034)
- US2: tests T052 to T055a in parallel; policies T056 and T057 in parallel
- US3: tests T065 to T068a in parallel
- Polish: T077 to T079 in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all User Story 1 tests together:
Task: "Integration test arrival through completion in tests/integration/UrgentCare.Integration.Tests/EndToEndFlowTests.cs"
Task: "Integration test urgency priority in tests/integration/UrgentCare.Integration.Tests/UrgencyPriorityTests.cs"
Task: "Integration test abandonment threshold in tests/integration/UrgentCare.Integration.Tests/AbandonmentTests.cs"
Task: "Unit tests for SimulationClock in tests/unit/UrgentCare.Core.Tests/SimulationClockTests.cs"

# Then launch the independent entities together:
Task: "Implement Patient in src/UrgentCare.Core/Domain/Patient.cs"
Task: "Implement CareResource in src/UrgentCare.Core/Domain/CareResource.cs"
Task: "Implement SimulationClock in src/UrgentCare.Core/Clock/SimulationClock.cs"
Task: "Define IQueuePolicy in src/UrgentCare.Core/Queues/IQueuePolicy.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1: Setup (T001 to T010)
2. Complete Phase 2: Foundational (T011 to T025a), which blocks everything
3. Complete Phase 3: User Story 1 (T026 to T051)
4. **STOP and VALIDATE**: run the quickstart scenario, confirm patients flow from arrival through treatment to completion or abandonment, and confirm queue length responds to staffing changes
5. Demo. This satisfies SC-001 and SC-005.

### Incremental Delivery

1. Setup and Foundational, giving contracts, determinism, and the event stream
2. Add US1, validate, demo (MVP)
3. Add US2, validate scenario comparison, demo (satisfies SC-002 and SC-003)
4. Add US3, validate reproducibility and aggregation, demo (satisfies SC-004)
5. Polish: docs, performance, and the constitution re-check
