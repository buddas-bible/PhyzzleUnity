using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 되감기 능력의 대상 선택, 실행, 종료 상태를 전환하고 관련 시스템을 조율한다.
    /// </summary>
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

        /// <summary>
        /// 되감기 능력에서 사용할 카메라, 타게팅, 실행 조율자와 설정 참조를 구성한다.
        /// </summary>
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

        /// <summary>
        /// 컴포넌트가 비활성화될 때 진행 중인 되감기 상태를 정리한다.
        /// </summary>
        private void OnDisable()
        {
            ReturnToDefault();
        }

        /// <summary>
        /// 현재 능력 상태에 맞춰 입력과 되감기 완료·취소 조건을 처리한다.
        /// </summary>
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

        /// <summary>
        /// 대상 선택 상태로 진입하고 필요하면 월드를 일시 정지한다.
        /// </summary>
        public void EnterSelecting()
        {
            coordinator?.EndRewind();
            State = AbilityState.Selecting;
            PauseWorld();
            cameraRig?.SetAbilityCamera(true);
            targeting?.Refresh();
        }

        /// <summary>
        /// 현재 선택된 대상의 되감기를 시작하고 실행 상태로 전환한다.
        /// </summary>
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

        /// <summary>
        /// 진행 중인 되감기를 종료하고 능력을 기본 상태로 복원한다.
        /// </summary>
        public void ReturnToDefault()
        {
            if (coordinator != null && coordinator.IsRewindingAny)
            {
                coordinator.EndRewind();
            }

            FinishRewindingState();
        }

        /// <summary>
        /// 선택 표시와 카메라, 시간 배율을 복원하고 기본 상태로 전환한다.
        /// </summary>
        private void FinishRewindingState()
        {
            targeting?.Clear();
            cameraRig?.SetAbilityCamera(false);
            ResumeWorld();
            State = AbilityState.Default;
        }

        /// <summary>
        /// 설정이 허용할 때 대상 선택 중 월드 시간을 일시 정지한다.
        /// </summary>
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

        /// <summary>
        /// 이 능력이 일시 정지한 월드 시간 배율을 이전 값으로 복원한다.
        /// </summary>
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
