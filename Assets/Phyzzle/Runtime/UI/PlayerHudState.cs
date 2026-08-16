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

    public readonly struct PlayerHudState
    {
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

    public readonly struct PlayerHudVisualState
    {
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
