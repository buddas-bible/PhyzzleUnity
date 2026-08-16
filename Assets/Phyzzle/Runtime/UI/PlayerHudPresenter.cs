using Phyzzle.Abilities;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.UI
{
    public static class PlayerHudPresenterState
    {
        public static PlayerHudState Build(
            AttachAbilityController.AbilityState attachState,
            bool attachHasTarget,
            RewindAbilityController.AbilityState rewindState,
            bool rewindHasTarget,
            bool rotateHeld,
            bool touchingAttachable,
            int islandSize,
            PlayerAbilityController.AbilityKind selectedAbility =
                PlayerAbilityController.AbilityKind.Attach,
            bool showAbilityNeighbors = false)
        {
            switch (attachState)
            {
                case AttachAbilityController.AbilityState.Selecting:
                    return new PlayerHudState(
                        PlayerHudMode.AttachSelecting,
                        hasTarget: attachHasTarget,
                        selectedAbility: selectedAbility,
                        showAbilityNeighbors: showAbilityNeighbors);

                case AttachAbilityController.AbilityState.Holding:
                    return new PlayerHudState(
                        PlayerHudMode.AttachHolding,
                        rotateMode: rotateHeld,
                        touchingAttachable: touchingAttachable,
                        islandSize: islandSize,
                        selectedAbility: selectedAbility,
                        showAbilityNeighbors: showAbilityNeighbors);
            }

            return rewindState == RewindAbilityController.AbilityState.Selecting
                ? new PlayerHudState(
                    PlayerHudMode.RewindSelecting,
                    hasTarget: rewindHasTarget,
                    selectedAbility: selectedAbility,
                    showAbilityNeighbors: showAbilityNeighbors)
                : new PlayerHudState(
                    PlayerHudMode.Default,
                    selectedAbility: selectedAbility,
                    showAbilityNeighbors: showAbilityNeighbors);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerHudPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerAbilityController abilityController;
        [SerializeField] private AttachAbilityController attachAbility;
        [SerializeField] private AttachHoldController attachHold;
        [SerializeField] private AttachmentService attachmentService;
        [SerializeField] private RewindAbilityController rewindAbility;
        [SerializeField] private PlayerHudView view;
        [SerializeField, Min(0f)] private float abilitySelectionDuration = 1.2f;

        private bool hasObservedAbility;
        private PlayerAbilityController.AbilityKind lastSelectedAbility;
        private float abilitySelectionRemaining;

        public void Configure(
            PlayerInputReader inputReader,
            PlayerAbilityController playerAbilityController,
            AttachAbilityController attachController,
            AttachHoldController holdController,
            AttachmentService service,
            RewindAbilityController rewindController,
            PlayerHudView hudView)
        {
            input = inputReader;
            abilityController = playerAbilityController;
            attachAbility = attachController;
            attachHold = holdController;
            attachmentService = service;
            rewindAbility = rewindController;
            view = hudView;
            hasObservedAbility = false;
            abilitySelectionRemaining = 0f;
        }

        public void Configure(
            PlayerInputReader inputReader,
            AttachAbilityController attachController,
            AttachHoldController holdController,
            AttachmentService service,
            RewindAbilityController rewindController,
            PlayerHudView hudView)
        {
            Configure(
                inputReader,
                null,
                attachController,
                holdController,
                service,
                rewindController,
                hudView);
        }

        private void OnEnable()
        {
            hasObservedAbility = false;
            abilitySelectionRemaining = 0f;
        }

        private void LateUpdate()
        {
            if (view == null)
            {
                return;
            }

            PlayerAbilityController.AbilityKind selectedAbility =
                abilityController != null
                    ? abilityController.SelectedAbility
                    : PlayerAbilityController.AbilityKind.Attach;
            if (!hasObservedAbility)
            {
                lastSelectedAbility = selectedAbility;
                hasObservedAbility = true;
            }
            else if (selectedAbility != lastSelectedAbility)
            {
                lastSelectedAbility = selectedAbility;
                abilitySelectionRemaining = abilitySelectionDuration;
            }
            else
            {
                abilitySelectionRemaining = Mathf.Max(
                    0f,
                    abilitySelectionRemaining - Time.unscaledDeltaTime);
            }

            AttachableObject held = attachHold != null ? attachHold.HeldObject : null;
            int islandSize = 1;
            bool touching = false;
            if (held != null && attachmentService != null)
            {
                islandSize = attachmentService.GetIsland(held).Count;
                touching = attachmentService.IsTouchingAttachable(held);
            }

            PlayerHudState state = PlayerHudPresenterState.Build(
                attachAbility != null
                    ? attachAbility.State
                    : AttachAbilityController.AbilityState.Default,
                attachAbility != null && attachAbility.CurrentTarget != null,
                rewindAbility != null
                    ? rewindAbility.State
                    : RewindAbilityController.AbilityState.Default,
                rewindAbility != null && rewindAbility.CurrentTarget != null,
                input != null && input.RotateHeld,
                touching,
                islandSize,
                selectedAbility,
                abilitySelectionRemaining > 0f);

            view.Render(
                state,
                input != null
                    ? input.CurrentInputDevice
                    : PlayerInputDeviceKind.Keyboard);
        }
    }
}
