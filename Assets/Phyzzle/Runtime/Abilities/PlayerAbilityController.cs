using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.Abilities
{
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

        public void Configure(
            PlayerMotor motor,
            AttachAbilityController attachAbility,
            RewindAbilityController rewindAbility)
        {
            playerMotor = motor;
            attach = attachAbility;
            rewind = rewindAbility;
        }

        private void OnDisable()
        {
            attach?.ReturnToDefault();
            rewind?.ReturnToDefault();
        }

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

        public void TickFixed()
        {
            if (attach != null && !attach.IsDefault)
            {
                attach.TickFixed();
            }
        }

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
