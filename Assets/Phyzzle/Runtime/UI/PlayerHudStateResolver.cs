namespace Phyzzle.UI
{
    /// <summary>
    /// 논리적인 플레이어 HUD 상태를 실제 표시할 HUD 요소 조합으로 변환한다.
    /// </summary>
    public static class PlayerHudStateResolver
    {
        /// <summary>
        /// 현재 HUD 모드와 세부 상태를 기준으로 각 UI 요소의 표시 여부를 계산한다.
        /// </summary>
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

