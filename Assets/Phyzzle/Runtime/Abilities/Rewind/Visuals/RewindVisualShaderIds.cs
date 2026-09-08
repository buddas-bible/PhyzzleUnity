using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 되감기 시각 효과가 공유하는 셰이더 프로퍼티 ID와 전역 기본값 초기화를 관리한다.
    /// </summary>
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

        /// <summary>
        /// Unity 서브시스템 재등록 시 되감기 전역 셰이더 값을 기본 상태로 초기화한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            ResetGlobals();
        }

        /// <summary>
        /// 되감기 선택 효과가 사용하는 모든 전역 셰이더 값을 비활성 기본값으로 복원한다.
        /// </summary>
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
