using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    [DisallowMultipleComponent]
    public sealed class RewindAbilityController : MonoBehaviour
    {
        public enum AbilityState
        {
            Default,
            Selecting,
            Rewinding
        }

        [SerializeField] private PlayerCameraRig cameraRig;
        [SerializeField] private RewindTargeting targeting;
        [SerializeField] private RewindCoordinator coordinator;
        [SerializeField] private RewindSettings settings;

        private bool ownsWorldPause;
        private float previousTimeScale = 1f;

        public AbilityState State { get; private set; }
        public bool IsDefault => State == AbilityState.Default;
        public bool BlocksJump => State == AbilityState.Selecting;
        public bool AllowDefaultCameraInput => true;
        public RewindRecorder CurrentTarget => targeting != null ? targeting.CurrentTarget : null;

        public void Configure(
            PlayerCameraRig playerCameraRig,
            RewindTargeting rewindTargeting,
            RewindCoordinator rewindCoordinator,
            RewindSettings rewindSettings)
        {
            cameraRig = playerCameraRig;
            targeting = rewindTargeting;
            coordinator = rewindCoordinator;
            settings = rewindSettings;
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
                case AbilityState.Selecting:
                    targeting?.Refresh();
                    if (input.ConsumeCancel())
                    {
                        TryStartRewind();
                    }
                    else if (input.ConsumeJump() || input.ConsumeActionX() ||
                             input.ConsumeActionY() || input.ConsumeUseAbility())
                    {
                        ReturnToDefault();
                    }
                    break;

                case AbilityState.Rewinding:
                    bool cancelRequested = input.ConsumeUseAbility();
                    if (coordinator == null || !coordinator.IsRewindingAny)
                    {
                        FinishRewindingState();
                    }
                    else if (cancelRequested)
                    {
                        coordinator.EndRewind();
                        FinishRewindingState();
                    }
                    break;
            }
        }

        public void EnterSelecting()
        {
            coordinator?.EndRewind();
            State = AbilityState.Selecting;
            PauseWorld();
            cameraRig?.SetAbilityCamera(true);
            targeting?.Refresh();
        }

        public bool TryStartRewind()
        {
            if (State != AbilityState.Selecting)
            {
                return false;
            }

            RewindRecorder target = targeting != null ? targeting.CurrentTarget : null;
            if (target == null || coordinator == null || !coordinator.StartRewind(target))
            {
                return false;
            }

            targeting?.Clear();
            cameraRig?.SetAbilityCamera(false);
            ResumeWorld();
            State = AbilityState.Rewinding;
            return true;
        }

        public void ReturnToDefault()
        {
            if (coordinator != null && coordinator.IsRewindingAny)
            {
                coordinator.EndRewind();
            }

            FinishRewindingState();
        }

        private void FinishRewindingState()
        {
            targeting?.Clear();
            cameraRig?.SetAbilityCamera(false);
            ResumeWorld();
            State = AbilityState.Default;
        }

        private void PauseWorld()
        {
            if (ownsWorldPause || settings == null || !settings.pauseWorldWhileTargeting)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ownsWorldPause = true;
        }

        private void ResumeWorld()
        {
            if (!ownsWorldPause)
            {
                return;
            }

            Time.timeScale = previousTimeScale;
            ownsWorldPause = false;
        }
    }
}
