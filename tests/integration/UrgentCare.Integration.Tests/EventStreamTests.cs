using System.Text.Json;
using UrgentCare.Analysis.Distributions;
using UrgentCare.Cli;
using UrgentCare.Core;
using UrgentCare.Core.Events;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// T080: the machine-readable event stream is a persisted boundary, so its format carries a
/// version (Constitution Principle IV). The version sits on the file header rather than on every
/// event, which is the decision recorded in research.md.
/// </summary>
public class EventStreamTests
{
    private static string WriteStreamToTempFile(out int eventCount, out Guid runId)
    {
        var config = ScenarioBuilder.Scenario(resourceCount: 2, timeHorizon: 480.0, seed: 606);
        var directory = Directory.CreateTempSubdirectory("urgentcare-events");
        var path = Path.Combine(directory.FullName, "events.jsonl");

        var log = new InMemoryEventLog();
        runId = Guid.NewGuid();

        using (var stream = JsonLinesEventSink.ToFile(path, config.SchemaVersion, config.ScenarioId, runId))
        {
            var sink = new CompositeEventSink(log, stream);
            SimulationEngine.Create(config, new MathNetDistributionFactory(), sink, runId).Run();
        }

        eventCount = log.Events.Count;
        return path;
    }

    [Fact]
    public void TheFileOpensWithAVersionedHeader()
    {
        var path = WriteStreamToTempFile(out _, out var runId);

        try
        {
            var header = JsonDocument.Parse(File.ReadLines(path).First()).RootElement;

            Assert.Equal("header", header.GetProperty("record").GetString());
            Assert.Equal(JsonLinesEventSink.EventStreamVersion, header.GetProperty("eventStreamVersion").GetString());
            Assert.Equal("1.0.0", header.GetProperty("schemaVersion").GetString());
            Assert.Equal(runId.ToString(), header.GetProperty("runId").GetString());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void EveryEmittedEventBecomesOneParsableLine()
    {
        var path = WriteStreamToTempFile(out var eventCount, out _);

        try
        {
            var lines = File.ReadAllLines(path);

            // One header plus one line per event.
            Assert.Equal(eventCount + 1, lines.Length);
            Assert.All(lines, line => JsonDocument.Parse(line).Dispose());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TheFileHasNoByteOrderMark()
    {
        // A BOM on the first line breaks readers that parse each line independently.
        var path = WriteStreamToTempFile(out _, out _);

        try
        {
            var firstBytes = File.ReadAllBytes(path).Take(3).ToArray();

            Assert.NotEqual<byte[]>([0xEF, 0xBB, 0xBF], firstBytes);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void EventRecordsCarryTheFieldsAMetricWouldBeDerivedFrom()
    {
        var path = WriteStreamToTempFile(out _, out _);

        try
        {
            var firstEvent = JsonDocument.Parse(File.ReadLines(path).Skip(1).First()).RootElement;

            Assert.True(firstEvent.TryGetProperty("t", out _));
            Assert.True(firstEvent.TryGetProperty("type", out _));
            Assert.True(firstEvent.TryGetProperty("queueLength", out _));
            Assert.True(firstEvent.TryGetProperty("replication", out _));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TimestampsUseTheInvariantCultureSoTheJsonStaysValid()
    {
        // A machine with a comma decimal separator would otherwise emit 1,04 and break the JSON.
        var path = WriteStreamToTempFile(out _, out _);

        try
        {
            var line = File.ReadLines(path).Skip(1).First();
            var timestamp = JsonDocument.Parse(line).RootElement.GetProperty("t");

            Assert.Equal(JsonValueKind.Number, timestamp.ValueKind);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RequestingAnEventStreamWithoutHeadless_IsRejected()
    {
        // The stream and the interactive operational view must never share the console.
        var ex = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["--events", "-"]));

        Assert.Contains("--headless", ex.Message);
    }

    [Fact]
    public void RequestingAnEventStreamWithReplications_IsRejected()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["--headless", "--replications", "5", "--events", "out.jsonl"]));

        Assert.Contains("--replications", ex.Message);
    }
}
