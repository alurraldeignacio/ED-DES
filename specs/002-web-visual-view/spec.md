# Feature Specification: Web Visual View

**Feature Branch**: `002-web-visual-view`

**Created**: 2026-11-03

**Status**: Draft

**Input**: User description: "Web visual view for the urgent-care simulator. A browser-based operational view, built with Blazor WebAssembly (C# only, per constitution v2.1.0 — no JavaScript/Node toolchain), that runs the existing UrgentCare.Core engine live in the browser and animates the department: patients arriving, waiting in the queue ordered by the active queue policy, being seen by nurses in rooms, completing, or abandoning. The user picks a bundled scenario (default, lean), can set the seed, play/pause, step one event, change playback speed, and restart. Running summary metrics (arrivals, completed, abandoned, average and P95 wait, utilization, queue length) update as the run progresses and must match the CLI headless output for the same scenario and seed. The view is a presentation layer only: it must not own simulation state or business rules (research.md decision previously applied to Unity). Fully free to run: no accounts, no paid tools. Replaces the deferred Unity visual layer as the project's visual front end; Unity remains optional/deferred."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Watch a simulation run in the browser (Priority: P1)

A user opens the visual view in a web browser, picks one of the bundled scenarios, and presses play. They see the urgent-care department as a picture: patients arriving, waiting in line, being treated by care staff in treatment rooms, and leaving either as completed or as abandoned. Each patient's urgency is visible at a glance, and the waiting line is shown in the order the active queue policy will serve it.

**Why this priority**: This is the whole point of the feature. The existing console view shows the same flow as text; a picture of the department makes bottlenecks, queue build-up, and the effect of triage obvious to someone who is not reading numbers.

**Independent Test**: Open the view, choose the default scenario, press play, and watch patients move from arrival through the waiting line into treatment and out, with at least one abandonment visible during a week-long run.

**Acceptance Scenarios**:

1. **Given** the view is open and a scenario is selected, **When** the user presses play, **Then** simulated time advances and patients appear, wait, enter treatment, and leave as the simulation produces those events.
2. **Given** several patients are waiting, **When** the user looks at the waiting line, **Then** the patients are shown in the order the scenario's queue policy will serve them, and the most acute patients are visually distinguishable from less acute ones.
3. **Given** a patient's waiting time exceeds their tolerance, **When** the simulation removes them, **Then** the view shows that patient leaving as abandoned, distinct from a completed discharge.
4. **Given** care staff are treating patients, **When** the user looks at the staff and rooms, **Then** each staff member shows whether they are available, busy (and with which patient), or offline, and each room shows whether it is occupied.
5. **Given** an acute patient whose urgency qualifies for the room bypass, **When** they start treatment while all rooms are occupied, **Then** the view shows them in treatment without occupying a room.

---

### User Story 2 - Control the pace of the run (Priority: P2)

A user wants to slow down to watch a single decision, speed up to see a whole week, or stop and look. They can pause and resume, advance exactly one event at a time, change the playback speed while running, jump to the end of the run, and restart from the beginning.

**Why this priority**: Without pace control, a week-long run is either too fast to follow or too slow to finish. Stepping one event at a time is what makes the triage and abandonment rules inspectable.

**Independent Test**: Start a run, pause it, step three times and confirm each step processes one simulation event (the clock moves to that event's time), change speed and confirm simulated time advances faster, jump to the end, then restart and confirm the department is empty again.

**Acceptance Scenarios**:

1. **Given** a run is playing, **When** the user pauses, **Then** simulated time stops and the department picture stays unchanged until the user resumes or steps.
2. **Given** a run is paused, **When** the user steps once, **Then** exactly one simulation event is processed and the picture and metrics reflect that event only. One event can change nothing visible (a scheduled patience check for a patient who has already been seen) or move two patients together (a treatment ends and the freed nurse immediately takes the next patient).
3. **Given** a run is playing, **When** the user changes the playback speed, **Then** simulated time advances at the new rate without restarting the run or changing its outcome.
4. **Given** a run is in progress, **When** the user jumps to the end, **Then** the run completes and the final metrics are shown.
5. **Given** a run is in progress or complete, **When** the user restarts, **Then** the run starts again from time zero with the same scenario and seed and produces the same sequence of events.

---

### User Story 3 - Read live metrics and choose scenario and seed (Priority: P3)

A user wants to see the run's numbers next to the picture, change the random seed or switch between the bundled scenarios, and trust that the view tells the same story as the command-line tool.

**Why this priority**: The picture is persuasive, but the numbers are what get compared. Matching the command-line results exactly lets a user move between the two tools without wondering which one is right.

**Independent Test**: Run the default scenario with seed 12345 to the end in the view and compare every summary metric with the command-line headless output for the same scenario and seed; they match.

**Acceptance Scenarios**:

1. **Given** a run is progressing, **When** events are processed, **Then** the summary metrics (arrivals, completed, abandoned, abandonment rate, average wait, 95th-percentile wait, utilization, throughput, current, average and peak queue length) update to reflect the run so far.
2. **Given** a scenario and seed, **When** the run completes in the view, **Then** every summary metric equals the command-line headless result for the same scenario and seed.
3. **Given** the user selects a different bundled scenario or enters a different seed, **When** they apply the change, **Then** the run restarts with the new scenario or seed and the scenario's key settings (horizon, staffing, rooms, room-bypass threshold, queue policy) are displayed.

---

### Edge Cases

- What happens when the user enters a seed that is not a whole number or is out of range? The view rejects it with a clear message and keeps the previous seed.
- What happens when the waiting line is far longer than the space available to draw it? The view shows as many patients as fit, in serve order, plus a count of the rest; the queue-length metric still reports the true number.
- What happens at very high playback speeds where many events fall within one screen refresh? The view processes all of them and draws the resulting state; no event is skipped by the simulation, only intermediate pictures are.
- What happens when the run reaches its time horizon? Playback stops, the run is marked complete, and patients still waiting or in treatment remain visible as in-system at the end.
- What happens if the browser tab is hidden while playing? Playback does not try to catch up with a burst of events when the tab returns; it continues from where it was.
- What happens when a staff member is offline? The view shows them as offline and not taking patients. No current engine path puts a resource offline (the state exists in the domain model but nothing sets it), so this display cannot be exercised with the bundled scenarios; it is drawn from the engine's reported state and needs no change when the engine starts using it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The view MUST run in a standard desktop web browser without the user installing anything beyond what is needed to build and serve the project locally, and without accounts, licences, or paid services.
- **FR-002**: The view MUST drive the project's existing simulation engine, so that the business rules, simulation state, and metrics come from the same model the command-line tool uses. The view MUST NOT re-implement or alter any simulation rule.
- **FR-003**: The view MUST offer the bundled scenarios (at least `default` and `lean`) for selection and display each selected scenario's key settings: time horizon, staffing, room count, room-bypass threshold, and queue policy.
- **FR-004**: The user MUST be able to set the random seed; a run with a given scenario and seed MUST produce the same sequence of events every time.
- **FR-005**: The view MUST show the department as distinct areas for arrival, the waiting line, treatment (staff and rooms), and exits for completed and abandoned patients, and MUST show each patient's location as the run progresses.
- **FR-006**: The view MUST show the waiting line in the order the active queue policy will serve it.
- **FR-007**: The view MUST make each patient's urgency (ESI 1 to 5) visually distinguishable, and MUST NOT rely on colour alone to do so.
- **FR-008**: The view MUST show each care staff member's state as the engine reports it (available, busy, or offline; offline is not produced by the current engine, see Edge Cases) and, when busy, which patient they are treating; and MUST show room occupancy, including patients treated under the room bypass.
- **FR-009**: The user MUST be able to play, pause, step one event, change playback speed during a run, jump to the end, and restart.
- **FR-010**: Playback speed MUST be expressed in simulated time per real second and MUST NOT change the run's outcome.
- **FR-011**: The view MUST display and continuously update the summary metrics: arrivals, completed, abandoned, abandonment rate, average wait, 95th-percentile wait, utilization, throughput, and current, average, and peak queue length, together with the current simulated time.
- **FR-012**: For any bundled scenario and seed, the final summary metrics shown in the view MUST equal those reported by the command-line headless run for the same scenario and seed.
- **FR-013**: When the waiting line exceeds the drawable space, the view MUST show the first patients in serve order and an explicit count of the patients not drawn.
- **FR-014**: The project documentation MUST record that this view is the project's visual front end and that the Unity presentation layer remains optional and deferred.

### Key Entities

- **Visual Session**: One run as seen in the view: the selected scenario, the seed, the playback state (idle, playing, paused, complete), and the playback speed. It holds no copy of simulation state; it owns the run and always asks the simulation for state.
- **Department Picture**: The drawn representation of the department at the current simulated time: arrival area, waiting line in serve order, staff and rooms with their occupants, and exit counts. It is derived entirely from the simulation's state and event stream.
- **Bundled Scenario**: A scenario configuration shipped with the project (`default`, `lean`) that the view can load without file access from the user.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with the project checked out can open the visual view and watch a run start within 2 minutes, using one documented command and no accounts or paid tools.
- **SC-002**: For both bundled scenarios with the default seed, 100% of the final summary metrics shown in the view match the command-line headless output.
- **SC-003**: A full week-long run of the default scenario can be watched end to end in under 2 minutes at the fastest playback speed, and in under 1 second with jump-to-end.
- **SC-004**: When stepping one event at a time, each step applies exactly one simulation event to the picture and metrics (which may change nothing visible, or move a departing and a starting patient together), so a user can verify a triage or abandonment decision by eye.
- **SC-005**: Restarting a run with the same scenario and seed reproduces the same event sequence in 100% of attempts.
- **SC-006**: A first-time user can identify, without explanation, which patients are waiting, which are being treated, and which left without being seen.

## Assumptions

- The audience is the same as the simulator's: learners and planners exploring urgent-care flow, using a desktop browser. Mobile layout is a nice-to-have, not a requirement.
- The view runs locally (served from the developer's machine). Public hosting is not required, though nothing should prevent publishing it as static files later.
- Scenario editing in the browser (changing staffing, rooms, policy, or arrival rates through form controls) is out of scope for this feature; users switch between bundled scenarios and seeds. Editing remains possible through the scenario files and the command-line tool.
- Scenario comparison and multi-replication aggregation stay in the command-line tool; the view shows one run at a time.
- "Matches the command-line output" means the same values at the precision the command line prints them.
- The existing console operational view stays as it is; this feature adds a second view and does not replace the console view.
- The constitution's single-language rule (C# on .NET) applies; the feature does not introduce a JavaScript or Node toolchain.

## Deferred Scope

- **Editing scenario parameters in the browser** (staffing, rooms, queue policy, arrival rates), with validation feedback.
- **Side-by-side comparison** of two scenarios animated together.
- **Unity presentation layer**, which remains optional and deferred as recorded in `unity/README.md`.
