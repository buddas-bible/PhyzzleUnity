using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.Abilities
{
    /// <summary>
    /// 플레이어가 사용할 부착·되감기 능력의 선택과 활성화, 상호 배제를 통합 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAbilityController : MonoBehaviour
    {
        public enum AbilityKind
        {
            Attach,
            Rewind
        }

        [SerializeField] private AttachAbilityController attach;
        [SerializeField] private RewindAbilityController rewind;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private AbilityKind selectedAbility = AbilityKind.Attach;

        private static readonly AbilityKind[] AbilityOrder =
        {
            AbilityKind.Attach,
            AbilityKind.Rewind
        };

        public AbilityKind SelectedAbility => selectedAbility;
        public bool IsUsingAbility => !IsIdle;
        public bool BlocksJump => attach?.BlocksJump == true || rewind?.BlocksJump == true;
        public bool AllowDefaultCameraInput =>
            (attach == null || attach.AllowDefaultCameraInput) &&
            (rewind == null || rewind.AllowDefaultCameraInput);

        private bool IsIdle =>
            (attach == null || attach.IsDefault) &&
            (rewind == null || rewind.IsDefault);

        /// <summary>
        /// 능력 제어에 필요한 플레이어 이동과 개별 능력 컨트롤러 참조를 구성한다.
        /// </summary>
        public void Configure(
            PlayerMotor motor,
            AttachAbilityController attachAbility,
            RewindAbilityController rewindAbility)
        {
            playerMotor = motor;
            attach = attachAbility;
            rewind = rewindAbility;
        }

        /// <summary>
        /// 컴포넌트가 비활성화될 때 모든 능력을 기본 상태로 되돌린다.
        /// </summary>
        private void OnDisable()
        {
            attach?.ReturnToDefault();
            rewind?.ReturnToDefault();
        }

        /// <summary>
        /// 능력 선택 입력을 처리하고 현재 활성 능력 또는 새 능력 사용 입력을 갱신한다.
        /// </summary>
        public void TickUpdate(PlayerInputReader input)
        {
            if (input == null)
            {
                return;
            }

            HandleAbilitySelection(input);

            if (attach != null && !attach.IsDefault)
            {
                attach.TickUpdate(input);
                return;
            }

            if (rewind != null && !rewind.IsDefault)
            {
                rewind.TickUpdate(input);
                return;
            }

            input.ConsumeCancel();
            input.ConsumeActionX();
            input.ConsumeActionY();
            if (input.ConsumeUseAbility())
            {
                ActivateSelectedAbility();
            }
        }

        /// <summary>
        /// 고정 프레임 갱신이 필요한 활성 능력의 물리 처리를 실행한다.
        /// </summary>
        public void TickFixed()
        {
            if (attach != null && !attach.IsDefault)
            {
                attach.TickFixed();
            }
        }

        /// <summary>
        /// 사용할 능력을 선택하고 필요하면 다른 능력의 선택 상태를 전환한다.
        /// </summary>
        public void Select(AbilityKind ability)
        {
            selectedAbility = ability;

            if (ability == AbilityKind.Attach && rewind != null && !rewind.IsDefault)
            {
                rewind.ReturnToDefault();
                attach?.EnterSelecting();
            }
            else if (ability == AbilityKind.Rewind && attach != null &&
                     attach.State == AttachAbilityController.AbilityState.Selecting)
            {
                attach.ReturnToDefault();
                rewind?.EnterSelecting();
            }
        }

        /// <summary>
        /// 플레이어가 접지된 유휴 상태라면 현재 선택된 능력의 선택 모드로 진입한다.
        /// </summary>
        public void ActivateSelectedAbility()
        {
            if (!IsIdle || (playerMotor != null && !playerMotor.IsGrounded))
            {
                return;
            }

            if (selectedAbility == AbilityKind.Attach)
            {
                attach?.EnterSelecting();
            }
            else
            {
                rewind?.EnterSelecting();
            }
        }

        /// <summary>
        /// 이전·다음 및 직접 선택 입력을 소비해 현재 선택 능력을 변경한다.
        /// </summary>
        private void HandleAbilitySelection(PlayerInputReader input)
        {
            bool previousRequested = input.ConsumePreviousAbility();
            bool nextRequested = input.ConsumeNextAbility();
            bool attachRequested = input.ConsumeSelectAttach();
            bool rewindRequested = input.ConsumeSelectRewind();

            if (attach != null && attach.State == AttachAbilityController.AbilityState.Holding)
            {
                return;
            }

            if (attachRequested != rewindRequested)
            {
                Select(attachRequested ? AbilityKind.Attach : AbilityKind.Rewind);
                return;
            }

            int offset = (nextRequested ? 1 : 0) - (previousRequested ? 1 : 0);
            if (offset == 0)
            {
                return;
            }

            int index = System.Array.IndexOf(AbilityOrder, selectedAbility);
            int nextIndex = (index + offset + AbilityOrder.Length) % AbilityOrder.Length;
            Select(AbilityOrder[nextIndex]);
        }
    }
}
