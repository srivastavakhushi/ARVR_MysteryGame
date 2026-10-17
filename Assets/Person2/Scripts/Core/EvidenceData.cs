using System;
using UnityEngine;

namespace CrimeSceneVR.Person2.Evidence
{
    /// <summary>
    /// Lifecycle state of a forensic evidence item during an investigation.
    /// </summary>
    public enum EvidenceState
    {
        Unseen,         // Undetected in scene
        Discovered,     // First spotted, approached, or illuminated
        Inspected,      // Held or examined in detail
        Tagged,         // Marked with an evidence tent at measured coordinates
        Confirmed,      // Secured into evidence chain of custody / bagged
        Missed          // Unresolved or left untagged at session end
    }

    /// <summary>
    /// Metadata definition for the canonical 8 forensic evidence items in the crime scene.
    /// </summary>
    [Serializable]
    public class EvidenceDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public Vector3 CanonicalPosition;
        public string ExpectedTimelineOrder;

        public EvidenceDefinition(string id, string name, string description, Vector3 canonicalPos, string orderNote)
        {
            Id = id;
            Name = name;
            Description = description;
            CanonicalPosition = canonicalPos;
            ExpectedTimelineOrder = orderNote;
        }
    }

    /// <summary>
    /// Static registry of the canonical 8 crime scene evidence items and tolerance settings.
    /// Boundaries: X in [-1.15, 7.85], Y in [0.00, 2.85], Z in [-0.06, 12.78].
    /// </summary>
    public static class EvidenceConstants
    {
        public const float SPATIAL_ACCURACY_TOLERANCE_METERS = 0.05f; // 5 cm threshold

        public static readonly EvidenceDefinition[] CanonicalItems = new[]
        {
            new EvidenceDefinition(
                "EV-001",
                "Footwear Impression",
                "Partial muddy boot tread impression near south entry door.",
                new Vector3(4.00f, 0.02f, 0.50f),
                "Step 1: Point of initial entry"
            ),
            new EvidenceDefinition(
                "EV-002",
                "Broken Glass",
                "Fractured glass shards and knocked-over tea glass on and around table.",
                new Vector3(-0.02f, 0.68f, 7.85f),
                "Step 2: Disturbance / struggle sign"
            ),
            new EvidenceDefinition(
                "EV-003",
                "Bloodied Cloth",
                "Torn textile fragment with dark bloodstains on carpet struggle area.",
                new Vector3(1.45f, 0.05f, 3.20f),
                "Step 3: Physical altercation area"
            ),
            new EvidenceDefinition(
                "EV-004",
                "Weapon / Firearm",
                "Discarded handgun partly concealed beside living room sofa.",
                new Vector3(3.96f, 0.15f, 8.89f),
                "Step 4: Weapon drawn and discharged"
            ),
            new EvidenceDefinition(
                "EV-005",
                "Ballistic Casing",
                "Spent 9mm brass cartridge casing ejected onto wooden floor.",
                new Vector3(0.80f, 0.02f, 7.20f),
                "Step 5: Discharge trajectory marker"
            ),
            new EvidenceDefinition(
                "EV-006",
                "Blood-Spatter Pattern",
                "Medium-velocity impact arterial spatter pattern on living room wall.",
                new Vector3(4.10f, 1.50f, 5.19f),
                "Step 6: High impact trauma surface"
            ),
            new EvidenceDefinition(
                "EV-007",
                "Mobile Phone",
                "Cracked screen telephone dropped during flight on photo desk.",
                new Vector3(4.16f, 0.92f, 3.43f),
                "Step 7: Flight / escape path drop"
            ),
            new EvidenceDefinition(
                "EV-008",
                "Small Trace Evidence",
                "Synthetic glove fibers and skin trace on north window latch exit.",
                new Vector3(0.98f, 1.05f, 12.17f),
                "Step 8: Point of final exit"
            )
        };
    }
}
