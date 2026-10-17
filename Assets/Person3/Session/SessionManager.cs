using System;
using UnityEngine;

namespace CrimeSceneVR.Person3.Session
{
    /// <summary>
    /// Central manager responsible for participant session lifecycle:
    /// ID assignment, experimental condition tracking, start/end timing, and state validation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/Person 3/Session Manager")]
    public class SessionManager : MonoBehaviour
    {
        private static SessionManager _instance;
        public static SessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SessionManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Default Session Configuration")]
        [Tooltip("Participant identifier assigned if not specified at runtime.")]
        [SerializeField] private string _defaultParticipantId = "P001";

        [Tooltip("Experimental condition (e.g., Baseline, SpatialAudio, VisualCue).")]
        [SerializeField] private string _defaultCondition = "Condition_A";

        [Tooltip("If true, automatically starts a default session on Start().")]
        [SerializeField] private bool _autoStartSession = false;

        [Header("Editor Keyboard Controls")]
        [Tooltip("Allow starting/ending sessions via F1/F2 keys in Play Mode.")]
        [SerializeField] private bool _enableHotkeys = true;

        [Header("Active Session Status")]
        [SerializeField] private SessionData _currentSession;

        private float _sessionStartTime = 0f;

        public SessionData CurrentSession => _currentSession;
        public bool IsSessionActive => _currentSession != null && _currentSession.Status == SessionStatus.InProgress;

        public float ElapsedSessionTime => IsSessionActive ? (Time.time - _sessionStartTime) : (_currentSession != null ? _currentSession.DurationSeconds : 0f);

        public event Action<SessionData> OnSessionStarted;
        public event Action<SessionData> OnSessionEnded;

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
            }
            else
            {
                Debug.LogWarning("[SessionManager] Duplicate instance detected. Destroying duplicate.", this);
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            if (Application.isPlaying && _autoStartSession && !IsSessionActive)
            {
                StartSession(_defaultParticipantId, _defaultCondition);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !_enableHotkeys) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame)
                {
                    if (!IsSessionActive)
                    {
                        StartSession(_defaultParticipantId, _defaultCondition);
                    }
                    else
                    {
                        Debug.Log($"[SessionManager] Session is already active: {_currentSession.SessionId}");
                    }
                }
                else if (keyboard.f2Key.wasPressedThisFrame)
                {
                    if (IsSessionActive)
                    {
                        EndSession(true);
                    }
                    else
                    {
                        Debug.Log("[SessionManager] No active session to end.");
                    }
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (!IsSessionActive)
                {
                    StartSession(_defaultParticipantId, _defaultCondition);
                }
                else
                {
                    Debug.Log($"[SessionManager] Session is already active: {_currentSession.SessionId}");
                }
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                if (IsSessionActive)
                {
                    EndSession(true);
                }
                else
                {
                    Debug.Log("[SessionManager] No active session to end.");
                }
            }
#endif
#endif
        }

        /// <summary>
        /// Begins a new research session with the specified participant and condition.
        /// Prevents accidental duplicate starts if a session is currently active.
        /// </summary>
        public bool StartSession(string participantId, string condition, string customSessionId = null)
        {
            if (IsSessionActive)
            {
                Debug.LogWarning($"[SessionManager] Cannot start new session: Active session {_currentSession.SessionId} is already in progress. End the current session first.");
                return false;
            }

            _currentSession = new SessionData(participantId, condition, customSessionId);
            _sessionStartTime = Time.time;

            Debug.Log($"[SessionManager] Started research session: {_currentSession.SessionId} | Participant: {_currentSession.ParticipantId} | Condition: {_currentSession.Condition}");

            OnSessionStarted?.Invoke(_currentSession);
            return true;
        }

        /// <summary>
        /// Gracefully ends the active session and marks it as completed or aborted.
        /// </summary>
        public bool EndSession(bool completed = true)
        {
            if (!IsSessionActive)
            {
                Debug.LogWarning("[SessionManager] Cannot end session: No session is currently in progress.");
                return false;
            }

            float elapsed = Time.time - _sessionStartTime;
            if (completed)
            {
                _currentSession.MarkComplete(elapsed);
                Debug.Log($"[SessionManager] Completed session: {_currentSession.SessionId} (Duration: {elapsed:F2}s)");
            }
            else
            {
                _currentSession.MarkAborted(elapsed);
                Debug.LogWarning($"[SessionManager] Aborted session: {_currentSession.SessionId} (Duration: {elapsed:F2}s)");
            }

            OnSessionEnded?.Invoke(_currentSession);
            return true;
        }

        [ContextMenu("Start Default Session")]
        public void ContextStartDefaultSession()
        {
            StartSession(_defaultParticipantId, _defaultCondition);
        }

        [ContextMenu("End Active Session")]
        public void ContextEndActiveSession()
        {
            EndSession(true);
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
