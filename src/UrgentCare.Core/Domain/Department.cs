using UrgentCare.Core.Configuration;
using UrgentCare.Core.Queues;

namespace UrgentCare.Core.Domain;

/// <summary>
/// The operational environment: care resources, the waiting area, a finite pool of treatment
/// rooms, and the active queue policy.
/// </summary>
/// <remarks>
/// Rooms model bed capacity. A patient needs both a free care resource and a free room to enter
/// treatment, except that patients at or below <see cref="RoomBypassUrgency"/> bypass the room
/// constraint, reflecting a real department holding or clearing a bay for a critical arrival.
/// </remarks>
public sealed class Department
{
    private readonly List<CareResource> _careResources;
    private readonly List<Patient> _waitingArea = [];

    public Department(
        DepartmentConfig config,
        ResourceProfileConfig resourceProfile,
        IQueuePolicy queuePolicy)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(resourceProfile);
        ArgumentNullException.ThrowIfNull(queuePolicy);

        if (config.ResourceCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config), config.ResourceCount, "resourceCount must be non-negative.");
        }

        if (config.RoomCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config), config.RoomCount, "roomCount must be non-negative.");
        }

        Name = config.Name;
        RoomCount = config.RoomCount;
        RoomBypassUrgency = config.RoomBypassUrgency;
        QueuePolicy = queuePolicy;

        var resourceType = Enum.TryParse<CareResourceType>(config.ResourceType, out var parsed)
            ? parsed
            : CareResourceType.Nurse;

        _careResources = [.. Enumerable.Range(0, config.ResourceCount)
            .Select(i => new CareResource($"R{i + 1}", resourceType, shiftStart: 0.0, resourceProfile))];
    }

    public string Name { get; }

    public int RoomCount { get; }

    /// <summary>Patients at or below this urgency bypass the room constraint. Zero disables the bypass.</summary>
    public int RoomBypassUrgency { get; }

    public IQueuePolicy QueuePolicy { get; }

    public IReadOnlyList<CareResource> CareResources => _careResources;

    /// <summary>Patients currently waiting for treatment.</summary>
    public IReadOnlyList<Patient> WaitingArea => _waitingArea;

    public int QueueLength => _waitingArea.Count;

    /// <summary>Rooms in use, which is the number of treatments occupying a room right now.</summary>
    public int OccupiedRooms { get; private set; }

    public bool HasFreeRoom => OccupiedRooms < RoomCount;

    public void Enqueue(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);
        _waitingArea.Add(patient);
    }

    public void Remove(Patient patient) => _waitingArea.Remove(patient);

    /// <summary>The first available care resource, or null when every resource is busy.</summary>
    public CareResource? FirstAvailableResource() =>
        _careResources.FirstOrDefault(r => r.State == ResourceState.Available);

    /// <summary>
    /// Whether this patient may enter treatment now, given room occupancy. High-acuity
    /// patients are admitted even with every room full.
    /// </summary>
    public bool CanAdmit(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);
        return BypassesRooms(patient) || HasFreeRoom;
    }

    /// <summary>Takes a room for this patient, unless their acuity lets them bypass the pool.</summary>
    public void OccupyRoom(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        if (BypassesRooms(patient))
        {
            return;
        }

        if (!HasFreeRoom)
        {
            throw new InvalidOperationException(
                $"No free room for patient '{patient.Id}' in department '{Name}'.");
        }

        OccupiedRooms++;
    }

    /// <summary>Returns the room this patient was occupying, if they were using one.</summary>
    public void ReleaseRoom(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        if (BypassesRooms(patient))
        {
            return;
        }

        if (OccupiedRooms > 0)
        {
            OccupiedRooms--;
        }
    }

    /// <summary>
    /// Whether this patient's acuity exempts them from the room pool. Public so a view can show
    /// where a treated patient is without restating the rule.
    /// </summary>
    public bool BypassesRooms(Patient patient) =>
        RoomBypassUrgency > 0 && patient.Urgency <= RoomBypassUrgency;
}
