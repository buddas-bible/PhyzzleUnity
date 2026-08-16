using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    internal static class RewindVisualShaderIds
    {
        internal static readonly int SelectionBlend =
            Shader.PropertyToID("_RewindSelectionBlend");
        internal static readonly int WorldSaturation =
            Shader.PropertyToID("_RewindWorldSaturation");
        internal static readonly int EligibleColor =
            Shader.PropertyToID("_RewindEligibleColor");
        internal static readonly int ActiveColor =
            Shader.PropertyToID("_RewindActiveColor");
        internal static readonly int EligibleOutlinePixels =
            Shader.PropertyToID("_RewindEligibleOutlinePixels");
        internal static readonly int ActiveOutlinePixels =
            Shader.PropertyToID("_RewindActiveOutlinePixels");
        internal static readonly int MaskTexture =
            Shader.PropertyToID("_RewindMaskTexture");
        internal static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        internal static readonly int PreviewMaskWeight =
            Shader.PropertyToID("_RewindPreviewMaskWeight");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            ResetGlobals();
        }

        internal static void ResetGlobals()
        {
            Shader.SetGlobalFloat(SelectionBlend, 0f);
            Shader.SetGlobalFloat(WorldSaturation, 1f);
            Shader.SetGlobalColor(EligibleColor, Color.clear);
            Shader.SetGlobalColor(ActiveColor, Color.clear);
            Shader.SetGlobalFloat(EligibleOutlinePixels, 0f);
            Shader.SetGlobalFloat(ActiveOutlinePixels, 0f);
            Shader.SetGlobalTexture(MaskTexture, Texture2D.blackTexture);
        }
    }
}
