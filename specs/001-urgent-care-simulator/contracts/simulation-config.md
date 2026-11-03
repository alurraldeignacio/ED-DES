# Simulation Configuration Contract

## Purpose

Define the external configuration contract for scenario parameters, patient generation, care-resource behavior, and result exports used by the urgent-care simulation.

## Units

All simulated time is expressed in **minutes**, with one exception that is named explicitly:

| Field | Unit |
|---|---|
| `timeHorizon` | minutes of simulated time (`10080` = 7 days) |
| `arrivalModel.baseRatePerHour` | expected arrivals per hour; the engine converts to a per-minute rate |
| `patientModel.serviceDurationDistribution` | minutes |
| `patientModel.abandonmentThresholdDistribution` | minutes |
| `resourceProfile.shiftLengthMinutes` | minutes |
| `resourceProfile.fatigueRatePerMinute` | fatigue units accrued per minute on shift |

## Configuration object

```json
{
  "schemaVersion": "1.0.0",
  "scenarioId": "string",
  "seed": 12345,
  "timeHorizon": 10080,
  "arrivalModel": {
    "baseRatePerHour": 6.0,
    "hourlyVariation": true,
    "dailyVariation": true,
    "seasonalVariation": false,
    "interarrivalDistribution": {
      "family": "exponential"
    }
  },
  "department": {
    "name": "UrgentCare",
    "resourceCount": 3,
    "resourceType": "Nurse",
    "queuePolicy": "UrgencyFirst",
    "roomCount": 2,
    "roomBypassUrgency": 2
  },
  "resourceProfile": {
    "baseEfficiency": 1.0,
    "experience": 0.8,
    "experienceBonus": 0.25,
    "shiftLengthMinutes": 480,
    "fatigueRatePerMinute": 0.000625,
    "minEfficiency": 0.5,
    "maxEfficiency": 1.5
  },
  "patientModel": {
    "urgencyDistribution": {
      "family": "categorical",
      "categories": [1, 2, 3, 4, 5],
      "weights": [0.05, 0.15, 0.40, 0.30, 0.10]
    },
    "ageDistribution": {
      "family": "normal",
      "mean": 42.0,
      "standardDeviation": 21.0,
      "min": 0,
      "max": 105
    },
    "serviceDurationDistribution": {
      "family": "lognormal",
      "mu": 2.9,
      "sigma": 0.5
    },
    "abandonmentThresholdDistribution": {
      "family": "triangular",
      "min": 30.0,
      "mode": 90.0,
      "max": 240.0
    }
  },
  "metrics": {
    "reportAverageWait": true,
    "reportP95Wait": true,
    "reportAbandonmentRate": true,
    "reportUtilization": true
  }
}
```

## Urgency scale

Urgency follows the ESI convention: **1 is the most severe** (resuscitation) and **5 is the least severe** (non-urgent). Level 3 is the most common presentation, which is why the default `urgencyDistribution` weights peak there. Queue policies that prioritize urgency therefore order by *ascending* urgency value.

## Room capacity

`roomCount` is the number of treatment rooms. A patient needs both a free care resource and a free room to enter treatment, except that patients whose urgency is at or below `roomBypassUrgency` bypass the room constraint entirely, reflecting a real department holding or clearing a resuscitation bay for a critical arrival. Setting `roomBypassUrgency` to 0 makes rooms a hard constraint for everyone.

## Distribution objects

Every distribution is an object with a `family` discriminator plus the parameters that family requires. A loader MUST reject an unknown family and MUST reject a family whose required parameters are missing.

| Family | Required parameters |
|---|---|
| `categorical` | `categories` (array), `weights` (array, same length, sum > 0) |
| `normal` | `mean`, `standardDeviation`; optional `min` and `max` for truncation |
| `lognormal` | `mu`, `sigma` (parameters of the underlying normal) |
| `triangular` | `min`, `mode`, `max` with `min <= mode <= max` |
| `exponential` | `rate`, or none when the rate is supplied by the caller (as in `interarrivalDistribution`, where the arrival model supplies it) |

## Care-resource behavior

`resourceProfile` supplies the parameters that drive time-dependent productivity. The engine derives effective service duration as follows:

```text
fatigue(t)    = min(1.0, fatigueRatePerMinute * minutesIntoCurrentShift)
efficiency(t) = clamp(baseEfficiency * (1 + experienceBonus * experience) * (1 - fatigue(t)),
                      minEfficiency, maxEfficiency)
effectiveServiceDuration = sampledServiceDuration / efficiency(t)
```

An efficiency above 1.0 shortens treatment; an efficiency below 1.0 lengthens it. With the defaults above, a resource starts a shift at efficiency 1.2 and ends an 8-hour shift at 0.84.

Fatigue accrues across the *current* shift and resets when the next one begins, so `minutesIntoCurrentShift` is the elapsed time since `shiftStart` reduced modulo `shiftLengthMinutes`. An exact multiple of the shift length counts as the end of that shift rather than the start of the next. Without this reset a multi-day run would pin every resource at `minEfficiency` permanently and `shiftLengthMinutes` would have no effect on behavior.

## Contract rules

- `schemaVersion` MUST be present and MUST use semantic versioning. A loader MUST reject a configuration whose major version it does not support.
- `schemaVersion` MUST be incremented when the meaning of an existing field changes, including when `arrivalModel`, `patientModel`, or `resourceProfile` behavior changes.
- `scenarioId` MUST be unique per scenario run.
- `seed` MUST be a valid integer when reproducibility is required.
- `timeHorizon` MUST be greater than zero.
- `queuePolicy` MUST map to a supported queue strategy: FIFO, UrgencyFirst, or UrgencyPlusWait.
  - **FIFO** serves patients in arrival order and ignores acuity. It exists as the baseline a triage policy is measured against.
  - **UrgencyFirst** serves the lowest urgency value first, breaking ties by arrival time.
  - **UrgencyPlusWait** serves ESI 1 and 2 first on acuity alone, and orders the remaining patients by `urgency - waitingTime / escalationMinutes` so a low-acuity patient is not starved. Patients at or below the protected acuity MUST NOT be overtaken by a waiting patient, however long that patient has waited.
- `resourceCount` and `roomCount` MUST be non-negative integers.
- `roomBypassUrgency` MUST be an integer from 0 to 5, where 0 disables the bypass.
- Every distribution object MUST declare a supported `family` and MUST supply that family's required parameters.
- `experience` MUST be in the range 0.0 to 1.0; `fatigue` is derived, never configured directly.
- `baseEfficiency`, `minEfficiency`, and `maxEfficiency` MUST be greater than zero, with `minEfficiency <= maxEfficiency`.
- `metrics` MUST enable at least the outcomes required by SC-002: average wait, P95 wait, abandonment rate, and utilization.

## Event stream export

When the event sink writes to a file, the file MUST open with a single header record carrying
`schemaVersion` and the `runId`, and every event record follows after it. The version belongs to
the file, not to the individual event, so `SimulationEvent` stays a lightweight in-process record.
A reader MUST reject a file whose major version it does not support.

## Result export

Result files MUST include aggregated summary metrics for waiting time, P95 waiting time, abandonment rate, utilization, throughput, and queue length (average and maximum). Export format may be JSON or CSV, but the output schema MUST remain consistent across scenario runs. Every exported result MUST carry the `schemaVersion` of the configuration that produced it, so a consumer can tell which contract version the numbers were produced under. The exported column set MUST NOT vary with `metrics`: a metric disabled there is exported as `null` rather than omitted, so results from different scenario runs stay directly comparable.
