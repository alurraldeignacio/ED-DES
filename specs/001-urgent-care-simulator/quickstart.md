# Quickstart: Urgent Care Queue Simulator

## Goal

Validate the MVP simulation end-to-end by running a scenario with patient arrivals, queue prioritization, and basic operational metrics.

## Prerequisites

- .NET 10 SDK or later for the simulation core and analysis library
- Visual Studio Code with the C# Dev Kit extension as the primary editor
- Optional: Unity 2022 LTS for visual layer validation
- A configured scenario file describing staffing, arrival assumptions, and queue policy

## Local validation workflow

1. Restore and build the simulation core.
   ```bash
   dotnet restore UrgentCare.sln
   dotnet build UrgentCare.sln
   ```
2. Run the scenario in console or headless mode.
   ```bash
   dotnet run --project src/UrgentCare.Cli/UrgentCare.Cli.csproj -- --scenario default
   ```
3. Review the output metrics for arrivals, waiting time, abandonment, throughput, and utilization.
4. Repeat the same scenario with a different seed or staffing count to compare results.
5. Validate the queue policy change by re-running the scenario with urgency-based ordering.

## Measured performance

T081 budget: a 10080-minute (168-hour) horizon on the default scenario completes in under 10 seconds.

Measured on the development machine: **0.03 s** for the full simulated week (1066 arrivals, 5042 events), well inside the budget.

## Expected outcomes

- Patients enter the simulation and progress through waiting and treatment states.
- Queue length and waiting time change in response to staffing and policy changes.
- The system records aggregate metrics that can be compared across scenarios.
- A reset or repeated run produces consistent results for a fixed seed and configuration.

## Notes

This quickstart is intentionally focused on validation rather than implementation details. The scenario and metrics contracts should evolve as the feature progresses from MVP to richer operational experiments.
