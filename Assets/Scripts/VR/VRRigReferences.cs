using UnityEngine;
using CrimeSceneVR.Locomotion;

namespace CrimeSceneVR.Core
{
    /// <summary>
    /// Central access point exposing VR Rig anchors and components for:
    /// - Person 2: Evidence grab, inspection, interaction tools (hands, controllers, eye ray).
    /// - Person 3: Player position/rotation telemetry and tracking loggers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/VR Rig References")]
    public class VRRigReferences : MonoBehaviour
    {
        public static VRRigReferences Instance { get; private set; }

        [Header("Primary Tracking Anchors")]
        [Tooltip("Root Transform of the VR player rig.")]
        [SerializeField] private Transform _rigTransform;

        [Tooltip("Center Eye Anchor representing player's head and VR viewpoint.")]
        [SerializeField] private Transform _centerEyeAnchor;

        [Tooltip("Left Hand tracking anchor.")]
        [SerializeField] private Transform _leftHandAnchor;

        [Tooltip("Right Hand tracking anchor.")]
        [SerializeField] private Transform _rightHandAnchor;

        [Header("Controller Specific Anchors")]
        [SerializeField] private Transform _leftControllerAnchor;
        [SerializeField] private Transform _rightControllerAnchor;

        [Header("Core VR Components")]
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private VRLocomotion _locomotion;
        [SerializeField] private OVRCameraRig _ovrCameraRig;

        #region Public Properties for Person 2 & Person 3
        public Transform RigTransform => _rigTransform != null ? _rigTransform : transform;
        public Transform CenterEyeAnchor => _centerEyeAnchor;
        public Transform LeftHandAnchor => _leftHandAnchor;
        public Transform RightHandAnchor => _rightHandAnchor;
        public Transform LeftControllerAnchor => _leftControllerAnchor;
        public Transform RightControllerAnchor => _rightControllerAnchor;

        public Camera MainCamera => _mainCamera;
        public CharacterController CharacterController => _characterController;
        public VRLocomotion Locomotion => _locomotion;
        public OVRCameraRig OVRCameraRig => _ovrCameraRig;

        // Convenient Telemetry & Interaction Accessors
        public Vector3 HeadPosition => _centerEyeAnchor != null ? _centerEyeAnchor.position : transform.position;
        public Quaternion HeadRotation => _centerEyeAnchor != null ? _centerEyeAnchor.rotation : transform.rotation;
        public Vector3 HeadForward => _centerEyeAnchor != null ? _centerEyeAnchor.forward : transform.forward;

        public Vector3 LeftHandPosition => _leftHandAnchor != null ? _leftHandAnchor.position : transform.position;
        public Quaternion LeftHandRotation => _leftHandAnchor != null ? _leftHandAnchor.rotation : transform.rotation;

        public Vector3 RightHandPosition => _rightHandAnchor != null ? _rightHandAnchor.position : transform.position;
        public Quaternion RightHandRotation => _rightHandAnchor != null ? _rightHandAnchor.rotation : transform.rotation;
        #endregion

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            if (_rigTransform == null) _rigTransform = transform;
            if (_ovrCameraRig == null) _ovrCameraRig = GetComponent<OVRCameraRig>();
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_locomotion == null) _locomotion = GetComponent<VRLocomotion>();

            if (_ovrCameraRig != null)
            {
                if (_centerEyeAnchor == null) _centerEyeAnchor = _ovrCameraRig.centerEyeAnchor;
                if (_leftHandAnchor == null) _leftHandAnchor = _ovrCameraRig.leftHandAnchor;
                if (_rightHandAnchor == null) _rightHandAnchor = _ovrCameraRig.rightHandAnchor;
            }

            Transform trackingSpace = transform.Find("TrackingSpace");
            if (trackingSpace != null)
            {
                if (_centerEyeAnchor == null) _centerEyeAnchor = trackingSpace.Find("CenterEyeAnchor");
                if (_leftHandAnchor == null) _leftHandAnchor = trackingSpace.Find("LeftHandAnchor");
                if (_rightHandAnchor == null) _rightHandAnchor = trackingSpace.Find("RightHandAnchor");

                if (_leftControllerAnchor == null && _leftHandAnchor != null)
                    _leftControllerAnchor = _leftHandAnchor.Find("LeftControllerAnchor");

                if (_rightControllerAnchor == null && _rightHandAnchor != null)
                    _rightControllerAnchor = _rightHandAnchor.Find("RightControllerAnchor");
            }

            if (_centerEyeAnchor != null && _mainCamera == null)
            {
                _mainCamera = _centerEyeAnchor.GetComponent<Camera>();
            }
        }
    }
}
