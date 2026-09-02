# Implementation Plan: Hospital Urgent Care Queue Simulator

**Branch**: `001-urgent-care-simulator` | **Date**: 2026-09-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-urgent-care-simulator/spec.md`

## Summary

The feature delivers a desktop urgent-care simulation that models patient arrival, queueing, triage, treatment, abandonment, and operational metrics. The design separates the simulation engine from the visualization and analysis layers so scenario tuning, parameter studies, and future Unity-based rendering can evolve without entangling the core logic.

## Technical Context

**Language/Version**: C# / .NET 10 for the simulation core and all analytical work; Unity 2022 LTS or later for the optional visual scene layer.

**Primary Dependencies**: .NET desktop runtime, MathNet.Numerics for distribution and statistical work, optional Unity engine for visualization.

**Storage**: Local configuration files and in-memory simulation state for MVP; optional JSON/CSV exports for scenario runs and metrics.

**Testing**: xUnit for simulation logic, probability sampling, and analysis checks, plus integration tests around scenario results and queue rule behavior.

**Tooling**: Visual Studio Code with the C# Dev Kit as the primary editor; `dotnet` CLI for restore, build, test, and run.

**Target Platform**: Windows desktop application with optional future Unity runtime support; simulation engine remains platform-independent for headless execution.

**Project Type**: desktop-app with simulation engine, configuration layer, and optional visual front end.

**Performance Goals**: A 10080-minute (168-hour, one-week) horizon on the default scenario completes in under 10 seconds on the development machine, while preserving deterministic seeded behavior for repeatable runs. T081 verifies this budget and records the measured timing in quickstart.md.

**Constraints**: Keep core simulation logic independent from UI timing; preserve explicit boundaries between patient generation, staffing logic, queue policy, telemetry, and visualization.

**Scale/Scope**: MVP covers one urgent-care department, one or more queues, one or more care resources, and scenario comparison across staffing and triage settings.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Pass**: The design follows the governing constitution by keeping the simulation in a single deployable core and by avoiding hidden coupling between domain objects and UI logic.

**Pass**: The architecture uses explicit dependency boundaries for patient generation, queue policy, statistics, and visual state; no singleton-driven simulation state is assumed.

**Pass**: The project will test transformation logic at the simulation-model level and keep operational metrics derived from structured event or metrics data rather than ad hoc UI-only reporting.

**Pass**: Recovery and revertibility are supported by configuration-driven scenario resets, seeded runs, and versioned scenario data rather than by hard-coded assumptions.

**Pass**: Commands and scenario actions remain discoverable and reproducible through a single local workflow and a CI-aligned validation path. The VS Code tasks (T009), the GitHub Actions workflow (T009a), and quickstart.md all invoke the same `dotnet restore`, `dotnet build`, and `dotnet test` commands against UrgentCare.sln.

## Project Structure

### Documentation (this feature)

```text
specs/001-urgent-care-simulator/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
ED-DES/                              # git repository root; source, specs, and tooling all live here
├── UrgentCare.sln
├── src/
│   ├── UrgentCare.Core/             # simulation engine: domain, clock, events, queues, metrics
│   └── UrgentCare.Cli/              # console and headless runner, operational view
├── analysis/
│   └── UrgentCare.Analysis/         # probability, scenario, and aggregation library
│       ├── Distributions/
│       ├── Scenarios/
│       └── Metrics/
├── unity/                           # deferred this release; only README.md exists (T078)
├── shared/                          # configuration and contracts
│   ├── config/
│   └── schemas/
├── specs/001-urgent-care-simulator/ # feature documents
│   └── ...
└── tests/
    ├── unit/
    └── integration/
```

**Structure Decision**: The project starts from scratch as a new .NET 10 solution; no code, naming, or project file is carried over from earlier exploratory work. All identifiers, comments, and documentation are written in English. A single simulation core remains the default architecture. Probability-heavy and analytical work stays in .NET, isolated in a dedicated class library that the core and any front end can reference. Unity is treated as an optional presentation layer rather than the source of truth for the simulation model.

## Complexity Tracking

> **No constitution violations require justification**

**Recorded exception, Principle IX (value reaches the user)**: this release ships no packaging or distribution step. The simulator is a local learning and experimentation tool run from the repository with `dotnet run`, which is what "in the user's hands" means for this audience. A `dotnet publish` step is the path forward if the tool is ever distributed to users who do not have the source.

**Recorded exception, Principle VII (recovery and revertibility)**: the feature-flag gating and exercised-rollback clauses do not apply here. This is a local command-line tool with no deployment surface, no live traffic, and no persistent production state, so reverting means checking out an earlier commit and re-running. Recovery inside the tool is covered by seeded, configuration-driven runs that reproduce exactly.
