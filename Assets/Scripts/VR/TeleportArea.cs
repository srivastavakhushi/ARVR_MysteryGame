using UnityEngine;

namespace CrimeSceneVR.Locomotion
{
    /// <summary>
    /// Designates a GameObject or surface as a valid teleportable area.
    /// Can be attached to individual floor tiles, composite floor parents, or ground colliders.
    /// </summary>
    [AddComponentMenu("Crime Scene VR/Teleport Area")]
    public class TeleportArea : MonoBehaviour
    {
        [Tooltip("Optional custom landing height offset above the surface.")]
        [SerializeField] private float _landingOffset = 0.0f;

        public float LandingOffset => _landingOffset;

        /// <summary>
        /// Validates whether a specific hit point is allowed for teleportation.
        /// </summary>
        public virtual bool IsValidLandingPosition(Vector3 worldPoint, Vector3 surfaceNormal)
        {
            // Surface must be sufficiently flat (upwards pointing normal)
            return surfaceNormal.y >= 0.7f;
        }
    }
}
