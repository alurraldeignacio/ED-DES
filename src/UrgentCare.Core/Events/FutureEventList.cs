namespace UrgentCare.Core.Events;

/// <summary>
/// Time-ordered queue of pending events, the spine of the discrete-event loop. Events come
/// out in timestamp order, and events sharing a timestamp come out in the order they were
/// scheduled, which is what keeps a seeded run reproducible.
/// </summary>
public sealed class FutureEventList
{
    private readonly PriorityQueue<ScheduledEvent, (double Time, long Sequence)> _queue = new();
    private long _nextSequence;

    public int Count => _queue.Count;

    public bool IsEmpty => _queue.Count == 0;

    /// <summary>Schedules an event, stamping it with the next sequence number.</summary>
    public ScheduledEvent Schedule(
        double time,
        ScheduledEventType type,
        string? patientId = null,
        string? resourceId = null)
    {
        var scheduled = new ScheduledEvent(time, type, _nextSequence++, patientId, resourceId);
        _queue.Enqueue(scheduled, (scheduled.Time, scheduled.Sequence));
        return scheduled;
    }

    /// <summary>Removes and returns the earliest pending event.</summary>
    public ScheduledEvent Dequeue()
    {
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException("The future event list is empty.");
        }

        return _queue.Dequeue();
    }

    /// <summary>Returns the earliest pending event without removing it.</summary>
    public bool TryPeek(out ScheduledEvent scheduledEvent)
    {
        if (_queue.TryPeek(out var next, out _))
        {
            scheduledEvent = next;
            return true;
        }

        scheduledEvent = default;
        return false;
    }

    public void Clear()
    {
        _queue.Clear();
        _nextSequence = 0;
    }
}
