using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CrimeSceneVR.Core;
using CrimeSceneVR.Person3.Session;

namespace CrimeSceneVR.Person3.Telemetry
{
    /// <summary>
    /// Captures periodic VR player pose telemetry and discrete research events
    /// during an active session using Person 1's VRRigReferences.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/Person 3/Telemetry Recorder")]
    public class TelemetryRecorder : MonoBehaviour
    {
        private static TelemetryRecorder _instance;
        public static TelemetryRecorder Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<TelemetryRecorder>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Sampling Configuration")]
        [Tooltip("Sampling rate in Hertz (samples per second). Default is 10 Hz.")]
        [Range(1f, 60f)]
        [SerializeField] private float _samplingRateHz = 10f;

        [Tooltip("If true, periodic movement telemetry will be recorded during active sessions.")]
        [SerializeField] private bool _recordMovement = true;

        [Header("Runtime Telemetry State")]
        [SerializeField] private int _totalRecordsCount = 0;

        private readonly List<TelemetryRecord> _records = new List<TelemetryRecord>(2048);
        private Coroutine _samplingCoroutine;
        private Vector3 _lastSampledRigPos;
        private float _lastSampleTime;

        public IReadOnlyList<TelemetryRecord> Records => _records;
        public int RecordCount => _records.Count;
        public float SamplingInterval => 1f / Mathf.Max(1f, _samplingRateHz);

        public event Action<TelemetryRecord> OnRecordCaptured;

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
            }
            else
            {
                Debug.LogWarning("[TelemetryRecorder] Duplicate instance detected. Destroying duplicate.", this);
                Destroy(this);
                return;
            }
        }

        private void OnEnable()
        {
            var sm = SessionManager.Instance;
            if (sm != null)
            {
                sm.OnSessionStarted -= HandleSessionStarted;
                sm.OnSessionStarted += HandleSessionStarted;
                sm.OnSessionEnded -= HandleSessionEnded;
                sm.OnSessionEnded += HandleSessionEnded;
            }
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionStarted -= HandleSessionStarted;
                SessionManager.Instance.OnSessionEnded -= HandleSessionEnded;
            }

            StopSampling();
        }

        private void Start()
        {
            // Re-subscribe if SessionManager was initialized in parallel
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionStarted -= HandleSessionStarted;
                SessionManager.Instance.OnSessionStarted += HandleSessionStarted;
                SessionManager.Instance.OnSessionEnded -= HandleSessionEnded;
                SessionManager.Instance.OnSessionEnded += HandleSessionEnded;

                if (SessionManager.Instance.IsSessionActive && _samplingCoroutine == null)
                {
                    StartSampling();
                }
            }
        }

        private void HandleSessionStarted(SessionData session)
        {
            _records.Clear();
            _totalRecordsCount = 0;

            // Log session start marker with initial rig pose
            CaptureSnapshot(TelemetryEventType.SessionStart, details: $"Session Started: {session.SessionId}");

            StartSampling();
        }

        private void HandleSessionEnded(SessionData session)
        {
            StopSampling();

            // Log session end marker with final rig pose
            CaptureSnapshot(TelemetryEventType.SessionEnd, details: $"Session Ended. Status: {session.Status}, Duration: {session.DurationSeconds:F2}s");
        }

        private void StartSampling()
        {
            StopSampling();
            if (_recordMovement)
            {
                _samplingCoroutine = StartCoroutine(SamplingLoop());
            }
        }

        private void StopSampling()
        {
            if (_samplingCoroutine != null)
            {
                StopCoroutine(_samplingCoroutine);
                _samplingCoroutine = null;
            }
        }

        private IEnumerator SamplingLoop()
        {
            while (SessionManager.Instance != null && SessionManager.Instance.IsSessionActive)
            {
                CaptureSnapshot(TelemetryEventType.MovementSample);
                yield return new WaitForSeconds(SamplingInterval);
            }
            _samplingCoroutine = null;
        }

        /// <summary>
        /// Reads Person 1's VRRigReferences safely without assuming missing anchors are valid.
        /// </summary>
        public TelemetryRecord CaptureSnapshot(TelemetryEventType eventType, string evidenceId = null, Vector3? evidencePosition = null, float? spatialError = null, string details = null, string customEventName = null)
        {
            double elapsedSeconds = SessionManager.Instance != null ? SessionManager.Instance.ElapsedSessionTime : Time.time;

            Vector3? rigPos = null;
            Vector3? headPos = null;
            Quaternion? headRot = null;
            Vector3? leftPos = null;
            Quaternion? leftRot = null;
            Vector3? rightPos = null;
            Quaternion? rightRot = null;
            Vector3? velocity = null;

            VRRigReferences vr = VRRigReferences.Instance;
            if (vr != null)
            {
                // Root / Player position
                if (vr.RigTransform != null)
                {
                    rigPos = vr.RigTransform.position;
                }

                // Head pose
                if (vr.CenterEyeAnchor != null)
                {
                    headPos = vr.HeadPosition;
                    headRot = vr.HeadRotation;
                }

                // Left hand pose
                if (vr.LeftHandAnchor != null)
                {
                    leftPos = vr.LeftHandPosition;
                    leftRot = vr.LeftHandRotation;
                }

                // Right hand pose
                if (vr.RightHandAnchor != null)
                {
                    rightPos = vr.RightHandPosition;
                    rightRot = vr.RightHandRotation;
                }

                // CharacterController velocity if available, otherwise compute from delta
                if (vr.CharacterController != null && vr.CharacterController.enabled)
                {
                    velocity = vr.CharacterController.velocity;
                }
                else if (rigPos.HasValue)
                {
                    float dt = Time.time - _lastSampleTime;
                    if (dt > 0.0001f && _lastSampleTime > 0f)
                    {
                        velocity = (rigPos.Value - _lastSampledRigPos) / dt;
                    }
                    _lastSampledRigPos = rigPos.Value;
                    _lastSampleTime = Time.time;
                }
            }

            var record = new TelemetryRecord(
                elapsedSeconds,
                eventType,
                rigPos,
                headPos,
                headRot,
                leftPos,
                leftRot,
                rightPos,
                rightRot,
                velocity,
                evidenceId,
                evidencePosition,
                spatialError,
                details,
                customEventName
            );

            _records.Add(record);
            _totalRecordsCount = _records.Count;
            OnRecordCaptured?.Invoke(record);

            return record;
        }

        /// <summary>
        /// Public entrypoint for recording discrete experimental events (e.g., Person 2 evidence events).
        /// </summary>
        public void RecordEvent(TelemetryEventType eventType, string evidenceId = null, Vector3? evidencePosition = null, float? spatialError = null, string details = null, string customEventName = null)
        {
            CaptureSnapshot(eventType, evidenceId, evidencePosition, spatialError, details, customEventName);
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
