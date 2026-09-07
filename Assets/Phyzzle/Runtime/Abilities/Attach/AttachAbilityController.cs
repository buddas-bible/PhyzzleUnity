using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 부착 능력의 선택, 들기, 해제 상태를 전환하고 관련 시스템을 조율한다.
    /// </summary>
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

        /// <summary>
        /// 부착 능력에서 사용할 플레이어와 하위 시스템 참조를 설정한다.
        /// </summary>
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

        /// <summary>
        /// 컴포넌트가 비활성화될 때 부착 능력을 기본 상태로 되돌린다.
        /// </summary>
        private void OnDisable()
        {
            ReturnToDefault();
        }

        /// <summary>
        /// 현재 능력 상태에 맞춰 입력을 소비하고 상태 전환을 처리한다.
        /// </summary>
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

        /// <summary>
        /// 들고 있는 오브젝트의 물리 갱신을 고정 프레임에 실행한다.
        /// </summary>
        public void TickFixed()
        {
            if (State == AbilityState.Holding)
            {
                holdController?.TickFixed(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// 기본 상태에서 대상 선택 상태로 진입하고 선택용 카메라를 활성화한다.
        /// </summary>
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

        /// <summary>
        /// 현재 선택된 대상을 들기 시작하고 플레이어 이동과 카메라 상태를 조정한다.
        /// </summary>
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

        /// <summary>
        /// 부착 관련 임시 상태를 정리하고 능력을 기본 상태로 복원한다.
        /// </summary>
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
