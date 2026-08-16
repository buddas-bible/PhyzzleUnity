using UnityEngine;
using Phyzzle.Abilities;

namespace Phyzzle.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor))]
    public sealed class PhyzzlePlayer : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerCameraRig cameraRig;
        [SerializeField] private PlayerAnimationDriver animationDriver;
        [SerializeField] private PlayerAbilityController abilityController;

        private int stopRequestCount;

        public bool IsStopped => stopRequestCount > 0;
        public PlayerInputReader Input => input;
        public PlayerMotor Motor => motor;
        public PlayerCameraRig CameraRig => cameraRig;
        public PlayerAbilityController Abilities => abilityController;

        private void Reset()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
        }

        private void Awake()
        {
            input ??= GetComponent<PlayerInputReader>();
            motor ??= GetComponent<PlayerMotor>();
        }

        private void Update()
        {
            input?.Sample();
            if (!IsStopped)
            {
                abilityController?.TickUpdate(input);
            }
        }

        private void FixedUpdate()
        {
            if (input == null || motor == null)
            {
                return;
            }

            motor.TickFixed(
                IsStopped ? Vector2.zero : input.Move,
                !IsStopped && (abilityController == null || !abilityController.BlocksJump) && input.ConsumeJump(),
                !IsStopped);
            if (!IsStopped)
            {
                abilityController?.TickFixed();
            }
        }

        private void LateUpdate()
        {
            bool allowCameraInput = !IsStopped &&
                                    (abilityController == null || abilityController.AllowDefaultCameraInput);
            cameraRig?.TickLate(
                allowCameraInput && input != null ? input.Look : Vector2.zero,
                allowCameraInput,
                input != null && input.LookIsPointerDelta);

            if (!IsStopped && motor != null)
            {
                animationDriver?.Tick(motor.IsGrounded, motor.MoveInputMagnitude);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (Application.isPlaying)
            {
                SetGameplayCursor(hasFocus);
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                SetGameplayCursor(Application.isFocused);
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SetGameplayCursor(false);
            }
        }

        public void Configure(
            PlayerInputReader inputReader,
            PlayerMotor playerMotor,
            PlayerCameraRig playerCameraRig,
            PlayerAnimationDriver playerAnimationDriver,
            PlayerAbilityController playerAbilityController = null)
        {
            input = inputReader;
            motor = playerMotor;
            cameraRig = playerCameraRig;
            animationDriver = playerAnimationDriver;
            abilityController = playerAbilityController;
        }

        public void SetStopped(bool stopped)
        {
            if (stopped)
            {
                stopRequestCount++;
                return;
            }

            stopRequestCount = Mathf.Max(0, stopRequestCount - 1);
        }

        private static void SetGameplayCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
