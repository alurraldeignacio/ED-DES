using UrgentCare.Core.Domain;
using UrgentCare.Core.Queues;

namespace UrgentCare.Core.Tests;

/// <summary>
/// Ordering rules for the two policies added in US2. FifoPolicy ignores acuity entirely, which
/// is what makes it a useful baseline to compare UrgencyFirst against. UrgencyPlusWaitPolicy
/// starts from acuity but lets waiting time escalate a patient within the non-critical band, so
/// a low-acuity patient cannot be starved indefinitely by a stream of more urgent arrivals —
/// while ESI 1 and 2 stay protected and are never overtaken.
/// </summary>
public class QueuePolicyTests
{
    private static Patient PatientWith(string id, int urgency, double arrivalTime) =>
        new(id, urgency, 42, arrivalTime, 18.0, 90.0);

    public class Fifo
    {
        private readonly FifoPolicy _policy = new();

        [Fact]
        public void PolicyExposesItsName()
        {
            Assert.Equal("FIFO", _policy.Name);
        }

        [Fact]
        public void EarliestArrivalIsServedFirst()
        {
            var waiting = new List<Patient>
            {
                PatientWith("P-second", 1, 50.0),
                PatientWith("P-first", 5, 10.0),
                PatientWith("P-third", 3, 90.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 100.0);

            Assert.NotNull(next);
            Assert.Equal("P-first", next.Id);
        }

        [Fact]
        public void AcuityDoesNotAffectOrdering()
        {
            // This is the whole point of the baseline: a resuscitation case that arrives late
            // waits behind a non-urgent case that arrived early.
            var waiting = new List<Patient>
            {
                PatientWith("P-non-urgent", 5, 0.0),
                PatientWith("P-critical", 1, 30.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 30.0);

            Assert.NotNull(next);
            Assert.Equal("P-non-urgent", next.Id);
        }

        [Fact]
        public void PatientsAreServedInStrictArrivalOrder()
        {
            var waiting = new List<Patient>
            {
                PatientWith("P3", 1, 20.0),
                PatientWith("P1", 5, 0.0),
                PatientWith("P4", 2, 30.0),
                PatientWith("P2", 3, 10.0)
            };

            var served = new List<string>();
            while (waiting.Count > 0)
            {
                var next = _policy.SelectNext(waiting, currentTime: 40.0);
                Assert.NotNull(next);
                served.Add(next.Id);
                waiting.Remove(next);
            }

            Assert.Equal(["P1", "P2", "P3", "P4"], served);
        }

        [Fact]
        public void EqualArrivalTimes_BreakTiesByIdSoTheChoiceIsStable()
        {
            var waiting = new List<Patient>
            {
                PatientWith("P-b", 3, 10.0),
                PatientWith("P-a", 1, 10.0)
            };

            var first = _policy.SelectNext(waiting, currentTime: 20.0);
            var second = _policy.SelectNext(waiting, currentTime: 20.0);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal("P-a", first.Id);
            Assert.Equal(first.Id, second.Id);
        }

        [Fact]
        public void EmptyQueue_ReturnsNull()
        {
            Assert.Null(_policy.SelectNext([], currentTime: 0.0));
        }

        [Fact]
        public void PatientsNoLongerWaiting_AreNotSelected()
        {
            var treated = PatientWith("P-in-treatment", 3, 0.0);
            treated.StartTreatment(5.0);

            var waiting = new List<Patient> { treated, PatientWith("P-waiting", 3, 10.0) };

            var next = _policy.SelectNext(waiting, currentTime: 20.0);

            Assert.NotNull(next);
            Assert.Equal("P-waiting", next.Id);
        }
    }

    public class UrgencyPlusWait
    {
        /// <summary>Every 30 minutes of waiting is worth one step of acuity in these tests.</summary>
        private readonly UrgencyPlusWaitPolicy _policy = new(escalationMinutes: 30.0);

        [Fact]
        public void PolicyExposesItsName()
        {
            Assert.Equal("UrgencyPlusWait", _policy.Name);
        }

        [Fact]
        public void WithNobodyWaiting_TheMostAcutePatientIsServedFirst()
        {
            // At currentTime equal to arrival time nobody has accrued any wait, so the policy
            // reduces to UrgencyFirst.
            var waiting = new List<Patient>
            {
                PatientWith("P-low", 5, 100.0),
                PatientWith("P-critical", 1, 100.0),
                PatientWith("P-mid", 3, 100.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 100.0);

            Assert.NotNull(next);
            Assert.Equal("P-critical", next.Id);
        }

        [Fact]
        public void ALongWaitingLessAcutePatient_OvertakesAFreshMoreAcuteOne()
        {
            // Both sit in the escalating band. Urgency 5 waiting 90 minutes scores 5 - 3 = 2;
            // urgency 3 just arrived scores 3.
            var waiting = new List<Patient>
            {
                PatientWith("P-waited-ages", 5, 0.0),
                PatientWith("P-just-arrived", 3, 90.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 90.0);

            Assert.NotNull(next);
            Assert.Equal("P-waited-ages", next.Id);
        }

        [Fact]
        public void ShortWait_IsNotEnoughToOvertakeAMoreAcutePatient()
        {
            // Urgency 5 waiting 15 minutes scores 4.5, still behind a fresh urgency 3.
            var waiting = new List<Patient>
            {
                PatientWith("P-waited-briefly", 5, 75.0),
                PatientWith("P-just-arrived", 3, 90.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 90.0);

            Assert.NotNull(next);
            Assert.Equal("P-just-arrived", next.Id);
        }

        [Fact]
        public void NoPatientIsStarvedForever()
        {
            // Starvation under pure acuity looks like this: a non-urgent patient sits while a
            // stream of more urgent patients keeps arriving fresh. Within the escalating band,
            // escalation must eventually put the long waiter in front of the newest arrival
            // (FR-005).
            var stuck = PatientWith("P-non-urgent", 5, 0.0);
            var justArrived = PatientWith("P-urgent", 3, 300.0);
            var waiting = new List<Patient> { stuck, justArrived };

            // Stuck scores 5 - 300/30 = -5; the fresh arrival scores 3.
            var next = _policy.SelectNext(waiting, currentTime: 300.0);

            Assert.NotNull(next);
            Assert.Equal("P-non-urgent", next.Id);
        }

        [Fact]
        public void EqualScores_BreakTiesByEarliestArrival()
        {
            var waiting = new List<Patient>
            {
                PatientWith("P-later", 3, 50.0),
                PatientWith("P-earlier", 3, 20.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 50.0);

            Assert.NotNull(next);
            Assert.Equal("P-earlier", next.Id);
        }

        [Fact]
        public void SelectionIsStableForIdenticalInput()
        {
            var waiting = new List<Patient>
            {
                PatientWith("P-a", 3, 10.0),
                PatientWith("P-b", 3, 10.0)
            };

            var first = _policy.SelectNext(waiting, currentTime: 40.0);
            var second = _policy.SelectNext(waiting, currentTime: 40.0);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first.Id, second.Id);
        }

        [Fact]
        public void EmptyQueue_ReturnsNull()
        {
            Assert.Null(_policy.SelectNext([], currentTime: 0.0));
        }

        [Fact]
        public void PatientsNoLongerWaiting_AreNotSelected()
        {
            var treated = PatientWith("P-in-treatment", 1, 0.0);
            treated.StartTreatment(5.0);

            var waiting = new List<Patient> { treated, PatientWith("P-waiting", 5, 0.0) };

            var next = _policy.SelectNext(waiting, currentTime: 10.0);

            Assert.NotNull(next);
            Assert.Equal("P-waiting", next.Id);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void AProtectedPatientIsNeverOvertaken_HoweverLongOthersHaveWaited(int urgency)
        {
            // The clinical invariant. A resuscitation or emergent case cannot wait, so no amount
            // of accrued waiting time promotes anyone past them. A full day of waiting would
            // score -43 on the raw formula, which is why the tiers are selected separately
            // rather than relying on the arithmetic.
            var waiting = new List<Patient>
            {
                PatientWith("P-waited-all-day", 5, 0.0),
                PatientWith("P-critical", urgency, 1440.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 1440.0);

            Assert.NotNull(next);
            Assert.Equal("P-critical", next.Id);
        }

        [Fact]
        public void WithinTheProtectedTier_TheMoreAcutePatientGoesFirst()
        {
            // Escalation does not operate inside the protected band either: ESI 1 outranks ESI 2
            // regardless of who waited longer.
            var waiting = new List<Patient>
            {
                PatientWith("P-emergent", 2, 0.0),
                PatientWith("P-resuscitation", 1, 600.0)
            };

            var next = _policy.SelectNext(waiting, currentTime: 600.0);

            Assert.NotNull(next);
            Assert.Equal("P-resuscitation", next.Id);
        }

        [Fact]
        public void TheProtectedTierIsFullyDrainedBeforeTheEscalatingBand()
        {
            var waiting = new List<Patient>
            {
                PatientWith("P-waited-forever", 4, 0.0),
                PatientWith("P-emergent", 2, 900.0),
                PatientWith("P-resuscitation", 1, 900.0)
            };

            var served = new List<string>();
            while (waiting.Count > 0)
            {
                var next = _policy.SelectNext(waiting, currentTime: 900.0);
                Assert.NotNull(next);
                served.Add(next.Id);
                waiting.Remove(next);
            }

            Assert.Equal(["P-resuscitation", "P-emergent", "P-waited-forever"], served);
        }

        [Fact]
        public void WithProtectionDisabled_ThePolicyEscalatesAcrossEveryAcuity()
        {
            // protectedUrgency 0 turns the protection off, matching the roomBypassUrgency
            // convention. This is the unsafe behavior, available only by asking for it.
            var unprotected = new UrgencyPlusWaitPolicy(escalationMinutes: 30.0, protectedUrgency: 0);

            var waiting = new List<Patient>
            {
                PatientWith("P-waited-all-day", 5, 0.0),
                PatientWith("P-critical", 1, 1440.0)
            };

            var next = unprotected.SelectNext(waiting, currentTime: 1440.0);

            Assert.NotNull(next);
            Assert.Equal("P-waited-all-day", next.Id);
        }

        [Fact]
        public void EscalationIntervalMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UrgencyPlusWaitPolicy(0.0));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        public void AProtectedUrgencyOutsideTheEsiScale_IsRejected(int protectedUrgency)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new UrgencyPlusWaitPolicy(30.0, protectedUrgency));
        }
    }
}
