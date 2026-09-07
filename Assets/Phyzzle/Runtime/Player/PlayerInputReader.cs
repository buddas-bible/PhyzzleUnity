using System;
using System.Collections.Generic;
using Phyzzle.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phyzzle.Player
{
    /// <summary>
    /// Unity Input System의 플레이어 입력을 샘플링하고 프레임 간 소비 가능한 입력 큐로 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Optional project actions")]
        [SerializeField] private InputActionReference moveReference;
        [SerializeField] private InputActionReference lookReference;
        [SerializeField] private InputActionReference jumpReference;
        [SerializeField] private InputActionReference useAbilityReference;
        [SerializeField] private InputActionReference cancelReference;
        [SerializeField] private InputActionReference actionXReference;
        [SerializeField] private InputActionReference actionYReference;
        [SerializeField] private InputActionReference rotateReference;
        [SerializeField] private InputActionReference previousAbilityReference;
        [SerializeField] private InputActionReference nextAbilityReference;
        [SerializeField] private InputActionReference selectAttachReference;
        [SerializeField] private InputActionReference selectRewindReference;
        [SerializeField] private InputActionReference dpadReference;
        [SerializeField] private InputActionReference leftTriggerReference;
        [SerializeField, Min(0f)] private float mouseLookScale = 1f / 900f;

        private InputActionMap fallbackMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction useAbilityAction;
        private InputAction cancelAction;
        private InputAction actionXAction;
        private InputAction actionYAction;
        private InputAction rotateAction;
        private InputAction previousAbilityAction;
        private InputAction nextAbilityAction;
        private InputAction selectAttachAction;
        private InputAction selectRewindAction;
        private InputAction dpadAction;
        private InputAction leftTriggerAction;

        private bool jumpQueued;
        private bool useAbilityQueued;
        private bool cancelQueued;
        private bool actionXQueued;
        private bool actionYQueued;
        private bool previousAbilityQueued;
        private bool nextAbilityQueued;
        private bool selectAttachQueued;
        private bool selectRewindQueued;
        private Vector2 previousDpad;
        private readonly PlayerInputDeviceTracker inputDeviceTracker = new();
        private readonly HashSet<InputAction> trackedActions = new();
        private readonly HashSet<InputAction> enabledActions = new();

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool LookIsPointerDelta { get; private set; }
        public Vector2 Dpad { get; private set; }
        public Vector2 DpadPressed { get; private set; }
        public float LeftTrigger { get; private set; }
        public bool RotateHeld => rotateAction?.IsPressed() == true;
        public PlayerInputDeviceKind CurrentInputDevice => inputDeviceTracker.Current;

        /// <summary>
        /// 활성화될 때 입력 액션을 구성하고 필요한 이벤트 구독과 액션 활성화를 수행한다.
        /// </summary>
        private void OnEnable()
        {
            BuildActions();
            EnableActions();
        }

        /// <summary>
        /// 비활성화될 때 액션과 이벤트 구독을 해제하고 현재 입력 상태를 초기화한다.
        /// </summary>
        private void OnDisable()
        {
            DisableActions();
            ClearQueuedInput();
            Move = Vector2.zero;
            Look = Vector2.zero;
            LookIsPointerDelta = false;
            Dpad = Vector2.zero;
            DpadPressed = Vector2.zero;
            LeftTrigger = 0f;
        }

        /// <summary>
        /// 컴포넌트가 파괴될 때 런타임에 생성한 fallback 입력 맵을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            fallbackMap?.Dispose();
        }

        /// <summary>
        /// 현재 축·버튼 입력을 읽고 단발 입력을 소비 대기 큐에 누적한다.
        /// </summary>
        public void Sample()
        {
            Move = Vector2.ClampMagnitude(moveAction?.ReadValue<Vector2>() ?? Vector2.zero, 1f);
            Vector2 rawLook = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            LookIsPointerDelta = lookAction?.activeControl?.device is Pointer;
            Look = LookIsPointerDelta
                ? rawLook * mouseLookScale
                : Vector2.ClampMagnitude(rawLook, 1f);
            Vector2 nextDpad = Vector2.ClampMagnitude(dpadAction?.ReadValue<Vector2>() ?? Vector2.zero, 1f);
            DpadPressed = new Vector2(
                Mathf.Abs(nextDpad.x) > 0.5f &&
                (Mathf.Abs(previousDpad.x) <= 0.5f || Mathf.Sign(nextDpad.x) != Mathf.Sign(previousDpad.x))
                    ? Mathf.Sign(nextDpad.x)
                    : 0f,
                Mathf.Abs(nextDpad.y) > 0.5f &&
                (Mathf.Abs(previousDpad.y) <= 0.5f || Mathf.Sign(nextDpad.y) != Mathf.Sign(previousDpad.y))
                    ? Mathf.Sign(nextDpad.y)
                    : 0f);
            Dpad = nextDpad;
            previousDpad = nextDpad;
            LeftTrigger = Mathf.Clamp01(leftTriggerAction?.ReadValue<float>() ?? 0f);

            jumpQueued |= jumpAction?.WasPressedThisFrame() == true;
            useAbilityQueued |= useAbilityAction?.WasPressedThisFrame() == true;
            cancelQueued |= cancelAction?.WasPressedThisFrame() == true;
            actionXQueued |= actionXAction?.WasPressedThisFrame() == true;
            actionYQueued |= actionYAction?.WasPressedThisFrame() == true;
            previousAbilityQueued |= previousAbilityAction?.WasPressedThisFrame() == true;
            nextAbilityQueued |= nextAbilityAction?.WasPressedThisFrame() == true;
            selectAttachQueued |= selectAttachAction?.WasPressedThisFrame() == true;
            selectRewindQueued |= selectRewindAction?.WasPressedThisFrame() == true;
        }

        /// <summary>
        /// 대기 중인 점프 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeJump() => Consume(ref jumpQueued);

        /// <summary>
        /// 대기 중인 능력 사용 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeUseAbility() => Consume(ref useAbilityQueued);

        /// <summary>
        /// 대기 중인 취소 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeCancel() => Consume(ref cancelQueued);

        /// <summary>
        /// 대기 중인 X 액션 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeActionX() => Consume(ref actionXQueued);

        /// <summary>
        /// 대기 중인 Y 액션 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeActionY() => Consume(ref actionYQueued);

        /// <summary>
        /// 대기 중인 이전 능력 선택 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumePreviousAbility() => Consume(ref previousAbilityQueued);

        /// <summary>
        /// 대기 중인 다음 능력 선택 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeNextAbility() => Consume(ref nextAbilityQueued);

        /// <summary>
        /// 대기 중인 부착 능력 직접 선택 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeSelectAttach() => Consume(ref selectAttachQueued);

        /// <summary>
        /// 대기 중인 되감기 능력 직접 선택 입력을 한 번 소비한다.
        /// </summary>
        public bool ConsumeSelectRewind() => Consume(ref selectRewindQueued);

        /// <summary>
        /// 프로젝트 액션 참조가 있으면 사용하고 없으면 fallback 액션을 생성해 연결한다.
        /// </summary>
        private void BuildActions()
        {
            if (moveAction != null)
            {
                return;
            }

            fallbackMap = new InputActionMap("Phyzzle Player Fallback");

            moveAction = Resolve(moveReference, CreateMoveAction());
            lookAction = Resolve(lookReference, CreateLookAction());
            jumpAction = Resolve(jumpReference, CreateButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth"));
            useAbilityAction = Resolve(useAbilityReference, CreateButton("Use Ability", "<Keyboard>/q", "<Gamepad>/leftShoulder"));
            cancelAction = Resolve(cancelReference, CreateButton("Cancel", "<Keyboard>/f", "<Gamepad>/buttonEast"));
            actionXAction = Resolve(actionXReference, CreateButton("Action X", "<Keyboard>/z", "<Gamepad>/buttonWest"));
            actionYAction = Resolve(actionYReference, CreateButton("Action Y", "<Keyboard>/r", "<Gamepad>/buttonNorth"));
            rotateAction = Resolve(rotateReference, CreateButton("Rotate", "<Keyboard>/e", "<Gamepad>/rightShoulder"));
            previousAbilityAction = Resolve(previousAbilityReference, CreateButton("Previous Ability", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left"));
            nextAbilityAction = Resolve(nextAbilityReference, CreateButton("Next Ability", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right"));
            selectAttachAction = Resolve(selectAttachReference, CreateButton("Select Attach", "<Keyboard>/1"));
            selectRewindAction = Resolve(selectRewindReference, CreateButton("Select Rewind", "<Keyboard>/2"));
            dpadAction = Resolve(dpadReference, CreateDpadAction());
            leftTriggerAction = Resolve(leftTriggerReference, CreateValue("Left Trigger", "Axis", "<Gamepad>/leftTrigger"));
        }

        /// <summary>
        /// 키보드 WASD와 게임패드 왼쪽 스틱을 사용하는 이동 fallback 액션을 생성한다.
        /// </summary>
        private InputAction CreateMoveAction()
        {
            InputAction action = fallbackMap.AddAction("Move", InputActionType.Value);
            action.expectedControlType = "Vector2";
            action.AddBinding("<Gamepad>/leftStick");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            return action;
        }

        /// <summary>
        /// 마우스 이동과 게임패드 오른쪽 스틱을 사용하는 시점 fallback 액션을 생성한다.
        /// </summary>
        private InputAction CreateLookAction()
        {
            InputAction action = fallbackMap.AddAction("Look", InputActionType.Value);
            action.expectedControlType = "Vector2";
            action.AddBinding("<Gamepad>/rightStick").WithProcessor("stickDeadzone(min=0.265)");
            action.AddBinding("<Mouse>/delta");
            return action;
        }

        /// <summary>
        /// 방향키와 게임패드 D-pad를 사용하는 방향 입력 fallback 액션을 생성한다.
        /// </summary>
        private InputAction CreateDpadAction()
        {
            InputAction action = fallbackMap.AddAction("Dpad", InputActionType.Value);
            action.expectedControlType = "Vector2";
            action.AddBinding("<Gamepad>/dpad");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            return action;
        }

        /// <summary>
        /// 지정한 경로들을 바인딩한 버튼형 fallback 액션을 생성한다.
        /// </summary>
        private InputAction CreateButton(string name, params string[] bindingPaths)
        {
            InputAction action = fallbackMap.AddAction(name, InputActionType.Button);
            foreach (string path in bindingPaths)
            {
                action.AddBinding(path);
            }

            return action;
        }

        /// <summary>
        /// 지정한 제어 타입과 경로를 사용하는 값형 fallback 액션을 생성한다.
        /// </summary>
        private InputAction CreateValue(string name, string controlType, string bindingPath)
        {
            InputAction action = fallbackMap.AddAction(name, InputActionType.Value);
            action.expectedControlType = controlType;
            action.AddBinding(bindingPath);
            return action;
        }

        /// <summary>
        /// 유효한 프로젝트 액션 참조가 있으면 반환하고 없으면 fallback 액션을 사용한다.
        /// </summary>
        private static InputAction Resolve(InputActionReference reference, InputAction fallback)
        {
            return reference != null && reference.action != null ? reference.action : fallback;
        }

        /// <summary>
        /// 모든 입력 액션에 디바이스 추적 이벤트를 연결하고 비활성 액션을 활성화한다.
        /// </summary>
        private void EnableActions()
        {
            ForEachAction(action =>
            {
                if (trackedActions.Add(action))
                {
                    action.performed += OnActionPerformed;
                }

                if (!action.enabled)
                {
                    action.Enable();
                    enabledActions.Add(action);
                }
            });
        }

        /// <summary>
        /// 등록한 디바이스 추적 이벤트와 이 컴포넌트가 활성화한 액션을 해제한다.
        /// </summary>
        private void DisableActions()
        {
            foreach (InputAction action in trackedActions)
            {
                action.performed -= OnActionPerformed;
            }

            trackedActions.Clear();
            foreach (InputAction action in enabledActions)
            {
                action.Disable();
            }

            enabledActions.Clear();
        }

        /// <summary>
        /// 액션을 수행한 실제 입력 장치를 최근 사용 디바이스로 기록한다.
        /// </summary>
        private void OnActionPerformed(InputAction.CallbackContext context)
        {
            inputDeviceTracker.Notify(context.control?.device);
        }

        /// <summary>
        /// 구성된 모든 입력 액션을 지정한 방문 함수에 순서대로 전달한다.
        /// </summary>
        private void ForEachAction(Action<InputAction> visitor)
        {
            Visit(moveAction, visitor);
            Visit(lookAction, visitor);
            Visit(jumpAction, visitor);
            Visit(useAbilityAction, visitor);
            Visit(cancelAction, visitor);
            Visit(actionXAction, visitor);
            Visit(actionYAction, visitor);
            Visit(rotateAction, visitor);
            Visit(previousAbilityAction, visitor);
            Visit(nextAbilityAction, visitor);
            Visit(selectAttachAction, visitor);
            Visit(selectRewindAction, visitor);
            Visit(dpadAction, visitor);
            Visit(leftTriggerAction, visitor);
        }

        /// <summary>
        /// 입력 액션이 존재할 때만 지정한 방문 함수를 호출한다.
        /// </summary>
        private static void Visit(InputAction action, Action<InputAction> visitor)
        {
            if (action != null)
            {
                visitor(action);
            }
        }

        /// <summary>
        /// 모든 단발 입력 큐와 D-pad 이전 상태를 초기화한다.
        /// </summary>
        private void ClearQueuedInput()
        {
            jumpQueued = false;
            useAbilityQueued = false;
            cancelQueued = false;
            actionXQueued = false;
            actionYQueued = false;
            previousAbilityQueued = false;
            nextAbilityQueued = false;
            selectAttachQueued = false;
            selectRewindQueued = false;
            previousDpad = Vector2.zero;
            LookIsPointerDelta = false;
        }

        /// <summary>
        /// 지정한 단발 입력 플래그의 현재 값을 반환하고 즉시 초기화한다.
        /// </summary>
        private static bool Consume(ref bool queued)
        {
            bool value = queued;
            queued = false;
            return value;
        }
    }
}
