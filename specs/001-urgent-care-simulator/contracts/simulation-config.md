# Simulation Configuration Contract

## Purpose

Define the external configuration contract for scenario parameters, patient generation, and result exports used by the urgent-care simulation.

## Configuration object

```json
{
  "scenarioId": "string",
  "seed": 12345,
  "timeHorizon": 168,
  "arrivalModel": {
    "baseRate": 6.0,
    "hourlyVariation": true,
    "dailyVariation": true,
    "seasonalVariation": false
  },
  "department": {
    "name": "UrgentCare",
    "resourceCount": 3,
    "resourceType": "Nurse",
    "queuePolicy": "UrgencyFirst",
    "roomCount": 2
  },
  "patientModel": {
    "urgencyDistribution": "categorical",
    "ageDistribution": "normal",
    "serviceDurationDistribution": "lognormal",
    "abandonmentThresholdDistribution": "triangular"
  },
  "metrics": {
    "reportAverageWait": true,
    "reportP95Wait": true,
    "reportAbandonmentRate": true,
    "reportUtilization": true
  }
}
```

## Contract rules

- `scenarioId` MUST be unique per scenario run.
- `seed` MUST be a valid integer when reproducibility is required.
- `timeHorizon` MUST be greater than zero.
- `queuePolicy` MUST map to a supported queue strategy such as FIFO or UrgencyFirst.
- `arrivalModel` and `patientModel` MUST be versioned when distribution behavior changes.
- `metrics` MUST include the minimum required outcomes for the feature.

## Result export

Result files SHOULD include aggregated summary metrics for waiting time, abandonment, utilization, and throughput. Export format may be JSON or CSV, but the output schema MUST remain consistent across scenario runs.
