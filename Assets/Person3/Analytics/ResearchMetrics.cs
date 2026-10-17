using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;

namespace CrimeSceneVR.Person3.Analytics
{
    /// <summary>
    /// Aggregated research statistics for a single participant session.
    /// </summary>
    [Serializable]
    public class SessionResearchSummary
    {
        public string ParticipantId { get; set; }
        public string Condition { get; set; }
        public string SessionId { get; set; }
        public string Status { get; set; }
        public double DurationSeconds { get; set; }

        // Locomotion Metrics
        public double TotalDistanceTraveledMeters { get; set; }

        // Evidence Discovery Metrics
        public int EvidenceTargetCount { get; set; } = 8;
        public int EvidenceDiscoveredCount { get; set; }
        public double DiscoveryRatePercent => EvidenceTargetCount > 0 ? (double)EvidenceDiscoveredCount / EvidenceTargetCount * 100.0 : 0.0;
        public double? TimeToFirstDiscoverySeconds { get; set; }
        public double? AverageDiscoveryIntervalSeconds { get; set; }

        // Spatial Accuracy Metrics (5 cm tolerance = 0.05m)
        public const float SPATIAL_TOLERANCE_METERS = 0.05f;
        public double? MeanSpatialErrorMeters { get; set; }
        public double? MinSpatialErrorMeters { get; set; }
        public double? MaxSpatialErrorMeters { get; set; }
        public int SpatialWithinToleranceCount { get; set; }
        public double SpatialWithinTolerancePercent { get; set; }

        // Timeline Reconstruction Metrics
        public bool TimelineSubmitted { get; set; }
        public int TimelineExactMatches { get; set; }
        public double TimelineExactMatchPercent { get; set; }
        public double KendallTauScore { get; set; }
        public double KendallTauNormalizedPercent { get; set; }
    }

    /// <summary>
    /// Utility calculating aggregated research metrics and producing session-level summary reports.
    /// </summary>
    public static class ResearchMetrics
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string SUMMARY_CSV_HEADER =
            "ParticipantId,Condition,SessionId,Status,DurationSeconds," +
            "TotalDistanceTraveledMeters,EvidenceTargetCount,EvidenceDiscoveredCount,DiscoveryRatePercent," +
            "TimeToFirstDiscoverySeconds,AverageDiscoveryIntervalSeconds," +
            "MeanSpatialErrorMeters,MinSpatialErrorMeters,MaxSpatialErrorMeters,SpatialAccuracyWithin5cmCount,SpatialAccuracyWithin5cmPercent," +
            "TimelineSubmitted,TimelineExactMatches,TimelineExactMatchPercent,KendallTauScore,KendallTauNormalizedPercent";

        /// <summary>
        /// Aggregates all session telemetry, evidence events, and timeline scores into a structured summary.
        /// </summary>
        public static SessionResearchSummary ComputeSummary(
            SessionData session,
            IEnumerable<TelemetryRecord> records,
            TimelineValidationResult timelineResult = null,
            int targetEvidenceCount = 8)
        {
            var summary = new SessionResearchSummary();
            if (session != null)
            {
                summary.ParticipantId = session.ParticipantId;
                summary.Condition = session.Condition;
                summary.SessionId = session.SessionId;
                summary.Status = session.Status.ToString();
                summary.DurationSeconds = session.DurationSeconds;
            }

            summary.EvidenceTargetCount = targetEvidenceCount;

            if (records != null)
            {
                var recordList = records.ToList();

                // 1. Calculate distance traveled from consecutive movement samples
                double distance = 0.0;
                Vector3? lastPos = null;

                var discoveredEvidenceTimes = new List<double>();
                var spatialErrors = new List<float>();
                var uniqueEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var rec in recordList)
                {
                    if (rec == null) continue;

                    // Distance
                    Vector3? currentPos = rec.RigPosition ?? rec.HeadPosition;
                    if (currentPos.HasValue)
                    {
                        if (lastPos.HasValue)
                        {
                            Vector3 delta = currentPos.Value - lastPos.Value;
                            delta.y = 0f; // horizontal floor distance
                            distance += delta.magnitude;
                        }
                        lastPos = currentPos;
                    }

                    // Evidence events
                    if (rec.EventType == TelemetryEventType.EvidenceDiscovered ||
                        rec.EventType == TelemetryEventType.EvidenceTagged ||
                        rec.EventType == TelemetryEventType.EvidenceInspected)
                    {
                        if (!string.IsNullOrWhiteSpace(rec.EvidenceId))
                        {
                            if (uniqueEvidence.Add(rec.EvidenceId))
                            {
                                discoveredEvidenceTimes.Add(rec.SessionTimestampSeconds);
                            }
                        }

                        if (rec.SpatialError.HasValue)
                        {
                            spatialErrors.Add(rec.SpatialError.Value);
                        }
                    }
                }

                summary.TotalDistanceTraveledMeters = distance;
                summary.EvidenceDiscoveredCount = uniqueEvidence.Count;

                if (discoveredEvidenceTimes.Count > 0)
                {
                    summary.TimeToFirstDiscoverySeconds = discoveredEvidenceTimes[0];
                    if (discoveredEvidenceTimes.Count > 1)
                    {
                        double span = discoveredEvidenceTimes[discoveredEvidenceTimes.Count - 1] - discoveredEvidenceTimes[0];
                        summary.AverageDiscoveryIntervalSeconds = span / (discoveredEvidenceTimes.Count - 1);
                    }
                    else
                    {
                        summary.AverageDiscoveryIntervalSeconds = 0.0;
                    }
                }

                if (spatialErrors.Count > 0)
                {
                    summary.MeanSpatialErrorMeters = spatialErrors.Average();
                    summary.MinSpatialErrorMeters = spatialErrors.Min();
                    summary.MaxSpatialErrorMeters = spatialErrors.Max();
                    summary.SpatialWithinToleranceCount = spatialErrors.Count(e => e <= SessionResearchSummary.SPATIAL_TOLERANCE_METERS);
                    summary.SpatialWithinTolerancePercent = (double)summary.SpatialWithinToleranceCount / spatialErrors.Count * 100.0;
                }
            }

            // Timeline metrics
            if (timelineResult != null)
            {
                summary.TimelineSubmitted = true;
                summary.TimelineExactMatches = timelineResult.ExactMatchCount;
                summary.TimelineExactMatchPercent = timelineResult.ExactMatchPercentage;
                summary.KendallTauScore = timelineResult.KendallTauScore;
                summary.KendallTauNormalizedPercent = timelineResult.KendallResult.NormalizedScore * 100.0;
            }

            return summary;
        }

        /// <summary>
        /// Exports the SessionResearchSummary as a CSV file to disk.
        /// </summary>
        public static string ExportSummaryCsv(SessionResearchSummary summary, string directoryPath = null)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                directoryPath = Path.Combine(Application.persistentDataPath, "TelemetryExports");
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string fileName = $"Summary_{summary.ParticipantId}_{summary.SessionId}.csv";
            string fullPath = Path.Combine(directoryPath, fileName);

            var sb = new StringBuilder(1024);
            sb.AppendLine(SUMMARY_CSV_HEADER);

            sb.Append(EscapeCsv(summary.ParticipantId)).Append(',');
            sb.Append(EscapeCsv(summary.Condition)).Append(',');
            sb.Append(EscapeCsv(summary.SessionId)).Append(',');
            sb.Append(EscapeCsv(summary.Status)).Append(',');
            sb.Append(summary.DurationSeconds.ToString("F2", Inv)).Append(',');
            sb.Append(summary.TotalDistanceTraveledMeters.ToString("F2", Inv)).Append(',');
            sb.Append(summary.EvidenceTargetCount).Append(',');
            sb.Append(summary.EvidenceDiscoveredCount).Append(',');
            sb.Append(summary.DiscoveryRatePercent.ToString("F1", Inv)).Append(',');
            sb.Append(summary.TimeToFirstDiscoverySeconds.HasValue ? summary.TimeToFirstDiscoverySeconds.Value.ToString("F2", Inv) : string.Empty).Append(',');
            sb.Append(summary.AverageDiscoveryIntervalSeconds.HasValue ? summary.AverageDiscoveryIntervalSeconds.Value.ToString("F2", Inv) : string.Empty).Append(',');
            sb.Append(summary.MeanSpatialErrorMeters.HasValue ? summary.MeanSpatialErrorMeters.Value.ToString("F4", Inv) : string.Empty).Append(',');
            sb.Append(summary.MinSpatialErrorMeters.HasValue ? summary.MinSpatialErrorMeters.Value.ToString("F4", Inv) : string.Empty).Append(',');
            sb.Append(summary.MaxSpatialErrorMeters.HasValue ? summary.MaxSpatialErrorMeters.Value.ToString("F4", Inv) : string.Empty).Append(',');
            sb.Append(summary.SpatialWithinToleranceCount).Append(',');
            sb.Append(summary.SpatialWithinTolerancePercent.ToString("F1", Inv)).Append(',');
            sb.Append(summary.TimelineSubmitted ? "TRUE" : "FALSE").Append(',');
            sb.Append(summary.TimelineExactMatches).Append(',');
            sb.Append(summary.TimelineExactMatchPercent.ToString("F1", Inv)).Append(',');
            sb.Append(summary.KendallTauScore.ToString("F3", Inv)).Append(',');
            sb.Append(summary.KendallTauNormalizedPercent.ToString("F1", Inv));
            sb.AppendLine();

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            return fullPath;
        }

        private static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            if (val.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return $"\"{val.Replace("\"", "\"\"")}\"";
            }
            return val;
        }
    }
}
