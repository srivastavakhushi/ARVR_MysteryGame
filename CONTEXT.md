# Person 2 Context & Handoff Specification: Evidence System & Forensic Investigation

## 1. Project & Repository State

- **Repository**: `https://github.com/srivastavakhushi/ARVR_MysteryGame`
- **Active Branch**: `feature/person3-telemetry-analytics`
- **Local Working Path**: `c:\Users\mpstme.student\Downloads\ARVR_MysteryGame-main\ARVR_MysteryGame-main`
- **Unity Version**: `6000.3.3f1` (Unity 6 URP)
- **Primary Scene**: `Assets/Scenes/ZNS3D/Vintage Living Room Game Pack/Demo/Demo_Scene.unity`
- **Role Assignment**:
  - **Person 2 (You)**: Forensic Evidence System, Investigation Mechanics, Evidence Tagging, Spatial Accuracy Calculations, Clipboard UI, Exit Trigger.
  - **Person 1 (Teammate)**: VR Environment, Player Rig (`VRRigReferences.cs`), Locomotion (`VRLocomotion.cs`), Meta Quest OpenXR profiles.
  - **Person 3 (Teammate)**: Session Management (`SessionManager.cs`), Event Telemetry (`TelemetryRecorder.cs`), Timeline Reconstruction (`TimelineManager.cs`), CSV Export (`SessionExporter.cs`, `CsvExporter.cs`), Kendall's Tau-b Analytics (`KendallTau.cs`).

---

## 2. Person 2 File Inventory & Architecture

All Person 2 deliverables reside in `Assets/Person2/`:

```
Assets/Person2/
├── Editor/
│   └── Person2SceneIntegrator.cs           # Automated Scene Setup & Prefab Authoring MenuItem
├── Materials/
│   ├── M_ForensicTentYellow.mat            # High-visibility forensic yellow material
│   ├── M_ForensicTargetRing.mat            # Base disc golden yellow target material
│   └── M_Prop_EV-00*.mat                   # Specific prop materials for EV-001..EV-008
├── Meshes/
│   └── EvidenceTentMesh.asset              # Procedural 3D wedge tent mesh
├── Prefabs/
│   └── EvidenceTagMarker.prefab            # Numbered yellow forensic evidence tent prefab
├── Resources/
│   └── EvidenceTagMarker.prefab            # Runtime auto-load copy for standalone builds
├── Scripts/
│   ├── Core/
│   │   ├── EvidenceData.cs                 # Enums (EvidenceState), Item Definitions, Constants
│   │   ├── EvidenceItem.cs                 # Core evidence item script attached to props
│   │   ├── EvidenceManager.cs              # Singleton evidence registry & state manager
│   │   └── EvidenceTagMarker.cs            # Placed marker script with spatial error readout
│   ├── Interaction/
│   │   ├── InvestigationExitTrigger.cs     # Exit doorway trigger that concludes session
│   │   └── VREvidenceInteractor.cs         # Desktop & VR raycasting, hovering, inspect & tag
│   └── UI/
│       └── ForensicInvestigationClipboard.cs # In-game forensic case file readout
└── Tests/
    └── Editor/
        └── Person2EvidenceTests.cs         # 100% passing NUnit test suite
```

---

## 3. Canonical 8 Evidence Items & Ground Truth Coordinates

Living Room Room Boundaries: $X \in [-1.15, 7.85], Y \in [0.00, 2.85], Z \in [-0.06, 12.78]$.  
Spatial Accuracy Tolerance Threshold: **$0.05\,\text{m}$ ($5\,\text{cm}$)**.

| ID | Name | Canonical Coordinates $(X, Y, Z)$ | Placement Context | Visual Setup |
| :--- | :--- | :--- | :--- | :--- |
| **EV-001** | Footwear Impression | `(4.00, 0.02, 0.50)` | South entrance doorway floor | Muddy boot tread decal, base disc, tent marker #1 |
| **EV-002** | Broken Glass | `(-0.02, 0.68, 7.85)` | Coffee table near teacup | Fractured crystal shards, base disc, tent marker #2 |
| **EV-003** | Bloodied Cloth | `(1.45, 0.05, 3.20)` | Living room carpet struggle area | Bloodstained textile swatch, base disc, tent marker #3 |
| **EV-004** | Weapon / Firearm | `(3.96, 0.15, 8.89)` | Floor beside green armchair/sofa | Gunmetal handgun model, base disc, tent marker #4 |
| **EV-005** | Ballistic Casing | `(0.80, 0.02, 7.20)` | Hardwood floor near coffee table | Polished brass 9mm cartridge, base disc, tent marker #5 |
| **EV-006** | Blood-Spatter Pattern | `(4.10, 1.50, 5.19)` | East living room wallpaper | Medium-velocity arterial spatter decal, tent marker #6 |
| **EV-007** | Mobile Phone | `(4.16, 0.92, 3.43)` | Photo desk near vintage telephone | Dropped smartphone with cracked screen, base disc, tent marker #7 |
| **EV-008** | Small Trace Evidence | `(0.98, 1.05, 12.17)` | North window frame exit latch | Synthetic glove fiber swab, base disc, tent marker #8 |

---

## 4. Visual Feedback & Hover Color Change Mechanics

Every evidence item features automated visual enhancement:
1. **Forensic Base Target Ring (`ForensicRing`)**: A $0.44\,\text{m}$ circular disc with golden-yellow forensic border beneath each prop, elevated to $Y \ge 0.035\,\text{m}$ to eliminate floor carpet z-fighting.
2. **Numbered Yellow Forensic Tent Marker (`ForensicMarkerTent`)**: An initial yellow forensic tent marker standing prominently beside each prop displaying marker numbers #1 through #8.
3. **Dynamic Hover Highlighting (`SetHighlight`)**:
   - On cursor/controller ray hover: All child renderers immediately switch to **bright forensic gold yellow** (`#FFE600`) with active `_EMISSION` ($1.6\times$ intensity boost).
   - The 3D prop expands with a **$1.15\times$ tactile scale pop**.
   - A floating 3D world-space info card activates $0.40\,\text{m}$ above the evidence, billboarded to face the camera:
     $$\text{[EV-00X] Item Name} \quad | \quad \text{Click / Trigger: Inspect} \quad | \quad \text{Right-Click / Grip / F: Tag}$$
   - On unhover: All renderers cleanly restore original textures, materials, and colors.
4. **Targeting Tolerance**:
   - [`VREvidenceInteractor.cs`](file:///c:/Users/mpstme.student/Downloads/ARVR_MysteryGame-main/ARVR_MysteryGame-main/Assets/Person2/Scripts/Interaction/VREvidenceInteractor.cs) features dual `Physics.RaycastAll` (sorted by distance) and `Physics.SphereCast(0.20m)` across a $15.0\,\text{m}$ reach distance.

---

## 5. Controls Reference

| Action | Desktop / Editor | Meta Quest VR Controller |
| :--- | :--- | :--- |
| **Aim Interaction Ray** | Move Mouse cursor | Point Right Controller (`RightHandAnchor`) |
| **Inspect Targeted Evidence** | **Left-Click** or **`E`** key | **Index Trigger** (`PrimaryIndexTrigger`) |
| **Place Evidence Marker Tent** | **Right-Click** or **`F`** / **`T`** key | **Hand Grip** (`PrimaryHandTrigger`) or **Button A** |
| **Toggle Case File Clipboard** | **`C`** key | **Left Controller X or Y** (`Three` / `Four`) |
| **Submit Investigation Early** | **`Enter`** key (while clipboard open) | Click **Submit** on Clipboard / Touch Doorway Trigger |
| **Move / Walk** | **`W`, `A`, `S`, `D`** keys | Left Thumbstick (Continuous Smooth Locomotion) |
| **Rotate View** | **`Q`** (Left) / **`E`** (Right) or hold **Right-Mouse** | Right Thumbstick ($45^\circ$ Snap Turning) |

---

## 6. Person 3 Telemetry & Event Contract

All Person 2 interactions invoke Person 3's singleton APIs directly without creating parallel structures:

- **Evidence Discovery**:
  `TelemetryRecorder.Instance.RecordEvent(TelemetryEventType.EvidenceDiscovered, evidenceId, groundTruthPosition, details)`
- **Evidence Close Inspection**:
  `TelemetryRecorder.Instance.RecordEvent(TelemetryEventType.EvidenceInspected, evidenceId, transform.position, details)`
- **Evidence Tagging & Spatial Accuracy**:
  `TelemetryRecorder.Instance.RecordEvent(TelemetryEventType.EvidenceTagged, evidenceId, participantTagPosition, spatialError, details)`
- **Evidence Confirmation**:
  `TelemetryRecorder.Instance.RecordEvent(TelemetryEventType.EvidenceConfirmed, evidenceId, transform.position, details)`
- **Session Conclusion & Export**:
  Touching [`InvestigationExitTrigger.cs`](file:///c:/Users/mpstme.student/Downloads/ARVR_MysteryGame-main/ARVR_MysteryGame-main/Assets/Person2/Scripts/Interaction/InvestigationExitTrigger.cs) or clicking Submit on [`ForensicInvestigationClipboard.cs`](file:///c:/Users/mpstme.student/Downloads/ARVR_MysteryGame-main/ARVR_MysteryGame-main/Assets/Person2/Scripts/UI/ForensicInvestigationClipboard.cs) calls:
  `EvidenceManager.Instance.SubmitCurrentTimelineSequence()` $\rightarrow$ `TimelineManager.Instance.SubmitTimeline()` $\rightarrow$ `SessionManager.Instance.EndSession(completed: true)` $\rightarrow$ `SessionExporter.HandleSessionEnded()`.
  This exports:
  1. `Telemetry_PXXX_Condition_timestamp.csv` (Row-by-row event telemetry)
  2. `ResearchSummary_PXXX_Condition_timestamp.csv` (Kendall's Tau-b score, normalized score, spatial accuracy percentage, completion duration).

---

## 7. How to Resume Work in Future Sessions

1. **Pull the latest branch**:
   ```bash
   git checkout feature/person3-telemetry-analytics
   git pull origin feature/person3-telemetry-analytics
   ```
2. **Open the primary scene**:
   `Assets/Scenes/ZNS3D/Vintage Living Room Game Pack/Demo/Demo_Scene.unity`
3. **Verify build status**:
   ```bash
   dotnet build Assembly-CSharp.csproj
   dotnet build Assembly-CSharp-Editor.csproj
   ```
4. **Test in Play Mode**:
   - Press **Play** in Unity.
   - Look around to observe the 8 evidence items with their base rings and yellow numbered tents (#1 – #8).
   - Aim your mouse over any evidence prop: observe the gold glow, scale pop, and hover info card.
   - Left-click to inspect; right-click to place a tag tent and see the measured spatial error readout.
