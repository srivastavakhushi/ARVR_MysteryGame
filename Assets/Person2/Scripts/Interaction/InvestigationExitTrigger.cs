using System;
using UnityEngine;
using TMPro;
using CrimeSceneVR.Person2.Evidence;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;
using CrimeSceneVR.Person3.Analytics;

namespace CrimeSceneVR.Person2.Interaction
{
    /// <summary>
    /// Physical interaction and collision trigger located at the living room exit door.
    /// Concludes the forensic investigation, evaluates final Kendall's Tau-b timeline,
    /// marks any untagged evidence as missed, and triggers Person 3 CSV telemetry export.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Crime Scene VR/Person 2/Investigation Exit Trigger")]
    public class InvestigationExitTrigger : MonoBehaviour
    {
        [Header("Doorway Interaction")]
        [Tooltip("If true, walking into the door collider completes the investigation.")]
        [SerializeField] private bool _triggerOnCollision = true;

        [Tooltip("If true, raycasting and pressing inspect/trigger on the door sign completes the investigation.")]
        [SerializeField] private bool _triggerOnInteract = true;

        [Header("Status Feedback")]
        [SerializeField] private TMP_Text _signStatusText;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _completionChime;

        private bool _isCompleted = false;

        public bool IsCompleted => _isCompleted;

        private void Start()
        {
            UpdateSign("EXIT & SUBMIT INVESTIGATION");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_triggerOnCollision || _isCompleted) return;

            // Check if player or VR rig entered
            if (other.CompareTag("Player") ||
                other.GetComponentInParent<Camera>() != null ||
                other.name.Contains("Rig") ||
                other.name.Contains("Anchor") ||
                other.name.Contains("Head"))
            {
                CompleteInvestigation();
            }
        }

        /// <summary>
        /// Interaction hook callable by VREvidenceInteractor or desktop click.
        /// </summary>
        public void OnInteracted()
        {
            if (!_triggerOnInteract || _isCompleted) return;
            CompleteInvestigation();
        }

        /// <summary>
        /// Finalizes timeline reconstruction, submits sequence to Person 3, and ends the session.
        /// </summary>
        public TimelineValidationResult CompleteInvestigation()
        {
            if (_isCompleted) return null;
            _isCompleted = true;

            Debug.Log("[InvestigationExitTrigger] Player initiated investigation conclusion at exit door.");

            // 1. Submit timeline sequence via EvidenceManager
            TimelineValidationResult timelineResult = null;
            if (EvidenceManager.Instance != null)
            {
                timelineResult = EvidenceManager.Instance.SubmitCurrentTimelineSequence();
            }

            // 2. Conclude session via Person 3 SessionManager (triggers SessionExporter CSV generation)
            if (SessionManager.Instance != null && SessionManager.Instance.IsSessionActive)
            {
                SessionManager.Instance.EndSession(completed: true);
            }

            // 3. Audio & visual confirmation
            if (_audioSource != null && _completionChime != null)
            {
                _audioSource.PlayOneShot(_completionChime);
            }

            string tauSummary = (timelineResult != null) ? $"Tau: {timelineResult.KendallTauScore:F3}" : "Complete";
            UpdateSign($"INVESTIGATION CLOSED\n<color=#00FF88>{tauSummary}</color>");

            return timelineResult;
        }

        private void UpdateSign(string message)
        {
            if (_signStatusText != null)
            {
                _signStatusText.text = message;
            }
        }
    }
}
