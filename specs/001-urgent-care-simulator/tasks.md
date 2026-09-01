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
- **Git**: `ED-DES/` is the git repository (remote `alurraldeignacio/ED-DES`, branch `main`) and the single project root. Source, `specs/`, and the `.specify/` tooling all live inside it, so the feature documents are version-controlled alongside the code.
- **Out of scope**: `ED-DES/Simularg/` and `ED-DES/SimulargPython/` are prior exploratory work. They stay on disk untouched and are not referenced, built, or migrated by any task here.

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

- [ ] T001 Create .NET 10 solution at ED-DES/UrgentCare.sln via `dotnet new sln`
- [ ] T002 [P] Create net10.0 class library ED-DES/src/UrgentCare.Core/UrgentCare.Core.csproj with nullable enabled, and add it to ED-DES/UrgentCare.sln
- [ ] T003 [P] Create net10.0 class library ED-DES/analysis/UrgentCare.Analysis/UrgentCare.Analysis.csproj referencing UrgentCare.Core, and add it to ED-DES/UrgentCare.sln
- [ ] T004 [P] Create net10.0 console app ED-DES/src/UrgentCare.Cli/UrgentCare.Cli.csproj referencing UrgentCare.Core and UrgentCare.Analysis, and add it to ED-DES/UrgentCare.sln
- [ ] T005 [P] Create xUnit project ED-DES/tests/unit/UrgentCare.Core.Tests/UrgentCare.Core.Tests.csproj referencing UrgentCare.Core, and add it to ED-DES/UrgentCare.sln
- [ ] T006 [P] Create xUnit project ED-DES/tests/integration/UrgentCare.Integration.Tests/UrgentCare.Integration.Tests.csproj referencing UrgentCare.Core, UrgentCare.Analysis, and UrgentCare.Cli, and add it to ED-DES/UrgentCare.sln
- [ ] T007 Add MathNet.Numerics 5.0.0 PackageReference to ED-DES/analysis/UrgentCare.Analysis/UrgentCare.Analysis.csproj (Analysis only, so Core stays dependency-free)
- [ ] T008 [P] Add ED-DES/.editorconfig with C# formatting rules and analyzer severities
- [ ] T009 [P] Add ED-DES/.vscode/tasks.json and ED-DES/.vscode/launch.json exposing restore, build, test, and run as named commands (Constitution Principle X)
- [ ] T010 [P] Add ED-DES/.gitignore covering bin/, obj/, .vs/, Unity Library/Temp artifacts, and the out-of-scope Simularg/ and SimulargPython/ directories so they stay on disk but out of the repository

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Contracts, determinism, and telemetry primitives that every user story depends on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T011 [P] Define PatientState, ResourceState, CareResourceType, and SimulationStatus enums in ED-DES/src/UrgentCare.Core/Domain/Enums.cs per data-model.md state transitions
- [ ] T012 [P] Define IRandomSource in ED-DES/src/UrgentCare.Core/Random/IRandomSource.cs (injected, never static, per Constitution Principle III)
- [ ] T013 Implement SeededRandomSource in ED-DES/src/UrgentCare.Core/Random/SeededRandomSource.cs producing identical streams for identical seeds
- [ ] T014 [P] Define IDistribution and IDistributionFactory abstractions in ED-DES/src/UrgentCare.Core/Distributions/IDistributionFactory.cs so Core stays free of MathNet
- [ ] T015 Implement MathNetDistributionFactory in ED-DES/analysis/UrgentCare.Analysis/Distributions/MathNetDistributionFactory.cs supporting categorical, normal, lognormal, triangular, and exponential per contracts/simulation-config.md
- [ ] T016 [P] Define ScenarioConfig, ArrivalModelConfig, DepartmentConfig, PatientModelConfig, and MetricsConfig records in ED-DES/src/UrgentCare.Core/Configuration/ScenarioConfig.cs mirroring contracts/simulation-config.md
- [ ] T017 Implement ScenarioConfigLoader using System.Text.Json in ED-DES/src/UrgentCare.Core/Configuration/ScenarioConfigLoader.cs
- [ ] T018 Implement ScenarioConfigValidator in ED-DES/src/UrgentCare.Core/Configuration/ScenarioConfigValidator.cs enforcing the contract rules: unique scenarioId, integer seed, timeHorizon greater than zero, supported queuePolicy, non-negative staffing
- [ ] T019 [P] Define the SimulationEvent telemetry record (timestamp, event type, patientId, resourceId, urgency, queueLength) in ED-DES/src/UrgentCare.Core/Events/SimulationEvent.cs (Constitution Principle VI)
- [ ] T020 [P] Define IEventSink in ED-DES/src/UrgentCare.Core/Events/IEventSink.cs
- [ ] T021 Implement InMemoryEventLog in ED-DES/src/UrgentCare.Core/Events/InMemoryEventLog.cs as the event source all metrics derive from
- [ ] T022 [P] Add the default scenario file ED-DES/shared/config/default-scenario.json matching the example object in contracts/simulation-config.md
- [ ] T023 [P] Add ED-DES/shared/schemas/simulation-config.schema.json describing the configuration contract
- [ ] T024 [P] Unit tests for configuration validation, including missing arrival rates and negative staffing, in ED-DES/tests/unit/UrgentCare.Core.Tests/ScenarioConfigValidatorTests.cs
- [ ] T025 [P] Unit test asserting two SeededRandomSource instances with the same seed emit identical sequences in ED-DES/tests/unit/UrgentCare.Core.Tests/SeededRandomSourceTests.cs

**Checkpoint**: Configuration contract, deterministic randomness, and the event stream exist, so user stories can begin

---

## Phase 3: User Story 1 - Run a live urgent-care simulation (Priority: P1) 🎯 MVP

**Goal**: A configured department runs end to end. Patients arrive, queue by urgency, are treated by a care resource, and complete or abandon, with metrics and an operational view.

**Independent Test**: Start a run from `shared/config/default-scenario.json`, watch patients enter and leave, and confirm queue length and waiting time change when staffing or arrival rate changes.

### Tests for User Story 1

> Write these first and confirm they FAIL before implementing.

- [ ] T026 [P] [US1] Integration test covering arrival through completion for one queue and one care resource (SC-005) in ED-DES/tests/integration/UrgentCare.Integration.Tests/EndToEndFlowTests.cs
- [ ] T027 [P] [US1] Integration test asserting a higher-urgency arrival is treated before lower-urgency patients already waiting (AS2) in ED-DES/tests/integration/UrgentCare.Integration.Tests/UrgencyPriorityTests.cs
- [ ] T028 [P] [US1] Integration test asserting a patient exceeding abandonmentThreshold leaves the queue and is counted as abandoned (AS3) in ED-DES/tests/integration/UrgentCare.Integration.Tests/AbandonmentTests.cs
- [ ] T029 [P] [US1] Unit tests for start, pause, resume, reset, and speed adjustment in ED-DES/tests/unit/UrgentCare.Core.Tests/SimulationClockTests.cs
- [ ] T030 [P] [US1] Unit tests for Patient validation and terminal-state rules in ED-DES/tests/unit/UrgentCare.Core.Tests/PatientTests.cs
- [ ] T031 [P] [US1] Unit tests for CareResource validation (efficiency above zero, bounded fatigue, null assignedPatient when available) in ED-DES/tests/unit/UrgentCare.Core.Tests/CareResourceTests.cs
- [ ] T032 [P] [US1] Unit tests for UrgencyFirstPolicy ordering in ED-DES/tests/unit/UrgentCare.Core.Tests/UrgencyFirstPolicyTests.cs

### Implementation for User Story 1

- [ ] T033 [P] [US1] Implement Patient in ED-DES/src/UrgentCare.Core/Domain/Patient.cs with the data-model.md fields and validation rules (urgency 1 to 5, non-negative threshold, positive serviceDuration)
- [ ] T034 [P] [US1] Implement CareResource in ED-DES/src/UrgentCare.Core/Domain/CareResource.cs with capacity, experience, efficiency, fatigue, and shiftStart
- [ ] T035 [US1] Implement Department in ED-DES/src/UrgentCare.Core/Domain/Department.cs owning care resources, the waiting area, rooms, and the active queue policy (depends on T033, T034)
- [ ] T036 [P] [US1] Implement SimulationClock in ED-DES/src/UrgentCare.Core/Clock/SimulationClock.cs, decoupled from real-time UI timing (FR-007)
- [ ] T037 [P] [US1] Implement the ScheduledEvent record in ED-DES/src/UrgentCare.Core/Events/ScheduledEvent.cs
- [ ] T038 [US1] Implement FutureEventList as a time-ordered priority queue in ED-DES/src/UrgentCare.Core/Events/FutureEventList.cs
- [ ] T039 [P] [US1] Define IQueuePolicy in ED-DES/src/UrgentCare.Core/Queues/IQueuePolicy.cs so new policies never require changes to Patient (FR-005)
- [ ] T040 [US1] Implement UrgencyFirstPolicy in ED-DES/src/UrgentCare.Core/Queues/UrgencyFirstPolicy.cs
- [ ] T041 [P] [US1] Define IPatientGenerator in ED-DES/src/UrgentCare.Core/Generation/IPatientGenerator.cs, keeping sampling outside the Patient entity (FR-006)
- [ ] T042 [US1] Implement PatientGenerator in ED-DES/src/UrgentCare.Core/Generation/PatientGenerator.cs sampling interarrival time, urgency, age, serviceDuration, and abandonmentThreshold via IDistributionFactory and IRandomSource
- [ ] T043 [US1] Implement the SimulationEngine loop (advance clock, pop and dispatch events, honor Idle/Running/Paused/Complete) in ED-DES/src/UrgentCare.Core/SimulationEngine.cs
- [ ] T044 [US1] Implement arrival handling (generate the next patient, enqueue via the active policy, schedule the following arrival) in ED-DES/src/UrgentCare.Core/SimulationEngine.cs
- [ ] T045 [US1] Implement resource assignment, treatment start, and treatment completion with Available/Busy transitions in ED-DES/src/UrgentCare.Core/SimulationEngine.cs
- [ ] T046 [US1] Implement the abandonment check that removes a waiting patient once waitingTime exceeds abandonmentThreshold in ED-DES/src/UrgentCare.Core/SimulationEngine.cs
- [ ] T047 [US1] Emit SimulationEvent telemetry to IEventSink at every arrival, treatment start, completion, abandonment, and resource state change in ED-DES/src/UrgentCare.Core/SimulationEngine.cs
- [ ] T048 [US1] Implement StatisticsCollector in ED-DES/src/UrgentCare.Core/Metrics/StatisticsCollector.cs deriving arrivals, completed, abandoned, averageWaitTime, throughput, and utilization from the event log rather than from ad hoc counters (FR-009)
- [ ] T049 [US1] Implement the console operational view showing queue state, patient locations, and resource status over time in ED-DES/src/UrgentCare.Cli/ConsoleOperationalView.cs (FR-008; the Unity view is deferred)
- [ ] T050 [US1] Implement the `--scenario <name>` CLI entry point loading from ED-DES/shared/config/ in ED-DES/src/UrgentCare.Cli/Program.cs and ED-DES/src/UrgentCare.Cli/CommandLineOptions.cs
- [ ] T051 [US1] Add explicit guards and messages for degenerate scenarios (no arrivals, no staffing, zero-capacity resources) in ED-DES/src/UrgentCare.Core/SimulationEngine.cs

**Checkpoint**: User Story 1 is fully functional and demoable on its own. This is the MVP.

---

## Phase 4: User Story 2 - Compare staffing and triage scenarios (Priority: P2)

**Goal**: Change staffing, queue policy, or arrival settings and compare outcomes against a baseline without touching engine code.

**Independent Test**: Alter one parameter, run the scenario, and compare waiting time, throughput, and utilization against the baseline run.

### Tests for User Story 2

- [ ] T052 [P] [US2] Integration test comparing two staffing levels and asserting comparable reported metrics (AS1) in ED-DES/tests/integration/UrgentCare.Integration.Tests/StaffingComparisonTests.cs
- [ ] T053 [P] [US2] Integration test asserting treatment order changes when the policy switches from FIFO to UrgencyFirst under a fixed seed (AS2) in ED-DES/tests/integration/UrgentCare.Integration.Tests/QueuePolicyComparisonTests.cs
- [ ] T054 [P] [US2] Integration test asserting different arrival patterns change queue length and abandonment (AS3) in ED-DES/tests/integration/UrgentCare.Integration.Tests/ArrivalPatternTests.cs
- [ ] T055 [P] [US2] Unit tests for FifoPolicy and UrgencyPlusWaitPolicy ordering in ED-DES/tests/unit/UrgentCare.Core.Tests/QueuePolicyTests.cs

### Implementation for User Story 2

- [ ] T056 [P] [US2] Implement FifoPolicy in ED-DES/src/UrgentCare.Core/Queues/FifoPolicy.cs
- [ ] T057 [P] [US2] Implement UrgencyPlusWaitPolicy in ED-DES/src/UrgentCare.Core/Queues/UrgencyPlusWaitPolicy.cs
- [ ] T058 [US2] Implement QueuePolicyFactory resolving the config queuePolicy string to a policy instance in ED-DES/src/UrgentCare.Core/Queues/QueuePolicyFactory.cs
- [ ] T059 [US2] Implement the non-stationary arrival time profile (hourly, daily, seasonal variation) in ED-DES/src/UrgentCare.Core/Generation/ArrivalModel.cs (FR-001)
- [ ] T060 [US2] Wire queuePolicy and arrivalModel selection from ScenarioConfig into engine construction in ED-DES/src/UrgentCare.Core/SimulationEngine.cs so behavior changes without code edits (FR-012)
- [ ] T061 [US2] Implement ScenarioRunner executing a batch of scenario configurations in ED-DES/analysis/UrgentCare.Analysis/Scenarios/ScenarioRunner.cs
- [ ] T062 [US2] Implement ScenarioComparison producing a baseline-versus-variant table of waiting time, throughput, abandonment, and utilization in ED-DES/analysis/UrgentCare.Analysis/Scenarios/ScenarioComparison.cs (FR-010)
- [ ] T063 [US2] Implement ResultExporter writing JSON and CSV with a schema stable across runs in ED-DES/analysis/UrgentCare.Analysis/Metrics/ResultExporter.cs
- [ ] T064 [US2] Add the `--compare <baseline> <variant>` CLI command in ED-DES/src/UrgentCare.Cli/Program.cs

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Review statistical outcomes and reproducibility (Priority: P3)

**Goal**: Run repeated replications under a fixed seed, aggregate results, and review scenario assumptions against observed outcomes.

**Independent Test**: Run several replications of one scenario, aggregate them, and compare average wait, P95 wait, throughput, and abandonment rate.

### Tests for User Story 3

- [ ] T065 [P] [US3] Integration test asserting a fixed seed and configuration reproduce identical results across runs (AS1) in ED-DES/tests/integration/UrgentCare.Integration.Tests/ReproducibilityTests.cs
- [ ] T066 [P] [US3] Integration test aggregating metrics across N replications (AS2) in ED-DES/tests/integration/UrgentCare.Integration.Tests/ReplicationAggregationTests.cs
- [ ] T067 [P] [US3] Unit tests for P95 and percentile edge cases (empty set, single sample) in ED-DES/tests/unit/UrgentCare.Core.Tests/PercentileCalculatorTests.cs
- [ ] T068 [P] [US3] Unit test asserting the per-replication seed sequence is deterministic and non-repeating in ED-DES/tests/unit/UrgentCare.Core.Tests/SeedSequenceTests.cs

### Implementation for User Story 3

- [ ] T069 [US3] Implement PercentileCalculator in ED-DES/analysis/UrgentCare.Analysis/Metrics/PercentileCalculator.cs
- [ ] T070 [US3] Add p95WaitTime and the per-resource utilization map to ED-DES/src/UrgentCare.Core/Metrics/StatisticsCollector.cs per data-model.md
- [ ] T071 [US3] Implement deterministic per-replication seed derivation in ED-DES/analysis/UrgentCare.Analysis/Scenarios/SeedSequence.cs
- [ ] T072 [US3] Implement ReplicationRunner executing N replications of one scenario in ED-DES/analysis/UrgentCare.Analysis/Scenarios/ReplicationRunner.cs (FR-011)
- [ ] T073 [US3] Implement MetricsAggregator computing mean, P95, standard deviation, and confidence interval across replications in ED-DES/analysis/UrgentCare.Analysis/Metrics/MetricsAggregator.cs
- [ ] T074 [US3] Implement RunManifest recording scenario assumptions alongside observed outcomes for review (AS3) in ED-DES/analysis/UrgentCare.Analysis/Scenarios/RunManifest.cs
- [ ] T075 [US3] Extend ResultExporter with an aggregated multi-run summary export in ED-DES/analysis/UrgentCare.Analysis/Metrics/ResultExporter.cs
- [ ] T076 [US3] Add the `--replications <n>` CLI option in ED-DES/src/UrgentCare.Cli/Program.cs and ED-DES/src/UrgentCare.Cli/CommandLineOptions.cs

**Checkpoint**: All three user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T077 [P] Rewrite ED-DES/README.md in English with a Stack section (C#, .NET 10, Unity optional, VS Code) and a short project description
- [ ] T078 [P] Add ED-DES/unity/README.md recording that the Unity presentation layer is deferred and must not own simulation state (research.md decision)
- [ ] T079 [P] Document the full command set in one place in specs/001-urgent-care-simulator/quickstart.md so local commands match CI (Constitution Principle X)
- [ ] T080 Add structured logging configuration bridging IEventSink to console and file output in ED-DES/src/UrgentCare.Cli/Program.cs
- [ ] T081 Verify a multi-week time horizon completes in accelerated time and record the timing in specs/001-urgent-care-simulator/quickstart.md
- [ ] T082 Verify every namespace, type, member, and comment across ED-DES/src/, ED-DES/analysis/, and ED-DES/tests/ is written in English
- [ ] T083 Run the full quickstart.md validation workflow end to end and fix any drift between documented and actual commands
- [ ] T084 Run `dotnet test ED-DES/UrgentCare.sln` and confirm all unit and integration tests pass
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

### Parallel Opportunities

- Setup: T002 to T006 and T008 to T010 in parallel
- Foundational: T011, T012, T014, T016, T019, T020, T022, T023 in parallel; tests T024 and T025 in parallel
- US1: all seven test tasks T026 to T032 in parallel; entity tasks T033, T034, T036, T037, T039, T041 in parallel
- US2: tests T052 to T055 in parallel; policies T056 and T057 in parallel
- US3: tests T065 to T068 in parallel
- Polish: T077 to T079 in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all User Story 1 tests together:
Task: "Integration test arrival through completion in ED-DES/tests/integration/UrgentCare.Integration.Tests/EndToEndFlowTests.cs"
Task: "Integration test urgency priority in ED-DES/tests/integration/UrgentCare.Integration.Tests/UrgencyPriorityTests.cs"
Task: "Integration test abandonment threshold in ED-DES/tests/integration/UrgentCare.Integration.Tests/AbandonmentTests.cs"
Task: "Unit tests for SimulationClock in ED-DES/tests/unit/UrgentCare.Core.Tests/SimulationClockTests.cs"

# Then launch the independent entities together:
Task: "Implement Patient in ED-DES/src/UrgentCare.Core/Domain/Patient.cs"
Task: "Implement CareResource in ED-DES/src/UrgentCare.Core/Domain/CareResource.cs"
Task: "Implement SimulationClock in ED-DES/src/UrgentCare.Core/Clock/SimulationClock.cs"
Task: "Define IQueuePolicy in ED-DES/src/UrgentCare.Core/Queues/IQueuePolicy.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1: Setup (T001 to T010)
2. Complete Phase 2: Foundational (T011 to T025), which blocks everything
3. Complete Phase 3: User Story 1 (T026 to T051)
4. **STOP and VALIDATE**: run the quickstart scenario, confirm patients flow from arrival through treatment to completion or abandonment, and confirm queue length responds to staffing changes
5. Demo. This satisfies SC-001 and SC-005.

### Incremental Delivery

1. Setup and Foundational, giving contracts, determinism, and the event stream
2. Add US1, validate, demo (MVP)
3. Add US2, validate scenario comparison, demo (satisfies SC-002 and SC-003)
4. Add US3, validate reproducibility and aggregation, demo (satisfies SC-004)
5. Polish: docs, performance, and the constitution re-check
