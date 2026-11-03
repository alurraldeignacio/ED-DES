# Research: Web Visual View

Each entry records a decision, why it was made, and what else was considered.

## 1. UI technology: Blazor WebAssembly

- **Decision**: Build the view as a Blazor WebAssembly app in C#.
- **Rationale**: Constitution v2.1.0 limits the stack to C# on .NET and forbids adding a JavaScript or Node toolchain without an amendment. Blazor WebAssembly runs .NET in the browser, so the existing `UrgentCare.Core` engine and `UrgentCare.Analysis` distribution factory load unchanged: both are plain `net10.0` libraries with no file, thread, or native dependencies on the simulation path. It is free, ships with the .NET SDK already installed, and needs no workload for a non-AOT build.
- **Alternatives considered**:
  - *Hand-written HTML/JavaScript replaying an exported event file*: violates the single-language rule, and could not rerun with a new seed.
  - *Blazor Server*: adds a server process and a SignalR connection (Principle I) for no gain; the engine is cheap enough to run in the browser.
  - *Unity*: needs an account, a large install, and a `netstandard2.1` interop layer (see `unity/README.md`). Remains deferred.
  - *Desktop UI (WPF/WinForms/MAUI)*: Windows-bound or heavier to set up; a browser was the user's choice.

## 2. Engine runs live in the browser, not replayed from a file

- **Decision**: The view constructs a `SimulationEngine` with `SimulationEngine.Create`, calls `Begin()`, then drives it with `RunOneEvent()`.
- **Rationale**: These entry points already exist for exactly this purpose (the console view uses them). Running live allows restart with a different seed or scenario from the page, and makes CLI parity a property of shared code rather than of a file format.
- **Alternatives considered**: Replaying `--events` JSON Lines output. Rejected by the user in favour of the live engine; it would also need a second step outside the browser.

## 3. Pacing playback by simulated time needs the next event's time

- **Decision**: Add a read-only `double? NextEventTime` property to `SimulationEngine`, returning the time of the next pending event if it falls within the horizon, otherwise null.
- **Rationale**: FR-010 expresses speed as simulated minutes per real second. Each frame the session computes a target time (`clock + speed × elapsed`) and processes events while `NextEventTime <= target`, then shows the clock at the target. Without a peek, the session could only run "one more event" and the clock would jump past the target. The property wraps the existing `FutureEventList.TryPeek` and the existing horizon check, so no scheduling rule moves into the view.
- **Alternatives considered**: Running events until `Clock.CurrentTime >= target` (overshoots by one event, clock display jumps, step-by-time becomes uneven); reading the event list via reflection (hidden coupling, Principle III).

## 4. Room occupancy needs the bypass rule from the engine

- **Decision**: Make `Department.BypassesRooms(Patient)` public.
- **Rationale**: The department tracks only a count of occupied rooms. To draw which treated patients are in a room and which were treated under the acuity bypass (spec US1 scenario 5), the view needs the same rule the engine applies. Exposing the existing private method keeps a single source of truth; copying `RoomBypassUrgency > 0 && Urgency <= RoomBypassUrgency` into the view would duplicate a business rule (FR-002).
- **Alternatives considered**: Tracking room assignment in Core (adds state to the domain purely for presentation); duplicating the rule in the view (rejected above).

## 5. Showing the waiting line in serve order

- **Decision**: The snapshot lists waiting patients by calling the active `IQueuePolicy.SelectNext` repeatedly on a shrinking copy of the waiting area at the current time.
- **Rationale**: `IQueuePolicy` is documented as read-only and deterministic, so repeated selection yields the policy's own total order, including UrgencyPlusWait's time-dependent escalation and protected tier, with no ordering logic in the view. Cost is O(n²) per frame; at the observed peak of 26 waiting it is negligible, and the snapshot only orders the patients it will draw plus a count.
- **Caveat recorded in the UI**: the engine may skip a room-blocked patient for a bypass-eligible one (`SelectAdmissiblePatient`). The line shows policy order; the room-blocked case is visible because the patient stays in line while a bypass patient moves to treatment.
- **Alternatives considered**: Adding an `Order` method to `IQueuePolicy` (changes the interface and all three policies for one consumer); sorting by urgency in the view (wrong for FIFO and UrgencyPlusWait, and a duplicated rule).

## 6. Running metrics mid-run

- **Decision**: After each frame, call `StatisticsCollector.Collect(log.Events, elapsed, resourceCount)` where `elapsed` is the current simulated time; at completion, call it with the scenario's `TimeHorizon`, exactly as the CLI does.
- **Rationale**: One metric implementation for both hosts (Principle VI), and the final call is literally the CLI's call, giving FR-012 by construction. A full recompute over about 5,000 events per frame is well under a millisecond.
- **Known effect, labelled in the UI as "so far"**: mid-run utilization counts only completed treatments, so it reads slightly low while treatments are in progress. This matches how the CLI treats treatments still running at the horizon, so the final figure agrees.
- **Alternatives considered**: An incremental collector (a second metrics implementation that could drift from the CLI's).

## 7. Shipping the scenario files to the browser

- **Decision**: Link `shared/config/*-scenario.json` into the web project as embedded resources and parse them with `ScenarioConfigLoader.Parse`, then `ScenarioConfigValidator.ValidateOrThrow`.
- **Rationale**: The same files the CLI loads, with no copy to drift; no HTTP fetch, so loading is synchronous and works from a static host without configuration. Adding a scenario file to `shared/config/` makes it appear in the dropdown.
- **Alternatives considered**: Copying the files into `wwwroot` and fetching them with `HttpClient` (async start-up and a second copy).

## 8. Trimming and System.Text.Json on publish

- **Decision**: In the web project set `<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>` and root `UrgentCare.Core` with `<TrimmerRootAssembly Include="UrgentCare.Core" />`.
- **Rationale**: `dotnet publish` trims Blazor WebAssembly apps, and trimmed apps disable reflection-based JSON by default, which `ScenarioConfigLoader` relies on. Rooting Core keeps the configuration types intact. `dotnet run` (Debug) does not trim, so the everyday command is unaffected either way.
- **Alternatives considered**: A source-generated `JsonSerializerContext` in Core (touches the CLI's loader for a web-only concern).

## 9. Animation without JavaScript

- **Decision**: Draw the department with absolutely positioned HTML elements; each patient is one element keyed by patient id (`@key`), placed by a CSS `transform`, with a CSS `transition` on `transform`. Moving a patient from the line to a nurse is just a new position on the next render.
- **Rationale**: Gives smooth movement with plain CSS, no canvas, no JS interop. Elements keyed by id keep their identity across renders, so the browser animates the move.
- **Alternatives considered**: Inline SVG (similar, but positioning text and tokens is more verbose); `<canvas>` (requires JS interop to draw).

## 10. Frame loop, hidden tabs, and step-by-event

- **Decision**: A `PeriodicTimer` at 50 ms drives frames while playing. Elapsed real time per frame is measured with a `Stopwatch` and capped at 250 ms. Step processes exactly one `RunOneEvent()` and sets the displayed clock to the engine clock. Jump-to-end runs `RunOneEvent()` until it returns false.
- **Rationale**: The cap satisfies the hidden-tab edge case: when the browser throttles timers and the tab returns, playback resumes from where it was instead of bursting through hours of events. WebAssembly is single-threaded, so the timer runs on the UI loop and no locking is needed.
- **Speed presets** (simulated minutes per real second): 1, 10, 60, 240, 1440. At 1440 the one-week default run plays in about 7 seconds, inside SC-003's 2 minutes.

## 11. Urgency must not rely on colour alone (FR-007)

- **Decision**: Each patient token shows its ESI number (1 to 5) as text and uses a colour scale from red (1) to blue (5); ESI 1 and 2 tokens also carry a heavier border.
- **Rationale**: Meets FR-007 and remains readable for colour-blind users and in greyscale.

## 12. Metric formatting and parity

- **Decision**: The view formats metrics with the same format strings the CLI uses (`0.0`, `P1`, `0.00`) under the browser's culture; the parity test compares the `SimulationMetrics` records numerically, not the printed strings.
- **Rationale**: The CLI prints culture-dependent decimals (`37,3` on a Spanish-locale machine). Comparing the underlying values is the meaningful check and is culture-independent; using the same format strings keeps the visible precision identical.
