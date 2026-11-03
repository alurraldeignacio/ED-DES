using UrgentCare.Core.Configuration;
using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Tests;

/// <summary>
/// CareResource validation. A resource serves at most one patient at a time; concurrency is
/// expressed by the number of resources, never by a per-resource capacity.
/// </summary>
public class CareResourceTests
{
    internal static ResourceProfileConfig DefaultProfile() => new()
    {
        BaseEfficiency = 1.0,
        Experience = 0.8,
        ExperienceBonus = 0.25,
        ShiftLengthMinutes = 480,
        FatigueRatePerMinute = 0.000625,
        MinEfficiency = 0.5,
        MaxEfficiency = 1.5
    };

    private static CareResource NewResource(ResourceProfileConfig? profile = null) =>
        new("R1", CareResourceType.Nurse, shiftStart: 0.0, profile ?? DefaultProfile());

    private static Patient NewPatient(double serviceDuration = 18.0) =>
        new("P1", 3, 42, 0.0, serviceDuration, 90.0);

    [Fact]
    public void NewResource_IsAvailableWithNoAssignedPatient()
    {
        var resource = NewResource();

        Assert.Equal(ResourceState.Available, resource.State);
        Assert.Null(resource.AssignedPatient);
    }

    [Fact]
    public void EfficiencyIsAlwaysGreaterThanZero()
    {
        var resource = NewResource();

        // Sampled across a full week, including deep into fatigue.
        for (var minute = 0.0; minute <= 10080.0; minute += 37.0)
        {
            Assert.True(resource.EfficiencyAt(minute) > 0);
        }
    }

    [Fact]
    public void FatigueStaysWithinZeroAndOne()
    {
        var resource = NewResource();

        for (var minute = 0.0; minute <= 10080.0; minute += 37.0)
        {
            Assert.InRange(resource.FatigueAt(minute), 0.0, 1.0);
        }
    }

    [Fact]
    public void Assign_MakesTheResourceBusyAndRecordsThePatient()
    {
        var resource = NewResource();
        var patient = NewPatient();

        resource.Assign(patient, 5.0);

        Assert.Equal(ResourceState.Busy, resource.State);
        Assert.Same(patient, resource.AssignedPatient);
    }

    [Fact]
    public void SecondConcurrentAssignment_IsRejected()
    {
        var resource = NewResource();
        resource.Assign(NewPatient(), 5.0);

        var second = new Patient("P2", 2, 30, 5.0, 20.0, 60.0);

        Assert.Throws<InvalidOperationException>(() => resource.Assign(second, 6.0));
    }

    [Fact]
    public void Release_ReturnsTheResourceToAvailableWithNoPatient()
    {
        var resource = NewResource();
        resource.Assign(NewPatient(), 5.0);

        resource.Release(25.0);

        Assert.Equal(ResourceState.Available, resource.State);
        Assert.Null(resource.AssignedPatient);
    }

    [Fact]
    public void ReleaseWithoutAssignment_IsRejected()
    {
        var resource = NewResource();

        Assert.Throws<InvalidOperationException>(() => resource.Release(10.0));
    }

    [Fact]
    public void MissingId_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new CareResource("", CareResourceType.Nurse, 0.0, DefaultProfile()));
    }

    [Fact]
    public void NonPositiveBaseEfficiency_IsRejected()
    {
        var profile = DefaultProfile() with { BaseEfficiency = 0.0 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CareResource("R1", CareResourceType.Nurse, 0.0, profile));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ExperienceOutsideZeroToOne_IsRejected(double experience)
    {
        var profile = DefaultProfile() with { Experience = experience };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CareResource("R1", CareResourceType.Nurse, 0.0, profile));
    }

    [Fact]
    public void MinEfficiencyAboveMaxEfficiency_IsRejected()
    {
        var profile = DefaultProfile() with { MinEfficiency = 2.0, MaxEfficiency = 1.0 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CareResource("R1", CareResourceType.Nurse, 0.0, profile));
    }

    [Fact]
    public void NonPositiveShiftLength_IsRejected()
    {
        var profile = DefaultProfile() with { ShiftLengthMinutes = 0.0 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CareResource("R1", CareResourceType.Nurse, 0.0, profile));
    }
}
