using UrgentCare.Core.Domain;
using UrgentCare.Web.Session;

namespace UrgentCare.Web.Tests;

/// <summary>
/// The snapshot turns engine state into a picture. It must show what the engine would do, in
/// the engine's own terms, and must never disturb the run it is drawing (FR-002, FR-006,
/// FR-008, FR-013).
/// </summary>
public class DepartmentSnapshotTests
{
    [Fact]
    public void WaitingLine_IsInTheOrderThePolicyWillServe()
    {
        var session = TestSessions.Create("default");
        Assert.True(TestSessions.PlayUntil(session, s => s.Engine.Department.QueueLength >= 6));

        var snapshot = session.Snapshot();

        var department = session.Engine.Department;
        var remaining = department.WaitingArea.ToList();
        var expected = new List<string>();
        while (remaining.Count > 0 && expected.Count < DepartmentSnapshot.MaxDrawnWaiting)
        {
            var next = department.QueuePolicy.SelectNext(remaining, session.DisplayTime)!;
            expected.Add(next.Id);
            remaining.Remove(next);
        }

        Assert.Equal(expected, snapshot.WaitingInServeOrder.Select(p => p.Id));

        // default runs UrgencyFirst, so serve order never puts a less acute patient first.
        var urgencies = snapshot.WaitingInServeOrder.Select(p => p.Urgency).ToList();
        Assert.Equal(urgencies.Order(), urgencies);
    }

    [Fact]
    public void LongQueue_DrawsThirtyAndCountsTheRest()
    {
        // lean peaks at 33 waiting with its own seed (CLI headless output).
        var session = TestSessions.Create("lean");
        var reached = TestSessions.PlayUntil(
            session, s => s.Engine.Department.QueueLength > DepartmentSnapshot.MaxDrawnWaiting);
        Assert.True(reached, "lean never queued more than 30; the scenario changed, pick another fixture.");

        var snapshot = session.Snapshot();

        Assert.Equal(DepartmentSnapshot.MaxDrawnWaiting, snapshot.WaitingInServeOrder.Count);
        Assert.Equal(
            session.Engine.Department.QueueLength - DepartmentSnapshot.MaxDrawnWaiting,
            snapshot.HiddenWaitingCount);
    }

    [Fact]
    public void EveryTreatedPatient_IsInARoomOrInTheBypass_AsTheDepartmentSays()
    {
        var session = TestSessions.Create("default");
        var sawBypass = false;
        var frames = 0;

        TestSessions.PlayUntil(session, s =>
        {
            if (frames++ % 20 != 0)
            {
                return false;
            }

            var snapshot = s.Snapshot();
            var department = s.Engine.Department;
            var inRooms = snapshot.PatientsInRooms.Select(p => p.Id).ToHashSet();
            var bypassing = snapshot.PatientsBypassingRooms.Select(p => p.Id).ToHashSet();

            foreach (var resource in department.CareResources.Where(r => r.AssignedPatient is not null))
            {
                var patient = resource.AssignedPatient!;
                var expectBypass = department.BypassesRooms(patient);

                Assert.Equal(expectBypass, bypassing.Contains(patient.Id));
                Assert.Equal(!expectBypass, inRooms.Contains(patient.Id));

                var staff = snapshot.Staff.Single(st => st.Id == resource.Id);
                Assert.Equal(ResourceState.Busy, staff.State);
                Assert.Equal(patient.Id, staff.Patient?.Id);
            }

            Assert.Equal(department.OccupiedRooms, snapshot.PatientsInRooms.Count);
            sawBypass |= bypassing.Count > 0;
            return false;
        }, minutesPerFrame: 1.0);

        Assert.True(sawBypass, "no bypass treatment was ever drawn, so the bypass branch went untested.");
    }

    [Fact]
    public void BuildingASnapshot_LeavesTheRunUntouched()
    {
        var session = TestSessions.Create("default");
        Assert.True(TestSessions.PlayUntil(session, s => s.Engine.Department.QueueLength >= 6));

        var waitingBefore = session.Engine.Department.WaitingArea.Select(p => p.Id).ToList();
        var eventsBefore = session.Log.Events.Count;
        var nextBefore = session.Engine.NextEventTime;

        _ = session.Snapshot();
        _ = session.Snapshot();

        Assert.Equal(waitingBefore, session.Engine.Department.WaitingArea.Select(p => p.Id));
        Assert.Equal(eventsBefore, session.Log.Events.Count);
        Assert.Equal(nextBefore, session.Engine.NextEventTime);
    }
}
