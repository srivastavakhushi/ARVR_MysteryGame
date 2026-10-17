using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;

namespace CrimeSceneVR.Person3.Export
{
    /// <summary>
    /// Utility class for generating RFC 4180 compliant CSV exports with invariant-culture numeric formatting.
    /// </summary>
    public static class CsvExporter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string CSV_HEADER = 
            "ParticipantId,Condition,SessionId,SessionTimestampSeconds,IsoTimestamp,EventType," +
            "RigPosX,RigPosY,RigPosZ," +
            "HeadPosX,HeadPosY,HeadPosZ,HeadRotX,HeadRotY,HeadRotZ,HeadRotW," +
            "LeftHandPosX,LeftHandPosY,LeftHandPosZ,LeftHandRotX,LeftHandRotY,LeftHandRotZ,LeftHandRotW," +
            "RightHandPosX,RightHandPosY,RightHandPosZ,RightHandRotX,RightHandRotY,RightHandRotZ,RightHandRotW," +
            "VelocityX,VelocityY,VelocityZ," +
            "EvidenceId,EvidencePosX,EvidencePosY,EvidencePosZ,SpatialError,Details";

        /// <summary>
        /// Serializes session metadata and time-series telemetry records into a structured CSV file.
        /// </summary>
        /// <param name="session">Active or completed session object.</param>
        /// <param name="records">List of telemetry/event records.</param>
        /// <param name="directoryPath">Target directory (defaults to Application.persistentDataPath/TelemetryExports if null).</param>
        /// <returns>Absolute path of the written CSV file.</returns>
        public static string ExportSession(SessionData session, IEnumerable<TelemetryRecord> records, string directoryPath = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session), "Cannot export CSV: SessionData is null.");
            }

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                directoryPath = Path.Combine(Application.persistentDataPath, "TelemetryExports");
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string sanitizedParticipant = SanitizeFileName(session.ParticipantId);
            string sanitizedSession = SanitizeFileName(session.SessionId);
            string fileName = $"Telemetry_{sanitizedParticipant}_{sanitizedSession}.csv";
            string fullPath = Path.Combine(directoryPath, fileName);

            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine(CSV_HEADER);

            string participantIdEscaped = EscapeCsv(session.ParticipantId);
            string conditionEscaped = EscapeCsv(session.Condition);
            string sessionIdEscaped = EscapeCsv(session.SessionId);

            if (records != null)
            {
                foreach (var rec in records)
                {
                    if (rec == null) continue;

                    sb.Append(participantIdEscaped).Append(',');
                    sb.Append(conditionEscaped).Append(',');
                    sb.Append(sessionIdEscaped).Append(',');
                    sb.Append(rec.SessionTimestampSeconds.ToString("F3", Inv)).Append(',');
                    sb.Append(EscapeCsv(rec.IsoTimestamp)).Append(',');
                    sb.Append(EscapeCsv(rec.CustomEventName)).Append(',');

                    // Rig Pos
                    AppendVector3(sb, rec.RigPosition);

                    // Head Pose
                    AppendVector3(sb, rec.HeadPosition);
                    AppendQuaternion(sb, rec.HeadRotation);

                    // Left Hand Pose
                    AppendVector3(sb, rec.LeftHandPosition);
                    AppendQuaternion(sb, rec.LeftHandRotation);

                    // Right Hand Pose
                    AppendVector3(sb, rec.RightHandPosition);
                    AppendQuaternion(sb, rec.RightHandRotation);

                    // Velocity
                    AppendVector3(sb, rec.Velocity);

                    // Evidence & details
                    sb.Append(EscapeCsv(rec.EvidenceId)).Append(',');
                    AppendVector3(sb, rec.EvidencePosition);
                    sb.Append(rec.SpatialError.HasValue ? rec.SpatialError.Value.ToString("F4", Inv) : string.Empty).Append(',');
                    sb.Append(EscapeCsv(rec.Details));

                    sb.AppendLine();
                }
            }

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            return fullPath;
        }

        private static void AppendVector3(StringBuilder sb, Vector3? v)
        {
            if (v.HasValue)
            {
                sb.Append(v.Value.x.ToString("F4", Inv)).Append(',');
                sb.Append(v.Value.y.ToString("F4", Inv)).Append(',');
                sb.Append(v.Value.z.ToString("F4", Inv)).Append(',');
            }
            else
            {
                sb.Append(",,,");
            }
        }

        private static void AppendQuaternion(StringBuilder sb, Quaternion? q)
        {
            if (q.HasValue)
            {
                sb.Append(q.Value.x.ToString("F4", Inv)).Append(',');
                sb.Append(q.Value.y.ToString("F4", Inv)).Append(',');
                sb.Append(q.Value.z.ToString("F4", Inv)).Append(',');
                sb.Append(q.Value.w.ToString("F4", Inv)).Append(',');
            }
            else
            {
                sb.Append(",,,,");
            }
        }

        public static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool containsSpecial = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (containsSpecial)
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (Array.IndexOf(invalid, c) < 0 && c != ' ')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            return sb.ToString();
        }
    }
}
