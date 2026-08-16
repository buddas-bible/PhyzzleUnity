using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    [DisallowMultipleComponent]
    public sealed class AttachAbilityController : MonoBehaviour
    {
        public enum AbilityState
        {
            Default,
            Selecting,
            Holding
        }

        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private PlayerCameraRig cameraRig;
        [SerializeField] private AttachTargeting targeting;
        [SerializeField] private AttachHoldController holdController;
        [SerializeField] private AttachSettings settings;

        public AbilityState State { get; private set; }
        public bool IsDefault => State == AbilityState.Default;
        public bool BlocksJump => State != AbilityState.Default;
        public bool AllowDefaultCameraInput => State != AbilityState.Holding;
        public AttachableObject CurrentTarget => targeting != null ? targeting.CurrentTarget : null;

        public void Configure(
            PlayerMotor motor,
            PlayerCameraRig playerCameraRig,
            AttachTargeting attachTargeting,
            AttachHoldController attachHoldController,
            AttachSettings attachSettings)
        {
            playerMotor = motor;
            cameraRig = playerCameraRig;
            targeting = attachTargeting;
            holdController = attachHoldController;
            settings = attachSettings;
        }

        private void OnDisable()
        {
            ReturnToDefault();
        }

        public void TickUpdate(PlayerInputReader input)
        {
            if (input == null)
            {
                return;
            }

            switch (State)
            {
                case AbilityState.Default:
                    input.ConsumeCancel();
                    input.ConsumeActionX();
                    input.ConsumeActionY();
                    input.ConsumePreviousAbility();
                    input.ConsumeNextAbility();
                    if (input.ConsumeUseAbility())
                    {
                        EnterSelecting();
                    }
                    break;

                case AbilityState.Selecting:
                    targeting?.Refresh();
                    if (input.ConsumeCancel())
                    {
                        TryBeginHolding();
                    }
                    else if (input.ConsumeJump() || input.ConsumeActionX() ||
                             input.ConsumeActionY() || input.ConsumeUseAbility())
                    {
                        ReturnToDefault();
                    }
                    break;

                case AbilityState.Holding:
                    holdController?.TickUpdate(input, Time.deltaTime);
                    if (input.ConsumeCancel())
                    {
                        if (holdController != null && holdController.TryAttach())
                        {
                            ReturnToDefault();
                        }
                    }
                    else if (input.ConsumeActionY())
                    {
                        holdController?.DetachHeldObject();
                    }
                    else if (input.ConsumeJump() || input.ConsumeActionX() || input.ConsumeUseAbility())
                    {
                        ReturnToDefault();
                    }
                    break;
            }
        }

        public void TickFixed()
        {
            if (State == AbilityState.Holding)
            {
                holdController?.TickFixed(Time.fixedDeltaTime);
            }
        }

        public void EnterSelecting()
        {
            if (State != AbilityState.Default)
            {
                ReturnToDefault();
            }

            State = AbilityState.Selecting;
            cameraRig?.SetAbilityCamera(true);
            targeting?.Refresh();
        }

        public bool TryBeginHolding()
        {
            AttachableObject target = targeting != null ? targeting.CurrentTarget : null;
            if (target == null || holdController == null || !holdController.Begin(target))
            {
                return false;
            }

            State = AbilityState.Holding;
            cameraRig?.EnterHoldingCamera(target.Body.transform);
            playerMotor?.SetMovementFacingEnabled(false);
            if (settings != null)
            {
                playerMotor?.SetSpeedOverride(settings.holdMoveSpeed);
            }

            return true;
        }

        public void ReturnToDefault()
        {
            holdController?.Release();
            targeting?.Clear();
            playerMotor?.ClearSpeedOverride();
            playerMotor?.SetMovementFacingEnabled(true);
            cameraRig?.ExitHoldingCamera();
            cameraRig?.SetAbilityCamera(false);
            State = AbilityState.Default;
        }
    }
}
