# ED-DES: Urgent Care Queue Simulator

A discrete-event simulator for a hospital urgent-care department. It models patients arriving,
waiting, being triaged, receiving treatment, and sometimes leaving before they are seen, then
reports the operational measures a department is actually run on: waiting time, queue length,
abandonment, staff utilisation, and throughput.

The point of the tool is comparison. Change the staffing, the triage policy, or the arrival
pattern, run the scenario again, and see what moved — with enough replications behind the numbers
to tell a real effect from run-to-run noise.

## Stack

| Layer | Technology |
|---|---|
| Simulation core, analysis, CLI | C# on .NET 10 |
| Probability and statistics | MathNet.Numerics |
| Tests | xUnit |
| Editor | Visual Studio Code with the C# Dev Kit |
| Visual layer | Unity — optional and deferred, see [unity/README.md](unity/README.md) |

Everything is a single-language stack by design. Adding another runtime requires an amendment to
[the project constitution](.specify/memory/constitution.md).

## Layout

```text
src/UrgentCare.Core/        simulation engine: domain, clock, events, queues, metrics
src/UrgentCare.Cli/         console runner, operational view, event stream
analysis/UrgentCare.Analysis/  distributions, scenario batches, replication, exports
shared/config/              scenario files
shared/schemas/             JSON Schema for the configuration contract
tests/                      unit and integration tests
specs/                      feature specification, plan, and task breakdown
```

The dependency runs one way: `Analysis` references `Core`, and `Core` references nothing. The
simulation never depends on how results are presented.

## Getting started

```bash
dotnet restore UrgentCare.sln
dotnet build UrgentCare.sln
dotnet test UrgentCare.sln
dotnet run --project src/UrgentCare.Cli -- --scenario default
```

The full command set is documented in
[specs/001-urgent-care-simulator/quickstart.md](specs/001-urgent-care-simulator/quickstart.md),
and CI runs the same commands.

## How a scenario is configured

Every operational assumption lives in a scenario file rather than in code — staffing, rooms,
queue policy, arrival intensity, and the distributions patient attributes are sampled from. See
[shared/config/default-scenario.json](shared/config/default-scenario.json) for a worked example
and [the configuration contract](specs/001-urgent-care-simulator/contracts/simulation-config.md)
for the rules a scenario must satisfy. Configurations are validated before a run starts, so a
bad scenario fails with a list of what is wrong rather than producing quietly wrong numbers.

## Design notes

- **Reproducible by default.** A scenario and a seed always produce the same run, down to the
  event stream. Replications derive their seeds from the scenario seed, so a whole batch can be
  re-run months later and land on the same numbers.
- **Every metric derives from the event stream.** Nothing is counted on the side, which means the
  same figures can be recomputed later from an exported log.
- **Triage follows the ESI scale**, where 1 is a resuscitation case and 5 is non-urgent. A lower
  urgency number means a higher treatment priority.

## Status

The three planned user stories are implemented: running a live simulation, comparing staffing and
triage scenarios, and reviewing statistical outcomes across replications. The Unity presentation
layer is deliberately deferred.
