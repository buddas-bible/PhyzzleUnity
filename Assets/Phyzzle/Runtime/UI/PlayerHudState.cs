using Phyzzle.Abilities;

namespace Phyzzle.UI
{
    public enum PlayerHudMode
    {
        Default,
        AttachSelecting,
        AttachHolding,
        RewindSelecting
    }

    /// <summary>
    /// 현재 플레이어 능력 상태에서 HUD가 판단에 사용할 논리 상태 값을 보관한다.
    /// </summary>
    public readonly struct PlayerHudState
    {
        /// <summary>
        /// HUD 모드와 대상·회전·부착 섬·능력 선택 상태로 논리 HUD 상태를 생성한다.
        /// </summary>
        public PlayerHudState(
            PlayerHudMode mode,
            bool hasTarget = false,
            bool rotateMode = false,
            bool touchingAttachable = false,
            int islandSize = 1,
            PlayerAbilityController.AbilityKind selectedAbility =
                PlayerAbilityController.AbilityKind.Attach,
            bool showAbilityNeighbors = false)
        {
            Mode = mode;
            HasTarget = hasTarget;
            RotateMode = rotateMode;
            TouchingAttachable = touchingAttachable;
            IslandSize = islandSize < 1 ? 1 : islandSize;
            SelectedAbility = selectedAbility;
            ShowAbilityNeighbors = showAbilityNeighbors;
        }

        public PlayerHudMode Mode { get; }
        public bool HasTarget { get; }
        public bool RotateMode { get; }
        public bool TouchingAttachable { get; }
        public int IslandSize { get; }
        public PlayerAbilityController.AbilityKind SelectedAbility { get; }
        public bool ShowAbilityNeighbors { get; }
    }

    /// <summary>
    /// 실제 HUD 요소별 표시 여부를 묶어 View에 전달하는 시각 상태다.
    /// </summary>
    public readonly struct PlayerHudVisualState
    {
        /// <summary>
        /// 각 HUD 요소의 표시 여부로 하나의 시각 상태를 생성한다.
        /// </summary>
        public PlayerHudVisualState(
            bool defaultCrosshair = false,
            bool targetCrosshair = false,
            bool attachDefault = false,
            bool catchPrompt = false,
            bool attachHoldSingle = false,
            bool attachHoldIsland = false,
            bool rotationSingle = false,
            bool rotationIsland = false,
            bool stick = false,
            bool rotationArrow = false)
        {
            DefaultCrosshair = defaultCrosshair;
            TargetCrosshair = targetCrosshair;
            AttachDefault = attachDefault;
            Catch = catchPrompt;
            AttachHoldSingle = attachHoldSingle;
            AttachHoldIsland = attachHoldIsland;
            RotationSingle = rotationSingle;
            RotationIsland = rotationIsland;
            Stick = stick;
            RotationArrow = rotationArrow;
        }

        public bool DefaultCrosshair { get; }
        public bool TargetCrosshair { get; }
        public bool AttachDefault { get; }
        public bool Catch { get; }
        public bool AttachHoldSingle { get; }
        public bool AttachHoldIsland { get; }
        public bool RotationSingle { get; }
        public bool RotationIsland { get; }
        public bool Stick { get; }
        public bool RotationArrow { get; }

        public bool AnyVisible =>
            DefaultCrosshair || TargetCrosshair || AttachDefault || Catch ||
            AttachHoldSingle || AttachHoldIsland || RotationSingle ||
            RotationIsland || Stick || RotationArrow;
    }
}
