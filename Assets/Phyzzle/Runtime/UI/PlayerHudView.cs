using System;
using Phyzzle.Abilities;
using UnityEngine;

namespace Phyzzle.UI
{
    /// <summary>
    /// HUD 능력 선택기의 한 슬롯에서 능력별 아이콘과 루트 표시를 관리한다.
    /// </summary>
    [Serializable]
    public sealed class PlayerHudAbilitySlot
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject attach;
        [SerializeField] private GameObject rewind;

        /// <summary>
        /// 슬롯의 루트와 능력별 표시 오브젝트로 슬롯을 생성한다.
        /// </summary>
        public PlayerHudAbilitySlot(GameObject rootObject, GameObject attachObject, GameObject rewindObject)
        {
            root = rootObject;
            attach = attachObject;
            rewind = rewindObject;
        }

        public GameObject Root => root;
        public GameObject Attach => attach;
        public GameObject Rewind => rewind;

        /// <summary>
        /// 지정한 능력과 슬롯 표시 여부에 맞춰 루트와 능력 아이콘을 갱신한다.
        /// </summary>
        public void Apply(PlayerAbilityController.AbilityKind ability, bool visible)
        {
            SetActive(root, visible);
            SetActive(attach, visible && ability == PlayerAbilityController.AbilityKind.Attach);
            SetActive(rewind, visible && ability == PlayerAbilityController.AbilityKind.Rewind);
        }

        /// <summary>
        /// 대상 GameObject가 존재하고 상태가 다를 때만 활성 상태를 변경한다.
        /// </summary>
        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }

    /// <summary>
    /// 현재 선택 능력과 양옆 능력 슬롯의 표시 상태를 관리한다.
    /// </summary>
    [Serializable]
    public sealed class PlayerHudAbilitySelector
    {
        [SerializeField] private GameObject root;
        [SerializeField] private PlayerHudAbilitySlot previous;
        [SerializeField] private PlayerHudAbilitySlot current;
        [SerializeField] private PlayerHudAbilitySlot next;

        /// <summary>
        /// 선택기 루트와 이전·현재·다음 슬롯 참조로 능력 선택기를 생성한다.
        /// </summary>
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

        /// <summary>
        /// 현재 선택된 능력을 중앙에 표시하고 필요하면 이웃 능력 슬롯도 함께 표시한다.
        /// </summary>
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

        /// <summary>
        /// 대상 GameObject가 존재하고 상태가 다를 때만 활성 상태를 변경한다.
        /// </summary>
        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }

    /// <summary>
    /// 한 입력 장치용 HUD 프롬프트 오브젝트 묶음과 표시 규칙을 관리한다.
    /// </summary>
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

        /// <summary>
        /// 프롬프트 루트와 개별 프롬프트 GameObject 참조로 묶음을 생성한다.
        /// </summary>
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

        /// <summary>
        /// 현재 HUD 시각 상태와 선택된 입력 장치 여부에 맞춰 프롬프트 표시를 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 대상 GameObject가 존재하고 상태가 다를 때만 활성 상태를 변경한다.
        /// </summary>
        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }

    /// <summary>
    /// 계산된 HUD 상태를 십자선, 프롬프트와 능력 선택기 GameObject 표시에 반영한다.
    /// </summary>
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

        /// <summary>
        /// HUD를 구성하는 십자선, 프롬프트와 능력 선택기 참조를 설정한다.
        /// </summary>
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

        /// <summary>
        /// 능력 선택기 없이 사용할 수 있도록 기본 HUD 표시 참조만 설정한다.
        /// </summary>
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

        /// <summary>
        /// 논리 HUD 상태를 시각 상태로 변환하고 현재 입력 장치에 맞춰 모든 HUD 요소를 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 에디터 미리보기용 상태를 직렬화 필드에 저장하고 즉시 HUD에 렌더링한다.
        /// </summary>
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

        /// <summary>
        /// 에디터에서 미리보기 값이 변경되면 현재 프리뷰 상태를 다시 렌더링한다.
        /// </summary>
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

        /// <summary>
        /// 대상 GameObject가 존재하고 상태가 다를 때만 활성 상태를 변경한다.
        /// </summary>
        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }
}
