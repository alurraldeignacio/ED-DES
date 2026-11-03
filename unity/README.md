# Unity presentation layer (deferred)

There is no Unity project in this release. This directory records the decision and the one
technical constraint a future visual layer will have to solve, so neither is rediscovered later.

## The decision

Unity is a **presentation layer, not a model authority**. It may render simulation state; it must
never own the business rules or hold authoritative simulation state.

The reason is that the simulation has to run headless. Scenario batches, replication runs, and CI
all execute without a renderer, and a rule that lived in a `MonoBehaviour` would be unavailable to
every one of them. Keeping state and logic in `UrgentCare.Core` also keeps them testable: the
current suite exercises the engine directly, with no scene, no frame loop, and no play mode.

This is recorded in full in
[research.md](../specs/001-urgent-care-simulator/research.md) under *Treat Unity as a
presentation layer, not a model authority*.

## The runtime constraint

**Unity 2022 LTS cannot reference a `net10.0` assembly.** Its scripting runtime targets .NET
Standard 2.1, so `UrgentCare.Core` as built today is not loadable from a Unity project.

A future visual layer therefore needs one of:

1. **A `netstandard2.1` interop surface over `UrgentCare.Core`** — a thin projection of the types
   a renderer actually needs (patient positions, queue length, resource states), multi-targeted so
   both runtimes can load it. The core stays on `net10.0`.
2. **A Unity version with a newer scripting runtime**, which removes the problem but ties the
   project to whatever Unity supports at that time.

Option 1 is the safer default: it keeps the simulation free to use current .NET, and it forces an
explicit, reviewable contract at the boundary rather than letting a renderer reach into domain
objects.

## What a visual layer would consume

The engine already exposes what a renderer needs, and none of it requires new simulation code:

- `SimulationEngine.RunOneEvent()` advances exactly one event, so a view can paint between steps.
- `SimulationEngine.Begin()` prepares a run the view drives itself.
- The `IEventSink` stream carries every state change with a timestamp and the queue length at that
  moment, which is enough to animate the department without polling it.

`ConsoleOperationalView` in `src/UrgentCare.Cli` is the reference consumer of that interface.
