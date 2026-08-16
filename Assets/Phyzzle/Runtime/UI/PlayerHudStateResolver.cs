namespace Phyzzle.UI
{
    public static class PlayerHudStateResolver
    {
        public static PlayerHudVisualState Resolve(PlayerHudState state)
        {
            switch (state.Mode)
            {
                case PlayerHudMode.AttachSelecting:
                case PlayerHudMode.RewindSelecting:
                    return new PlayerHudVisualState(
                        defaultCrosshair: true,
                        targetCrosshair: state.HasTarget,
                        attachDefault: true,
                        catchPrompt: state.HasTarget);

                case PlayerHudMode.AttachHolding:
                    bool island = state.IslandSize > 1;
                    return new PlayerHudVisualState(
                        attachHoldSingle: !state.RotateMode && !island,
                        attachHoldIsland: !state.RotateMode && island,
                        rotationSingle: state.RotateMode && !island,
                        rotationIsland: state.RotateMode && island,
                        stick: state.TouchingAttachable,
                        rotationArrow: state.RotateMode);

                default:
                    return default;
            }
        }
    }
}

