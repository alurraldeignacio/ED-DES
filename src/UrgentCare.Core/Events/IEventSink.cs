namespace UrgentCare.Core.Events;

/// <summary>
/// Destination for simulation telemetry. The engine writes here and never formats output itself.
/// </summary>
public interface IEventSink
{
    void Emit(in SimulationEvent simulationEvent);
}
