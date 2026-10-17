using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;

namespace CrimeSceneVR.Person3.Analytics
{
    /// <summary>
    /// Comprehensive validation and scoring summary of a participant's timeline reconstruction.
    /// </summary>
    [Serializable]
    public class TimelineValidationResult
    {
        public bool IsComplete { get; }
        public int GroundTruthCount { get; }
        public int SubmittedCount { get; }
        public int ExactMatchCount { get; }
        public double ExactMatchPercentage => GroundTruthCount > 0 ? (double)ExactMatchCount / GroundTruthCount * 100.0 : 0.0;

        public IReadOnlyList<string> OmittedEvidenceIds { get; }
        public IReadOnlyList<string> UnexpectedEvidenceIds { get; }
        public IReadOnlyList<string> DuplicateEvidenceIds { get; }
        public IReadOnlyList<string> MisplacedEvidenceIds { get; }

        public KendallTauResult KendallResult { get; }
        public double KendallTauScore => KendallResult.Tau;
        public string SummaryNotes { get; }

        public TimelineValidationResult(
            bool isComplete,
            int groundTruthCount,
            int submittedCount,
            int exactMatchCount,
            List<string> omitted,
            List<string> unexpected,
            List<string> duplicates,
            List<string> misplaced,
            KendallTauResult kendallResult,
            string summaryNotes)
        {
            IsComplete = isComplete;
            GroundTruthCount = groundTruthCount;
            SubmittedCount = submittedCount;
            ExactMatchCount = exactMatchCount;
            OmittedEvidenceIds = omitted ?? new List<string>();
            UnexpectedEvidenceIds = unexpected ?? new List<string>();
            DuplicateEvidenceIds = duplicates ?? new List<string>();
            MisplacedEvidenceIds = misplaced ?? new List<string>();
            KendallResult = kendallResult;
            SummaryNotes = summaryNotes;
        }

        public override string ToString()
        {
            return $"Timeline Submission: Matches={ExactMatchCount}/{GroundTruthCount} ({ExactMatchPercentage:F1}%), Tau={KendallTauScore:F3}, Omitted={OmittedEvidenceIds.Count}, Duplicates={DuplicateEvidenceIds.Count} | {SummaryNotes}";
        }
    }

    /// <summary>
    /// Manages crime scene chronological reconstruction, ground-truth sequence evaluation,
    /// and Kendall's Tau sequencing metrics.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/Person 3/Timeline Manager")]
    public class TimelineManager : MonoBehaviour
    {
        private static TimelineManager _instance;
        public static TimelineManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<TimelineManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Ground Truth Narrative Sequence")]
        [Tooltip("The canonical chronological sequence of evidence items in the crime narrative.")]
        [SerializeField]
        private List<string> _groundTruthSequence = new List<string>
        {
            "EV-001", // 1. Footwear impression (Initial entry)
            "EV-002", // 2. Broken glass (Point of forced entry)
            "EV-003", // 3. Bloodied cloth / struggle
            "EV-004", // 4. Weapon (Altercation)
            "EV-005", // 5. Ballistic casing (Discharge)
            "EV-006", // 6. Blood-spatter pattern (Impact)
            "EV-007", // 7. Mobile phone (Dropped during flight)
            "EV-008"  // 8. Small trace evidence (Exit point)
        };

        [Header("Runtime State")]
        [SerializeField] private bool _isReconstructing = false;
        [SerializeField] private List<string> _currentSubmittedSequence = new List<string>();
        [SerializeField] private double _lastKendallTau = 0.0;

        public IReadOnlyList<string> GroundTruthSequence => _groundTruthSequence;
        public IReadOnlyList<string> CurrentSubmittedSequence => _currentSubmittedSequence;
        public TimelineValidationResult LastValidationResult { get; private set; }
        public bool IsReconstructing => _isReconstructing;
        public double LastKendallTau => _lastKendallTau;

        public event Action<TimelineValidationResult> OnTimelineSubmitted;
        public event Action<string, int> OnTimelineItemPlaced;

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
            }
            else
            {
                Debug.LogWarning("[TimelineManager] Duplicate instance detected. Destroying duplicate.", this);
                Destroy(this);
                return;
            }
        }

        /// <summary>
        /// Updates the authoritative ground-truth crime scene narrative sequence.
        /// </summary>
        public void SetGroundTruthSequence(IEnumerable<string> sequence)
        {
            if (sequence == null) return;
            _groundTruthSequence = sequence.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            Debug.Log($"[TimelineManager] Ground truth sequence updated: {string.Join(" -> ", _groundTruthSequence)}");
        }

        /// <summary>
        /// Call when the participant opens or initiates the timeline reconstruction interface.
        /// </summary>
        public void StartTimelineReconstruction()
        {
            _isReconstructing = true;
            _currentSubmittedSequence.Clear();

            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    TelemetryEventType.TimelineStarted,
                    details: "Participant opened timeline reconstruction interface."
                );
            }

            Debug.Log("[TimelineManager] Started timeline reconstruction.");
        }

        /// <summary>
        /// Records an evidence item placed at a specific slot in the timeline.
        /// </summary>
        public void PlaceTimelineItem(string evidenceId, int slotIndex, string details = null)
        {
            if (string.IsNullOrWhiteSpace(evidenceId)) return;
            evidenceId = evidenceId.Trim();

            while (_currentSubmittedSequence.Count <= slotIndex)
            {
                _currentSubmittedSequence.Add(string.Empty);
            }
            _currentSubmittedSequence[slotIndex] = evidenceId;

            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    TelemetryEventType.TimelineEventSelected,
                    evidenceId: evidenceId,
                    details: details ?? $"Placed {evidenceId} at timeline slot {slotIndex}"
                );
            }

            OnTimelineItemPlaced?.Invoke(evidenceId, slotIndex);
        }

        /// <summary>
        /// Evaluates a submitted sequence against the ground-truth sequence, computes Kendall's Tau-b,
        /// records telemetry, and returns detailed metrics.
        /// </summary>
        public TimelineValidationResult SubmitTimeline(IList<string> submittedSequence = null)
        {
            if (submittedSequence != null)
            {
                _currentSubmittedSequence = submittedSequence.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            }

            var cleanSubmission = _currentSubmittedSequence.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            // 1. Identify missing, unexpected, duplicate, and exact matches
            var omitted = new List<string>();
            var unexpected = new List<string>();
            var duplicates = new List<string>();
            var misplaced = new List<string>();
            int exactMatches = 0;

            var submissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenOnce = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in cleanSubmission)
            {
                if (!seenOnce.Add(item))
                {
                    if (!duplicates.Contains(item, StringComparer.OrdinalIgnoreCase))
                    {
                        duplicates.Add(item);
                    }
                }
                submissionSet.Add(item);
            }

            foreach (var gt in _groundTruthSequence)
            {
                if (!submissionSet.Contains(gt))
                {
                    omitted.Add(gt);
                }
            }

            foreach (var sub in cleanSubmission)
            {
                if (!_groundTruthSequence.Contains(sub, StringComparer.OrdinalIgnoreCase))
                {
                    if (!unexpected.Contains(sub, StringComparer.OrdinalIgnoreCase))
                    {
                        unexpected.Add(sub);
                    }
                }
            }

            int compareLength = Math.Min(_groundTruthSequence.Count, cleanSubmission.Count);
            for (int i = 0; i < compareLength; i++)
            {
                if (string.Equals(_groundTruthSequence[i], cleanSubmission[i], StringComparison.OrdinalIgnoreCase))
                {
                    exactMatches++;
                }
                else
                {
                    misplaced.Add(cleanSubmission[i]);
                }
            }

            bool isComplete = omitted.Count == 0 && cleanSubmission.Count >= _groundTruthSequence.Count;

            // 2. Compute Kendall's Tau-b
            KendallTauResult tauResult = KendallTau.Calculate(_groundTruthSequence, cleanSubmission, penalizeOmissions: true);
            _lastKendallTau = tauResult.Tau;

            string notes = $"Exact: {exactMatches}/{_groundTruthSequence.Count}, Omissions: {omitted.Count}, Duplicates: {duplicates.Count}. {tauResult.Interpretation}";

            var result = new TimelineValidationResult(
                isComplete,
                _groundTruthSequence.Count,
                cleanSubmission.Count,
                exactMatches,
                omitted,
                unexpected,
                duplicates,
                misplaced,
                tauResult,
                notes
            );

            LastValidationResult = result;
            _isReconstructing = false;

            // 3. Log telemetry event
            if (TelemetryRecorder.Instance != null)
            {
                TelemetryRecorder.Instance.RecordEvent(
                    TelemetryEventType.TimelineSubmitted,
                    details: $"Submitted {cleanSubmission.Count} items. Exact={exactMatches}/{_groundTruthSequence.Count}, Tau={tauResult.Tau:F3}"
                );
            }

            Debug.Log($"<color=cyan>[TimelineManager] {result}</color>");
            OnTimelineSubmitted?.Invoke(result);

            return result;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
