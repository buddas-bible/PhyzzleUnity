using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    [CreateAssetMenu(fileName = "RewindSettings", menuName = "Phyzzle/Abilities/Rewind Settings")]
    public sealed class RewindSettings : ScriptableObject
    {
        [Min(0.1f)] public float historyDuration = 20f;
        [Min(0.01f)] public float playbackRate = 1f;
        [Min(0f)] public float recordPositionThreshold = 0.002f;
        [Min(0f)] public float recordRotationThreshold = 0.0573f;
        [Min(0.01f)] public float maxLinearSpeed = 50f;
        [Min(0.01f)] public float maxAngularSpeed = 720f;
        [Min(0f)] public float targetRayDistance = 20f;
        [Min(0f)] public float nearbySearchRadius = 40f;
        [Min(1)] public int overlapBufferSize = 65;
        public LayerMask targetMask = UnityEngine.Physics.DefaultRaycastLayers;
        public bool pauseWorldWhileTargeting = true;

        [Header("Selection Visuals")]
        [Min(0f)] public float selectionVisualEnterDuration = 0.14f;
        [Min(0f)] public float selectionVisualExitDuration = 0.16f;
        [Range(0f, 1f)] public float selectionWorldSaturation = 0.12f;
        public Color eligibleVisualColor = new Color32(0xD7, 0xA5, 0x2D, 0xFF);
        public Color activeVisualColor = new Color32(0xFF, 0xD4, 0x5C, 0xFF);
        [Min(0f)] public float eligibleOutlinePixels = 1f;
        [Min(0f)] public float activeOutlinePixels = 2.5f;
        [Min(0f)] public float previewPathWidth = 0.06f;
        [Min(2)] public int previewPathMaxPoints = 256;
        [Range(2, 12)] public int previewGhostCount = 8;
        [Range(0f, 1f)] public float previewGhostAlpha = 0.28f;

        public int GetHistoryCapacity(float fixedDeltaTime) =>
            Mathf.Max(2, Mathf.RoundToInt(historyDuration / Mathf.Max(0.000001f, fixedDeltaTime)) + 1);
    }
}
