using System;
using UnityEngine;

namespace CrimeSceneVR.Person3.Telemetry
{
    /// <summary>
    /// Type of telemetry entry captured in the time series.
    /// </summary>
    public enum TelemetryEventType
    {
        MovementSample,
        SessionStart,
        SessionEnd,
        EvidenceDiscovered,
        EvidenceInspected,
        EvidenceTagged,
        EvidenceConfirmed,
        EvidenceMissed,
        TimelineStarted,
        TimelineEventSelected,
        TimelineEventRecorded,
        TimelineSubmitted,
        CustomMarker
    }

    /// <summary>
    /// Immutable record holding a snapshot of VR player telemetry or a discrete experimental event.
    /// </summary>
    [Serializable]
    public class TelemetryRecord
    {
        public double SessionTimestampSeconds { get; }
        public string IsoTimestamp { get; }
        public TelemetryEventType EventType { get; }
        public string CustomEventName { get; }

        // VR Tracking Poses (Nullable to represent unavailable / untracked states without fake zeroes)
        public Vector3? RigPosition { get; }
        public Vector3? HeadPosition { get; }
        public Quaternion? HeadRotation { get; }
        public Vector3? LeftHandPosition { get; }
        public Quaternion? LeftHandRotation { get; }
        public Vector3? RightHandPosition { get; }
        public Quaternion? RightHandRotation { get; }
        public Vector3? Velocity { get; }

        // Evidence & Research Fields
        public string EvidenceId { get; }
        public Vector3? EvidencePosition { get; }
        public float? SpatialError { get; }
        public string Details { get; }

        public TelemetryRecord(
            double sessionTimestampSeconds,
            TelemetryEventType eventType,
            Vector3? rigPosition = null,
            Vector3? headPosition = null,
            Quaternion? headRotation = null,
            Vector3? leftHandPosition = null,
            Quaternion? leftHandRotation = null,
            Vector3? rightHandPosition = null,
            Quaternion? rightHandRotation = null,
            Vector3? velocity = null,
            string evidenceId = null,
            Vector3? evidencePosition = null,
            float? spatialError = null,
            string details = null,
            string customEventName = null)
        {
            SessionTimestampSeconds = sessionTimestampSeconds;
            IsoTimestamp = DateTime.UtcNow.ToString("o");
            EventType = eventType;
            CustomEventName = customEventName ?? eventType.ToString();

            RigPosition = rigPosition;
            HeadPosition = headPosition;
            HeadRotation = headRotation;
            LeftHandPosition = leftHandPosition;
            LeftHandRotation = leftHandRotation;
            RightHandPosition = rightHandPosition;
            RightHandRotation = rightHandRotation;
            Velocity = velocity;

            EvidenceId = evidenceId;
            EvidencePosition = evidencePosition;
            SpatialError = spatialError;
            Details = details;
        }
    }
}
