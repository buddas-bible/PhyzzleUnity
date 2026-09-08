namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 되감기 선택 효과에서 사용하는 전용 Rendering Layer 비트 마스크를 정의한다.
    /// </summary>
    internal static class RewindVisualLayers
    {
        internal const uint PlayerPreserve = 1u << 29;
        internal const uint Eligible = 1u << 30;
        internal const uint Active = 1u << 31;
        internal const uint Owned = PlayerPreserve | Eligible | Active;
    }
}
