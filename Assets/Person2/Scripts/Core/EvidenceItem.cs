using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using CrimeSceneVR.Person3.Telemetry;

namespace CrimeSceneVR.Person2.Evidence
{
    /// <summary>
    /// Component attached to physical forensic evidence GameObjects in the crime scene.
    /// Manages discovery, close inspection, tag placement, and telemetry recording.
    /// Provides vibrant color-changing highlight feedback, prominent base rings, and hover tooltips.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/Person 2/Evidence Item")]
    public class EvidenceItem : MonoBehaviour
    {
        [Header("Evidence Identity")]
        [Tooltip("Canonical Evidence ID (e.g. EV-001 through EV-008).")]
        [SerializeField] private string _evidenceId = "EV-001";

        [SerializeField] private string _itemName = "Evidence Item";

        [TextArea(2, 4)]
        [SerializeField] private string _description = "Forensic evidence found in crime scene.";

        [Header("Spatial Ground Truth")]
        [Tooltip("The authoritative ground-truth coordinates where this item was authored.")]
        [SerializeField] private Vector3 _groundTruthPosition;

        [Tooltip("If true, automatically updates GroundTruthPosition from transform.position on Awake/Validate.")]
        [SerializeField] private bool _autoAssignGroundTruthFromTransform = true;

        [Header("State Tracking")]
        [SerializeField] private EvidenceState _currentState = EvidenceState.Unseen;
        [SerializeField] private Vector3? _lastParticipantTagPosition = null;
        [SerializeField] private float? _lastSpatialErrorMeters = null;
        [SerializeField] private bool _isWithinSpatialTolerance = false;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer[] _highlightRenderers;
        [SerializeField] private Color _highlightColor = new Color(1.0f, 0.90f, 0.05f, 1.0f); // Bright forensic gold yellow
        private MaterialPropertyBlock _propBlock;

        [Header("Events")]
        public UnityEvent<EvidenceItem> OnDiscovered = new UnityEvent<EvidenceItem>();
        public UnityEvent<EvidenceItem> OnInspected = new UnityEvent<EvidenceItem>();
        public UnityEvent<EvidenceItem, float> OnTagged = new UnityEvent<EvidenceItem, float>(); // Passes evidence and spatial error in meters
        public UnityEvent<EvidenceItem> OnConfirmed = new UnityEvent<EvidenceItem>();

        public string EvidenceId => _evidenceId;
        public string ItemName => _itemName;
        public string Description => _description;
        public Vector3 GroundTruthPosition => _groundTruthPosition;
        public EvidenceState CurrentState => _currentState;
        public float? LastSpatialErrorMeters => _lastSpatialErrorMeters;
        public bool IsWithinSpatialTolerance => _isWithinSpatialTolerance;
        public Vector3? LastParticipantTagPosition => _lastParticipantTagPosition;

        // Internal visual highlight and hover card tracking
        private class RendererBackup
        {
            public Renderer Renderer;
            public Material[] Materials;
            public Color[] OriginalBaseColors;
            public Color[] OriginalColors;
            public Color[] OriginalEmissions;
            public bool[] HadEmissionKeyword;
        }

        private List<RendererBackup> _backupList = new List<RendererBackup>();
        private bool _isHighlighted = false;
        private Transform _visualRoot;
        private Vector3 _originalVisualScale = Vector3.one;
        private GameObject _hoverCardGo;
        private TextMeshPro _hoverCardText;

        private void Awake()
        {
            if (_autoAssignGroundTruthFromTransform)
            {
                _groundTruthPosition = transform.position;
            }

            _propBlock = new MaterialPropertyBlock();
            EnsureForensicVisualSetup();
        }

        private void Start()
        {
            EnsureForensicVisualSetup();
        }

        private void OnValidate()
        {
            if (_autoAssignGroundTruthFromTransform && !Application.isPlaying)
            {
                _groundTruthPosition = transform.position;
            }
        }

        /// <summary>
        /// Automatically ensures that every evidence item has a generous collider, a prominent visual,
        /// a high-visibility forensic target base ring, a numbered tent marker, and a floating hover card.
        /// </summary>
        public void EnsureForensicVisualSetup()
        {
            // 1. Ensure generous collider for raycast and physical detection
            var col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.12f, 0f);
                box.size = new Vector3(0.50f, 0.35f, 0.50f);
            }
            else if (col is BoxCollider box)
            {
                if (box.size.x < 0.35f || box.size.z < 0.35f)
                {
                    box.size = new Vector3(Mathf.Max(box.size.x, 0.45f), Mathf.Max(box.size.y, 0.25f), Mathf.Max(box.size.z, 0.45f));
                }
            }
            else if (col is CapsuleCollider cap)
            {
                if (cap.radius < 0.15f) cap.radius = 0.20f;
            }
            else if (col is SphereCollider sph)
            {
                if (sph.radius < 0.15f) sph.radius = 0.22f;
            }

            // 2. Identify visual root
            _visualRoot = transform.Find("Visual");
            if (_visualRoot != null)
            {
                _originalVisualScale = _visualRoot.localScale;
            }

            // 3. Ensure high-visibility forensic base ring
            Transform existingRing = transform.Find("ForensicRing");
            if (existingRing == null)
            {
                GameObject ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = "ForensicRing";
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localPosition = new Vector3(0f, 0.005f, 0f);
                ringGo.transform.localScale = new Vector3(0.44f, 0.006f, 0.44f);

                var ringCol = ringGo.GetComponent<Collider>();
                if (ringCol != null) Destroy(ringCol);

                var mr = ringGo.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                    var mat = new Material(shader);
                    Color ringColor = new Color(1.0f, 0.86f, 0.05f, 0.95f); // High-vis forensic gold
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ringColor);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", ringColor);
                    mr.material = mat;
                }
            }

            // 4. Ensure numbered yellow forensic tent marker next to the prop
            Transform existingTent = transform.Find("ForensicMarkerTent");
            if (existingTent == null && transform.Find("EvidenceTagMarker") == null)
            {
                CreateForensicMarkerTent();
            }

            // 5. Ensure floating hover info card
            EnsureHoverCard();

            // 6. Cache renderers for instant color switching
            _highlightRenderers = GetComponentsInChildren<Renderer>(true);
            BackupOriginalMaterials();
        }

        private void CreateForensicMarkerTent()
        {
            GameObject tentRoot = new GameObject("ForensicMarkerTent");
            tentRoot.transform.SetParent(transform, false);
            tentRoot.transform.localPosition = new Vector3(0.20f, 0.005f, 0.14f);
            tentRoot.transform.localRotation = Quaternion.Euler(0f, -25f, 0f);

            // Simple tent shape using triangular/box prism
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "TentBody";
            body.transform.SetParent(tentRoot.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.038f, 0f);
            body.transform.localScale = new Vector3(0.075f, 0.075f, 0.09f);

            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                var mat = new Material(shader);
                Color tentYellow = new Color(1.0f, 0.88f, 0.05f, 1.0f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tentYellow);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", tentYellow);
                mr.material = mat;
            }

            // Extract item number (1-8)
            string numStr = "1";
            if (_evidenceId.Length >= 6 && int.TryParse(_evidenceId.Substring(_evidenceId.Length - 1), out int numVal))
            {
                numStr = numVal.ToString();
            }

            // Number Label on tent face
            GameObject textGo = new GameObject("MarkerNumber");
            textGo.transform.SetParent(tentRoot.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0.042f, 0.046f);
            textGo.transform.localRotation = Quaternion.identity;

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = numStr;
            tmp.fontSize = 2.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.rectTransform.sizeDelta = new Vector2(0.08f, 0.08f);
        }

        private void EnsureHoverCard()
        {
            if (_hoverCardGo != null) return;

            Transform existing = transform.Find("HoverInfoCard");
            if (existing != null)
            {
                _hoverCardGo = existing.gameObject;
                _hoverCardText = _hoverCardGo.GetComponentInChildren<TextMeshPro>();
                _hoverCardGo.SetActive(false);
                return;
            }

            _hoverCardGo = new GameObject("HoverInfoCard");
            _hoverCardGo.transform.SetParent(transform, false);
            _hoverCardGo.transform.localPosition = new Vector3(0f, 0.40f, 0f);

            _hoverCardText = _hoverCardGo.AddComponent<TextMeshPro>();
            _hoverCardText.text = $"<b>[{_evidenceId}] <color=#FFE600>{_itemName}</color></b>\n<size=75%><color=#EEEEEE>[Click / Trigger]: Inspect   |   [Right-Click / Grip / F]: Tag</color></size>";
            _hoverCardText.fontSize = 1.8f;
            _hoverCardText.alignment = TextAlignmentOptions.Center;
            _hoverCardText.color = Color.white;
            _hoverCardText.rectTransform.sizeDelta = new Vector2(1.6f, 0.5f);

            _hoverCardGo.SetActive(false);
        }

        private void LateUpdate()
        {
            // Hover card always faces the camera when active
            if (_hoverCardGo != null && _hoverCardGo.activeSelf)
            {
                Camera cam = GetActiveCamera();
                if (cam != null)
                {
                    Vector3 dir = (_hoverCardGo.transform.position - cam.transform.position).normalized;
                    if (dir != Vector3.zero)
                    {
                        _hoverCardGo.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
            }
        }

        private Camera GetActiveCamera()
        {
            var vr = CrimeSceneVR.Core.VRRigReferences.Instance;
            if (vr != null && vr.MainCamera != null) return vr.MainCamera;
            if (Camera.main != null) return Camera.main;
            return FindFirstObjectByType<Camera>();
        }

        private void BackupOriginalMaterials()
        {
            _backupList.Clear();
            if (_highlightRenderers == null) return;

            foreach (var r in _highlightRenderers)
            {
                if (r == null) continue;

                var mats = r.materials;
                var backup = new RendererBackup
                {
                    Renderer = r,
                    Materials = mats,
                    OriginalBaseColors = new Color[mats.Length],
                    OriginalColors = new Color[mats.Length],
                    OriginalEmissions = new Color[mats.Length],
                    HadEmissionKeyword = new bool[mats.Length]
                };

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    backup.OriginalBaseColors[i] = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    backup.OriginalColors[i] = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    backup.OriginalEmissions[i] = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                    backup.HadEmissionKeyword[i] = m.IsKeywordEnabled("_EMISSION");
                }

                _backupList.Add(backup);
            }
        }

        /// <summary>
        /// Configures evidence identity programmatically.
        /// </summary>
        public void Configure(string id, string name, string description, Vector3 groundTruthPos)
        {
            _evidenceId = id;
            _itemName = name;
            _description = description;
            _groundTruthPosition = groundTruthPos;

            if (_hoverCardText != null)
            {
                _hoverCardText.text = $"<b>[{_evidenceId}] <color=#FFE600>{_itemName}</color></b>\n<size=75%><color=#EEEEEE>[Click / Trigger]: Inspect   |   [Right-Click / Grip / F]: Tag</color></size>";
            }
        }

        /// <summary>
        /// Called when the player first identifies, shines light on, or spots the evidence.
        /// </summary>
        public bool Discover(string details = null)
        {
            if (_currentState != EvidenceState.Unseen) return false;

            _currentState = EvidenceState.Discovered;
            Debug.Log($"[EvidenceItem] Discovered: {_evidenceId} ({_itemName}) at {_groundTruthPosition}");

            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    eventType: TelemetryEventType.EvidenceDiscovered,
                    evidenceId: _evidenceId,
                    evidencePosition: _groundTruthPosition,
                    details: details ?? $"Discovered {_itemName}: {_description}"
                );
            }

            OnDiscovered?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Called when the player picks up, magnifies, or opens the detail card for this evidence.
        /// </summary>
        public void Inspect(string details = null)
        {
            if (_currentState == EvidenceState.Unseen)
            {
                Discover();
            }

            if (_currentState == EvidenceState.Discovered)
            {
                _currentState = EvidenceState.Inspected;
            }

            Debug.Log($"[EvidenceItem] Inspected: {_evidenceId} ({_itemName})");

            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    eventType: TelemetryEventType.EvidenceInspected,
                    evidenceId: _evidenceId,
                    evidencePosition: transform.position,
                    details: details ?? $"Inspected {_itemName}: {_description}"
                );
            }

            OnInspected?.Invoke(this);
        }

        /// <summary>
        /// Called when the participant places an evidence marker tent at a measured position.
        /// Computes Euclidean distance to ground truth and checks 5 cm tolerance.
        /// </summary>
        public float Tag(Vector3 participantTagPosition, string details = null)
        {
            if (_currentState == EvidenceState.Unseen)
            {
                Discover();
            }

            _lastParticipantTagPosition = participantTagPosition;
            float spatialError = Vector3.Distance(_groundTruthPosition, participantTagPosition);
            _lastSpatialErrorMeters = spatialError;
            _isWithinSpatialTolerance = (spatialError <= EvidenceConstants.SPATIAL_ACCURACY_TOLERANCE_METERS);
            _currentState = EvidenceState.Tagged;

            string toleranceStatus = _isWithinSpatialTolerance 
                ? $"Tag WITHIN 5cm tolerance ({spatialError * 100f:F1} cm)" 
                : $"Tag OUTSIDE 5cm tolerance ({spatialError * 100f:F1} cm)";

            Debug.Log($"[EvidenceItem] Tagged {_evidenceId} at {participantTagPosition}. Error: {spatialError:F4} m. {toleranceStatus}");

            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    eventType: TelemetryEventType.EvidenceTagged,
                    evidenceId: _evidenceId,
                    evidencePosition: participantTagPosition,
                    spatialError: spatialError,
                    details: details ?? $"{_itemName}: {toleranceStatus}"
                );
            }

            OnTagged?.Invoke(this, spatialError);
            return spatialError;
        }

        /// <summary>
        /// Called when evidence is secured / confirmed into custody.
        /// </summary>
        public void Confirm(string details = null)
        {
            _currentState = EvidenceState.Confirmed;
            Debug.Log($"[EvidenceItem] Confirmed: {_evidenceId} secured into evidence custody.");

            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    eventType: TelemetryEventType.EvidenceConfirmed,
                    evidenceId: _evidenceId,
                    evidencePosition: transform.position,
                    details: details ?? $"{_itemName} secured into chain of custody."
                );
            }

            OnConfirmed?.Invoke(this);
        }

        /// <summary>
        /// Called at session end if the evidence was left undiscovered or untagged.
        /// </summary>
        public void MarkMissed(string details = null)
        {
            if (_currentState == EvidenceState.Tagged || _currentState == EvidenceState.Confirmed) return;

            _currentState = EvidenceState.Missed;
            Debug.Log($"[EvidenceItem] Missed: {_evidenceId} was not tagged before session conclusion.");

            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    eventType: TelemetryEventType.EvidenceMissed,
                    evidenceId: _evidenceId,
                    evidencePosition: _groundTruthPosition,
                    details: details ?? $"{_itemName} was missed by the investigator."
                );
            }
        }

        /// <summary>
        /// Resets state for a new session.
        /// </summary>
        public void ResetState()
        {
            _currentState = EvidenceState.Unseen;
            _lastParticipantTagPosition = null;
            _lastSpatialErrorMeters = null;
            _isWithinSpatialTolerance = false;
        }

        /// <summary>
        /// Toggles visual highlight on the evidence mesh.
        /// Instantly changes color to vivid glowing forensic gold on hover raycast.
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;

            if (_backupList == null || _backupList.Count == 0)
            {
                _highlightRenderers = GetComponentsInChildren<Renderer>(true);
                BackupOriginalMaterials();
            }

            if (highlighted)
            {
                ApplyHighlightVisuals();
                if (_hoverCardGo != null) _hoverCardGo.SetActive(true);
                if (_visualRoot != null) _visualRoot.localScale = _originalVisualScale * 1.15f;
            }
            else
            {
                RestoreOriginalVisuals();
                if (_hoverCardGo != null) _hoverCardGo.SetActive(false);
                if (_visualRoot != null) _visualRoot.localScale = _originalVisualScale;
            }
        }

        private void ApplyHighlightVisuals()
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            Color glow = _highlightColor;
            Color emissionGlow = glow * 1.6f;

            foreach (var backup in _backupList)
            {
                if (backup.Renderer == null) continue;

                var mats = backup.Materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    m.EnableKeyword("_EMISSION");
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", glow);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", glow);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissionGlow);
                }

                // Also apply property block for GPU instancing
                backup.Renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", glow);
                _propBlock.SetColor("_Color", glow);
                _propBlock.SetColor("_EmissionColor", emissionGlow);
                backup.Renderer.SetPropertyBlock(_propBlock);
            }
        }

        private void RestoreOriginalVisuals()
        {
            foreach (var backup in _backupList)
            {
                if (backup.Renderer == null) continue;

                var mats = backup.Materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", backup.OriginalBaseColors[i]);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", backup.OriginalColors[i]);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", backup.OriginalEmissions[i]);
                    if (!backup.HadEmissionKeyword[i]) m.DisableKeyword("_EMISSION");
                }

                backup.Renderer.SetPropertyBlock(null);
            }
        }
    }
}
