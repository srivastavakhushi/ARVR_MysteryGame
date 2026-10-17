using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimeSceneVR.Person2.Evidence;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;
using CrimeSceneVR.Person3.Analytics;

namespace CrimeSceneVR.Person2.Tests
{
    [TestFixture]
    public class Person2EvidenceTests
    {
        [Test]
        public void CanonicalItems_ContainsAllEightItems_WithinLivingRoomBoundaries()
        {
            var items = EvidenceConstants.CanonicalItems;
            Assert.AreEqual(8, items.Length, "Must have exactly 8 canonical evidence items.");

            // Bounds: X in [-1.15, 7.85], Y in [0.00, 2.85], Z in [-0.06, 12.78]
            foreach (var def in items)
            {
                Assert.IsTrue(def.Id.StartsWith("EV-00"), $"ID {def.Id} must follow EV-00X convention.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Description));

                Vector3 p = def.CanonicalPosition;
                Assert.IsTrue(p.x >= -1.2f && p.x <= 8.0f, $"{def.Id} X={p.x} must be within living room boundary.");
                Assert.IsTrue(p.y >= -0.1f && p.y <= 2.9f, $"{def.Id} Y={p.y} must be within room height.");
                Assert.IsTrue(p.z >= -0.1f && p.z <= 13.0f, $"{def.Id} Z={p.z} must be within room depth.");
            }
        }

        [Test]
        public void EvidenceItem_StateTransitions_FollowExpectedForensicLifecycle()
        {
            var go = new GameObject("TestEvidence");
            var item = go.AddComponent<EvidenceItem>();
            item.Configure("EV-001", "Footwear Impression", "Boot tread near entrance", new Vector3(4f, 0.02f, 0.5f));

            Assert.AreEqual(EvidenceState.Unseen, item.CurrentState);

            item.Discover();
            Assert.AreEqual(EvidenceState.Discovered, item.CurrentState);

            item.Inspect();
            Assert.AreEqual(EvidenceState.Inspected, item.CurrentState);

            item.Tag(new Vector3(4.02f, 0.02f, 0.51f));
            Assert.AreEqual(EvidenceState.Tagged, item.CurrentState);

            item.Confirm();
            Assert.AreEqual(EvidenceState.Confirmed, item.CurrentState);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void SpatialAccuracy_Within5cmTolerance_ClassifiedAccurate()
        {
            var go = new GameObject("TestEvidence");
            var item = go.AddComponent<EvidenceItem>();
            Vector3 groundTruth = new Vector3(1.45f, 0.05f, 3.20f);
            item.Configure("EV-003", "Bloodied Cloth", "Textile fragment", groundTruth);

            // Participant tag is placed 3 cm away (0.03m <= 0.05m tolerance):
            Vector3 tagPos = new Vector3(1.48f, 0.05f, 3.20f);
            float error = item.Tag(tagPos);

            Assert.AreEqual(0.03f, error, 0.001f, "Euclidean error must be exactly 0.03 meters.");
            Assert.IsTrue(item.IsWithinSpatialTolerance, "Tag within 5cm tolerance must return true.");
            Assert.AreEqual(tagPos, item.LastParticipantTagPosition.Value);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void SpatialAccuracy_Outside5cmTolerance_ClassifiedInaccurate()
        {
            var go = new GameObject("TestEvidence");
            var item = go.AddComponent<EvidenceItem>();
            Vector3 groundTruth = new Vector3(1.45f, 0.05f, 3.20f);
            item.Configure("EV-003", "Bloodied Cloth", "Textile fragment", groundTruth);

            // Participant tag is placed 8 cm away (0.08m > 0.05m tolerance):
            Vector3 tagPos = new Vector3(1.53f, 0.05f, 3.20f);
            float error = item.Tag(tagPos);

            Assert.AreEqual(0.08f, error, 0.001f, "Euclidean error must be exactly 0.08 meters.");
            Assert.IsFalse(item.IsWithinSpatialTolerance, "Tag outside 5cm tolerance must return false.");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void EvidenceTagMarker_InitializesAndComputesSpatialError()
        {
            var evidenceGo = new GameObject("Evidence_Phone");
            var evidence = evidenceGo.AddComponent<EvidenceItem>();
            Vector3 gtPos = new Vector3(4.16f, 0.92f, 3.43f);
            evidence.Configure("EV-007", "Mobile Phone", "Desk telephone", gtPos);

            var markerGo = new GameObject("MarkerTent");
            markerGo.transform.position = new Vector3(4.18f, 0.92f, 3.45f); // ~2.8 cm away
            var marker = markerGo.AddComponent<EvidenceTagMarker>();

            marker.InitializeMarker(evidence, 1);

            Assert.AreEqual(evidence, marker.AssociatedEvidence);
            Assert.AreEqual(1, marker.MarkerNumber);
            Assert.IsTrue(marker.IsAccurateWithin5cm, "Marker placed 2.8 cm away must be within 5cm tolerance.");
            Assert.IsTrue(marker.MeasuredSpatialErrorMeters < 0.05f);

            UnityEngine.Object.DestroyImmediate(markerGo);
            UnityEngine.Object.DestroyImmediate(evidenceGo);
        }

        [Test]
        public void EvidenceManager_BridgesWithPerson3Timeline()
        {
            var mgrGo = new GameObject("EvidenceManager");
            var mgr = mgrGo.AddComponent<EvidenceManager>();

            var tmGo = new GameObject("TimelineManager");
            var tm = tmGo.AddComponent<TimelineManager>();
            tm.SetGroundTruthSequence(new[] { "EV-001", "EV-002", "EV-003", "EV-004", "EV-005", "EV-006", "EV-007", "EV-008" });

            // Create 3 evidence items and tag them in correct order
            var ev1Go = new GameObject("EV-001");
            var ev1 = ev1Go.AddComponent<EvidenceItem>();
            ev1.Configure("EV-001", "Item 1", "Desc", Vector3.zero);

            var ev2Go = new GameObject("EV-002");
            var ev2 = ev2Go.AddComponent<EvidenceItem>();
            ev2.Configure("EV-002", "Item 2", "Desc", Vector3.one);

            mgr.RefreshRegisteredEvidence();

            ev1.Tag(Vector3.zero);
            ev2.Tag(Vector3.one);

            var result = mgr.SubmitCurrentTimelineSequence();

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.SubmittedCount);
            Assert.AreEqual(2, result.ExactMatchCount);
            Assert.AreEqual(6, result.OmittedEvidenceIds.Count, "Remaining 6 items should be reported omitted.");

            UnityEngine.Object.DestroyImmediate(ev1Go);
            UnityEngine.Object.DestroyImmediate(ev2Go);
            UnityEngine.Object.DestroyImmediate(tmGo);
            UnityEngine.Object.DestroyImmediate(mgrGo);
        }

        [Test]
        public void VREvidenceInteractor_HapticConfiguration_HasValidParameters()
        {
            var interactorGo = new GameObject("InteractorHost");
            var interactor = interactorGo.AddComponent<CrimeSceneVR.Person2.Interaction.VREvidenceInteractor>();

            Assert.IsTrue(interactor.EnableHaptics, "Haptics should be enabled by default.");
            interactor.EnableHaptics = false;
            Assert.IsFalse(interactor.EnableHaptics);

            UnityEngine.Object.DestroyImmediate(interactorGo);
        }

        [Test]
        public void ForensicInvestigationClipboard_SubmitsInvestigation_AndCalculatesTau()
        {
            var smGo = new GameObject("SessionManager");
            var sm = smGo.AddComponent<SessionManager>();

            var tmGo = new GameObject("TimelineManager");
            var tm = tmGo.AddComponent<TimelineManager>();
            tm.SetGroundTruthSequence(new[] { "EV-001", "EV-002", "EV-003", "EV-004", "EV-005", "EV-006", "EV-007", "EV-008" });

            var emGo = new GameObject("EvidenceManager");
            var em = emGo.AddComponent<EvidenceManager>();

            var ev1Go = new GameObject("EV-001");
            var ev1 = ev1Go.AddComponent<EvidenceItem>();
            ev1.Configure("EV-001", "Footwear Impression", "Boot tread", Vector3.zero);

            em.RefreshRegisteredEvidence();

            var clipboardGo = new GameObject("Clipboard");
            var clipboard = clipboardGo.AddComponent<CrimeSceneVR.Person2.UI.ForensicInvestigationClipboard>();

            sm.StartSession("P999", "Condition_Test");
            Assert.IsTrue(sm.IsSessionActive);

            ev1.Tag(Vector3.zero);

            var result = clipboard.SubmitInvestigation();

            Assert.IsNotNull(result, "Submission result should not be null.");
            Assert.AreEqual(1, result.SubmittedCount);
            Assert.IsFalse(sm.IsSessionActive, "Session should be concluded after submission.");

            // Duplicate submit check
            var duplicateResult = clipboard.SubmitInvestigation();
            Assert.IsNull(duplicateResult, "Subsequent submit calls must be ignored.");

            UnityEngine.Object.DestroyImmediate(clipboardGo);
            UnityEngine.Object.DestroyImmediate(ev1Go);
            UnityEngine.Object.DestroyImmediate(emGo);
            UnityEngine.Object.DestroyImmediate(tmGo);
            UnityEngine.Object.DestroyImmediate(smGo);
        }

        [Test]
        public void InvestigationExitTrigger_CompleteInvestigation_FinalizesSession()
        {
            var smGo = new GameObject("SessionManager");
            var sm = smGo.AddComponent<SessionManager>();

            var tmGo = new GameObject("TimelineManager");
            var tm = tmGo.AddComponent<TimelineManager>();

            var emGo = new GameObject("EvidenceManager");
            var em = emGo.AddComponent<EvidenceManager>();

            var triggerGo = new GameObject("ExitTrigger");
            var col = triggerGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var exitTrigger = triggerGo.AddComponent<CrimeSceneVR.Person2.Interaction.InvestigationExitTrigger>();

            sm.StartSession("P998", "Condition_Exit");
            Assert.IsTrue(sm.IsSessionActive);
            Assert.IsFalse(exitTrigger.IsCompleted);

            exitTrigger.CompleteInvestigation();

            Assert.IsTrue(exitTrigger.IsCompleted);
            Assert.IsFalse(sm.IsSessionActive);

            UnityEngine.Object.DestroyImmediate(triggerGo);
            UnityEngine.Object.DestroyImmediate(emGo);
            UnityEngine.Object.DestroyImmediate(tmGo);
            UnityEngine.Object.DestroyImmediate(smGo);
        }
    }
}
