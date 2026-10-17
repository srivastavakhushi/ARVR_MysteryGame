using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimeSceneVR.Person3.Analytics;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;
using CrimeSceneVR.Person3.Export;

namespace CrimeSceneVR.Person3.Tests
{
    [TestFixture]
    public class Person3UnitTests
    {
        [Test]
        public void SessionData_GeneratesValidUniqueSessionId()
        {
            var session1 = new SessionData("P001", "Condition_A");
            var session2 = new SessionData("P001", "Condition_A");

            Assert.IsNotNull(session1.SessionId);
            Assert.IsNotNull(session2.SessionId);
            Assert.AreNotEqual(session1.SessionId, session2.SessionId, "Each session must have a unique ID.");
            Assert.AreEqual("P001", session1.ParticipantId);
            Assert.AreEqual("Condition_A", session1.Condition);
            Assert.AreEqual(SessionStatus.InProgress, session1.Status);
        }

        [Test]
        public void KendallTau_PerfectAgreement_ReturnsOne()
        {
            var gt = new List<string> { "EV-001", "EV-002", "EV-003", "EV-004", "EV-005" };
            var sub = new List<string> { "EV-001", "EV-002", "EV-003", "EV-004", "EV-005" };

            var result = KendallTau.Calculate(gt, sub);

            Assert.AreEqual(1.0, result.Tau, 0.001, "Identical sequence must yield Tau = 1.0.");
            Assert.AreEqual(1.0, result.NormalizedScore, 0.001);
            Assert.AreEqual(0, result.DiscordantPairs);
        }

        [Test]
        public void KendallTau_PerfectInversion_ReturnsMinusOne()
        {
            var gt = new List<string> { "EV-001", "EV-002", "EV-003", "EV-004" };
            var sub = new List<string> { "EV-004", "EV-003", "EV-002", "EV-001" };

            var result = KendallTau.Calculate(gt, sub);

            Assert.AreEqual(-1.0, result.Tau, 0.001, "Completely inverted sequence must yield Tau = -1.0.");
            Assert.AreEqual(0.0, result.NormalizedScore, 0.001);
            Assert.AreEqual(0, result.ConcordantPairs);
        }

        [Test]
        public void KendallTau_IncompleteSequence_PenalizesOmissions()
        {
            var gt = new List<string> { "EV-001", "EV-002", "EV-003", "EV-004" };
            // Participant only submitted 2 items:
            var sub = new List<string> { "EV-001", "EV-002" };

            var resultPenalized = KendallTau.Calculate(gt, sub, penalizeOmissions: true);
            var resultUnpenalized = KendallTau.Calculate(gt, sub, penalizeOmissions: false);

            Assert.IsTrue(resultPenalized.Tau < resultUnpenalized.Tau, "Penalized Tau must be strictly less than unpenalized Tau for omissions.");
        }

        [Test]
        public void KendallTau_EmptyOrNullSubmission_SafelyHandlesEdgeCases()
        {
            var gt = new List<string> { "EV-001", "EV-002", "EV-003" };

            var nullSubResult = KendallTau.Calculate(gt, null);
            var emptySubResult = KendallTau.Calculate(gt, new List<string>());
            var nullGtResult = KendallTau.Calculate(null, gt);

            Assert.IsFalse(double.IsNaN(nullSubResult.Tau));
            Assert.IsFalse(double.IsNaN(emptySubResult.Tau));
            Assert.IsFalse(double.IsNaN(nullGtResult.Tau));
        }

        [Test]
        public void TimelineManager_IdentifiesOmissionsAndDuplicates()
        {
            var tmGo = new GameObject("TestTimelineManager");
            var tm = tmGo.AddComponent<TimelineManager>();
            tm.SetGroundTruthSequence(new[] { "EV-001", "EV-002", "EV-003", "EV-004" });

            // Submission has duplicate EV-001 and omits EV-004, with unexpected EV-099
            var sub = new List<string> { "EV-001", "EV-001", "EV-003", "EV-099" };
            var result = tm.SubmitTimeline(sub);

            Assert.IsFalse(result.IsComplete);
            Assert.IsTrue(result.OmittedEvidenceIds.Contains("EV-002"));
            Assert.IsTrue(result.OmittedEvidenceIds.Contains("EV-004"));
            Assert.IsTrue(result.DuplicateEvidenceIds.Contains("EV-001"));
            Assert.IsTrue(result.UnexpectedEvidenceIds.Contains("EV-099"));

            UnityEngine.Object.DestroyImmediate(tmGo);
        }

        [Test]
        public void ResearchMetrics_AggregatesSpatialAccuracyAndTimes()
        {
            var session = new SessionData("P001", "Condition_A");
            session.MarkComplete(120.0f);

            var records = new List<TelemetryRecord>
            {
                new TelemetryRecord(10.0, TelemetryEventType.EvidenceDiscovered, evidenceId: "EV-001", spatialError: 0.03f), // Within 5cm
                new TelemetryRecord(25.0, TelemetryEventType.EvidenceTagged, evidenceId: "EV-002", spatialError: 0.08f),     // Outside 5cm
                new TelemetryRecord(40.0, TelemetryEventType.EvidenceTagged, evidenceId: "EV-003", spatialError: 0.04f)      // Within 5cm
            };

            var summary = ResearchMetrics.ComputeSummary(session, records, targetEvidenceCount: 8);

            Assert.AreEqual(3, summary.EvidenceDiscoveredCount);
            Assert.AreEqual(10.0, summary.TimeToFirstDiscoverySeconds.Value, 0.001);
            Assert.AreEqual(15.0, summary.AverageDiscoveryIntervalSeconds.Value, 0.001); // (40 - 10) / 2 = 15s
            Assert.AreEqual(2, summary.SpatialWithinToleranceCount, "2 out of 3 tags were <= 5cm.");
            Assert.AreEqual(66.666, summary.SpatialWithinTolerancePercent, 1.0);
            Assert.AreEqual(0.05, summary.MeanSpatialErrorMeters.Value, 0.001); // (0.03 + 0.08 + 0.04) / 3 = 0.05m
        }
    }
}
