using System;
using System.IO;
using UnityEngine;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;

namespace CrimeSceneVR.Person3.Export
{
    /// <summary>
    /// Component that automatically triggers CSV serialization upon session completion
    /// and provides inspection hooks for research data extraction.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/Person 3/Session Exporter")]
    public class SessionExporter : MonoBehaviour
    {
        private static SessionExporter _instance;
        public static SessionExporter Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SessionExporter>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Export Configuration")]
        [Tooltip("If true, automatically generates CSV when SessionManager ends a session.")]
        [SerializeField] private bool _autoExportOnSessionEnd = true;

        [Tooltip("Optional custom output directory. If empty, uses Application.persistentDataPath/TelemetryExports.")]
        [SerializeField] private string _customExportDirectory = "";

        [Header("Export Diagnostics")]
        [SerializeField] private string _lastExportedFilePath = "";
        [SerializeField] private string _lastExportedSummaryPath = "";
        [SerializeField] private int _lastExportedRecordCount = 0;

        public string LastExportedFilePath => _lastExportedFilePath;
        public string LastExportedSummaryPath => _lastExportedSummaryPath;
        public int LastExportedRecordCount => _lastExportedRecordCount;

        public event Action<string> OnExportCompleted;

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
            }
            else
            {
                Debug.LogWarning("[SessionExporter] Duplicate instance detected. Destroying duplicate.", this);
                Destroy(this);
                return;
            }
        }

        private void OnEnable()
        {
            var sm = SessionManager.Instance;
            if (sm != null)
            {
                sm.OnSessionEnded -= HandleSessionEnded;
                sm.OnSessionEnded += HandleSessionEnded;
            }
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionEnded -= HandleSessionEnded;
            }
        }

        private void Start()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnSessionEnded -= HandleSessionEnded;
                SessionManager.Instance.OnSessionEnded += HandleSessionEnded;
            }
        }

        private void HandleSessionEnded(SessionData session)
        {
            if (_autoExportOnSessionEnd)
            {
                ExportCurrentSession();
            }
        }

        /// <summary>
        /// Exports the active or most recently completed session to CSV.
        /// </summary>
        [ContextMenu("Export Current Session to CSV")]
        public string ExportCurrentSession()
        {
            if (SessionManager.Instance == null || SessionManager.Instance.CurrentSession == null)
            {
                Debug.LogWarning("[SessionExporter] Cannot export: No active or completed session found in SessionManager.");
                return null;
            }

            var session = SessionManager.Instance.CurrentSession;
            var records = TelemetryRecorder.Instance != null ? TelemetryRecorder.Instance.Records : null;

            try
            {
                string targetDir = string.IsNullOrWhiteSpace(_customExportDirectory) ? null : _customExportDirectory;
                string exportedPath = CsvExporter.ExportSession(session, records, targetDir);

                _lastExportedFilePath = exportedPath;
                _lastExportedRecordCount = records != null ? records.Count : 0;

                // Also generate and export session research summary
                try
                {
                    var timelineResult = CrimeSceneVR.Person3.Analytics.TimelineManager.Instance != null
                        ? CrimeSceneVR.Person3.Analytics.TimelineManager.Instance.LastValidationResult
                        : null;
                    var summary = CrimeSceneVR.Person3.Analytics.ResearchMetrics.ComputeSummary(session, records, timelineResult);
                    string summaryPath = CrimeSceneVR.Person3.Analytics.ResearchMetrics.ExportSummaryCsv(summary, targetDir);
                    _lastExportedSummaryPath = summaryPath;
                    Debug.Log($"<color=cyan>[SessionExporter] Successfully exported Research Summary to: {summaryPath}</color>");
                }
                catch (Exception summaryEx)
                {
                    Debug.LogWarning($"[SessionExporter] Non-fatal: Research summary export failed: {summaryEx.Message}");
                }

                Debug.Log($"<color=green>[SessionExporter] Successfully exported {(_lastExportedRecordCount)} records to: {exportedPath}</color>");

                OnExportCompleted?.Invoke(exportedPath);
                return exportedPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionExporter] Failed to export session CSV: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Convenience method to open the export directory in Windows Explorer (Editor only).
        /// </summary>
        [ContextMenu("Open Export Folder")]
        public void OpenExportFolder()
        {
            string dir = string.IsNullOrWhiteSpace(_customExportDirectory) 
                ? Path.Combine(Application.persistentDataPath, "TelemetryExports") 
                : _customExportDirectory;

            if (Directory.Exists(dir))
            {
                Application.OpenURL("file://" + Path.GetFullPath(dir));
            }
            else
            {
                Debug.LogWarning($"[SessionExporter] Directory does not exist yet: {dir}");
            }
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
