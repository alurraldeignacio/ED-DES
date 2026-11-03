# Feature Specification: Hospital Urgent Care Queue Simulator

**Feature Branch**: `001-urgent-care-simulator`

**Created**: 2026-09-01

**Status**: Ready

**Input**: User description: "Hospital Urgent Care Queue Simulator"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a live urgent-care simulation (Priority: P1)

A user wants to launch a simulation of an urgent-care department and observe how patients move through arrival, waiting, assessment, treatment, and discharge. The user needs to see queue pressure, patient waiting times, and staff utilization in real time or accelerated time.

**Why this priority**: This is the core product outcome. If the system cannot model and display patient flow and operational bottlenecks, the simulator does not deliver its core educational or planning value.

**Independent Test**: A user can start a run, watch patients enter and leave the system, and confirm that queue length and waiting time change as patient and staffing conditions vary.

**Acceptance Scenarios**:

1. **Given** a configured urgent-care department with at least one waiting queue and one care resource, **When** the simulation starts, **Then** patients appear over time and move through the system according to the configured arrival and treatment rules.
2. **Given** a patient enters the queue with a more severe triage acuity than other patients (a lower urgency value on the 1-to-5 ESI scale), **When** the triage policy prioritizes urgency, **Then** that patient is treated before less acute patients already waiting.
3. **Given** a patient waits longer than their tolerance threshold, **When** the queue reaches the abandonment condition, **Then** the patient is removed from the queue and counted as abandoned.

---

### User Story 2 - Compare staffing and triage scenarios (Priority: P2)

A user wants to change staffing levels, queue rules, or patient arrival settings and compare how those changes affect care flow. They want to answer operational questions such as whether adding another clinician reduces congestion or whether a different triage policy helps lower-urgency patients.

**Why this priority**: Decision support is the main value beyond basic simulation. Scenario comparison makes the system useful for learning and operational experimentation.

**Independent Test**: A user can alter a single parameter, run a scenario, and compare key outcomes against a baseline without changing the underlying simulation engine.

**Acceptance Scenarios**:

1. **Given** two scenario configurations with different staffing levels, **When** each run completes, **Then** the system reports results for waiting time, throughput, and utilization in a comparable way.
2. **Given** a queue policy is changed from simple FIFO to urgency-based priority, **When** the simulation runs, **Then** the distribution of treatment order reflects the new policy and metrics update accordingly.
3. **Given** multiple patient arrival patterns, **When** the user runs the scenario, **Then** the system shows how demand changes affect queue length and abandonment.

---

### User Story 3 - Review statistical outcomes and reproducibility (Priority: P3)

A user wants to run repeated simulations with the same inputs, compare aggregated results, and understand the effect of stochastic variation. This supports learning, research, and evidence-based experimentation without relying on a single run.

**Why this priority**: Reproducibility and statistical analysis make the simulator useful for repeated experiments and robust planning, but they are not required to achieve the initial product value.

**Independent Test**: A user can run several scenarios under controlled settings, aggregate the results, and compare averages, percentiles, and rates such as abandonment or throughput.

**Acceptance Scenarios**:

1. **Given** a fixed random seed and configuration, **When** the simulation is run multiple times, **Then** each run produces consistent results allowing a comparison of stochastic variation.
2. **Given** aggregated metrics from multiple runs, **When** the user reviews the results, **Then** they can compare average waiting time, P95 waiting time, throughput, and abandonment rate across scenarios.
3. **Given** a scenario is configured with a known set of parameters, **When** the system records results, **Then** the user can review the scenario assumptions and compare them against observed outcomes.

---

### Edge Cases

- What happens when the system has more patients than available resources for extended periods?
- How does the system handle a patient whose waiting time exceeds their tolerance threshold while still in queue?
- What happens when a scenario includes no arrivals, no staffing, or zero treatment rooms with the acuity bypass disabled?
- How does the system handle invalid or incomplete configuration values such as missing arrival rates or negative staffing counts?
- What happens when multiple queue policies are compared using the same patient population and random seed?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST model patient arrivals over simulated time using configurable arrival patterns and non-stationary demand assumptions.
- **FR-002**: The system MUST represent each patient with attributes including urgency, age, arrival time, waiting time, treatment duration, abandonment threshold, and current state.
- **FR-003**: The system MUST allow patients to transition through waiting, treatment, completion, and abandonment states during a simulation run.
- **FR-004**: The system MUST model one or more care resources, each treating at most one patient at a time, with configurable efficiency and with fatigue or productivity changes over time. Concurrency is expressed by the number of resources, not by a per-resource capacity.
- **FR-005**: The system MUST support at least one queueing policy that prioritizes patient urgency, ordering by ascending urgency value on the ESI scale where 1 is most severe, and allows future queue policies to be introduced without changing patient entities.
- **FR-006**: The system MUST separate patient generation and statistical distribution logic from the patient entity so sampled attributes are generated by a dedicated model.
- **FR-007**: The system MUST provide a simulation clock that operates independently from real-time UI timing and supports start, pause, resume, reset, and speed adjustments.
- **FR-008**: The system MUST provide an operational view of the urgent-care environment showing patient locations, queue state, care resources, and flow over time.
- **FR-009**: The system MUST calculate and report patient, queue, resource, and system metrics throughout a simulation run.
- **FR-010**: The system MUST support scenario comparison by allowing users to change operational settings and compare outcomes across multiple runs.
- **FR-011**: The system MUST provide reproducible results through configurable random seeds and the ability to aggregate results across repeated runs.
- **FR-012**: The system MUST allow the user to configure department capacity (staffing count and room count), patient characteristics, distribution parameters, arrival assumptions, queue policy, and care-resource settings without editing the simulation logic.

### Key Entities

- **Patient**: An individual simulated entity that arrives, waits, receives care, and exits the system, with urgency, age, and behavioral timing assumptions. Urgency follows the ESI convention: 1 is most severe, 5 is least severe.
- **Care Resource**: A clinician or treatment resource that serves one patient at a time, with efficiency and time-dependent performance characteristics.
- **Department**: The operational environment that contains queues, staff, rooms, and patient flow rules.
- **Patient Generator**: The model responsible for generating arrival timing and patient attribute values according to configured distributions.
- **Simulation**: The orchestration layer that advances time, schedules events, and enforces operational rules.
- **Simulation Clock**: The time model that tracks the current simulated time and supports acceleration or pause behavior.
- **Queue Policy**: The policy that determines prioritization and ordering within patient queues.
- **Statistics Collector**: The component that records performance metrics across the simulation run and supports scenario comparison.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can start a simulation and observe patient flow through the system without requiring manual intervention for each event.
- **SC-002**: The system provides metrics that allow users to compare queue length, waiting time, abandonment, throughput, and utilization across scenarios.
- **SC-003**: The simulator supports scenario comparison such that a change in staffing or triage policy produces measurable differences in patient flow outcomes.
- **SC-004**: Repeated runs under the same configuration can be compared to understand variation and identify plausible operating ranges.
- **SC-005**: The minimum viable simulation demonstrates a complete path from patient arrival to completion or abandonment for a single urgent-care queue and a single care resource.

## Assumptions

- The initial scope includes one urgent-care environment, one or more queues, and a small set of care resources rather than a full hospital system.
- The project will focus first on learning and experimentation, with a later path toward richer operational modeling if the simulation proves valuable.
- Patient arrival and service-time processes are assumed to be stochastic and configurable, even when empirical data is not yet available.
- The simulation will treat operational assumptions as explicit configuration rather than hidden constants.
- Triage acuity follows the ESI convention (1 most severe, 5 least severe), so a lower urgency value means a higher treatment priority.
- Patients whose urgency value is at or below the configured `roomBypassUrgency` (that is, the most acute) bypass the treatment-room constraint, as a real department holds or clears a bay for a critical arrival. Triage itself is never room-constrained.
- Under the UrgencyPlusWait policy, ESI 1 and 2 patients are never overtaken by a longer-waiting less acute patient. Waiting time escalates a patient only within the ESI 3 to 5 band. A resuscitation or emergent case cannot wait, so a policy that let accrued waiting time promote anyone past them would not be clinically usable.
- This first version is intentionally limited to core flow logic and metrics and does not require hospital-grade clinical fidelity.

## Deferred Scope

Concepts accepted as valuable but deliberately out of scope for this release. They are recorded here so they are not rediscovered as gaps during implementation.

- **A more realistic abandonment model** (low priority). Two limitations of the current model are known and accepted for now. First, patience is drawn from one distribution for every patient, so tolerance is uncorrelated with triage acuity; the observed concentration of abandonment in ESI 4 and 5 is emergent from waiting time rather than modelled directly. Second, abandonment is a hard deadline fixed at arrival rather than a hazard process, so it cannot represent a patient who leaves early because the waiting room looked full. A future model would give patience a per-acuity distribution and re-express leaving as a time-varying hazard influenced by observable queue state.

- **Patient care load and department capacity pool** (low priority). Each patient would carry a `careLoad` value sampled from a distribution conditioned on case complexity or urgency, and the department would hold a finite total load capacity. A patient would enter treatment only when a care resource is free *and* the department has enough remaining capacity for that patient's load. This would add an intensity-based congestion mechanism alongside the existing headcount and room-occupancy constraints, in which case complexity affects only `serviceDuration`.
