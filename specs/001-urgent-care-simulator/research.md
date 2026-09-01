# Research: Hospital Urgent Care Queue Simulator

## Decision: Keep the entire stack in C# / .NET

**Decision**: Model the hospital workflow in a single C# simulation engine responsible for state transitions, queue logic, staffing, and metrics collection. Keep distribution fitting, scenario generation, and export analysis in C# as well, isolated in a dedicated .NET analysis library rather than a second language runtime.

**Rationale**: The feature focuses on discrete-event behavior and object-oriented patient/resource modeling. A central C# engine preserves explicit ownership and reduces hidden coupling between simulation logic and UI code. This aligns with the constitution’s “do not distribute by default” and “make dependencies explicit” principles.

**Alternatives considered**:
- Adding a Python analytical backend: rejected because a second language runtime doubles the toolchain, build, and test surface for statistical work that MathNet.Numerics already covers in .NET.
- Fully distributed service architecture: rejected because the problem is not yet large enough to justify inter-process coordination, queueing, or multi-service management overhead.
- Unity-first simulation engine: rejected because the visual layer should remain presentation-focused rather than owning business logic.

## Decision: Separate patient generation from patient state

**Decision**: Create a patient generator and distribution model that produces patient attributes and arrival timing outside the patient entity itself.

**Rationale**: The specification explicitly requires distributions and scenario parameters to be configurable and independent of entity state. This keeps the domain model clean and allows later changes to arrival models without rewriting patient definitions.

**Alternatives considered**:
- Embedding distribution logic into Patient: rejected because it creates hidden coupling and makes parameter changes harder to test.
- Hard-coding distributions as global constants: rejected because it prevents scenario experimentation and reproducibility.

## Decision: Use explicit queue-policy abstraction

**Decision**: Define a queue policy interface with implementations such as FIFO, urgency-first, and urgency-plus-waiting-time strategies.

**Rationale**: Queue behavior is mission-critical to ED flow and naturally varies by policy. A clear abstraction allows operational experiments without forcing patient logic to know which policy is active.

**Alternatives considered**:
- Inlining priority logic inside the hospital or patient model: rejected because it increases coupling and makes policy comparison harder.
- Single fixed priority policy: rejected because the feature explicitly requires future experimentation and policy variation.

## Decision: Keep metrics as first-class outputs from the simulation engine

**Decision**: Collect waiting times, queue lengths, abandonment, throughput, and utilization as structured metrics produced from simulation events.

**Rationale**: The constitution requires structured event and metrics derivation rather than UI-only reporting. A metrics layer also supports reproducibility, comparison runs, and scenario analysis.

**Alternatives considered**:
- Compute statistics only in the UI: rejected because it impedes headless simulation and scenario comparison.
- Record ad hoc logs only: rejected because it makes verification and aggregation harder.

## Decision: Treat Unity as a presentation layer, not a model authority

**Decision**: Unity or a desktop visual layer renders simulation state but does not own the business rules or maintain authoritative simulation state.

**Rationale**: This ensures the application can run in console or batch mode, supporting reproducible experiments without the visual layer. It also matches the requirement that the simulation must be executable independently of the UI.

**Alternatives considered**:
- Place state and logic in Unity scripts: rejected because it complicates testing and makes non-visual runs harder.
- Use the UI as the source of truth for simulation state: rejected because it creates hidden coupling and poor separation of concerns.
