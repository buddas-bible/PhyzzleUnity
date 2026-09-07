using UnityEngine;
using Phyzzle.Abilities;

namespace Phyzzle.Player
{
    /// <summary>
    /// 플레이어 입력, 이동, 카메라, 애니메이션과 능력 시스템의 프레임 갱신을 조율한다.
    /// </summary>
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

        /// <summary>
        /// 인스펙터 초기화 시 입력과 이동 컴포넌트를 자동으로 연결한다.
        /// </summary>
        private void Reset()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
        }

        /// <summary>
        /// 런타임 시작 시 입력과 이동 참조가 비어 있으면 자동으로 찾는다.
        /// </summary>
        private void Awake()
        {
            input ??= GetComponent<PlayerInputReader>();
            motor ??= GetComponent<PlayerMotor>();
        }

        /// <summary>
        /// 매 프레임 입력을 샘플링하고 정지 상태가 아니면 능력 입력을 갱신한다.
        /// </summary>
        private void Update()
        {
            input?.Sample();
            if (!IsStopped)
            {
                abilityController?.TickUpdate(input);
            }
        }

        /// <summary>
        /// 고정 프레임마다 플레이어 이동과 활성 능력의 물리 처리를 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 이동과 능력 갱신 이후 카메라와 애니메이션 표시 상태를 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 애플리케이션 포커스 상태에 맞춰 게임 플레이 커서 잠금 상태를 갱신한다.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (Application.isPlaying)
            {
                SetGameplayCursor(hasFocus);
            }
        }

        /// <summary>
        /// 플레이어가 활성화될 때 현재 포커스 상태에 맞춰 커서를 설정한다.
        /// </summary>
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                SetGameplayCursor(Application.isFocused);
            }
        }

        /// <summary>
        /// 플레이어가 비활성화될 때 커서 잠금을 해제해 다시 보이도록 한다.
        /// </summary>
        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SetGameplayCursor(false);
            }
        }

        /// <summary>
        /// 플레이어 프레임 조율에 사용할 입력, 이동, 카메라, 애니메이션과 능력 참조를 구성한다.
        /// </summary>
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

        /// <summary>
        /// 중첩 가능한 정지 요청 수를 증감해 플레이어 제어 정지 상태를 관리한다.
        /// </summary>
        public void SetStopped(bool stopped)
        {
            if (stopped)
            {
                stopRequestCount++;
                return;
            }

            stopRequestCount = Mathf.Max(0, stopRequestCount - 1);
        }

        /// <summary>
        /// 게임 플레이 여부에 따라 커서 잠금 모드와 표시 여부를 설정한다.
        /// </summary>
        private static void SetGameplayCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
