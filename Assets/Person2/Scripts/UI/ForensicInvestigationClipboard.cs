using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using CrimeSceneVR.Core;
using CrimeSceneVR.Person2.Evidence;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;
using CrimeSceneVR.Person3.Analytics;

namespace CrimeSceneVR.Person2.UI
{
    /// <summary>
    /// Interactive forensic investigation clipboard / HUD.
    /// Displays live case status, evidence discovery cards, spatial accuracy (<= 5cm),
    /// Kendall's Tau-b timeline reconstruction preview, and an investigation submit trigger.
    /// </summary>
    [AddComponentMenu("Crime Scene VR/Person 2/Forensic Investigation Clipboard")]
    public class ForensicInvestigationClipboard : MonoBehaviour
    {
        [Header("UI Text Displays (TextMeshPro)")]
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _metricsSummaryText;
        [SerializeField] private TMP_Text _evidenceListText;
        [SerializeField] private TMP_Text _timelineResultText;
        [SerializeField] private TMP_Text _statusBannerText;

        [Header("Mount & Follow Settings")]
        [Tooltip("Root GameObject containing clipboard visual meshes and text canvas.")]
        [SerializeField] private GameObject _visualRoot;
        [Tooltip("Optional transform to anchor to (e.g., LeftHandAnchor). If null, floats in front of player.")]
        [SerializeField] private Transform _anchorTarget;
        [SerializeField] private Vector3 _handOffset = new Vector3(0.05f, 0.08f, 0.12f);
        [SerializeField] private Vector3 _handRotation = new Vector3(45f, 0f, 0f);
        [SerializeField] private bool _followHand = false;

        [Header("Visibility & Controls")]
        [SerializeField] private bool _isVisible = false;

        private float _lastUpdateTime = 0f;
        private const float UpdateInterval = 0.25f;
        private bool _isSubmitted = false;

        public bool IsVisible => _isVisible;

        private void Start()
        {
            if (_visualRoot == null)
            {
                var board = transform.Find("BoardVisual");
                if (board != null) _visualRoot = board.gameObject;
            }

            ResolveAnchorTarget();
            SubscribeEvents();
            SetVisibility(_isVisible);
            UpdateDisplay();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            UpdateDisplay();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void ResolveAnchorTarget()
        {
            if (_anchorTarget == null && VRRigReferences.Instance != null)
            {
                _anchorTarget = VRRigReferences.Instance.LeftHandAnchor;
            }
        }

        private void SubscribeEvents()
        {
            if (EvidenceManager.Instance != null)
            {
                EvidenceManager.Instance.OnAnyEvidenceDiscovered += HandleEvidenceUpdated;
                EvidenceManager.Instance.OnAnyEvidenceTagged += HandleEvidenceUpdated;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionStarted += HandleSessionStateChanged;
                SessionManager.Instance.OnSessionEnded += HandleSessionStateChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (EvidenceManager.Instance != null)
            {
                EvidenceManager.Instance.OnAnyEvidenceDiscovered -= HandleEvidenceUpdated;
                EvidenceManager.Instance.OnAnyEvidenceTagged -= HandleEvidenceUpdated;
            }

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionStarted -= HandleSessionStateChanged;
                SessionManager.Instance.OnSessionEnded -= HandleSessionStateChanged;
            }
        }

        private void HandleEvidenceUpdated(EvidenceItem item)
        {
            UpdateDisplay();
        }

        private void HandleSessionStateChanged(SessionData session)
        {
            UpdateDisplay();
        }

        private void Update()
        {
            HandleInput();

            if (_followHand && _anchorTarget != null)
            {
                transform.position = _anchorTarget.TransformPoint(_handOffset);
                transform.rotation = _anchorTarget.rotation * Quaternion.Euler(_handRotation);
            }

            if (Time.time - _lastUpdateTime >= UpdateInterval)
            {
                _lastUpdateTime = Time.time;
                UpdateDisplay();
            }
        }

        private void HandleInput()
        {
            // Toggle clipboard visibility via Left Hand controller (X/Y button) or Desktop 'C' key
            bool togglePressed = false;
            if (OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch) ||
                OVRInput.GetDown(OVRInput.Button.Four, OVRInput.Controller.LTouch))
            {
                togglePressed = true;
            }

#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }

            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame && !_isSubmitted)
            {
                SubmitInvestigation();
            }
#endif

            if (togglePressed)
            {
                ToggleVisibility();
            }
        }

        public void ToggleVisibility()
        {
            SetVisibility(!_isVisible);
        }

        public void SetVisibility(bool visible)
        {
            _isVisible = visible;

            if (_visualRoot != null && _visualRoot != gameObject)
            {
                _visualRoot.SetActive(_isVisible);
            }
            else
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = _isVisible;
                foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = _isVisible;
            }

            if (_isVisible)
            {
                PositionInFrontOfPlayer();
                UpdateDisplay();
            }
        }

        private void PositionInFrontOfPlayer()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.position = cam.transform.position + cam.transform.forward * 0.75f - cam.transform.up * 0.12f;
                transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }
        }

        /// <summary>
        /// Submits the current evidence timeline sequence and concludes the active session.
        /// </summary>
        public TimelineValidationResult SubmitInvestigation()
        {
            if (_isSubmitted)
            {
                Debug.Log("[ForensicClipboard] Investigation already submitted.");
                return null;
            }

            Debug.Log("[ForensicClipboard] Submitting investigation sequence...");
            TimelineValidationResult result = null;

            if (EvidenceManager.Instance != null)
            {
                result = EvidenceManager.Instance.SubmitCurrentTimelineSequence();
            }

            if (SessionManager.Instance != null && SessionManager.Instance.IsSessionActive)
            {
                SessionManager.Instance.EndSession(completed: true);
            }

            _isSubmitted = true;
            UpdateDisplay();

            if (_statusBannerText != null)
            {
                _statusBannerText.text = "<color=#00FF88>★ INVESTIGATION CONCLUDED & EXPORTED ★</color>";
            }

            return result;
        }

        /// <summary>
        /// Refreshes all TextMeshPro readout fields with current evidence metrics.
        /// </summary>
        public void UpdateDisplay()
        {
            var em = EvidenceManager.Instance;
            var sm = SessionManager.Instance;
            var tm = TimelineManager.Instance;

            // 1. Header
            if (_headerText != null)
            {
                string pId = (sm != null && sm.CurrentSession != null) ? sm.CurrentSession.ParticipantId : "P001";
                string cond = (sm != null && sm.CurrentSession != null) ? sm.CurrentSession.Condition : "Baseline";
                float elapsed = (sm != null) ? sm.ElapsedSessionTime : 0f;
                int mins = Mathf.FloorToInt(elapsed / 60f);
                int secs = Mathf.FloorToInt(elapsed % 60f);
                string sessionStatus = _isSubmitted ? "<color=#00FF88>SUBMITTED</color>" : ((sm != null && sm.IsSessionActive) ? "<color=#FFD700>ACTIVE</color>" : "<color=#AAAAAA>READY</color>");

                _headerText.text = $"<b>FORENSIC CASE FILE</b> | Status: {sessionStatus}\n" +
                                   $"<size=80%>Participant: <b>{pId}</b>  |  Cond: <b>{cond}</b>  |  Time: <b>{mins:D2}:{secs:D2}</b></size>";
            }

            // 2. Metrics Summary
            if (_metricsSummaryText != null && em != null)
            {
                int total = em.TotalEvidenceCount > 0 ? em.TotalEvidenceCount : 8;
                _metricsSummaryText.text = $"Discovered: <b>{em.DiscoveredCount}/{total}</b>  |  " +
                                           $"Tagged: <b>{em.TaggedCount}/{total}</b>  |  " +
                                           $"<=5cm Accuracy: <b><color={(em.AccurateWithin5cmCount > 0 ? "#00FF88" : "#FFFFFF")}>{em.AccurateWithin5cmCount}/{total}</color></b>";
            }

            // 3. Evidence Cards List
            if (_evidenceListText != null && em != null)
            {
                var sb = new StringBuilder();
                foreach (var item in em.RegisteredEvidence)
                {
                    string statusColor = "#888888";
                    string statusText = item.CurrentState.ToString().ToUpper();

                    switch (item.CurrentState)
                    {
                        case EvidenceState.Discovered:
                            statusColor = "#33CCFF"; // Cyan
                            break;
                        case EvidenceState.Inspected:
                            statusColor = "#FFBB00"; // Amber
                            break;
                        case EvidenceState.Tagged:
                            statusColor = item.IsWithinSpatialTolerance ? "#00FF88" : "#FF4444";
                            break;
                        case EvidenceState.Confirmed:
                            statusColor = "#AA00FF"; // Purple
                            break;
                        case EvidenceState.Missed:
                            statusColor = "#FF2222"; // Red
                            break;
                    }

                    string errorStr = "";
                    if (item.CurrentState == EvidenceState.Tagged || item.CurrentState == EvidenceState.Confirmed)
                    {
                        float errCm = (item.LastSpatialErrorMeters ?? 0f) * 100f;
                        string mark = item.IsWithinSpatialTolerance ? "✓" : "✗";
                        string markColor = item.IsWithinSpatialTolerance ? "#00FF88" : "#FF4444";
                        errorStr = $" (<color={markColor}>{errCm:F1}cm {mark}</color>)";
                    }

                    sb.AppendLine($"<b>{item.EvidenceId}</b> {item.ItemName}: <color={statusColor}>[{statusText}]</color>{errorStr}");
                }
                _evidenceListText.text = sb.ToString();
            }

            // 4. Timeline Evaluation
            if (_timelineResultText != null)
            {
                if (tm != null && tm.LastValidationResult != null)
                {
                    var res = tm.LastValidationResult;
                    double tau = res.KendallTauScore;
                    string tauColor = tau >= 0.7 ? "#00FF88" : (tau >= 0.3 ? "#FFBB00" : "#FF4444");
                    _timelineResultText.text = $"Kendall's Tau: <color={tauColor}><b>{tau:F3}</b></color> ({res.ExactMatchCount}/{res.GroundTruthCount} exact)\n" +
                                               $"<size=75%>{res.SummaryNotes}</size>";
                }
                else
                {
                    _timelineResultText.text = "Timeline: Tag items to build chronological sequence.";
                }
            }
        }
    }
}
