using System;
using UnityEngine;
using TMPro;

namespace CrimeSceneVR.Person2.Evidence
{
    /// <summary>
    /// Represents a yellow forensic evidence marker tent placed in the scene by the investigator.
    /// Computes and displays spatial accuracy relative to target evidence ground-truth coordinates.
    /// </summary>
    [AddComponentMenu("Crime Scene VR/Person 2/Evidence Tag Marker")]
    public class EvidenceTagMarker : MonoBehaviour
    {
        [Header("Marker Information")]
        [Tooltip("The evidence item this marker is tagging.")]
        [SerializeField] private EvidenceItem _associatedEvidence;

        [Tooltip("Numbered marker label (e.g. '1', '2', etc.).")]
        [SerializeField] private int _markerNumber = 1;

        [Header("Spatial Accuracy Output")]
        [SerializeField] private float _measuredSpatialErrorMeters = 0f;
        [SerializeField] private bool _isAccurateWithin5cm = false;

        [Header("UI / Visual Components (Optional)")]
        [SerializeField] private TextMeshPro _numberText;
        [SerializeField] private TextMeshPro _errorText;
        [SerializeField] private Renderer _tentRenderer;
        [SerializeField] private Color _accurateColor = new Color(0.1f, 0.85f, 0.2f); // Green
        [SerializeField] private Color _inaccurateColor = new Color(0.95f, 0.25f, 0.1f); // Red

        public EvidenceItem AssociatedEvidence => _associatedEvidence;
        public int MarkerNumber => _markerNumber;
        public float MeasuredSpatialErrorMeters => _measuredSpatialErrorMeters;
        public bool IsAccurateWithin5cm => _isAccurateWithin5cm;

        /// <summary>
        /// Initializes and locks this marker to an evidence item, calculating spatial error.
        /// </summary>
        public void InitializeMarker(EvidenceItem evidence, int number)
        {
            _associatedEvidence = evidence;
            _markerNumber = number;

            if (_numberText != null)
            {
                _numberText.text = number.ToString();
            }

            if (_associatedEvidence != null)
            {
                Vector3 tagPos = transform.position;
                _measuredSpatialErrorMeters = _associatedEvidence.Tag(tagPos);
                _isAccurateWithin5cm = (_measuredSpatialErrorMeters <= EvidenceConstants.SPATIAL_ACCURACY_TOLERANCE_METERS);

                UpdateVisualFeedback();
            }
        }

        private void UpdateVisualFeedback()
        {
            if (_errorText != null)
            {
                float errorCm = _measuredSpatialErrorMeters * 100f;
                _errorText.text = $"{errorCm:F1}cm " + (_isAccurateWithin5cm ? "✓" : "✗");
                _errorText.color = _isAccurateWithin5cm ? _accurateColor : _inaccurateColor;
            }
        }

        /// <summary>
        /// Visual helper drawing ground truth vs tag error vector in editor scene view.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_associatedEvidence != null)
            {
                Gizmos.color = _isAccurateWithin5cm ? Color.green : Color.red;
                Gizmos.DrawLine(transform.position, _associatedEvidence.GroundTruthPosition);
                Gizmos.DrawWireSphere(_associatedEvidence.GroundTruthPosition, EvidenceConstants.SPATIAL_ACCURACY_TOLERANCE_METERS);
            }
        }
    }
}
