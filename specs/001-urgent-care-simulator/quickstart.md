# Quickstart: Urgent Care Queue Simulator

## Goal

Validate the simulator end to end: a scenario runs, patients flow from arrival to completion or
abandonment, and the results can be compared across scenarios and replications.

## Prerequisites

- .NET 10 SDK or later for the simulation core and analysis library
- Visual Studio Code with the C# Dev Kit extension as the primary editor
- Optional: Unity 2022 LTS for a future visual layer (deferred, see [unity/README.md](../../unity/README.md))

## Command set

Every repeatable action, in one place (Constitution Principle X). These are the same commands CI
runs and the same ones exposed as VS Code tasks in `.vscode/tasks.json`.

| Action | Command |
|---|---|
| Restore | `dotnet restore UrgentCare.sln` |
| Build | `dotnet build UrgentCare.sln` |
| Test | `dotnet test UrgentCare.sln` |
| Run (interactive) | `dotnet run --project src/UrgentCare.Cli -- --scenario default` |
| Run (headless) | `dotnet run --project src/UrgentCare.Cli -- --scenario default --headless` |
| Compare two scenarios | `dotnet run --project src/UrgentCare.Cli -- --compare default lean` |
| Replicate and aggregate | `dotnet run --project src/UrgentCare.Cli -- --scenario default --replications 30` |
| Export the event stream | `dotnet run --project src/UrgentCare.Cli -- --scenario default --headless --events events.jsonl` |
| Show all options | `dotnet run --project src/UrgentCare.Cli -- --help` |
| Run the web view (feature 002) | `dotnet run --project src/UrgentCare.Web`, then open `http://localhost:5270` |

CI runs `dotnet test UrgentCare.sln --no-build` because the preceding step already built the
solution; the command is otherwise identical.

### Options

| Option | Meaning |
|---|---|
| `-s`, `--scenario <name>` | Scenario to run, loaded from `shared/config/<name>-scenario.json` (default: `default`) |
| `--config-dir <dir>` | Directory holding scenario files |
| `--headless` | Run to completion without the interactive view |
| `--compare <a> <b>` | Run two scenarios and print a baseline-versus-variant table |
| `--replications <n>` | Run the scenario `n` times and report the aggregate with confidence intervals |
| `--events <path>` | Write the machine-readable event stream as JSON Lines; `-` means standard output. Requires `--headless` |
| `-h`, `--help` | Show usage |

### Interactive keys

When the interactive view is running: `space` pauses and resumes, `r` resets and runs again,
`+` and `-` change speed, `q` quits.

## Local validation workflow

1. Restore, build, and test.
   ```bash
   dotnet restore UrgentCare.sln
   dotnet build UrgentCare.sln
   dotnet test UrgentCare.sln
   ```
2. Run the default scenario headless and read the summary.
   ```bash
   dotnet run --project src/UrgentCare.Cli -- --scenario default --headless
   ```
3. Confirm arrivals, waiting time, abandonment, throughput, utilisation, and queue length are all
   reported.
4. Compare a leaner department against the default and confirm the deltas move as staffing implies.
   ```bash
   dotnet run --project src/UrgentCare.Cli -- --compare default lean
   ```
5. Replicate the default scenario and confirm the confidence intervals narrow as `n` grows.
   ```bash
   dotnet run --project src/UrgentCare.Cli -- --scenario default --replications 30
   ```
6. Export the event stream and confirm the file opens with a versioned header.
   ```bash
   dotnet run --project src/UrgentCare.Cli -- --scenario default --headless --events events.jsonl
   head -1 events.jsonl
   ```

## Scenarios

| Scenario | What it is |
|---|---|
| `default` | Three nurses, two rooms, urgency-first triage, a week-long horizon with hourly and daily demand variation |
| `lean` | The same department with two nurses and FIFO ordering, as a comparison baseline |

Every operational assumption is a configuration value. See
[contracts/simulation-config.md](contracts/simulation-config.md) for the rules a scenario must
satisfy; invalid scenarios are rejected before a run starts, with one message per broken rule.

## Measured performance

T081 budget: a 10080-minute (168-hour) horizon on the default scenario completes in under 10 seconds.

Measured on the development machine: **0.01 s** for the full simulated week (1066 arrivals, 5042
events), and **0.14 s** for 30 replications of that same week. Both are well inside the budget.

## Expected outcomes

- Patients enter the simulation and progress through waiting and treatment states.
- Queue length and waiting time change in response to staffing and policy changes.
- The system records aggregate metrics that can be compared across scenarios.
- A repeated run with a fixed seed produces an identical event stream, not merely similar totals.
- A replication batch reports a spread alongside each mean, so a scenario difference can be
  judged against the model's own run-to-run variation.
