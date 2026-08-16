using System;
using Phyzzle.Abilities;
using UnityEngine;

namespace Phyzzle.UI
{
    [Serializable]
    public sealed class PlayerHudAbilitySlot
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject attach;
        [SerializeField] private GameObject rewind;

        public PlayerHudAbilitySlot(GameObject rootObject, GameObject attachObject, GameObject rewindObject)
        {
            root = rootObject;
            attach = attachObject;
            rewind = rewindObject;
        }

        public GameObject Root => root;
        public GameObject Attach => attach;
        public GameObject Rewind => rewind;

        public void Apply(PlayerAbilityController.AbilityKind ability, bool visible)
        {
            SetActive(root, visible);
            SetActive(attach, visible && ability == PlayerAbilityController.AbilityKind.Attach);
            SetActive(rewind, visible && ability == PlayerAbilityController.AbilityKind.Rewind);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }

    [Serializable]
    public sealed class PlayerHudAbilitySelector
    {
        [SerializeField] private GameObject root;
        [SerializeField] private PlayerHudAbilitySlot previous;
        [SerializeField] private PlayerHudAbilitySlot current;
        [SerializeField] private PlayerHudAbilitySlot next;

        public PlayerHudAbilitySelector(
            GameObject rootObject,
            PlayerHudAbilitySlot previousSlot,
            PlayerHudAbilitySlot currentSlot,
            PlayerHudAbilitySlot nextSlot)
        {
            root = rootObject;
            previous = previousSlot;
            current = currentSlot;
            next = nextSlot;
        }

        public GameObject Root => root;

        public void Apply(
            PlayerAbilityController.AbilityKind selected,
            bool showNeighbors)
        {
            SetActive(root, true);
            current?.Apply(selected, true);
            PlayerAbilityController.AbilityKind other =
                selected == PlayerAbilityController.AbilityKind.Attach
                    ? PlayerAbilityController.AbilityKind.Rewind
                    : PlayerAbilityController.AbilityKind.Attach;
            previous?.Apply(other, showNeighbors);
            next?.Apply(other, showNeighbors);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }

    [Serializable]
    public sealed class PlayerHudPromptSet
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject attachDefault;
        [SerializeField] private GameObject catchPrompt;
        [SerializeField] private GameObject attachHoldSingle;
        [SerializeField] private GameObject attachHoldIsland;
        [SerializeField] private GameObject rotationSingle;
        [SerializeField] private GameObject rotationIsland;
        [SerializeField] private GameObject stick;

        public PlayerHudPromptSet(
            GameObject root,
            GameObject attachDefault,
            GameObject catchPrompt,
            GameObject attachHoldSingle,
            GameObject attachHoldIsland,
            GameObject rotationSingle,
            GameObject rotationIsland,
            GameObject stick)
        {
            this.root = root;
            this.attachDefault = attachDefault;
            this.catchPrompt = catchPrompt;
            this.attachHoldSingle = attachHoldSingle;
            this.attachHoldIsland = attachHoldIsland;
            this.rotationSingle = rotationSingle;
            this.rotationIsland = rotationIsland;
            this.stick = stick;
        }

        public GameObject Root => root;
        public GameObject AttachDefault => attachDefault;
        public GameObject Catch => catchPrompt;
        public GameObject AttachHoldSingle => attachHoldSingle;
        public GameObject AttachHoldIsland => attachHoldIsland;
        public GameObject RotationSingle => rotationSingle;
        public GameObject RotationIsland => rotationIsland;
        public GameObject Stick => stick;

        public void Apply(PlayerHudVisualState state, bool selectedDevice)
        {
            bool hasPrompt = state.AttachDefault || state.Catch ||
                             state.AttachHoldSingle || state.AttachHoldIsland ||
                             state.RotationSingle || state.RotationIsland || state.Stick;
            SetActive(root, selectedDevice && hasPrompt);
            SetActive(attachDefault, selectedDevice && state.AttachDefault);
            SetActive(catchPrompt, selectedDevice && state.Catch);
            SetActive(attachHoldSingle, selectedDevice && state.AttachHoldSingle);
            SetActive(attachHoldIsland, selectedDevice && state.AttachHoldIsland);
            SetActive(rotationSingle, selectedDevice && state.RotationSingle);
            SetActive(rotationIsland, selectedDevice && state.RotationIsland);
            SetActive(stick, selectedDevice && state.Stick);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerHudView : MonoBehaviour
    {
        [SerializeField] private GameObject defaultCrosshair;
        [SerializeField] private GameObject targetCrosshair;
        [SerializeField] private GameObject rotationArrow;
        [SerializeField] private PlayerHudPromptSet gamepadPrompts;
        [SerializeField] private PlayerHudPromptSet keyboardPrompts;
        [SerializeField] private PlayerHudAbilitySelector abilitySelector;

        [Header("Editor Preview")]
        [SerializeField] private PlayerHudMode previewMode;
        [SerializeField] private bool previewHasTarget;
        [SerializeField] private bool previewRotateMode;
        [SerializeField] private bool previewTouchingAttachable;
        [SerializeField, Min(1)] private int previewIslandSize = 1;
        [SerializeField] private PlayerInputDeviceKind previewInputDevice =
            PlayerInputDeviceKind.Gamepad;
        [SerializeField] private PlayerAbilityController.AbilityKind previewSelectedAbility =
            PlayerAbilityController.AbilityKind.Attach;
        [SerializeField] private bool previewShowAbilityNeighbors;

        public void Configure(
            GameObject normalCrosshair,
            GameObject selectedCrosshair,
            GameObject rotateArrow,
            PlayerHudPromptSet gamepad,
            PlayerHudPromptSet keyboard,
            PlayerHudAbilitySelector selector)
        {
            defaultCrosshair = normalCrosshair;
            targetCrosshair = selectedCrosshair;
            rotationArrow = rotateArrow;
            gamepadPrompts = gamepad;
            keyboardPrompts = keyboard;
            abilitySelector = selector;
        }

        public void Configure(
            GameObject normalCrosshair,
            GameObject selectedCrosshair,
            GameObject rotateArrow,
            PlayerHudPromptSet gamepad,
            PlayerHudPromptSet keyboard)
        {
            Configure(
                normalCrosshair,
                selectedCrosshair,
                rotateArrow,
                gamepad,
                keyboard,
                null);
        }

        public void Render(PlayerHudState state, PlayerInputDeviceKind inputDevice)
        {
            PlayerHudVisualState visual = PlayerHudStateResolver.Resolve(state);
            SetActive(defaultCrosshair, visual.DefaultCrosshair);
            SetActive(targetCrosshair, visual.TargetCrosshair);
            SetActive(rotationArrow, visual.RotationArrow);
            gamepadPrompts?.Apply(visual, inputDevice == PlayerInputDeviceKind.Gamepad);
            keyboardPrompts?.Apply(visual, inputDevice == PlayerInputDeviceKind.Keyboard);
            abilitySelector?.Apply(state.SelectedAbility, state.ShowAbilityNeighbors);
        }

        public void SetPreview(PlayerHudState state, PlayerInputDeviceKind inputDevice)
        {
            previewMode = state.Mode;
            previewHasTarget = state.HasTarget;
            previewRotateMode = state.RotateMode;
            previewTouchingAttachable = state.TouchingAttachable;
            previewIslandSize = state.IslandSize;
            previewInputDevice = inputDevice;
            previewSelectedAbility = state.SelectedAbility;
            previewShowAbilityNeighbors = state.ShowAbilityNeighbors;
            Render(state, inputDevice);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Render(
                    new PlayerHudState(
                        previewMode,
                        previewHasTarget,
                        previewRotateMode,
                        previewTouchingAttachable,
                        previewIslandSize,
                        previewSelectedAbility,
                        previewShowAbilityNeighbors),
                    previewInputDevice);
            }
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }
}
