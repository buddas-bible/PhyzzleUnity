using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    internal static class AttachVisualLayers
    {
        internal const uint Eligible = 1u << 26;
        internal const uint Focused = 1u << 27;
        internal const uint Held = 1u << 28;
        internal const uint Owned = Eligible | Focused | Held;
    }

    internal static class AttachVisualShaderIds
    {
        internal static readonly int VisualBlend = Shader.PropertyToID("_AttachVisualBlend");
        internal static readonly int EligibleColor = Shader.PropertyToID("_AttachEligibleColor");
        internal static readonly int FocusedColor = Shader.PropertyToID("_AttachFocusedColor");
        internal static readonly int HeldColor = Shader.PropertyToID("_AttachHeldColor");
        internal static readonly int EligibleOutlinePixels = Shader.PropertyToID("_AttachEligibleOutlinePixels");
        internal static readonly int FocusedOutlinePixels = Shader.PropertyToID("_AttachFocusedOutlinePixels");
        internal static readonly int HeldOutlinePixels = Shader.PropertyToID("_AttachHeldOutlinePixels");
        internal static readonly int HeldPulseSpeed = Shader.PropertyToID("_AttachHeldPulseSpeed");
        internal static readonly int HeldPulseStrength = Shader.PropertyToID("_AttachHeldPulseStrength");
        internal static readonly int MaskTexture = Shader.PropertyToID("_AttachMaskTexture");
        internal static readonly int ProjectionPlane = Shader.PropertyToID("_AttachProjectionPlane");
        internal static readonly int ProjectionDirection = Shader.PropertyToID("_AttachProjectionDirection");
        internal static readonly int ProjectionOpacity = Shader.PropertyToID("_AttachProjectionOpacity");
        internal static readonly int ProjectionBias = Shader.PropertyToID("_AttachProjectionBias");
        internal static readonly int ProjectionDepthTolerance = Shader.PropertyToID("_AttachProjectionDepthTolerance");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            Shader.SetGlobalFloat(VisualBlend, 0f);
            Shader.SetGlobalTexture(MaskTexture, Texture2D.blackTexture);
        }
    }
}
