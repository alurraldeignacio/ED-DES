using UrgentCare.Core.Events;

namespace UrgentCare.Cli;

/// <summary>
/// Fans one event stream out to several sinks. The engine emits to exactly one sink, but a run
/// usually needs two destinations at once: the in-memory log every metric is derived from, and
/// the machine-readable stream written to a file or standard output.
/// </summary>
public sealed class CompositeEventSink : IEventSink, IDisposable
{
    private readonly IReadOnlyList<IEventSink> _sinks;

    public CompositeEventSink(params IEventSink[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = sinks;
    }

    public void Emit(in SimulationEvent simulationEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Emit(simulationEvent);
        }
    }

    /// <summary>Disposes the sinks that hold resources; the others are left alone.</summary>
    public void Dispose()
    {
        foreach (var sink in _sinks.OfType<IDisposable>())
        {
            sink.Dispose();
        }
    }
}
