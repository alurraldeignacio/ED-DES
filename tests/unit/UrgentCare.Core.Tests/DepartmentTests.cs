using UrgentCare.Core.Configuration;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Queues;

namespace UrgentCare.Core.Tests;

/// <summary>
/// The room-bypass rule. A view draws treated patients in a room or in the bypass area, so it
/// asks the department rather than restating the rule (feature 002, research.md §4).
/// </summary>
public class DepartmentTests
{
    private static Department NewDepartment(int roomBypassUrgency) => new(
        new DepartmentConfig
        {
            Name = "Test",
            ResourceCount = 1,
            ResourceType = "Nurse",
            QueuePolicy = "UrgencyFirst",
            RoomCount = 2,
            RoomBypassUrgency = roomBypassUrgency
        },
        CareResourceTests.DefaultProfile(),
        new UrgencyFirstPolicy());

    private static Patient PatientWithUrgency(int urgency) =>
        new($"P{urgency}", urgency, 42, 0.0, 18.0, 90.0);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PatientAtOrBelowThreshold_BypassesRooms(int urgency)
    {
        var department = NewDepartment(roomBypassUrgency: 2);

        Assert.True(department.BypassesRooms(PatientWithUrgency(urgency)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void PatientAboveThreshold_NeedsARoom(int urgency)
    {
        var department = NewDepartment(roomBypassUrgency: 2);

        Assert.False(department.BypassesRooms(PatientWithUrgency(urgency)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ZeroThreshold_DisablesTheBypassForEveryone(int urgency)
    {
        var department = NewDepartment(roomBypassUrgency: 0);

        Assert.False(department.BypassesRooms(PatientWithUrgency(urgency)));
    }
}
