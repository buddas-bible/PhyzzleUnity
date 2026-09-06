using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    [CreateAssetMenu(fileName = "AttachSettings", menuName = "Phyzzle/Abilities/Attach Settings")]
    public sealed class AttachSettings : ScriptableObject
    {
        [Header("Target search")]
        [Min(1)] public int overlapBufferSize = 65;
        [Min(0f)] public float nearbySearchRadius = 40f;
        [Min(0f)] public float targetRayDistance = 20f;
        public LayerMask nearbyMask = UnityEngine.Physics.DefaultRaycastLayers;
        public LayerMask targetMask = UnityEngine.Physics.DefaultRaycastLayers;

        [Header("Hold target")]
        [Min(0f)] public float targetVerticalSpeed = 4f;
        [Min(0f)] public float targetDepthStep = 1f;
        public float minTargetY = -4f;
        public float maxTargetY = 10f;
        [Min(0f)] public float minTargetZ = 1f;
        [Min(0f)] public float maxTargetZ = 20f;
        [Min(0f)] public float targetPositionOffset = 1f;
        [Min(0f)] public float holdMoveSpeed = 3f;
        [Min(0f)] public float orbitDegreesPerSecond = 180f;
        [Min(0f)] public float orbitArcRatio = 1.5f;

        [Header("Legacy spring")]
        [Min(0f)] public float linearDampingRatio = 3f;
        [Min(0f)] public float linearFrequency = 500f;
        [Min(0f)] public float maxLinearAcceleration = 50f;
        [Min(0f)] public float angularDampingRatio = 3.6f;
        [Min(0f)] public float angularFrequency = 500f;
        [Min(0f)] public float maxAngularAcceleration = 40f;
        [Min(0f)] public float velocityDamping = 3f;

        [Header("Rotation")]
        [Range(1f, 180f)] public float rotationStepDegrees = 45f;
        [Min(0f)] public float fineAdjustmentSpeed = 45f;

        [Header("Selected rigidbody override")]
        [Min(0.0001f)] public float selectedMass = 0.1f;
        public Vector3 selectedInertiaTensor = new(100f, 100f, 100f);
        public PhysicsMaterial selectedPhysicsMaterial;

        [Header("Visuals")]
        [Min(0f)] public float visualEnterDuration = 0.14f;
        [Min(0f)] public float visualExitDuration = 0.16f;
        public Color eligibleVisualColor = new Color32(0xE8, 0x65, 0x16, 0xFF);
        public Color focusedVisualColor = new Color32(0xFF, 0x85, 0x2B, 0xFF);
        public Color heldVisualColor = new Color32(0x43, 0xF0, 0x8A, 0xFF);
        [Min(0f)] public float eligibleOutlinePixels = 1f;
        [Min(0f)] public float focusedOutlinePixels = 2.5f;
        [Min(0f)] public float heldOutlinePixels = 2f;
        [Min(0f)] public float heldPulseSpeed = 1.2f;
        [Range(0f, 1f)] public float heldPulseStrength = 0.25f;
        [Min(0f)] public float projectionMaxDistance = 12f;
        [Min(0f)] public float projectionSurfaceBias = 0.015f;
        [Range(0f, 1f)] public float projectionOpacity = 0.28f;
        [Min(0f)] public float projectionDepthTolerance = 0.06f;
        [Range(0f, 1f)] public float projectionParallelThreshold = 0.15f;
    }
}
