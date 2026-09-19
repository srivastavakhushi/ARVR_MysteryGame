using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CrimeSceneVR.Locomotion
{
    public enum LocomotionMode
    {
        TeleportOnly,
        SmoothOnly,
        Both
    }

    /// <summary>
    /// Comprehensive VR Locomotion system for Meta Quest.
    /// Features:
    /// 1. Parabolic ballistic teleportation with boundary checks.
    /// 2. Optional continuous smooth thumbstick movement using CharacterController.
    /// 3. Head-centered snap turning (45°).
    /// 4. Teleport comfort transition (rapid subtle screen fade).
    /// 5. Subtle audio footsteps for continuous movement.
    /// 6. Complete Editor fallback controls (WASD, Q/E, Space/T).
    /// </summary>
    [RequireComponent(typeof(OVRCameraRig))]
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Crime Scene VR/VR Locomotion")]
    public class VRLocomotion : MonoBehaviour
    {
        [Header("Locomotion Mode")]
        [Tooltip("Active locomotion paradigm: TeleportOnly, SmoothOnly, or Both.")]
        [SerializeField] private LocomotionMode _locomotionMode = LocomotionMode.Both;

        [Header("Smooth Locomotion Settings")]
        [Tooltip("Enable continuous thumbstick movement.")]
        [SerializeField] private bool _enableSmoothLocomotion = true;
        [Tooltip("Walk speed in meters per second.")]
        [SerializeField] private float _moveSpeed = 2.2f;
        [Tooltip("Controller for smooth movement (typically Left Touch).")]
        [SerializeField] private OVRInput.Controller _smoothMoveController = OVRInput.Controller.LTouch;
        [Tooltip("Deadzone for movement thumbstick.")]
        [SerializeField] private float _moveDeadzone = 0.15f;

        [Header("Footstep Audio")]
        [Tooltip("Enable subtle footstep sounds during continuous movement.")]
        [SerializeField] private bool _enableFootsteps = true;
        [SerializeField] private float _stepDistance = 0.65f;
        [SerializeField] private AudioClip[] _footstepClips;
        [SerializeField] private float _footstepVolume = 0.2f;
        private AudioSource _footstepAudioSource;
        private float _distanceTraveled = 0f;

        [Header("Snap Turn Settings")]
        [Tooltip("Enable or disable snap turning.")]
        [SerializeField] private bool _enableSnapTurn = true;
        [Tooltip("Angle per snap in degrees.")]
        [SerializeField] private float _snapAngle = 45.0f;
        [Tooltip("Deadzone threshold for turn thumbstick.")]
        [SerializeField] private float _turnDeadzone = 0.5f;
        [Tooltip("Cooldown time between turns in seconds.")]
        [SerializeField] private float _turnCooldown = 0.3f;

        [Header("Teleport Settings")]
        [Tooltip("Enable or disable teleportation.")]
        [SerializeField] private bool _enableTeleport = true;
        [Tooltip("Controller initiating teleport aim.")]
        [SerializeField] private OVRInput.Controller _teleportController = OVRInput.Controller.LTouch;
        [Tooltip("Thumbstick threshold to begin aiming.")]
        [SerializeField] private float _teleportAimThreshold = 0.5f;
        [Tooltip("Thumbstick threshold to release and trigger teleport.")]
        [SerializeField] private float _teleportReleaseThreshold = 0.2f;

        [Header("Teleport Comfort Transition")]
        [Tooltip("Enable a fast subtle fade transition during teleport to prevent motion sickness.")]
        [SerializeField] private bool _enableComfortFade = true;
        [Tooltip("Duration of fade to black in seconds.")]
        [SerializeField] private float _fadeDuration = 0.08f;
        [SerializeField] private Color _fadeColor = new Color(0f, 0f, 0f, 1f);

        [Header("Arc Physics & Validation")]
        [SerializeField] private float _initialVelocity = 8.5f;
        [SerializeField] private float _arcSimulationStep = 0.035f;
        [SerializeField] private int _maxSimulationSteps = 50;
        [SerializeField] private LayerMask _collisionLayerMask = ~0;
        [SerializeField] private LayerMask _teleportLayerMask = ~0;

        [Header("Boundary Constraints")]
        [Tooltip("Enforce bounding box constraints so player cannot escape the crime scene.")]
        [SerializeField] private bool _enforceSceneBoundaries = true;
        [SerializeField] private Bounds _sceneBoundaries = new Bounds(
            new Vector3(3.35f, 1.4f, 6.36f), 
            new Vector3(8.5f, 2.8f, 12.4f)
        );

        [Header("Visuals")]
        [SerializeField] private LineRenderer _arcLineRenderer;
        [SerializeField] private GameObject _reticleObject;
        [SerializeField] private Color _validColor = new Color(0.15f, 0.85f, 1.0f, 0.8f);
        [SerializeField] private Color _invalidColor = new Color(1.0f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private float _lineWidth = 0.02f;

        // References
        private OVRCameraRig _cameraRig;
        private CharacterController _characterController;
        private Transform _centerEyeAnchor;
        private Transform _leftHandAnchor;
        private Transform _rightHandAnchor;

        // State
        private bool _hasTurned = false;
        private float _lastTurnTime = 0f;
        private bool _isAiming = false;
        private bool _isTargetValid = false;
        private bool _isTeleporting = false;
        private Vector3 _targetPosition;
        private Vector3 _targetNormal = Vector3.up;
        private readonly List<Vector3> _arcPoints = new List<Vector3>(64);
        private Material _lineMaterial;
        private Material _reticleMaterial;

        // Comfort Screen Fade Quad
        private MeshRenderer _fadeRenderer;
        private Material _fadeMaterial;

        #region Public Properties
        public LocomotionMode Mode
        {
            get => _locomotionMode;
            set => _locomotionMode = value;
        }

        public bool EnableSmoothLocomotion
        {
            get => _enableSmoothLocomotion;
            set => _enableSmoothLocomotion = value;
        }

        public float MoveSpeed
        {
            get => _moveSpeed;
            set => _moveSpeed = value;
        }

        public bool EnableTeleport
        {
            get => _enableTeleport;
            set => _enableTeleport = value;
        }

        public bool EnableSnapTurn
        {
            get => _enableSnapTurn;
            set => _enableSnapTurn = value;
        }

        public Bounds SceneBoundaries => _sceneBoundaries;
        #endregion

        private void Awake()
        {
            _cameraRig = GetComponent<OVRCameraRig>();
            _characterController = GetComponent<CharacterController>();

            FindAnchors();
            SetupVisuals();
            SetupFadeOverlay();
            SetupFootstepAudio();
        }

        private void Start()
        {
            FindAnchors();
        }

        private void FindAnchors()
        {
            if (_cameraRig != null)
            {
                if (_centerEyeAnchor == null) _centerEyeAnchor = _cameraRig.centerEyeAnchor;
                if (_leftHandAnchor == null) _leftHandAnchor = _cameraRig.leftHandAnchor;
                if (_rightHandAnchor == null) _rightHandAnchor = _cameraRig.rightHandAnchor;
            }

            if (_centerEyeAnchor == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) _centerEyeAnchor = cam.transform;
            }
        }

        private void Update()
        {
            if (_isTeleporting) return;

            HandleSnapTurn();

            bool smoothAllowed = _enableSmoothLocomotion && (_locomotionMode == LocomotionMode.SmoothOnly || _locomotionMode == LocomotionMode.Both);
            if (smoothAllowed)
            {
                HandleSmoothLocomotion();
            }

            bool teleportAllowed = _enableTeleport && (_locomotionMode == LocomotionMode.TeleportOnly || _locomotionMode == LocomotionMode.Both);
            if (teleportAllowed)
            {
                HandleTeleport();
            }
            else
            {
                HideVisuals();
            }
        }

        #region Smooth Locomotion
        private void HandleSmoothLocomotion()
        {
            // Read movement vector from thumbstick
            Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _smoothMoveController);

            // Editor fallback: WASD keys
#if UNITY_EDITOR
            if (input.sqrMagnitude < 0.01f)
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                input = new Vector2(h, v);
            }
#endif

            if (input.magnitude < _moveDeadzone)
            {
                return;
            }

            // If in Both mode and currently aiming teleport with the same hand, suppress smooth movement
            if (_locomotionMode == LocomotionMode.Both && _isAiming && _teleportController == _smoothMoveController)
            {
                return;
            }

            // Direction oriented to player's head view
            Vector3 headForward = _centerEyeAnchor != null ? _centerEyeAnchor.forward : transform.forward;
            Vector3 headRight = _centerEyeAnchor != null ? _centerEyeAnchor.right : transform.right;
            headForward.y = 0f;
            headRight.y = 0f;
            headForward.Normalize();
            headRight.Normalize();

            Vector3 moveDirection = (headForward * input.y + headRight * input.x);
            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

            Vector3 motion = moveDirection * (_moveSpeed * Time.deltaTime);

            // Apply slight gravity to keep grounded
            motion.y = Physics.gravity.y * Time.deltaTime;

            // CharacterController movement
            if (_characterController != null && _characterController.enabled)
            {
                Vector3 prevPos = transform.position;
                _characterController.Move(motion);

                // Boundary containment
                if (_enforceSceneBoundaries)
                {
                    Vector3 currentPos = transform.position;
                    if (!_sceneBoundaries.Contains(currentPos))
                    {
                        Vector3 clamped = _sceneBoundaries.ClosestPoint(currentPos);
                        transform.position = new Vector3(clamped.x, currentPos.y, clamped.z);
                    }
                }

                // Footstep tracking
                Vector3 horizontalDelta = transform.position - prevPos;
                horizontalDelta.y = 0f;
                float distThisFrame = horizontalDelta.magnitude;
                if (distThisFrame > 0.001f)
                {
                    _distanceTraveled += distThisFrame;
                    if (_distanceTraveled >= _stepDistance)
                    {
                        PlayFootstepAudio();
                        _distanceTraveled = 0f;
                    }
                }
            }
            else
            {
                transform.position += motion;
            }
        }
        #endregion

        #region Footstep Audio
        private void SetupFootstepAudio()
        {
            _footstepAudioSource = GetComponent<AudioSource>();
            if (_footstepAudioSource == null)
            {
                _footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
            _footstepAudioSource.playOnAwake = false;
            _footstepAudioSource.loop = false;
            _footstepAudioSource.spatialBlend = 0f; // 2D player audio
            _footstepAudioSource.volume = _footstepVolume;
        }

        private void PlayFootstepAudio()
        {
            if (!_enableFootsteps || _footstepClips == null || _footstepClips.Length == 0) return;

            AudioClip clip = _footstepClips[Random.Range(0, _footstepClips.Length)];
            if (clip != null && _footstepAudioSource != null)
            {
                _footstepAudioSource.pitch = Random.Range(0.95f, 1.05f);
                _footstepAudioSource.PlayOneShot(clip, _footstepVolume);
            }
        }

        public void SetFootstepClips(AudioClip[] clips)
        {
            _footstepClips = clips;
        }
        #endregion

        #region Snap Turning
        private void HandleSnapTurn()
        {
            if (!_enableSnapTurn) return;

            float stickX = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;

#if UNITY_EDITOR
            if (Mathf.Approximately(stickX, 0f))
            {
                if (Input.GetKeyDown(KeyCode.Q)) stickX = -1f;
                else if (Input.GetKeyDown(KeyCode.E)) stickX = 1f;
            }
#endif

            if (Mathf.Abs(stickX) > _turnDeadzone)
            {
                if (!_hasTurned && (Time.time - _lastTurnTime) > _turnCooldown)
                {
                    float angle = (stickX > 0f) ? _snapAngle : -_snapAngle;
                    ExecuteSnapTurn(angle);
                    _hasTurned = true;
                    _lastTurnTime = Time.time;
                }
            }
            else
            {
                _hasTurned = false;
            }
        }

        private void ExecuteSnapTurn(float angle)
        {
            Vector3 headWorldPos = _centerEyeAnchor != null ? _centerEyeAnchor.position : transform.position;
            transform.RotateAround(headWorldPos, Vector3.up, angle);
        }
        #endregion

        #region Teleportation
        private void HandleTeleport()
        {
            bool aimInput = false;

            if (_locomotionMode == LocomotionMode.Both)
            {
                // In Both mode: Left stick moves smoothly, holding Grip + pushing stick forward OR secondary trigger aims teleport
                bool gripHeld = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, _teleportController);
                Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _teleportController);
                bool stickForward = stick.y > _teleportAimThreshold;

                // Also allow aiming with Right Hand thumbstick forward if left is moving
                Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
                bool rightStickForward = rightStick.y > _teleportAimThreshold && Mathf.Abs(rightStick.x) < 0.35f;

                aimInput = (gripHeld && stickForward) || rightStickForward;
            }
            else
            {
                // TeleportOnly mode: simple forward push on teleport controller
                Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _teleportController);
                aimInput = stick.y > _teleportAimThreshold;
            }

#if UNITY_EDITOR
            if (Input.GetKey(KeyCode.T) || Input.GetKey(KeyCode.Space))
            {
                aimInput = true;
            }
#endif

            if (aimInput)
            {
                _isAiming = true;
                SimulateArc();
            }
            else if (_isAiming)
            {
                Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, _teleportController);
                if (stick.magnitude < _teleportReleaseThreshold)
                {
                    if (_isTargetValid)
                    {
                        StartCoroutine(ExecuteTeleportRoutine());
                    }
                    _isAiming = false;
                    HideVisuals();
                }
            }
            else
            {
                HideVisuals();
            }
        }

        private void SimulateArc()
        {
            Transform aimTransform = (_teleportController == OVRInput.Controller.LTouch) ? _leftHandAnchor : _rightHandAnchor;
            if (aimTransform == null) aimTransform = transform;

            Vector3 startPos = aimTransform.position;
            Vector3 forward = aimTransform.forward;
            Vector3 velocity = forward * _initialVelocity;

            _arcPoints.Clear();
            _arcPoints.Add(startPos);

            Vector3 currentPos = startPos;
            Vector3 gravity = Physics.gravity;
            _isTargetValid = false;

            for (int i = 0; i < _maxSimulationSteps; i++)
            {
                Vector3 nextPos = currentPos + velocity * _arcSimulationStep + 0.5f * gravity * (_arcSimulationStep * _arcSimulationStep);
                velocity += gravity * _arcSimulationStep;

                Vector3 segment = nextPos - currentPos;
                float dist = segment.magnitude;

                if (dist > 0.0001f && Physics.Raycast(currentPos, segment.normalized, out RaycastHit hit, dist, _collisionLayerMask, QueryTriggerInteraction.Ignore))
                {
                    _arcPoints.Add(hit.point);
                    ValidateHit(hit);
                    break;
                }

                _arcPoints.Add(nextPos);
                currentPos = nextPos;
            }

            UpdateVisuals();
        }

        private void ValidateHit(RaycastHit hit)
        {
            _targetPosition = hit.point;
            _targetNormal = hit.normal;

            // Surface normal check (must be upward facing ground)
            if (hit.normal.y < 0.65f)
            {
                _isTargetValid = false;
                return;
            }

            // Teleport Area or Layer check
            bool hasTeleportArea = hit.collider.GetComponentInParent<TeleportArea>() != null;
            bool layerMatches = (_teleportLayerMask.value & (1 << hit.collider.gameObject.layer)) != 0;

            if (!hasTeleportArea && !layerMatches)
            {
                _isTargetValid = false;
                return;
            }

            // Scene boundary constraint check
            if (_enforceSceneBoundaries && !_sceneBoundaries.Contains(hit.point))
            {
                _isTargetValid = false;
                return;
            }

            // Line of sight clearance check
            Vector3 headPos = _centerEyeAnchor != null ? _centerEyeAnchor.position : transform.position + Vector3.up * 1.7f;
            Vector3 targetHeadHeight = hit.point + Vector3.up * 1.5f;
            if (Physics.Linecast(headPos, targetHeadHeight, out RaycastHit wallHit, _collisionLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (wallHit.collider != hit.collider && wallHit.normal.y < 0.7f)
                {
                    _isTargetValid = false;
                    return;
                }
            }

            _isTargetValid = true;
        }

        private IEnumerator ExecuteTeleportRoutine()
        {
            _isTeleporting = true;

            // 1. Subtle Comfort Fade Out
            if (_enableComfortFade)
            {
                yield return StartCoroutine(FadeScreen(0f, 1f, _fadeDuration));
            }

            // 2. Perform Movement
            ApplyTeleportPosition();

            // 3. Controller Haptic Feedback
            OVRInput.SetControllerVibration(0.4f, 0.4f, _teleportController);
            yield return new WaitForSeconds(0.04f);
            OVRInput.SetControllerVibration(0f, 0f, _teleportController);

            // 4. Comfort Fade In
            if (_enableComfortFade)
            {
                yield return StartCoroutine(FadeScreen(1f, 0f, _fadeDuration * 1.25f));
            }

            _isTeleporting = false;
        }

        private void ApplyTeleportPosition()
        {
            Vector3 headWorld = _centerEyeAnchor != null ? _centerEyeAnchor.position : transform.position;
            Vector3 currentGroundHead = new Vector3(headWorld.x, transform.position.y, headWorld.z);
            Vector3 targetGround = new Vector3(_targetPosition.x, _targetPosition.y, _targetPosition.z);

            Vector3 delta = targetGround - currentGroundHead;

            if (_characterController != null)
            {
                _characterController.enabled = false;
                transform.position += delta;
                _characterController.enabled = true;
            }
            else
            {
                transform.position += delta;
            }
        }
        #endregion

        #region Comfort Fade Implementation
        private void SetupFadeOverlay()
        {
            if (_centerEyeAnchor == null) return;

            Transform existingFade = _centerEyeAnchor.Find("ComfortFadeOverlay");
            GameObject fadeGO;
            if (existingFade == null)
            {
                fadeGO = new GameObject("ComfortFadeOverlay");
                fadeGO.transform.SetParent(_centerEyeAnchor, false);
                fadeGO.transform.localPosition = new Vector3(0f, 0f, 0.15f); // 15cm in front of eye
                fadeGO.transform.localRotation = Quaternion.identity;
            }
            else
            {
                fadeGO = existingFade.gameObject;
            }

            MeshFilter mf = fadeGO.GetComponent<MeshFilter>() ?? fadeGO.AddComponent<MeshFilter>();
            _fadeRenderer = fadeGO.GetComponent<MeshRenderer>() ?? fadeGO.AddComponent<MeshRenderer>();

            // Quad mesh
            Mesh mesh = new Mesh();
            mesh.name = "ComfortFadeQuad";
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.3f, -0.3f, 0f),
                new Vector3(0.3f, -0.3f, 0f),
                new Vector3(-0.3f, 0.3f, 0f),
                new Vector3(0.3f, 0.3f, 0f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _fadeMaterial = new Material(shader);
            SetFadeAlpha(0f);

            _fadeRenderer.sharedMaterial = _fadeMaterial;
            _fadeRenderer.enabled = false;
        }

        private IEnumerator FadeScreen(float fromAlpha, float toAlpha, float duration)
        {
            if (_fadeRenderer == null || _fadeMaterial == null) yield break;

            _fadeRenderer.enabled = true;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float a = Mathf.Lerp(fromAlpha, toAlpha, t);
                SetFadeAlpha(a);
                yield return null;
            }

            SetFadeAlpha(toAlpha);
            if (toAlpha <= 0.001f)
            {
                _fadeRenderer.enabled = false;
            }
        }

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeMaterial == null) return;
            Color c = _fadeColor;
            c.a = alpha;
            _fadeMaterial.color = c;
        }
        #endregion

        #region Visuals Setup & Rendering
        private void SetupVisuals()
        {
            if (_arcLineRenderer == null)
            {
                _arcLineRenderer = GetComponent<LineRenderer>();
                if (_arcLineRenderer == null)
                {
                    _arcLineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _lineMaterial = new Material(unlitShader);
            _lineMaterial.color = _validColor;

            _arcLineRenderer.material = _lineMaterial;
            _arcLineRenderer.startWidth = _lineWidth;
            _arcLineRenderer.endWidth = _lineWidth * 0.7f;
            _arcLineRenderer.positionCount = 0;
            _arcLineRenderer.useWorldSpace = true;
            _arcLineRenderer.enabled = false;

            if (_reticleObject == null)
            {
                _reticleObject = CreateProceduralReticle();
                _reticleObject.transform.SetParent(transform, false);
            }
            _reticleObject.SetActive(false);
        }

        private GameObject CreateProceduralReticle()
        {
            GameObject reticle = new GameObject("TeleportReticle");
            MeshFilter mf = reticle.AddComponent<MeshFilter>();
            MeshRenderer mr = reticle.AddComponent<MeshRenderer>();

            int segments = 32;
            float innerRadius = 0.28f;
            float outerRadius = 0.35f;

            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * innerRadius, 0.005f, sin * innerRadius);
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0.005f, sin * outerRadius);

                int next = (i + 1) % segments;
                triangles[i * 6 + 0] = i * 2;
                triangles[i * 6 + 1] = next * 2;
                triangles[i * 6 + 2] = i * 2 + 1;

                triangles[i * 6 + 3] = next * 2;
                triangles[i * 6 + 4] = next * 2 + 1;
                triangles[i * 6 + 5] = i * 2 + 1;
            }

            Mesh mesh = new Mesh();
            mesh.name = "TeleportReticleMesh";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _reticleMaterial = new Material(unlitShader);
            _reticleMaterial.color = _validColor;
            mr.sharedMaterial = _reticleMaterial;

            return reticle;
        }

        private void UpdateVisuals()
        {
            Color activeColor = _isTargetValid ? _validColor : _invalidColor;

            if (_arcLineRenderer != null)
            {
                _arcLineRenderer.enabled = true;
                _arcLineRenderer.positionCount = _arcPoints.Count;
                _arcLineRenderer.SetPositions(_arcPoints.ToArray());
                if (_lineMaterial != null) _lineMaterial.color = activeColor;
            }

            if (_reticleObject != null)
            {
                _reticleObject.SetActive(_isTargetValid);
                if (_isTargetValid)
                {
                    _reticleObject.transform.position = _targetPosition + _targetNormal * 0.005f;
                    _reticleObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, _targetNormal);
                    if (_reticleMaterial != null) _reticleMaterial.color = activeColor;
                }
            }
        }

        private void HideVisuals()
        {
            if (_arcLineRenderer != null && _arcLineRenderer.enabled)
            {
                _arcLineRenderer.positionCount = 0;
                _arcLineRenderer.enabled = false;
            }

            if (_reticleObject != null && _reticleObject.activeSelf)
            {
                _reticleObject.SetActive(false);
            }
        }
        #endregion

        public void SetSceneBoundaries(Bounds bounds)
        {
            _sceneBoundaries = bounds;
            _enforceSceneBoundaries = true;
        }
    }
}
