using UrgentCare.Core.Configuration;
using UrgentCare.Core.Domain;

namespace UrgentCare.Core.Tests;

/// <summary>
/// The fatigue and efficiency formula from data-model.md:
/// <code>
/// fatigue(t)    = min(1.0, fatigueRatePerMinute * minutesIntoCurrentShift)
/// efficiency(t) = clamp(baseEfficiency * (1 + experienceBonus * experience) * (1 - fatigue(t)),
///                       minEfficiency, maxEfficiency)
/// effectiveServiceDuration = sampledServiceDuration / efficiency(t)
/// </code>
/// This is FR-004's time-dependent clause, so it is tested directly rather than inferred
/// from a full simulation run.
/// </summary>
public class ResourceEfficiencyTests
{
    private const double Tolerance = 1e-9;

    private static CareResource NewResource(ResourceProfileConfig profile, double shiftStart = 0.0) =>
        new("R1", CareResourceType.Nurse, shiftStart, profile);

    private static Patient NewPatient(double serviceDuration) =>
        new("P1", 3, 42, 0.0, serviceDuration, 90.0);

    [Fact]
    public void AtShiftStart_ThereIsNoFatigue()
    {
        var resource = NewResource(CareResourceTests.DefaultProfile());

        Assert.Equal(0.0, resource.FatigueAt(0.0), Tolerance);
    }

    [Fact]
    public void FatigueAccruesLinearlyThroughTheShift()
    {
        var resource = NewResource(CareResourceTests.DefaultProfile());

        // 0.000625 per minute * 240 minutes = 0.15 at the half-shift mark.
        Assert.Equal(0.15, resource.FatigueAt(240.0), Tolerance);

        // ...and 0.30 by the end of the 480-minute shift.
        Assert.Equal(0.30, resource.FatigueAt(480.0), Tolerance);
    }

    [Fact]
    public void DefaultProfile_MatchesTheEfficiencyFiguresDocumentedInTheContract()
    {
        // contracts/simulation-config.md states a resource starts a shift at efficiency 1.2
        // and ends an 8-hour shift at 0.84.
        var resource = NewResource(CareResourceTests.DefaultProfile());

        Assert.Equal(1.2, resource.EfficiencyAt(0.0), Tolerance);
        Assert.Equal(0.84, resource.EfficiencyAt(480.0), Tolerance);
    }

    [Fact]
    public void FatigueResetsWithEachNewShift()
    {
        // Without a per-shift reset, a week-long run would pin every resource at
        // minEfficiency forever, which would make shiftLengthMinutes dead configuration.
        // Sampled inside a shift rather than on a boundary: an exact multiple of the shift
        // length is the end of that shift, not the start of the next one.
        var resource = NewResource(CareResourceTests.DefaultProfile());

        Assert.Equal(resource.FatigueAt(10.0), resource.FatigueAt(480.0 * 3 + 10.0), Tolerance);
        Assert.Equal(resource.FatigueAt(240.0), resource.FatigueAt(480.0 * 3 + 240.0), Tolerance);
    }

    [Fact]
    public void FatigueIsMeasuredFromTheResourcesOwnShiftStart()
    {
        var resource = NewResource(CareResourceTests.DefaultProfile(), shiftStart: 100.0);

        Assert.Equal(0.0, resource.FatigueAt(100.0), Tolerance);
        Assert.Equal(0.15, resource.FatigueAt(340.0), Tolerance);
    }

    [Fact]
    public void BeforeTheShiftStarts_ThereIsNoFatigue()
    {
        var resource = NewResource(CareResourceTests.DefaultProfile(), shiftStart: 100.0);

        Assert.Equal(0.0, resource.FatigueAt(0.0), Tolerance);
    }

    [Fact]
    public void EfficiencyClampsAtMaxEfficiency()
    {
        // A large experience bonus would push efficiency above the ceiling.
        var profile = CareResourceTests.DefaultProfile() with
        {
            ExperienceBonus = 5.0,
            MaxEfficiency = 1.5
        };
        var resource = NewResource(profile);

        Assert.Equal(1.5, resource.EfficiencyAt(0.0), Tolerance);
    }

    [Fact]
    public void EfficiencyClampsAtMinEfficiency()
    {
        // A punishing fatigue rate drives raw efficiency below the floor.
        var profile = CareResourceTests.DefaultProfile() with
        {
            FatigueRatePerMinute = 0.01,
            MinEfficiency = 0.5
        };
        var resource = NewResource(profile);

        Assert.Equal(0.5, resource.EfficiencyAt(480.0), Tolerance);
    }

    [Fact]
    public void EfficiencyNeverLeavesTheConfiguredBounds()
    {
        var profile = CareResourceTests.DefaultProfile() with { FatigueRatePerMinute = 0.005 };
        var resource = NewResource(profile);

        for (var minute = 0.0; minute <= 10080.0; minute += 13.0)
        {
            Assert.InRange(resource.EfficiencyAt(minute), profile.MinEfficiency, profile.MaxEfficiency);
        }
    }

    [Fact]
    public void EffectiveDuration_IsShorterThanSampledWhenEfficiencyIsAboveOne()
    {
        var resource = NewResource(CareResourceTests.DefaultProfile());
        var patient = NewPatient(serviceDuration: 18.0);

        // 18 / 1.2 = 15
        Assert.Equal(15.0, resource.EffectiveServiceDuration(patient, 0.0), Tolerance);
    }

    [Fact]
    public void EffectiveDuration_LengthensAsEfficiencyFalls()
    {
        var resource = NewResource(CareResourceTests.DefaultProfile());
        var patient = NewPatient(serviceDuration: 18.0);

        var atShiftStart = resource.EffectiveServiceDuration(patient, 0.0);
        var atShiftEnd = resource.EffectiveServiceDuration(patient, 480.0);

        Assert.True(atShiftEnd > atShiftStart);

        // 18 / 0.84
        Assert.Equal(18.0 / 0.84, atShiftEnd, Tolerance);
    }

    [Fact]
    public void EffectiveDuration_IsAlwaysPositive()
    {
        var profile = CareResourceTests.DefaultProfile() with { FatigueRatePerMinute = 0.01 };
        var resource = NewResource(profile);
        var patient = NewPatient(serviceDuration: 18.0);

        for (var minute = 0.0; minute <= 2000.0; minute += 17.0)
        {
            Assert.True(resource.EffectiveServiceDuration(patient, minute) > 0);
        }
    }

    [Fact]
    public void ZeroFatigueRate_KeepsEfficiencyFlat()
    {
        var profile = CareResourceTests.DefaultProfile() with { FatigueRatePerMinute = 0.0 };
        var resource = NewResource(profile);

        Assert.Equal(resource.EfficiencyAt(0.0), resource.EfficiencyAt(5000.0), Tolerance);
    }
}
