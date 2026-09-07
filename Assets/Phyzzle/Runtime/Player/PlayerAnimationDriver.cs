using UnityEngine;

namespace Phyzzle.Player
{
    /// <summary>
    /// 플레이어의 접지 상태와 이동 입력 크기를 Animator의 이동 상태 재생으로 변환한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        private enum LocomotionState
        {
            Idle,
            Walk,
            Run,
            Jumping
        }

        [SerializeField] private Animator animator;
        [SerializeField] private string idleState = "Idle|Idle";
        [SerializeField] private string walkState = "Idle|Walk";
        [SerializeField] private string runState = "Idle|Run";
        [SerializeField] private string jumpingState = "Idle|Jump_Loop";
        [SerializeField, Range(0f, 1f)] private float runThreshold = 0.75f;

        private LocomotionState currentState;
        private bool hasState;

        /// <summary>
        /// 이동 애니메이션을 재생할 Animator 참조를 구성한다.
        /// </summary>
        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
        }

        /// <summary>
        /// 접지 여부와 이동 입력으로 이동 상태를 결정하고 필요한 애니메이션을 갱신한다.
        /// </summary>
        public void Tick(bool grounded, float moveInputMagnitude)
        {
            LocomotionState nextState = !grounded
                ? LocomotionState.Jumping
                : moveInputMagnitude <= 0.000001f
                    ? LocomotionState.Idle
                    : moveInputMagnitude > runThreshold
                        ? LocomotionState.Run
                        : LocomotionState.Walk;

            if (!hasState || nextState != currentState)
            {
                currentState = nextState;
                hasState = true;
                PlayState(nextState);
            }

            if (animator != null && (nextState == LocomotionState.Walk || nextState == LocomotionState.Run))
            {
                animator.speed = Mathf.Max(0.01f, moveInputMagnitude);
            }
        }

        /// <summary>
        /// 이동 상태에 대응하는 Animator 상태가 존재하면 해당 애니메이션을 재생한다.
        /// </summary>
        private void PlayState(LocomotionState state)
        {
            if (animator == null)
            {
                return;
            }

            animator.speed = 1f;
            string stateName = state switch
            {
                LocomotionState.Walk => walkState,
                LocomotionState.Run => runState,
                LocomotionState.Jumping => jumpingState,
                _ => idleState
            };

            int stateHash = Animator.StringToHash(stateName);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash, 0);
            }
        }
    }
}
