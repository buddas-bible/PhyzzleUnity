using UnityEngine;

namespace Phyzzle.Player
{
    [CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Phyzzle/Player/Movement Settings")]
    public sealed class PlayerMovementSettings : ScriptableObject
    {
        [Header("Original Phyzzle values")]
        [Min(0f)] public float moveSpeed = 5f;
        [Min(0f)] public float holdMoveSpeed = 3f;
        [Min(0f)] public float jumpAcceleration = 10f;
        [Min(0f)] public float maxVerticalSpeed = 30f;
        [Range(0f, 89f)] public float slopeLimitDegrees = 45f;

        [Header("Air control")]
        [Min(0f)] public float airWishSpeed = 2.5f;
        [Min(0f)] public float airAcceleration = 8f;

        [Header("Ground probe")]
        [Min(0.01f)] public float groundProbeDistance = 1f;
        [Min(0f)] public float groundProbeRadius = 0.15f;
        public LayerMask groundMask = UnityEngine.Physics.DefaultRaycastLayers;

        [Header("Collision response")]
        [Min(0f)] public float impactThreshold = 50f;
        [Min(0f)] public float flyingDuration = 0.5f;
    }
}
