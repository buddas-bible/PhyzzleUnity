using UnityEngine;

namespace Phyzzle.Player
{
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

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator;
        }

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
