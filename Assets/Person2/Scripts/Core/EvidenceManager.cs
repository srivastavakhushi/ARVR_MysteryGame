using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;
using CrimeSceneVR.Person3.Analytics;

namespace CrimeSceneVR.Person2.Evidence
{
    /// <summary>
    /// Singleton coordinator managing all forensic evidence items in the crime scene.
    /// Bridges Person 2 evidence events directly with Person 3 Session and Timeline systems.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/Person 2/Evidence Manager")]
    public class EvidenceManager : MonoBehaviour
    {
        private static EvidenceManager _instance;
        public static EvidenceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<EvidenceManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Evidence Registry")]
        [SerializeField] private List<EvidenceItem> _registeredEvidence = new List<EvidenceItem>(8);

        [Header("Runtime Metrics")]
        [SerializeField] private int _discoveredCount = 0;
        [SerializeField] private int _inspectedCount = 0;
        [SerializeField] private int _taggedCount = 0;
        [SerializeField] private int _confirmedCount = 0;
        [SerializeField] private int _accurateWithin5cmCount = 0;

        public IReadOnlyList<EvidenceItem> RegisteredEvidence => _registeredEvidence;
        public int TotalEvidenceCount => _registeredEvidence.Count;
        public int DiscoveredCount => _discoveredCount;
        public int InspectedCount => _inspectedCount;
        public int TaggedCount => _taggedCount;
        public int ConfirmedCount => _confirmedCount;
        public int AccurateWithin5cmCount => _accurateWithin5cmCount;

        public event Action<EvidenceItem> OnAnyEvidenceDiscovered;
        public event Action<EvidenceItem> OnAnyEvidenceTagged;

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            RefreshRegisteredEvidence();
        }

        private void OnEnable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionStarted -= HandleSessionStarted;
                SessionManager.Instance.OnSessionStarted += HandleSessionStarted;
                SessionManager.Instance.OnSessionEnded -= HandleSessionEnded;
                SessionManager.Instance.OnSessionEnded += HandleSessionEnded;
            }
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionStarted -= HandleSessionStarted;
                SessionManager.Instance.OnSessionEnded -= HandleSessionEnded;
            }
        }

        private void Start()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionStarted -= HandleSessionStarted;
                SessionManager.Instance.OnSessionStarted += HandleSessionStarted;
                SessionManager.Instance.OnSessionEnded -= HandleSessionEnded;
                SessionManager.Instance.OnSessionEnded += HandleSessionEnded;
            }

            RefreshRegisteredEvidence();
        }

        /// <summary>
        /// Scans the scene to find and link all EvidenceItem components.
        /// </summary>
        public void RefreshRegisteredEvidence()
        {
            var found = FindObjectsByType<EvidenceItem>(FindObjectsSortMode.None);
            _registeredEvidence = found.OrderBy(e => e.EvidenceId).ToList();

            foreach (var item in _registeredEvidence)
            {
                if (item.OnDiscovered != null)
                {
                    item.OnDiscovered.RemoveListener(HandleItemDiscovered);
                    item.OnDiscovered.AddListener(HandleItemDiscovered);
                }

                if (item.OnTagged != null)
                {
                    item.OnTagged.RemoveListener(HandleItemTagged);
                    item.OnTagged.AddListener(HandleItemTagged);
                }

                if (item.OnConfirmed != null)
                {
                    item.OnConfirmed.RemoveListener(HandleItemConfirmed);
                    item.OnConfirmed.AddListener(HandleItemConfirmed);
                }
            }

            UpdateCounts();
        }

        private void HandleSessionStarted(SessionData session)
        {
            Debug.Log($"[EvidenceManager] Resetting all evidence states for session: {session.SessionId}");
            foreach (var item in _registeredEvidence)
            {
                item.ResetState();
            }
            UpdateCounts();
        }

        private void HandleSessionEnded(SessionData session)
        {
            Debug.Log("[EvidenceManager] Session ended. Marking untagged evidence as missed...");
            foreach (var item in _registeredEvidence)
            {
                if (item.CurrentState != EvidenceState.Tagged && item.CurrentState != EvidenceState.Confirmed)
                {
                    item.MarkMissed();
                }
            }
            UpdateCounts();
        }

        private void HandleItemDiscovered(EvidenceItem item)
        {
            UpdateCounts();
            OnAnyEvidenceDiscovered?.Invoke(item);
        }

        private void HandleItemTagged(EvidenceItem item, float spatialError)
        {
            UpdateCounts();
            OnAnyEvidenceTagged?.Invoke(item);

            // Forward to TimelineManager if available
            if (TimelineManager.Instance != null)
            {
                int nextIndex = _taggedCount - 1;
                TimelineManager.Instance.PlaceTimelineItem(
                    evidenceId: item.EvidenceId,
                    slotIndex: nextIndex,
                    details: $"Player tagged {item.ItemName} with spatial error {spatialError * 100f:F1} cm"
                );
            }
        }

        private void HandleItemConfirmed(EvidenceItem item)
        {
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            _discoveredCount = _registeredEvidence.Count(e => e.CurrentState != EvidenceState.Unseen);
            _inspectedCount = _registeredEvidence.Count(e => e.CurrentState == EvidenceState.Inspected || e.CurrentState == EvidenceState.Tagged || e.CurrentState == EvidenceState.Confirmed);
            _taggedCount = _registeredEvidence.Count(e => e.CurrentState == EvidenceState.Tagged || e.CurrentState == EvidenceState.Confirmed);
            _confirmedCount = _registeredEvidence.Count(e => e.CurrentState == EvidenceState.Confirmed);
            _accurateWithin5cmCount = _registeredEvidence.Count(e => e.IsWithinSpatialTolerance);
        }

        /// <summary>
        /// Retrieves an evidence item by its canonical ID (e.g. 'EV-001').
        /// </summary>
        public EvidenceItem GetEvidenceById(string id)
        {
            return _registeredEvidence.FirstOrDefault(e => string.Equals(e.EvidenceId, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper submitting the current sequence of tagged evidence items to Person 3's TimelineManager.
        /// </summary>
        public TimelineValidationResult SubmitCurrentTimelineSequence()
        {
            if (TimelineManager.Instance == null)
            {
                Debug.LogWarning("[EvidenceManager] TimelineManager.Instance is null. Cannot submit timeline.");
                return null;
            }

            var orderedIds = _registeredEvidence
                .Where(e => e.CurrentState == EvidenceState.Tagged || e.CurrentState == EvidenceState.Confirmed)
                .Select(e => e.EvidenceId)
                .ToList();

            var result = TimelineManager.Instance.SubmitTimeline(orderedIds);
            Debug.Log($"[EvidenceManager] Submitted timeline to Person 3: Tau = {result.KendallTauScore:F3} | {result.SummaryNotes}");
            return result;
        }
    }
}
