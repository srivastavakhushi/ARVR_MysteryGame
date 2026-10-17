using System;
using System.Collections;
using UnityEngine;
using CrimeSceneVR.Core;
using CrimeSceneVR.Person2.Evidence;

namespace CrimeSceneVR.Person2.Interaction
{
    /// <summary>
    /// VR and Desktop interaction controller for inspecting and tagging forensic evidence.
    /// Operates from Person 1's VRRigReferences.RightHandAnchor with Desktop mouse/keyboard fallback.
    /// Provides generous raycast detection, color-changing hover triggers, and haptic feedback.
    /// </summary>
    [AddComponentMenu("Crime Scene VR/Person 2/VR Evidence Interactor")]
    public class VREvidenceInteractor : MonoBehaviour
    {
        [Header("Raycast Configuration")]
        [Tooltip("Max reach distance for evidence raycasting in meters.")]
        [SerializeField] private float _maxRayDistance = 15.0f;

        [Tooltip("Layer mask containing evidence interactable colliders.")]
        [SerializeField] private LayerMask _evidenceLayerMask = ~0;

        [Header("Evidence Marker Placement")]
        [Tooltip("Prefab instantiated when the player drops an evidence marker tent.")]
        [SerializeField] private GameObject _markerPrefab;

        [Header("Haptic Feedback (Meta Quest)")]
        [SerializeField] private bool _enableHaptics = true;
        [SerializeField] private float _hoverHapticFreq = 0.25f;
        [SerializeField] private float _hoverHapticAmp = 0.25f;
        [SerializeField] private float _hoverHapticDuration = 0.05f;
        [SerializeField] private float _inspectHapticFreq = 0.6f;
        [SerializeField] private float _inspectHapticAmp = 0.55f;
        [SerializeField] private float _inspectHapticDuration = 0.12f;
        [SerializeField] private float _tagHapticFreq = 0.85f;
        [SerializeField] private float _tagHapticAmp = 0.85f;
        [SerializeField] private float _tagHapticDuration = 0.20f;

        [Header("Visual Reticle")]
        [SerializeField] private LineRenderer _laserLineRenderer;
        [SerializeField] private float _laserWidth = 0.008f;
        [SerializeField] private Color _idleLaserColor = new Color(0.2f, 0.6f, 1f, 0.35f);
        [SerializeField] private Color _hoverLaserColor = new Color(1.0f, 0.90f, 0.05f, 0.95f);

        private EvidenceItem _currentHoveredItem;
        private int _markersPlacedCount = 0;
        private Coroutine _hapticCoroutine;

        public EvidenceItem CurrentHoveredItem => _currentHoveredItem;
        public bool EnableHaptics { get => _enableHaptics; set => _enableHaptics = value; }

        private void Awake()
        {
            SetupLaserRenderer();
            EnsureMarkerPrefab();
        }

        private void Start()
        {
            EnsureMarkerPrefab();
        }

        private void EnsureMarkerPrefab()
        {
            if (_markerPrefab == null)
            {
                _markerPrefab = Resources.Load<GameObject>("EvidenceTagMarker");
            }
        }

        private void SetupLaserRenderer()
        {
            if (_laserLineRenderer == null)
            {
                _laserLineRenderer = GetComponent<LineRenderer>();
                if (_laserLineRenderer == null)
                {
                    _laserLineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

            _laserLineRenderer.useWorldSpace = true;
            _laserLineRenderer.startWidth = _laserWidth;
            _laserLineRenderer.endWidth = _laserWidth * 0.5f;
            _laserLineRenderer.positionCount = 2;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null && _laserLineRenderer.material == null)
            {
                _laserLineRenderer.material = new Material(shader);
            }
        }

        private void Update()
        {
            Ray ray = GetInteractionRay();
            bool hitEvidence = false;
            EvidenceItem hitEvidenceItem = null;
            Vector3 hitPoint = ray.origin + ray.direction * _maxRayDistance;

            // 1. Raycast all hits to detect evidence even if behind partial room colliders
            RaycastHit[] hits = Physics.RaycastAll(ray, _maxRayDistance, _evidenceLayerMask);
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    var ev = h.collider.GetComponentInParent<EvidenceItem>();
                    if (ev != null)
                    {
                        hitEvidence = true;
                        hitEvidenceItem = ev;
                        hitPoint = h.point;
                        break;
                    }
                }
            }

            // 2. If direct ray didn't hit an EvidenceItem, try SphereCast for generous aiming tolerance
            if (!hitEvidence)
            {
                if (Physics.SphereCast(ray, 0.20f, out RaycastHit sphereHit, _maxRayDistance, _evidenceLayerMask))
                {
                    var ev = sphereHit.collider.GetComponentInParent<EvidenceItem>();
                    if (ev != null)
                    {
                        hitEvidence = true;
                        hitEvidenceItem = ev;
                        hitPoint = sphereHit.point;
                    }
                }
            }

            // 3. Process Evidence Item Hover & Interactions
            if (hitEvidence && hitEvidenceItem != null)
            {
                if (_currentHoveredItem != hitEvidenceItem)
                {
                    ClearHover();
                    _currentHoveredItem = hitEvidenceItem;
                    _currentHoveredItem.SetHighlight(true);
                    _currentHoveredItem.Discover();
                    TriggerHaptic(_hoverHapticFreq, _hoverHapticAmp, _hoverHapticDuration);
                }

                UpdateLaser(ray.origin, hitPoint, _hoverLaserColor);

                // Inspect
                if (IsInspectPressed())
                {
                    _currentHoveredItem.Inspect();
                    TriggerHaptic(_inspectHapticFreq, _inspectHapticAmp, _inspectHapticDuration);
                }

                // Tag
                if (IsTagPressed())
                {
                    PlaceTagMarker(hitPoint, _currentHoveredItem);
                }
            }
            else
            {
                // Check UI / Clipboard / Exit Doorway
                if (Physics.Raycast(ray, out RaycastHit generalHit, _maxRayDistance, _evidenceLayerMask))
                {
                    var exitTrigger = generalHit.collider.GetComponentInParent<InvestigationExitTrigger>();
                    if (exitTrigger != null)
                    {
                        hitEvidence = true;
                        UpdateLaser(ray.origin, generalHit.point, _hoverLaserColor);
                        if (IsInspectPressed())
                        {
                            exitTrigger.OnInteracted();
                            TriggerHaptic(_tagHapticFreq, _tagHapticAmp, _tagHapticDuration);
                        }
                    }
                    else
                    {
                        var clipboard = generalHit.collider.GetComponentInParent<CrimeSceneVR.Person2.UI.ForensicInvestigationClipboard>();
                        if (clipboard != null)
                        {
                            hitEvidence = true;
                            UpdateLaser(ray.origin, generalHit.point, _hoverLaserColor);
                            if (IsInspectPressed())
                            {
                                clipboard.SubmitInvestigation();
                                TriggerHaptic(_tagHapticFreq, _tagHapticAmp, _tagHapticDuration);
                            }
                        }
                    }
                }

                if (!hitEvidence)
                {
                    ClearHover();
                    UpdateLaser(ray.origin, ray.origin + ray.direction * _maxRayDistance, _idleLaserColor);

                    if (IsInspectPressed() || IsTagPressed())
                    {
                        Debug.Log("<color=yellow>[Forensic Interactor] Clicked empty space. Aim at a yellow numbered evidence item (#1 to #8) to inspect or tag.</color>");
                    }
                }
            }
        }

        private void ClearHover()
        {
            if (_currentHoveredItem != null)
            {
                _currentHoveredItem.SetHighlight(false);
                _currentHoveredItem = null;
            }
        }

        private void UpdateLaser(Vector3 start, Vector3 end, Color color)
        {
            if (_laserLineRenderer != null)
            {
                var vr = VRRigReferences.Instance;
                Vector3 laserStart = (!Application.isEditor && vr != null && vr.RightHandAnchor != null)
                    ? vr.RightHandAnchor.position
                    : start;

                _laserLineRenderer.SetPosition(0, laserStart);
                _laserLineRenderer.SetPosition(1, end);
                _laserLineRenderer.startColor = color;
                _laserLineRenderer.endColor = color;
            }
        }

        private Camera GetActiveCamera()
        {
            var vr = VRRigReferences.Instance;
            if (vr != null && vr.MainCamera != null) return vr.MainCamera;
            if (Camera.main != null) return Camera.main;
            return FindFirstObjectByType<Camera>();
        }

        private Ray GetInteractionRay()
        {
            var vr = VRRigReferences.Instance;

            // 1. In physical VR build with HMD present, raycast from RightHandAnchor
            if (!Application.isEditor && OVRManager.isHmdPresent && vr != null && vr.RightHandAnchor != null)
            {
                return new Ray(vr.RightHandAnchor.position, vr.RightHandAnchor.forward);
            }

            // 2. Editor / Desktop / Simulator fallback (aims where mouse cursor or center reticle points)
            Camera cam = GetActiveCamera();
            if (cam != null)
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null)
                {
                    Vector2 mousePos = mouse.position.ReadValue();
                    if (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height)
                    {
                        return cam.ScreenPointToRay(mousePos);
                    }
                }
#endif
                return new Ray(cam.transform.position, cam.transform.forward);
            }

            if (vr != null && vr.RightHandAnchor != null)
            {
                return new Ray(vr.RightHandAnchor.position, vr.RightHandAnchor.forward);
            }

            return new Ray(transform.position, transform.forward);
        }

        private bool IsInspectPressed()
        {
            // VR Controller trigger
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                return true;
            }

            // Desktop New Input System
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) return true;
#endif
            return false;
        }

        private bool IsTagPressed()
        {
            // VR Controller grip or secondary button
            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch) ||
                OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                return true;
            }

            // Desktop New Input System
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) return true;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.tKey.wasPressedThisFrame)) return true;
#endif
            return false;
        }

        private void PlaceTagMarker(Vector3 position, EvidenceItem evidence)
        {
            _markersPlacedCount++;

            if (_markerPrefab != null)
            {
                GameObject markerGo = Instantiate(_markerPrefab, position, Quaternion.identity);
                var marker = markerGo.GetComponent<EvidenceTagMarker>();
                if (marker != null)
                {
                    marker.InitializeMarker(evidence, _markersPlacedCount);
                    TriggerHaptic(_tagHapticFreq, _tagHapticAmp, _tagHapticDuration);
                    return;
                }
            }

            // If no marker prefab assigned, tag directly on the evidence item
            evidence.Tag(position);
            TriggerHaptic(_tagHapticFreq, _tagHapticAmp, _tagHapticDuration);
        }

        /// <summary>
        /// Pulses tactile haptic vibration on the specified VR controller (defaults to Right Touch).
        /// </summary>
        public void TriggerHaptic(float frequency, float amplitude, float duration, OVRInput.Controller controller = OVRInput.Controller.RTouch)
        {
            if (!_enableHaptics || !Application.isPlaying) return;

            if (_hapticCoroutine != null)
            {
                StopCoroutine(_hapticCoroutine);
            }
            _hapticCoroutine = StartCoroutine(HapticRoutine(frequency, amplitude, duration, controller));
        }

        private IEnumerator HapticRoutine(float freq, float amp, float dur, OVRInput.Controller controller)
        {
            OVRInput.SetControllerVibration(freq, amp, controller);
            yield return new WaitForSeconds(dur);
            OVRInput.SetControllerVibration(0, 0, controller);
        }
    }
}
