namespace UrgentCare.Core.Events;

/// <summary>
/// Event sink that retains every event for the run, and the source all metrics derive from.
/// </summary>
/// <remarks>
/// Retention limit: this log holds the entire event stream in memory. That is acceptable at
/// the default scale (roughly 1000 patients per simulated week, a few thousand events), but it
/// must be replaced by a streaming or windowed sink before long horizons or large replication
/// batches, where the log would grow without bound.
/// </remarks>
public sealed class InMemoryEventLog : IEventSink
{
    private readonly List<SimulationEvent> _events = [];

    public IReadOnlyList<SimulationEvent> Events => _events;

    public void Emit(in SimulationEvent simulationEvent) => _events.Add(simulationEvent);

    public void Clear() => _events.Clear();
}
