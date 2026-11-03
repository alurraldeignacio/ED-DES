using System.Text.Json;
using UrgentCare.Analysis.Metrics;
using UrgentCare.Core.Configuration;
using UrgentCare.Analysis.Scenarios;
using UrgentCare.Core.Metrics;

namespace UrgentCare.Analysis.Tests;

/// <summary>
/// The export is a producer/consumer boundary, so its schema is fixed (Constitution Principle
/// IV, contracts/simulation-config.md). The column set must not vary with what a run happened
/// to measure: a metric switched off in MetricsConfig is exported as null, never omitted, so
/// results from different runs stack up in the same table.
/// </summary>
public class ResultExporterTests
{
    private readonly ResultExporter _exporter = new();

    private static ScenarioResult FullResult(string scenarioId = "full") =>
        new(scenarioId, "1.0.0", 12345, new SimulationMetrics
        {
            Arrivals = 100,
            Completed = 90,
            Abandoned = 10,
            AverageWaitTime = 42.5,
            P95WaitTime = 120.0,
            AbandonmentRate = 0.1,
            Utilization = 0.85,
            Throughput = 5.25,
            AverageQueueLength = 3.5,
            MaxQueueLength = 12
        });

    /// <summary>A run whose optional metrics were switched off, so they come back null.</summary>
    private static ScenarioResult SparseResult(string scenarioId = "sparse") =>
        new(scenarioId, "1.0.0", 999, new SimulationMetrics
        {
            Arrivals = 50,
            Completed = 45,
            Abandoned = 5,
            AverageWaitTime = null,
            P95WaitTime = null,
            AbandonmentRate = null,
            Utilization = null,
            Throughput = 2.5,
            AverageQueueLength = 1.25,
            MaxQueueLength = 4
        });

    [Fact]
    public void CsvHeaderIsTheDeclaredColumnSet()
    {
        var csv = _exporter.ToCsv([FullResult()]);

        var header = csv.Split('\n')[0].TrimEnd('\r');

        Assert.Equal(string.Join(",", ResultExporter.Columns), header);
    }

    [Fact]
    public void CsvHeaderIsIdenticalWhetherOrNotMetricsWereMeasured()
    {
        var full = _exporter.ToCsv([FullResult()]).Split('\n')[0].TrimEnd('\r');
        var sparse = _exporter.ToCsv([SparseResult()]).Split('\n')[0].TrimEnd('\r');

        Assert.Equal(full, sparse);
    }

    [Fact]
    public void EveryRowHasOneFieldPerColumn()
    {
        var csv = _exporter.ToCsv([FullResult(), SparseResult()]);

        var lines = csv.Split('\n').Where(l => l.Trim().Length > 0).ToList();

        Assert.Equal(3, lines.Count);
        foreach (var line in lines)
        {
            Assert.Equal(ResultExporter.Columns.Count, line.TrimEnd('\r').Split(',').Length);
        }
    }

    [Fact]
    public void ADisabledMetric_IsAnEmptyCsvFieldRatherThanAMissingColumn()
    {
        var csv = _exporter.ToCsv([SparseResult()]);

        var header = csv.Split('\n')[0].TrimEnd('\r').Split(',');
        var row = csv.Split('\n')[1].TrimEnd('\r').Split(',');

        var waitIndex = Array.IndexOf(header, "averageWaitTime");

        Assert.True(waitIndex >= 0);
        Assert.Equal(string.Empty, row[waitIndex]);
    }

    [Fact]
    public void EveryExportCarriesTheSchemaVersionOfTheConfigurationThatProducedIt()
    {
        var csv = _exporter.ToCsv([FullResult()]);

        var header = csv.Split('\n')[0].TrimEnd('\r').Split(',');
        var row = csv.Split('\n')[1].TrimEnd('\r').Split(',');

        var index = Array.IndexOf(header, "schemaVersion");

        Assert.True(index >= 0);
        Assert.Equal("1.0.0", row[index]);
    }

    [Fact]
    public void JsonCarriesEveryColumnIncludingTheNullOnes()
    {
        var json = _exporter.ToJson([SparseResult()]);

        using var document = JsonDocument.Parse(json);
        var row = document.RootElement.GetProperty("results")[0];

        foreach (var column in ResultExporter.Columns)
        {
            Assert.True(row.TryGetProperty(column, out _), $"Column '{column}' missing from JSON export.");
        }

        Assert.Equal(JsonValueKind.Null, row.GetProperty("averageWaitTime").ValueKind);
    }

    [Fact]
    public void JsonSchemaIsStableAcrossRuns()
    {
        var first = _exporter.ToJson([FullResult("run-a")]);
        var second = _exporter.ToJson([SparseResult("run-b")]);

        static IEnumerable<string> PropertyNames(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("results")[0]
                .EnumerateObject()
                .Select(p => p.Name)
                .ToList();
        }

        Assert.Equal(PropertyNames(first), PropertyNames(second));
    }

    [Fact]
    public void ScenarioIdsContainingASeparator_AreQuotedSoColumnsStayAligned()
    {
        var result = FullResult("busy,department");

        var csv = _exporter.ToCsv([result]);
        var row = csv.Split('\n')[1].TrimEnd('\r');

        Assert.Contains("\"busy,department\"", row);
    }

    [Fact]
    public void AnEmptyBatch_StillWritesTheHeader()
    {
        var csv = _exporter.ToCsv([]);

        Assert.Equal(string.Join(",", ResultExporter.Columns), csv.Split('\n')[0].TrimEnd('\r'));
    }

    [Fact]
    public void WritingToDisk_ProducesTheSameContentAsTheInMemoryExport()
    {
        var results = new[] { FullResult(), SparseResult() };
        var directory = Directory.CreateTempSubdirectory("urgentcare-export-tests");

        try
        {
            var csvPath = Path.Combine(directory.FullName, "results.csv");
            var jsonPath = Path.Combine(directory.FullName, "results.json");

            _exporter.WriteCsv(csvPath, results);
            _exporter.WriteJson(jsonPath, results);

            Assert.Equal(_exporter.ToCsv(results), File.ReadAllText(csvPath));
            Assert.Equal(_exporter.ToJson(results), File.ReadAllText(jsonPath));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ExportedMetricsRoundTripTheValuesTheyWereGiven()
    {
        var json = _exporter.ToJson([FullResult()]);

        using var document = JsonDocument.Parse(json);
        var row = document.RootElement.GetProperty("results")[0];

        Assert.Equal(100, row.GetProperty("arrivals").GetInt32());
        Assert.Equal(42.5, row.GetProperty("averageWaitTime").GetDouble());
        Assert.Equal(12, row.GetProperty("maxQueueLength").GetInt32());
    }

    [Fact]
    public void TheAggregateCsvHeaderIsTheDeclaredColumnSet()
    {
        var aggregates = MetricsAggregator.Aggregate([FullResult(), SparseResult()]);

        var header = _exporter.ToAggregateCsv(aggregates).Split('\n')[0].TrimEnd('\r');

        Assert.Equal(string.Join(",", ResultExporter.AggregateColumns), header);
    }

    [Fact]
    public void TheAggregateCsvHasOneRowPerComparedMetric()
    {
        var aggregates = MetricsAggregator.Aggregate([FullResult(), SparseResult()]);

        var lines = _exporter.ToAggregateCsv(aggregates)
            .Split('\n')
            .Where(l => l.Trim().Length > 0)
            .ToList();

        Assert.Equal(ScenarioComparison.ComparedMetrics.Count + 1, lines.Count);
    }

    [Fact]
    public void TheManifestJsonRecordsAssumptionsAlongsideOutcomes()
    {
        var config = new ScenarioConfig
        {
            SchemaVersion = "1.0.0",
            ScenarioId = "manifest-test",
            Seed = 4242,
            TimeHorizon = 1440.0,
            ArrivalModel = new ArrivalModelConfig { BaseRatePerHour = 6.0, HourlyVariation = true },
            Department = new DepartmentConfig
            {
                Name = "UrgentCare",
                ResourceCount = 3,
                ResourceType = "Nurse",
                QueuePolicy = "UrgencyFirst",
                RoomCount = 2,
                RoomBypassUrgency = 2
            }
        };

        var manifest = RunManifest.For(config, [FullResult(), SparseResult()]);

        using var document = JsonDocument.Parse(_exporter.ToAggregateJson(manifest));
        var root = document.RootElement;

        Assert.Equal("manifest-test", root.GetProperty("scenarioId").GetString());
        Assert.Equal("1.0.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(4242, root.GetProperty("baseSeed").GetInt32());
        Assert.Equal("UrgencyFirst", root.GetProperty("queuePolicy").GetString());
        Assert.Equal("hourly", root.GetProperty("arrivalProfile").GetString());
        Assert.Equal(2, root.GetProperty("replicationSeeds").GetArrayLength());
        Assert.NotEqual(0, root.GetProperty("aggregates").GetArrayLength());
    }

    [Fact]
    public void TheColumnSetMatchesWhatTheContractRequires()
    {
        // contracts/simulation-config.md, Result export: waiting time, P95 waiting time,
        // abandonment rate, utilization, throughput, and queue length.
        string[] required =
        [
            "averageWaitTime", "p95WaitTime", "abandonmentRate",
            "utilization", "throughput", "averageQueueLength", "maxQueueLength"
        ];

        foreach (var column in required)
        {
            Assert.Contains(column, ResultExporter.Columns);
        }
    }
}
