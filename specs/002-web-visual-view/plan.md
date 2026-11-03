# Implementation Plan: Web Visual View

**Branch**: `002-web-visual-view` | **Date**: 2026-11-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-web-visual-view/spec.md`

## Summary

Add a browser-based operational view that runs the existing `UrgentCare.Core` engine live and draws the department: arrivals, the waiting line in serve order, nurses and rooms with their occupants, and completed and abandoned exits, with play, pause, step, speed, jump-to-end and restart controls and running metrics. The view is a Blazor WebAssembly app written in C#, so the constitution's single-language rule holds and no JavaScript or Node toolchain enters the repository. The engine runs in the browser in-process; the view asks it for state after each event and never holds simulation state of its own. Metrics come from the same `StatisticsCollector` the CLI uses, so the final figures match the headless CLI run for the same scenario and seed by construction, and a test proves it.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`), the same SDK the rest of the solution uses (10.0.301 installed).

**Primary Dependencies**: Blazor WebAssembly (`Microsoft.AspNetCore.Components.WebAssembly` and `...WebAssembly.DevServer`, MIT licensed, shipped with .NET); existing `UrgentCare.Core` and `UrgentCare.Analysis` (for `MathNetDistributionFactory`); MathNet.Numerics transitively. No new third-party packages.

**Storage**: None. The two bundled scenario files in `shared/config/` are compiled into the web assembly as embedded resources and parsed with the existing `ScenarioConfigLoader.Parse`.

**Testing**: xUnit, as elsewhere. A new `tests/unit/UrgentCare.Web.Tests` project covers the session and snapshot logic (pure C#, no browser) and the CLI-parity check. Component rendering is not unit-tested; it is verified by the quickstart walkthrough.

**Tooling**: VS Code; `dotnet run --project src/UrgentCare.Web` serves the app locally. A `run-web` VS Code task exposes the same command.

**Target Platform**: Current desktop browsers with WebAssembly support (Edge, Chrome, Firefox). Served locally by the .NET dev server; `dotnet publish` produces static files that any static host can serve.

**Project Type**: Single-page web application (static, client-only) added to the existing solution.

**Performance Goals**: Smooth playback at about 20 frames per second while the waiting line holds dozens of patients; jump-to-end of the one-week default scenario in under 1 second in the browser (SC-003); fastest playback finishes the week in under 2 minutes.

**Constraints**: Presentation only (FR-002): no simulation rule is re-implemented in the web project. Final metrics equal the CLI headless output (FR-012). No JavaScript authored in the repository; the only script is the framework's own `blazor.webassembly.js` loader. Free to run (FR-001).

**Scale/Scope**: One department, up to a few thousand patients per run, one run on screen at a time, two bundled scenarios.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Single-language stack (Additional Constraints)**: Pass. Blazor WebAssembly is C# on .NET; the UI is Razor components and CSS. No JavaScript, Node, or npm is added. CSS is a stylesheet, not a language runtime or toolchain.
- **Principle I, do not distribute by default**: Pass. No server, no API, no new process boundary. The engine runs in the same browser process as the view; the dev server only serves static files.
- **Principle II, optimize for deletion**: Pass. The web project is a leaf: nothing references it except its own test project. Deleting `src/UrgentCare.Web` and `tests/unit/UrgentCare.Web.Tests` removes the feature entirely. Session and snapshot logic stay in two small classes, no framework of view-models.
- **Principle III, make dependencies explicit**: Pass. The session receives the scenario, seed, and distribution factory through its constructor; components receive the session through Blazor's constructor-injected services. No static mutable state.
- **Principle IV, contract at the boundary**: Pass. The view consumes the engine through its public API and the versioned `SimulationEvent` stream; the scenario files keep their existing `schemaVersion` contract. The two small Core additions (see research.md) are public, documented, read-only members. The view's own contract is recorded, with a version (1.0.0) and the rule for bumping it, in [contracts/visual-session.md](./contracts/visual-session.md).
- **Principle V, test the transformation**: Pass. Unit tests cover the snapshot transformation (engine state to picture), playback pacing, and serve-order listing against the real engine and real policies; no domain mocking.
- **Principle VI, emit structured events, derive everything else**: Pass. Running metrics are recomputed from the event log with `StatisticsCollector`; the picture is derived from engine state after each event. The view keeps no counters.
- **Principle VII, recovery over prevention**: Pass. The feature is additive and switched off simply by not running the web project, which takes no code change and leaves the CLI and engine unchanged. The Core additions are read-only and do not change any existing behaviour, which the existing 280-test suite verifies.
- **Principle VIII, attention is finite**: N/A. Local tool, no alerting surface.
- **Principle IX, value at the user**: Exception, as for feature 001: "shipped" for a local learning tool means runnable from the documented command and verified by the quickstart walkthrough. Static publish is possible but not part of this feature.
- **Principle X, commands discoverable; local dev matches CI**: Pass. The web project joins `UrgentCare.sln`, so CI's existing `restore`, `build`, `test` commands cover it unchanged. The run command is added to the quickstart command table and to `.vscode/tasks.json`.

### Post-implementation re-check (T042)

Re-checked against `.specify/memory/constitution.md` after US1 to US3 and the polish tasks were built.

- **Single-language stack**: Pass. The web project is C# and Razor with one stylesheet. The only script on the page is the framework's own loader; no JavaScript was authored and no Node or npm is involved.
- **Principle I, do not distribute by default**: Pass. No server-side component. The app is static files: `dotnet run` serves them in development, and the Release publish runs from a plain static file server.
- **Principle II, optimize for deletion**: Pass, verified. Only `tests/unit/UrgentCare.Web.Tests` references `UrgentCare.Web`. Session logic is four small files (`BundledScenarios`, `VisualSession`, `DepartmentSnapshot`, `SeedInput`), and the UI is four components and one page.
- **Principle III, make dependencies explicit**: Pass, verified. The page receives `BundledScenarios` and `IDistributionFactory` by injection, and the session receives scenario, seed and factory through its constructor. The only static members are immutable: the frame interval and the speed presets.
- **Principle IV, contract at the boundary**: Pass. The T042 check of engine members used against the contract found two unlisted reads, `SimulationEngine.Patients` and `Patient.WaitingTime`. They are now listed, and the contract is bumped to 1.1.0 under its own versioning rule. The view calls no mutating member of the domain.
- **Principle V, test the transformation**: Pass. 47 new tests (327 in total), all against the real engine, real policies and the real distribution factory, with no domain mocks. `CliParityTests` proves FR-012 field by field for four scenario and seed pairs.
- **Principle VI, emit structured events, derive everything else**: Pass. Every displayed metric comes from `StatisticsCollector` over the event log, and the exit and arrival counts are read from the same log.
- **Principle VII, recovery over prevention**: Pass. The feature is off unless the web project is run. The Core additions are read-only, and the 280 pre-existing tests pass unchanged.
- **Principle VIII, attention is finite**: N/A.
- **Principle IX, value at the user**: Exception unchanged (see Complexity Tracking). The Release publish was verified on a static host, so hosting it later needs no code change.
- **Principle X, commands discoverable; local dev matches CI**: Pass. `run-web` is in `.vscode/tasks.json`, both quickstarts list the command, and CI builds and tests the web projects through `UrgentCare.sln`. That CI run passed on Linux for the `002-web-visual-view` push.

### Post-design re-check

Re-checked after writing research.md, data-model.md, and the contract. No new violations. The two Core changes (`Department.BypassesRooms` made public and `SimulationEngine.NextEventTime` added) expose existing logic rather than moving any rule into the view, which is exactly what Principle IV and FR-002 ask for.

## Project Structure

### Documentation (this feature)

```text
specs/002-web-visual-view/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── visual-session.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
├── UrgentCare.Core/                      # existing; two read-only additions
│   ├── SimulationEngine.cs               # + NextEventTime
│   └── Domain/Department.cs              # BypassesRooms made public
├── UrgentCare.Cli/                       # existing, unchanged
└── UrgentCare.Web/                       # NEW Blazor WebAssembly app
    ├── UrgentCare.Web.csproj             # embeds shared/config/*-scenario.json
    ├── Program.cs                        # host setup, registers services
    ├── App.razor / _Imports.razor
    ├── Session/
    │   ├── BundledScenarios.cs           # lists and parses embedded scenarios
    │   ├── VisualSession.cs              # owns engine + log, playback state, pacing
    │   └── DepartmentSnapshot.cs         # immutable picture derived from engine state
    ├── Pages/
    │   └── Home.razor                    # page layout, frame timer
    ├── Components/
    │   ├── ControlBar.razor              # scenario, seed, play/pause/step/speed/end/restart
    │   ├── DepartmentView.razor          # arrival, waiting line, staff, rooms, exits
    │   ├── PatientToken.razor            # one patient: ESI colour + number
    │   └── MetricsPanel.razor            # running metrics + scenario settings
    └── wwwroot/
        ├── index.html
        └── css/app.css

tests/
└── unit/
    └── UrgentCare.Web.Tests/             # NEW
        ├── DepartmentSnapshotTests.cs
        ├── VisualSessionTests.cs
        └── CliParityTests.cs
```

**Structure Decision**: One new Blazor WebAssembly project, `src/UrgentCare.Web`, beside the CLI as a second host for the same engine, and one new unit-test project. Session and snapshot logic live inside the web project rather than in a separate shared library: there is exactly one consumer, so a shared library would be an abstraction without a second use (Principle II). The test project references the web project directly.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle IX exception (shipped = runnable locally, not deployed) | The tool is a local learning and planning aid with no hosted users | Hosting it would add infrastructure and an account, contradicting FR-001's "no accounts" for no user benefit |
