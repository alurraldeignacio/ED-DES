# Quickstart: Web Visual View

## Prerequisites

- .NET 10 SDK (the same one the rest of the solution uses). Nothing else: no Node, no npm, no accounts.
- A current desktop browser (Edge, Chrome, or Firefox).

## Commands

| Action | Command |
|---|---|
| Run the web view | `dotnet run --project src/UrgentCare.Web` |
| Build and test everything (unchanged, as CI) | `dotnet restore UrgentCare.sln`, `dotnet build UrgentCare.sln`, `dotnet test UrgentCare.sln` |
| Static publish (optional) | `dotnet publish src/UrgentCare.Web -c Release` → serve `bin/Release/net10.0/publish/wwwroot` from any static host |

`dotnet run` prints the local URL (for example `http://localhost:5xxx`); open it in the browser. The same command is available as the `run-web` task in VS Code.

## Validation walkthrough

Each step names the spec item it proves.

1. **Opens and runs** (SC-001, US1). Run the command, open the URL, leave the scenario on `default`, press Play. Patients appear in the arrival area, join the waiting line, move to a nurse, and leave.
2. **Serve order and urgency** (US1-2, FR-006, FR-007). Pause when several patients are waiting. The leftmost patient is the next to be served; under `UrgencyFirst` the line is sorted by ESI number, and each token shows its number as text.
3. **Room bypass** (US1-5). Step until both rooms are full and an ESI 1 or 2 patient starts treatment; they appear in the "No room (bypass)" area next to their nurse.
4. **Abandonment** (US1-3). Play at 60 min/s until the abandoned counter rises; the patient leaves through the abandoned exit, distinct from the completed one.
5. **Step** (US2-2, SC-004). Pause and press Step three times; each press processes one event and moves the clock to its time. Usually one patient moves. A press may move nobody (a patience check for a patient already in treatment) or two patients at once (a treatment ends and the nurse takes the next patient).
6. **Speed** (US2-3). Switch between speed presets while playing; the clock rate changes and the run continues.
7. **Jump to end and parity** (US3-2, SC-002, SC-003). Press Jump to end, then compare with:

   ```bash
   dotnet run --project src/UrgentCare.Cli -- --scenario default --headless
   ```

   Every metric matches (decimal separator follows your locale). Repeat with `lean`.
8. **Restart is reproducible** (US2-5, SC-005). Press Restart, then Jump to end; the metrics are identical to step 7.
9. **Seed** (FR-004, edge case). Enter `abc` as the seed: a message appears and the seed is unchanged. Enter `7`, Apply, Jump to end; the metrics differ from step 7. (The CLI has no seed flag; parity for non-default seeds is covered by `CliParityTests`.)

The automated equivalent of steps 7 and 8 is `CliParityTests` and `VisualSessionTests`, run by `dotnet test UrgentCare.sln`.

## Validation log

**2026-11-04, MVP (T024), steps 1 to 4.** Driven in headless Edge through Playwright from a throwaway console app (not part of the repository), default scenario, seed 12345, 60 simulated min/s:

- Step 1: the page loads at `http://localhost:5270`, Play starts the clock (Day 1, 04:59 after 5 s of real time), patients arrive, wait, are treated and leave.
- Step 2: with nine waiting, the line read ESI 3,3,3,4,4,4,4,5,5 left to right under UrgencyFirst; the next patient is ringed and every token prints its ESI number.
- Step 3: at Day 1, 05:20 both rooms were full and an ESI 2 patient was being treated by R3 under "No room (bypass)". **Deviation**: validated by playing, not stepping, because Step arrives with US2; step 3 should be re-run with Step after T029.
- Step 4: the first abandonment appeared at Day 1, 12:34 in "Left without being seen", matching the first `PatientAbandoned` event (t = 741.5 min) in the CLI event stream for the same seed. Pause stopped the clock.
- No browser console errors.

At 60 min/s one real second covers an hour, so many tokens are mid-move at any instant; the picture settles when paused.
