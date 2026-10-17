# Person 3 Integration & Handoff Guide for Person 2

**Target Audience:** Person 2 (Forensic Investigation & Evidence System)  
**Author:** Person 3 (Session Management, Telemetry, Timeline & Research Analytics)  
**Unity Version:** `6000.5.6f1`  
**Input System:** `com.unity.inputsystem` (New Input System active, `activeInputHandler: 1`)  
**Shared Scene:** `Assets/Scenes/ZNS3D/Vintage Living Room Game Pack/Demo/Demo_Scene.unity`  

---

## 1. What Person 3 Has Already Built

Person 3 has implemented and verified the session management, VR movement telemetry, structured event recording, timeline reconstruction, Kendall's Tau analytics, and dual CSV export pipelines.

| Module | Exact File Location | Purpose & Functionality | Verification Status |
| :--- | :--- | :--- | :--- |
| **Session Model** | [`Assets/Person3/Session/SessionData.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Session/SessionData.cs) | Data container for Participant ID, Condition, unique Session ID, start/end UTC ISO 8601 timestamps, duration, and completion status. | **Automated Tested** (Unit/MCP) |
| **Session Manager** | [`Assets/Person3/Session/SessionManager.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Session/SessionManager.cs) | Singleton managing session lifecycle state transitions (`NotStarted`, `InProgress`, `Completed`, `Aborted`), duplicate start rejection, editor hotkeys (<kbd>F1</kbd>/<kbd>F2</kbd>), and `OnSessionStarted` / `OnSessionEnded` C# events. | **Automated Tested** (Unit/MCP) |
| **Telemetry Record** | [`Assets/Person3/Telemetry/TelemetryRecord.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Telemetry/TelemetryRecord.cs) | Immutable time-series data model supporting 13 event types. All spatial vectors (`Vector3?`, `Quaternion?`) are nullable so untracked anchors do not emit fake `(0,0,0)` vectors. | **Automated Tested** (Unit/MCP) |
| **Telemetry Recorder** | [`Assets/Person3/Telemetry/TelemetryRecorder.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Telemetry/TelemetryRecorder.cs) | Samples Person 1's `VRRigReferences.Instance` at 10 Hz for player root, head, left hand, right hand, and controller velocity. Exposes public `RecordEvent(...)` for Person 2. | **Automated Tested** in Editor; **Hardware 6DoF Unverified** |
| **CSV Exporter** | [`Assets/Person3/Export/CsvExporter.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Export/CsvExporter.cs) | RFC 4180 compliant CSV serializer with `CultureInfo.InvariantCulture`, 39 columns, string escaping, and safe storage under `Application.persistentDataPath/TelemetryExports/`. | **Automated Tested** (Byte-for-byte verified) |
| **Session Exporter** | [`Assets/Person3/Export/SessionExporter.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Export/SessionExporter.cs) | Auto-triggers upon session completion, writing both time-series `Telemetry_{P}_{S}.csv` and research summary `Summary_{P}_{S}.csv`. | **Automated Tested** (Disk verified) |
| **Kendall's Tau Engine** | [`Assets/Person3/Analytics/KendallTau.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Analytics/KendallTau.cs) | Computes Kendall's Tau-b ($\tau_b$) rank correlation between ground-truth and participant timeline sequences. Safely handles ties, incomplete submissions, duplicates, and omissions. | **Automated Tested** (Mathematical edge cases verified) |
| **Timeline Manager** | [`Assets/Person3/Analytics/TimelineManager.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Analytics/TimelineManager.cs) | Holds the canonical 8-item crime sequence, tracks participant placements, validates ordering/omissions/duplicates, and computes Kendall's Tau upon submission. | **Automated Tested** (Scene MCP verified) |
| **Research Metrics** | [`Assets/Person3/Analytics/ResearchMetrics.cs`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Person3/Analytics/ResearchMetrics.cs) | Aggregates horizontal distance traveled, discovery rates, time-to-first-discovery, 5 cm spatial accuracy statistics, and outputs a 21-column research summary CSV. | **Automated Tested** (Disk verified) |
| **Scene GameObject** | `[Person3] Systems` in `Demo_Scene.unity` | Hosts `SessionManager`, `TelemetryRecorder`, `SessionExporter`, and `TimelineManager`. Pre-configured and saved. | **Scene Verified** |

> [!NOTE]
> All automated tests were executed inside Unity Editor (PID 14532) via MCP. **Physical Meta Quest 6DoF tracking and physical Touch controller button clicks remain unverified on hardware** because physical headset hardware was not connected during testing.

---

## 2. What Person 3 Still Needs to Complete

Person 3's core architecture and analytics engine are fully written and compiling. The remaining tasks are:
1. **Waiting for Person 2's concrete implementation:** Once Person 2 builds the 8 evidence GameObjects and interaction scripts, Person 3 will verify that actual scene triggers fire cleanly into `TelemetryRecorder.Instance.RecordEvent(...)`.
2. **Team Ground-Truth Finalization:** The team must agree on the final crime narrative sequence (currently configured with default narrative items `EV-001` through `EV-008`).
3. **Physical Meta Quest Verification:** Live verification on a physical headset (via Meta Quest Link or `.apk` build) to validate physical controller interaction and continuous 6DoF headset pose logging.

---

## 3. Exact API Contract for Person 2

Person 2 should communicate with Person 3 exclusively via `TelemetryRecorder.Instance` and `TimelineManager.Instance`.

### Namespace Imports
```csharp
using CrimeSceneVR.Person3.Telemetry;
using CrimeSceneVR.Person3.Analytics;
```

### The Primary Telemetry Method Signature
```csharp
public void RecordEvent(
    TelemetryEventType eventType,
    string evidenceId = null,
    Vector3? evidencePosition = null,
    float? spatialError = null,
    string details = null,
    string customEventName = null
);
```

### Supported Event Types (`TelemetryEventType`)
```csharp
public enum TelemetryEventType
{
    MovementSample,         // Continuous 10Hz rig tracking (handled internally by Person 3)
    SessionStart,           // Fired on session start (handled internally by SessionManager)
    SessionEnd,             // Fired on session end (handled internally by SessionManager)
    EvidenceDiscovered,     // Person 2: When an evidence object is first spotted / entered view
    EvidenceInspected,      // Person 2: When an evidence object is held / examined in detail
    EvidenceTagged,         // Person 2: When a participant places a marker / tag on evidence
    EvidenceConfirmed,      // Person 2: When an evidence tag is verified / locked in
    EvidenceMissed,         // Person 2: When an evidence item is missed or left untagged
    TimelineStarted,        // Person 2/3: When timeline reconstruction UI opens
    TimelineEventSelected,  // Person 2/3: When an evidence item is moved into a timeline slot
    TimelineEventRecorded,  // Person 2/3: When an evidence event is saved to the timeline
    TimelineSubmitted,      // Person 2/3: When the participant submits the final sequence
    CustomMarker            // Generic research marker
}
```

---

### Copy-Paste-Ready Examples for Person 2

#### A. Evidence Discovered (Initial Detection)
Call this when the participant shines a light on or first approaches/spots an evidence item:
```csharp
if (TelemetryRecorder.Instance != null)
{
    TelemetryRecorder.Instance.RecordEvent(
        eventType: TelemetryEventType.EvidenceDiscovered,
        evidenceId: "EV-001",
        evidencePosition: evidenceTransform.position,
        details: "Footwear impression detected near entry door"
    );
}
```

#### B. Evidence Inspected (Close Examination)
Call this when the participant picks up, magnifies, or opens the detail card for an evidence item:
```csharp
if (TelemetryRecorder.Instance != null)
{
    TelemetryRecorder.Instance.RecordEvent(
        eventType: TelemetryEventType.EvidenceInspected,
        evidenceId: "EV-005",
        evidencePosition: transform.position,
        details: "9mm shell casing inspected: stamped headstamp noted"
    );
}
```

#### C. Evidence Tagged (Marker Placed with Spatial Accuracy)
Call this when the participant drops an evidence tent/marker. You **must** calculate the Euclidean distance between `groundTruthPosition` and `participantTagPosition` in meters:
```csharp
Vector3 groundTruthPos = new Vector3(1.45f, 0.05f, 3.20f);
Vector3 participantTagPos = tagMarkerTransform.position;
float spatialErrorMeters = Vector3.Distance(groundTruthPos, participantTagPos);

if (TelemetryRecorder.Instance != null)
{
    TelemetryRecorder.Instance.RecordEvent(
        eventType: TelemetryEventType.EvidenceTagged,
        evidenceId: "EV-002",
        evidencePosition: participantTagPos,
        spatialError: spatialErrorMeters,
        details: spatialErrorMeters <= 0.05f ? "Tag within 5cm tolerance" : "Tag outside 5cm tolerance"
    );
}
```

#### D. Evidence Confirmed / Missed (Chain of Custody)
```csharp
if (TelemetryRecorder.Instance != null)
{
    TelemetryRecorder.Instance.RecordEvent(
        eventType: TelemetryEventType.EvidenceConfirmed,
        evidenceId: "EV-004",
        details: "Weapon bagged and locked into evidence locker"
    );
}
```

#### E. Timeline Placement and Submission
When the participant interacts with the timeline board:
```csharp
// When an evidence item is placed at a timeline slot:
if (TimelineManager.Instance != null)
{
    TimelineManager.Instance.PlaceTimelineItem(
        evidenceId: "EV-001", 
        slotIndex: 0, 
        details: "Placed Footwear Impression at step 1"
    );
}

// When the participant hits the final "Submit Timeline" button:
if (TimelineManager.Instance != null)
{
    // Pass the full ordered list of evidence IDs:
    List<string> orderedSubmission = new List<string> { "EV-001", "EV-002", "EV-003", "EV-004", "EV-005", "EV-006", "EV-007", "EV-008" };
    TimelineValidationResult result = TimelineManager.Instance.SubmitTimeline(orderedSubmission);
    
    Debug.Log($"Kendall Tau Score: {result.KendallTauScore:F3} ({result.SummaryNotes})");
}
```

---

## 4. Where Person 2 Should Make Changes

- **DO NOT** modify anything inside `Assets/Person3/`.
- **DO NOT** place evidence state machine or raycast logic inside Person 3 files.
- **CREATE YOUR CODE** under:
  ```text
  Assets/Person2/Evidence/
  Assets/Person2/Interaction/
  Assets/Person2/UI/
  ```
- **ATTACH YOUR COMPONENTS** to:
  - Your individual evidence GameObjects (e.g., `EvidenceItem.cs`).
  - An evidence manager GameObject (e.g., `[Person2] Evidence Manager`).

---

## 5. Required Evidence Data and Spatial Accuracy

### The 8 Canonical Evidence Items
In accordance with `Person_1_Final_Handoff_Person_2_3 (1).md`, the 8 evidence items are:
1. `EV-001`: Footwear impression (Initial entry point)
2. `EV-002`: Broken glass (Point of forced entry)
3. `EV-003`: Bloodied cloth / struggle
4. `EV-004`: Weapon / firearm (Violent altercation)
5. `EV-005`: Ballistic casing (Discharge)
6. `EV-006`: Blood-spatter pattern (Impact surface)
7. `EV-007`: Mobile phone (Dropped during flight)
8. `EV-008`: Small trace evidence (Exit point)

### Spatial Accuracy: The 5 cm Tolerance Rule
- **Ground Truth Position:** The exact `Vector3` coordinates where the evidence item was authored in the scene.
- **Participant Tag Position:** The `Vector3` coordinates where the participant placed their marker tent.
- **Spatial Error:** $E = \|\mathbf{P}_{\text{tag}} - \mathbf{P}_{\text{ground\_truth}}\|$ (Euclidean distance in meters).
- **Tolerance Threshold:** **`0.05 meters (5.0 cm)`**.
- When `E <= 0.05f`, the tag is classified as accurate. Person 3's `ResearchMetrics` automatically aggregates the percentage and count of tags meeting this criterion.

---

## 6. Exact End-to-End Integration Procedure

Follow these 12 steps in order:
1. Open the scene [`Assets/Scenes/ZNS3D/Vintage Living Room Game Pack/Demo/Demo_Scene.unity`](file:///C:/Users/Lenovo/Desktop/ARVR_MysteryGame/Assets/Scenes/ZNS3D/Vintage%20Living%20Room%20Game%20Pack/Demo/Demo_Scene.unity).
2. Check the Hierarchy to verify that `[BuildingBlock] Camera Rig` (Person 1) and `[Person3] Systems` (Person 3) exist.
3. In `Assets/Person2/`, create your evidence script model (`EvidenceItem.cs`).
4. Place the 8 physical evidence GameObjects in the living room environment.
5. Assign IDs `EV-001` through `EV-008` to your evidence components.
6. Implement discovery and inspection raycasting using Person 1's `VRRigReferences.Instance.RightHandAnchor` (or Left Hand).
7. Implement marker placement to capture the participant's tag position.
8. Calculate `spatialError = Vector3.Distance(groundTruthPosition, tagPosition)` upon tag placement.
9. Call `TelemetryRecorder.Instance.RecordEvent(TelemetryEventType.EvidenceTagged, id, tagPos, spatialError)`.
10. Press **Play** in Unity. Press <kbd>F1</kbd> to start a test session.
11. Interact with evidence items, tag them, and press <kbd>F2</kbd> to complete the session.
12. Inspect the generated CSV files in `%LOCALAPPDATA%Low\DefaultCompany\My project\TelemetryExports\` to confirm your evidence events and spatial error appear.

---

## 7. Common Integration Errors & How to Avoid Them

- ❌ **Duplicate VR Rigs:** Do NOT add another OVRCameraRig, XR Origin, or Main Camera. Use Person 1's `[BuildingBlock] Camera Rig` and `VRRigReferences.Instance`.
- ❌ **Hardcoded Keyboards on Quest:** Do NOT rely on <kbd>F1</kbd>/<kbd>F2</kbd> for the final Quest build. F1/F2 are Editor-only development shortcuts. On the headset, call `SessionManager.Instance.StartSession(...)` via UI.
- ❌ **Default (0,0,0) Vectors:** If an evidence position is unknown or pending, pass `null` to `RecordEvent(...)`. Never pass `Vector3.zero` unless the evidence is genuinely located at the world origin.
- ❌ **Duplicate Events:** Do NOT call `RecordEvent` repeatedly in `Update()`. Call it once per discrete state change (e.g. on trigger press).
- ❌ **Corrupting CSV Schema:** Do NOT attempt to format your own CSV file for telemetry. Calling `RecordEvent(...)` guarantees that your data conforms to the 39-column schema.

---

## 8. Verification Checklist & Acceptance Test

### Acceptance Test Sequence
$$\text{START SESSION} \longrightarrow \text{MOVE} \longrightarrow \text{DISCOVER EV-001} \longrightarrow \text{INSPECT} \longrightarrow \text{TAG} \longrightarrow \text{SUBMIT TIMELINE} \longrightarrow \text{END SESSION} \longrightarrow \text{VERIFY CSV}$$

### Checklist
- [ ] Session starts and generates a unique Session ID (`SessionManager.Instance.IsSessionActive == true`).
- [ ] Moving around the scene logs periodic `MovementSample` rows at 10 Hz.
- [ ] Inspecting an evidence item logs an `EvidenceInspected` row with matching `EvidenceId`.
- [ ] Tagging an evidence item logs an `EvidenceTagged` row with `EvidencePosX, EvidencePosY, EvidencePosZ` and `SpatialError`.
- [ ] Submitting the timeline evaluates Kendall's Tau and logs `TimelineSubmitted`.
- [ ] Ending the session creates both:
  - `Telemetry_{ParticipantId}_{SessionId}.csv` (39 columns, full time-series)
  - `Summary_{ParticipantId}_{SessionId}.csv` (21 columns, research analytics summary)

---

## 9. Person 2's First Actions

1. **Step 1:** Create `Assets/Person2/Evidence/` and write your `EvidenceItem` MonoBehaviour.
2. **Step 2:** Place your first test evidence item (`EV-001`) in `Demo_Scene`.
3. **Step 3:** Call `TelemetryRecorder.Instance.RecordEvent(...)` from your interaction script and verify the log output in Unity Console!
