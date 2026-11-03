using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UrgentCare.Analysis.Scenarios;

namespace UrgentCare.Analysis.Metrics;

/// <summary>
/// Writes scenario results as JSON or CSV. This is a producer/consumer boundary, so the schema
/// is fixed and versioned (Constitution Principle IV, contracts/simulation-config.md).
/// </summary>
/// <remarks>
/// The column set never varies with what a run measured. A metric switched off in MetricsConfig
/// is exported as null (an empty CSV field), never omitted, so results from different scenarios
/// and different runs stack into one table without the columns shifting underneath a reader.
/// Each row carries the `schemaVersion` of the configuration that produced it, so a consumer can
/// tell which contract version the numbers were produced under.
/// </remarks>
public sealed class ResultExporter
{
    /// <summary>The exported columns, in order. This ordering is part of the contract.</summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        "scenarioId",
        "schemaVersion",
        "seed",
        "arrivals",
        "completed",
        "abandoned",
        "averageWaitTime",
        "p95WaitTime",
        "abandonmentRate",
        "utilization",
        "throughput",
        "averageQueueLength",
        "maxQueueLength"
    ];

    /// <summary>
    /// Columns of the aggregated multi-run summary, in order. Like the per-run schema, this
    /// ordering is part of the contract.
    /// </summary>
    public static IReadOnlyList<string> AggregateColumns { get; } =
    [
        "metric",
        "samples",
        "mean",
        "p95",
        "standardDeviation",
        "confidenceIntervalLow",
        "confidenceIntervalHigh"
    ];

    private static readonly JsonSerializerOptions JsonFormat = new() { WriteIndented = true };

    private static readonly JsonSerializerOptions ManifestFormat = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public string ToCsv(IEnumerable<ScenarioResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", Columns));

        foreach (var result in results)
        {
            builder.AppendLine(string.Join(",", Columns.Select(c => CsvField(Value(result, c)))));
        }

        return builder.ToString();
    }

    public string ToJson(IEnumerable<ScenarioResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var rows = new JsonArray();

        foreach (var result in results)
        {
            var row = new JsonObject();
            foreach (var column in Columns)
            {
                row[column] = ToJsonNode(Value(result, column));
            }

            rows.Add(row);
        }

        var document = new JsonObject { ["results"] = rows };

        return document.ToJsonString(JsonFormat);
    }

    public void WriteCsv(string path, IEnumerable<ScenarioResult> results) =>
        File.WriteAllText(path, ToCsv(results));

    public void WriteJson(string path, IEnumerable<ScenarioResult> results) =>
        File.WriteAllText(path, ToJson(results));

    /// <summary>
    /// The replication summary as CSV: one row per measure, carrying the spread as well as the
    /// mean so a reader can see whether two scenarios are actually distinguishable (FR-011).
    /// </summary>
    public string ToAggregateCsv(IEnumerable<AggregateStatistic> aggregates)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", AggregateColumns));

        foreach (var a in aggregates)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                CsvField(a.Metric),
                CsvField(a.Samples),
                CsvField(a.Mean),
                CsvField(a.P95),
                CsvField(a.StandardDeviation),
                CsvField(a.ConfidenceIntervalLow),
                CsvField(a.ConfidenceIntervalHigh)
            }));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The full run manifest as JSON: the assumptions the batch was run under alongside what it
    /// observed, so a result stays interpretable away from the scenario file (US3 AS3).
    /// </summary>
    public string ToAggregateJson(RunManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return JsonSerializer.Serialize(manifest, ManifestFormat);
    }

    public void WriteAggregateCsv(string path, IEnumerable<AggregateStatistic> aggregates) =>
        File.WriteAllText(path, ToAggregateCsv(aggregates));

    public void WriteAggregateJson(string path, RunManifest manifest) =>
        File.WriteAllText(path, ToAggregateJson(manifest));

    /// <summary>
    /// Reads one column off a result. Returning object? rather than a formatted string keeps the
    /// null case distinguishable all the way to the writer, which is what lets CSV emit an empty
    /// field and JSON emit a real null for the same absent metric.
    /// </summary>
    private static object? Value(ScenarioResult result, string column)
    {
        var m = result.Metrics;

        return column switch
        {
            "scenarioId" => result.ScenarioId,
            "schemaVersion" => result.SchemaVersion,
            "seed" => result.Seed,
            "arrivals" => m.Arrivals,
            "completed" => m.Completed,
            "abandoned" => m.Abandoned,
            "averageWaitTime" => m.AverageWaitTime,
            "p95WaitTime" => m.P95WaitTime,
            "abandonmentRate" => m.AbandonmentRate,
            "utilization" => m.Utilization,
            "throughput" => m.Throughput,
            "averageQueueLength" => m.AverageQueueLength,
            "maxQueueLength" => m.MaxQueueLength,
            _ => throw new ArgumentOutOfRangeException(
                nameof(column), column, "Column is not part of the export schema.")
        };
    }

    /// <summary>
    /// Formats a value for CSV. Numbers use the invariant culture so a machine with a comma
    /// decimal separator cannot silently split one field into two.
    /// </summary>
    private static string CsvField(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }

    private static JsonNode? ToJsonNode(object? value) => value switch
    {
        null => null,
        double d => JsonValue.Create(d),
        int i => JsonValue.Create(i),
        _ => JsonValue.Create(value.ToString())
    };
}
