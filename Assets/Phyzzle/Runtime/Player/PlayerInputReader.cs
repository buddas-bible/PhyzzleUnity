using System;
using System.Collections.Generic;
using Phyzzle.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phyzzle.Player
{
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

        private void OnEnable()
        {
            BuildActions();
            EnableActions();
        }

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

        private void OnDestroy()
        {
            fallbackMap?.Dispose();
        }

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

        public bool ConsumeJump() => Consume(ref jumpQueued);
        public bool ConsumeUseAbility() => Consume(ref useAbilityQueued);
        public bool ConsumeCancel() => Consume(ref cancelQueued);
        public bool ConsumeActionX() => Consume(ref actionXQueued);
        public bool ConsumeActionY() => Consume(ref actionYQueued);
        public bool ConsumePreviousAbility() => Consume(ref previousAbilityQueued);
        public bool ConsumeNextAbility() => Consume(ref nextAbilityQueued);
        public bool ConsumeSelectAttach() => Consume(ref selectAttachQueued);
        public bool ConsumeSelectRewind() => Consume(ref selectRewindQueued);

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

        private InputAction CreateLookAction()
        {
            InputAction action = fallbackMap.AddAction("Look", InputActionType.Value);
            action.expectedControlType = "Vector2";
            action.AddBinding("<Gamepad>/rightStick").WithProcessor("stickDeadzone(min=0.265)");
            action.AddBinding("<Mouse>/delta");
            return action;
        }

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

        private InputAction CreateButton(string name, params string[] bindingPaths)
        {
            InputAction action = fallbackMap.AddAction(name, InputActionType.Button);
            foreach (string path in bindingPaths)
            {
                action.AddBinding(path);
            }

            return action;
        }

        private InputAction CreateValue(string name, string controlType, string bindingPath)
        {
            InputAction action = fallbackMap.AddAction(name, InputActionType.Value);
            action.expectedControlType = controlType;
            action.AddBinding(bindingPath);
            return action;
        }

        private static InputAction Resolve(InputActionReference reference, InputAction fallback)
        {
            return reference != null && reference.action != null ? reference.action : fallback;
        }

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

        private void OnActionPerformed(InputAction.CallbackContext context)
        {
            inputDeviceTracker.Notify(context.control?.device);
        }

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

        private static void Visit(InputAction action, Action<InputAction> visitor)
        {
            if (action != null)
            {
                visitor(action);
            }
        }

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

        private static bool Consume(ref bool queued)
        {
            bool value = queued;
            queued = false;
            return value;
        }
    }
}
