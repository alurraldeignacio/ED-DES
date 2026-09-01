<!--
Sync Impact Report:
- Version change: 2.0.0 -> 2.1.0
- Modified principles: none
- Modified sections: Additional Constraints — the pragmatic mixed-technology architecture (C#/Unity runtime plus a Python analytical backend) is replaced by a single-language stack limited to C#, .NET, Unity, and Visual Studio Code. Adding another language runtime now requires an amendment.
- Added sections: none
- Removed sections: none
- Templates requiring updates: ✅ specs/001-urgent-care-simulator/plan.md; ✅ specs/001-urgent-care-simulator/research.md; ✅ specs/001-urgent-care-simulator/quickstart.md
- Follow-up TODOs: none
-->

# ED-DES Constitution

## Core Principles

### I. Do not distribute by default
**Rationale:** A distributed system adds coordination cost, operational complexity, and failure modes. In simulation work, the default should be one deployable with vertical scaling until a real bottleneck proves otherwise. The burden of proof sits with the change that introduces a new process, queue, or service.

**Rule:** A new process, service, queue, or deployment boundary MUST have a written justification tied to one of: working-set overflow, genuinely independent compute, geographic latency, or organizational independence. If no such rationale exists, the team MUST keep the design in a single deployable unit.

**How to Apply:** When a feature starts to look distributed, document the real constraint first. If the root cause is within one process, keep the logic local. Add a queue, worker, or multi-service boundary only when the cost of coordination is justified by measured capacity, latency, or ownership boundaries.

### II. Optimize for deletion, not extension
**Rationale:** The cost of software is not only in building it; it is in keeping it understandable. A module is healthy when one engineer can delete and rewrite it in a day. Speculative generalization increases maintenance cost without improving user value.

**Rule:** Modules MUST remain small enough to understand, delete, and rewrite quickly. Abstractions MUST be justified by repeated use or a concrete requirement, not by hypothetical future needs. Duplication below three occurrences is cheaper than the wrong abstraction.

**How to Apply:** Prefer simple functions and local data structures before creating shared frameworks. Extract a helper only when the abstraction is proven by repeated needs, not by a single future-looking use case. If a component cannot be explained in one paragraph, it is likely too large.

### III. Make dependencies explicit
**Rationale:** Hidden coupling is the main source of brittle software. When dependencies are implicit, a change in one module can silently alter behavior elsewhere. The system must reveal dependencies at the boundary of the code that uses them.

**Rule:** No hidden coupling, implicit global state, or import-time side effects are allowed. A reader MUST be able to see every dependency of a function in its signature or at the top of the file. Dependency injection MUST be used over singleton-style implicit state whenever a dependency is not a true global invariant.

**How to Apply:** Pass configuration and collaborators explicitly into constructors or functions. Avoid modules that mutate process-global state during import. When a dependency crosses a file or layer boundary, make it visible in the API and document its ownership.

### IV. Contract at the boundary, not in the middle
**Rationale:** Boundaries create translation zones. The side that understands both contexts owns semantic reconciliation. Without a contract, teams silently drift into incompatible data assumptions, especially across HTTP calls, file ingestion, queues, and databases.

**Rule:** Every producer/consumer boundary MUST define a schema with a version. Contracts MUST be explicit at the boundary, and all semantic reconciliation MUST happen there. No shared mutable schema may be used as an implicit communication channel across layers.

**How to Apply:** Define input and output contracts at API, file, queue, and persistence boundaries. Version formats when behavior changes. If two systems disagree on meaning, reconcile at the boundary rather than letting hidden assumptions spread into the middle of the application.

### V. Test the transformation, not the plumbing
**Rationale:** Complex infrastructure is not the value; the transformation is. Tests should prove the decision logic, not the number of objects created or the specific wiring used to connect them. This keeps the codebase stable when implementation details change.

**Rule:** Unit tests MUST cover pure transformation logic. Integration tests MUST cover process boundaries. Code under test MUST not mock what it owns. A fix is not considered complete unless the test that failed before the fix now passes.

**How to Apply:** Write a failing test for the bug, then adjust the transformation logic. Mock third-party systems or remote dependencies, but do not mock the domain logic under active development. Validate behavior at boundary edges and keep CI red when the bug remains unfixed.

### VI. Emit structured events, derive everything else
**Rationale:** Logs, metrics, and traces are projections of one primitive: the structured event. Unstructured lines make diagnosis slower and hide the context needed to understand production incidents. High-cardinality identifiers are required to connect behavior across systems.

**Rule:** New code MUST emit structured events with context such as request identifiers, tenant or user identifiers, feature-flag state, and relevant domain attributes. Metrics, dashboards, alerts, and traces MUST be derived from these events rather than written ad hoc in code paths.

**How to Apply:** Use consistent event shapes and names, include correlation keys, and avoid free-form log messages for machine-readable diagnostics. If a signal is needed for investigation, it should be captured as structured data at the source instead of reconstructed later.

### VII. Recovery over prevention
**Rationale:** Failures are inevitable. The correct design question is not whether a system can avoid every outage, but whether it can recover quickly, reversibly, and with minimal harm. Rollback and feature gating are operational capabilities, not afterthoughts.

**Rule:** Every change MUST be revertible in under five minutes without a code change. Risky paths MUST be gated by feature flags. Schema and data migrations MUST use expand-then-contract patterns, and rollback MUST be exercised as part of deployment readiness.

**How to Apply:** Prefer reversible deployments and additive migrations. Verify rollback steps in staging or pre-production. Provide a clear operational path to disable risky paths before shipping broader changes.

### VIII. Attention is finite
**Rationale:** Signal quality matters more than signal volume. Every alert consumes engineering attention, so each alert must carry a clear user-visible symptom and a runbook. Noise erodes the ability to notice real failure.

**Rule:** Every alert MUST map to a user-visible symptom and a documented runbook. Dashboards MUST be saved queries for operational decisions, not decorative displays. Alerts and metrics that have not fired a useful page in 90 days MUST be removed or rewritten.

**How to Apply:** Review alert noise during regular operational review. Keep only signals that have actionable owners and clear response procedures. Remove vanity dashboards and stale metrics that no longer inform a decision.

### IX. Value is realized at the user, not at merge
**Rationale:** A branch is not the product; a deployed and observable release is. Code that is unshipped, unobserved, and unrecoverable is not valuable in the same way as code that users depend on every day.

**Rule:** A change is not complete until it is in the hands of users, observable in production, and revertible. "Shipped" means deployed, instrumented, and monitored, not merged.

**How to Apply:** Treat deployment as part of the engineering contract. Verify the change works in the target environment, confirm instrumentation is active, and ensure rollback is operationally ready. Do not mark a task as done until the user-visible effect is observable.

### X. Commands are discoverable; local dev matches CI
**Rationale:** Reproducibility is a product feature. A developer should be able to discover and run every repeatable action from one known interface, without hidden shell behavior or a different command set between local machines and CI.

**Rule:** Every repeatable action, including build, test, lint, migrate, deploy, and seed, MUST be exposed as a single named command and documented in one place. The command a developer runs locally MUST be the same command CI runs.

**How to Apply:** Centralize command definitions and documentation. If a contributor cannot list the core commands in under 30 seconds, the interface is broken. Avoid undocumented shell snippets, hidden flags, and CI-only commands that are absent in local development.

## Additional Constraints
This project MUST use a single-language stack. C# on .NET is the runtime and implementation layer for object models, UI, domain behavior, probability distributions, scenario processing, and data-oriented computation. Unity is the optional presentation layer, and Visual Studio Code is the primary editor. Introducing an additional language runtime or toolchain, including Python or a JavaScript/Node stack, requires a recorded amendment to this Constitution.

All simulation behavior MUST remain explicit, reviewable, and traceable. No rule may hide in a global singleton or hidden dependency chain. Domain contracts, event streams, and boundary schemas are mandatory to preserve correctness across modules and re-runs.

## Development Workflow
Every feature MUST begin with a clearly defined user need, operational question, or scenario outcome. Work is decomposed into small, testable units that align to one principle at a time, and changes are reviewed for adherence to the governing rules before merge.

The simplest valid implementation is preferred. Refactoring is allowed only when it clarifies ownership, reduces hidden coupling, improves testability, or removes a real maintenance burden. Unclear assumptions MUST be surfaced and resolved before code is merged into the shared path.

## Governance
This Constitution supersedes ad hoc project practices and is binding for architecture, delivery, and operational decisions. Changes to this document require a recorded rationale, explicit impact review, and approval before the rule is enforced for new work.

Amendments MUST include:
- the principle or section affected
- the reason for the change and any migration impact
- the updated requirement or workflow change
- the new version number and amendment date

Versioning follows semantic versioning:
- MAJOR: backward-incompatible governance or principle removals or redefinitions
- MINOR: new principle or materially expanded guidance
- PATCH: wording, clarification, or non-semantic refinement

Compliance review occurs before major milestones and before release. Any work that violates this Constitution MUST be corrected or explicitly justified with a documented exception and a remedial plan.

**Version**: 2.1.0 | **Ratified**: 2026-09-01 | **Last Amended**: 2026-09-01
