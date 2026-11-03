using System.Globalization;
using System.Text;
using System.Text.Json;
using UrgentCare.Core.Events;

namespace UrgentCare.Cli;

/// <summary>
/// Writes the simulation event stream as JSON Lines: one header record, then one record per
/// event. This is the machine-readable telemetry stream, entirely separate from the human-facing
/// operational view, and the two never share a writer.
/// </summary>
/// <remarks>
/// The schema version belongs to the file, not to each event (research.md). A header carrying it
/// once satisfies the versioned-boundary rule (Constitution Principle IV) at the point where
/// persistence actually happens, while <see cref="SimulationEvent"/> stays a small in-process
/// record constructed on every arrival, treatment and departure. A reader must check the header's
/// major version before trusting the records that follow.
/// </remarks>
public sealed class JsonLinesEventSink : IEventSink, IDisposable
{
    /// <summary>Version of the event-file format itself, independent of any one scenario.</summary>
    public const string EventStreamVersion = "1.0.0";

    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;

    private JsonLinesEventSink(TextWriter writer, bool ownsWriter)
    {
        _writer = writer;
        _ownsWriter = ownsWriter;
    }

    /// <summary>
    /// Opens a sink writing to <paramref name="path"/>, replacing any existing file, and writes
    /// the header immediately so a truncated run still identifies itself.
    /// </summary>
    public static JsonLinesEventSink ToFile(
        string path,
        string schemaVersion,
        string scenarioId,
        Guid runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // No byte-order mark: a BOM on the first line trips naive JSON Lines readers that
        // parse each line independently.
        var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var sink = new JsonLinesEventSink(writer, ownsWriter: true);

        sink.WriteHeader(schemaVersion, scenarioId, runId);

        return sink;
    }

    /// <summary>
    /// Opens a sink writing to an existing writer, typically standard output. The writer is not
    /// owned, so disposing the sink leaves it open.
    /// </summary>
    public static JsonLinesEventSink To(
        TextWriter writer,
        string schemaVersion,
        string scenarioId,
        Guid runId)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var sink = new JsonLinesEventSink(writer, ownsWriter: false);

        sink.WriteHeader(schemaVersion, scenarioId, runId);

        return sink;
    }

    public void Emit(in SimulationEvent simulationEvent)
    {
        var builder = new StringBuilder();

        builder.Append('{');
        Append(builder, "t", simulationEvent.Timestamp);
        builder.Append(',');
        AppendText(builder, "type", simulationEvent.Type.ToString());
        AppendOptionalText(builder, "patientId", simulationEvent.PatientId);
        AppendOptionalText(builder, "resourceId", simulationEvent.ResourceId);

        if (simulationEvent.Urgency is { } urgency)
        {
            builder.Append(',');
            Append(builder, "urgency", urgency);
        }

        builder.Append(',');
        Append(builder, "queueLength", simulationEvent.QueueLength);
        builder.Append(',');
        Append(builder, "replication", simulationEvent.ReplicationIndex);
        builder.Append('}');

        _writer.WriteLine(builder.ToString());
    }

    /// <summary>
    /// The one record that carries the format version and the correlation keys shared by every
    /// event below it, so those keys are not repeated on every line.
    /// </summary>
    private void WriteHeader(string schemaVersion, string scenarioId, Guid runId)
    {
        var header = new StringBuilder();

        header.Append('{');
        AppendText(header, "record", "header");
        header.Append(',');
        AppendText(header, "eventStreamVersion", EventStreamVersion);
        header.Append(',');
        AppendText(header, "schemaVersion", schemaVersion);
        header.Append(',');
        AppendText(header, "scenarioId", scenarioId);
        header.Append(',');
        AppendText(header, "runId", runId.ToString());
        header.Append('}');

        _writer.WriteLine(header.ToString());
    }

    private static void Append(StringBuilder builder, string name, double value) =>
        builder.Append(JsonEncodedName(name)).Append(value.ToString("R", CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, string name, int value) =>
        builder.Append(JsonEncodedName(name)).Append(value.ToString(CultureInfo.InvariantCulture));

    private static void AppendText(StringBuilder builder, string name, string value) =>
        builder.Append(JsonEncodedName(name)).Append(JsonSerializer.Serialize(value));

    private static void AppendOptionalText(StringBuilder builder, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        builder.Append(',');
        AppendText(builder, name, value);
    }

    private static string JsonEncodedName(string name) => "\"" + name + "\":";

    public void Dispose()
    {
        _writer.Flush();

        if (_ownsWriter)
        {
            _writer.Dispose();
        }
    }
}
