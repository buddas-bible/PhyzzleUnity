namespace Phyzzle.Abilities.Rewind
{
    internal static class RewindVisualLayers
    {
        internal const uint PlayerPreserve = 1u << 29;
        internal const uint Eligible = 1u << 30;
        internal const uint Active = 1u << 31;
        internal const uint Owned = PlayerPreserve | Eligible | Active;
    }
}
