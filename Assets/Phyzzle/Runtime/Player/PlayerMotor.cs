using UnityEngine;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Compatibility;

namespace Phyzzle.Player
{
    /// <summary>
    /// 플레이어의 지상·공중 이동, 점프, 충돌 반응과 이동 방향 회전을 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private const float DirectionEpsilon = 0.000001f;

        [SerializeField] private Rigidbody body;
        [SerializeField] private PlayerGroundSensor groundSensor;
        [SerializeField] private Transform movementReference;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private PlayerMovementSettings settings;

        private Vector3 lastGroundNormal = Vector3.up;
        private Vector3 platformVelocity;
        private Vector3 pendingFlyingVelocity;
        private float flyingTimeRemaining;
        private float speedOverride = -1f;
        private bool movementFacingEnabled = true;

        public bool IsGrounded { get; private set; }
        public bool IsOnStandableSlope { get; private set; }
        public bool IsFlying => flyingTimeRemaining > 0f;
        public bool IsMoving { get; private set; }
        public float MoveInputMagnitude { get; private set; }
        public Vector3 LastGroundNormal => lastGroundNormal;
        public Rigidbody Body => body;
        public bool MovementFacingEnabled => movementFacingEnabled;

        /// <summary>
        /// 인스펙터 초기화 시 동일 오브젝트의 Rigidbody 참조를 자동으로 연결한다.
        /// </summary>
        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 런타임 시작 시 Rigidbody 참조가 비어 있으면 자동으로 찾는다.
        /// </summary>
        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 플레이어 이동에 필요한 물리, 지면 감지, 기준 Transform과 설정을 구성한다.
        /// </summary>
        public void Configure(
            Rigidbody rigidbody,
            PlayerGroundSensor sensor,
            Transform cameraMovementReference,
            Transform playerModelRoot,
            PlayerMovementSettings movementSettings)
        {
            body = rigidbody;
            groundSensor = sensor;
            movementReference = cameraMovementReference;
            modelRoot = playerModelRoot;
            settings = movementSettings;
        }

        /// <summary>
        /// 능력 등 외부 시스템이 사용할 임시 이동 속도를 설정한다.
        /// </summary>
        public void SetSpeedOverride(float speed)
        {
            speedOverride = speed;
        }

        /// <summary>
        /// 임시 이동 속도를 해제하고 기본 설정 속도를 사용하도록 복원한다.
        /// </summary>
        public void ClearSpeedOverride()
        {
            speedOverride = -1f;
        }

        /// <summary>
        /// 이동 방향을 바라보도록 모델을 회전시키는 기능의 사용 여부를 설정한다.
        /// </summary>
        public void SetMovementFacingEnabled(bool enabled)
        {
            movementFacingEnabled = enabled;
        }

        /// <summary>
        /// 고정 프레임마다 지면 상태, 점프, 지상·공중 이동을 갱신한다.
        /// </summary>
        public void TickFixed(Vector2 moveInput, bool jumpPressed, bool allowControl)
        {
            // 이동 계산에 필요한 Rigidbody, 기준 Transform, 설정이 없으면 해당 물리 프레임을 처리하지 않음
            if (!IsConfigured())
            {
                return;
            }

            UpdateGroundState();
            ApplyPendingImpulse();
            UpdateFlyingTimer();

            Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
            MoveInputMagnitude = allowControl ? clampedInput.magnitude : 0f;

            if (allowControl && jumpPressed)
            {
                TryJump();
            }

            if (allowControl)
            {
                ApplyMovement(clampedInput);
            }
            else
            {
                // 능력이 이동 제어를 막는 동안에도 플랫폼 속도나 과도한 수직 속도가 다음 프레임에 남지 않도록 정리
                IsMoving = false;
                platformVelocity = Vector3.zero;
                ClampVerticalVelocity();
            }
        }

        /// <summary>
        /// 이동 계산에 필요한 핵심 참조와 설정이 준비됐는지 확인한다.
        /// </summary>
        private bool IsConfigured()
        {
            return body != null && movementReference != null && settings != null;
        }

        /// <summary>
        /// 센서와 레이캐스트를 이용해 접지 여부, 지면 법선과 허용 경사를 갱신한다.
        /// </summary>
        private void UpdateGroundState()
        {
            IsGrounded = groundSensor != null && groundSensor.IsGrounded;
            IsOnStandableSlope = false;

            // 트리거 센서가 지면과 접촉하지 않았다면 추가 레이캐스트를 하지 않음
            if (!IsGrounded)
            {
                return;
            }

            Vector3 origin = body.position + Vector3.up * settings.groundProbeRadius;
            bool hitGround = UnityEngine.Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                settings.groundProbeDistance,
                settings.groundMask,
                QueryTriggerInteraction.Ignore);

            // 센서 접촉은 있지만 발 아래에서 실제 지면을 찾지 못하면 경사 이동으로 판단하지 않음
            if (!hitGround)
            {
                return;
            }

            lastGroundNormal = hit.normal;
            float slope = Vector3.Angle(Vector3.up, hit.normal);
            IsOnStandableSlope = slope <= settings.slopeLimitDegrees;
        }

        /// <summary>
        /// 접지 및 경사 조건이 유효할 때 점프 가속도를 적용한다.
        /// </summary>
        private bool TryJump()
        {
            // 공중이거나 서 있을 수 없는 급경사에서는 점프를 허용하지 않음
            if (!IsGrounded || !IsOnStandableSlope)
            {
                return false;
            }

            body.AddForce(
                Vector3.up * settings.jumpAcceleration,
                LegacyForceModeMap.ToUnity(LegacyForceType.Accelration));
            return true;
        }

        /// <summary>
        /// 현재 접지 상태에 따라 지상 또는 공중 이동을 적용하고 이동 방향을 갱신한다.
        /// </summary>
        private void ApplyMovement(Vector2 input)
        {
            Vector3 currentVelocity = PlayerMovementMath.ClampVerticalSpeed(
                body.linearVelocity,
                settings.maxVerticalSpeed);
            body.linearVelocity = currentVelocity;

            if (IsGrounded && !IsFlying)
            {
                ApplyGroundMovement(input, currentVelocity);
            }
            else
            {
                ApplyAirMovement(input, currentVelocity);
            }

            // 플랫폼에서 받은 속도는 이번 이동 계산에 한 번만 더하고 다음 프레임에 다시 충돌 정보로 갱신
            platformVelocity = Vector3.zero;
            IsMoving = input.magnitude >= 0.000001f;

            if (IsMoving && movementFacingEnabled)
            {
                FaceMovementDirection(body.linearVelocity);
            }
        }

        /// <summary>
        /// 경사면을 고려한 목표 속도를 계산해 지상 이동에 적용한다.
        /// </summary>
        private void ApplyGroundMovement(Vector2 input, Vector3 currentVelocity)
        {
            // 카메라 기준 원래 입력 방향과 실제 경사면을 따라갈 방향을 각각 계산
            Vector3 originDirection = PlayerMovementMath.CameraRelativeDirection(
                input,
                movementReference.forward);
            Vector3 direction = IsOnStandableSlope
                ? PlayerMovementMath.ProjectDirectionOnSlope(
                    input,
                    movementReference.forward,
                    lastGroundNormal)
                : originDirection;
            Vector3 targetVelocity = CalculateTargetVelocity(
                input,
                originDirection,
                direction);

            // 목표 수평 속도 - 현재 수평 속도만큼 VelocityChange를 줘 한 스텝에 목표 이동 속도로 맞춤
            Vector3 horizontalVelocity = currentVelocity;
            horizontalVelocity.y = 0f;
            Vector3 additionalVelocity = targetVelocity - horizontalVelocity;
            additionalVelocity.y = 0f;

            body.AddForce(
                additionalVelocity + platformVelocity,
                ForceMode.VelocityChange);
        }

        /// <summary>
        /// 공중 입력 방향과 가속 제한을 적용해 공중 제어를 처리한다.
        /// </summary>
        private void ApplyAirMovement(Vector2 input, Vector3 currentVelocity)
        {
            Vector3 wishDirection = PlayerMovementMath.CameraRelativeDirection(
                input,
                movementReference.forward);

            if (IsFlying)
            {
                // 강한 충격으로 비행 중일 때는 일반 공중 가속이 아니라 기존 방식의 힘 입력을 사용
                Vector3 targetVelocity = CalculateTargetVelocity(
                    input,
                    wishDirection,
                    wishDirection);
                body.AddForce(targetVelocity, ForceMode.Force);
                return;
            }

            // 입력 방향이 없으면 정규화 및 Dot 계산을 할 필요가 없으므로 공중 제어를 종료
            if (input.sqrMagnitude <= DirectionEpsilon ||
                wishDirection.sqrMagnitude <= DirectionEpsilon)
            {
                return;
            }

            wishDirection.Normalize();

            Vector3 horizontalVelocity = currentVelocity;
            horizontalVelocity.y = 0f;

            float wishSpeed = settings.airWishSpeed * input.magnitude;
            // 현재 속도를 입력 방향에 투영해서 이미 그 방향으로 확보한 속도를 구함
            float speedAlongWishDirection = Vector3.Dot(horizontalVelocity, wishDirection);
            float speedRoom = wishSpeed - speedAlongWishDirection;
            // 원하는 방향 속도가 이미 wishSpeed 이상이면 추가 가속하지 않아 공중 속도가 계속 증가하는 것을 방지
            if (speedRoom <= 0f)
            {
                return;
            }

            // 이번 물리 프레임에서 늘릴 속도는 남은 속도 여유와 acceleration * dt 중 작은 값만 허용
            float deltaSpeed = Mathf.Min(
                speedRoom,
                settings.airAcceleration * Time.fixedDeltaTime);

            body.AddForce(
                wishDirection * deltaSpeed,
                ForceMode.VelocityChange);
        }

        /// <summary>
        /// 입력 크기와 방향 정렬, 속도 설정을 이용해 목표 이동 속도를 계산한다.
        /// </summary>
        private Vector3 CalculateTargetVelocity(
            Vector2 input,
            Vector3 originDirection,
            Vector3 direction)
        {
            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                return Vector3.zero;
            }

            float movementSpeed = speedOverride >= 0f ? speedOverride : settings.moveSpeed;
            // 경사면 투영 후 방향과 원래 카메라 입력 방향의 Dot을 곱해 투영으로 늘어난 속도 성분을 보정
            float alignment = Vector3.Dot(direction, originDirection);
            return direction * (movementSpeed * input.magnitude * alignment);
        }

        /// <summary>
        /// 현재 수평 이동 속도 방향을 바라보도록 모델을 회전시킨다.
        /// </summary>
        private void FaceMovementDirection(Vector3 worldVelocity)
        {
            if (modelRoot == null)
            {
                return;
            }

            worldVelocity.y = 0f;
            // 정지에 가까운 속도로 LookRotation을 만들면 방향이 불안정해지므로 회전을 유지
            if (worldVelocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            modelRoot.rotation = Quaternion.LookRotation(worldVelocity.normalized, Vector3.up);
        }

        /// <summary>
        /// 리지드바디의 수직 속도를 설정된 최대값으로 제한한다.
        /// </summary>
        private void ClampVerticalVelocity()
        {
            body.linearVelocity = PlayerMovementMath.ClampVerticalSpeed(
                body.linearVelocity,
                settings.maxVerticalSpeed);
        }

        /// <summary>
        /// 충돌에서 예약된 비행 충격량을 플레이어에 적용하고 소모한다.
        /// </summary>
        private void ApplyPendingImpulse()
        {
            if (pendingFlyingVelocity.sqrMagnitude > 0f)
            {
                // 충돌 콜백에서 바로 힘을 주지 않고 다음 FixedUpdate에서 적용해 물리 갱신 순서를 일정하게 유지
                body.AddForce(
                    pendingFlyingVelocity,
                    LegacyForceModeMap.ToUnity(LegacyForceType.Accelration));
                pendingFlyingVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// 비행 상태의 남은 지속 시간을 고정 프레임 기준으로 감소시킨다.
        /// </summary>
        private void UpdateFlyingTimer()
        {
            if (flyingTimeRemaining > 0f)
            {
                flyingTimeRemaining = Mathf.Max(0f, flyingTimeRemaining - Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// 새 충돌이 시작되면 플랫폼 이동과 충격 반응 정보를 읽는다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            ReadCollision(collision);
        }

        /// <summary>
        /// 충돌이 유지되는 동안 플랫폼 이동과 충격 반응 정보를 계속 갱신한다.
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            ReadCollision(collision);
        }

        /// <summary>
        /// 충돌 상대의 플랫폼 속도와 충격량을 분석해 플레이어 이동 상태에 반영한다.
        /// </summary>
        private void ReadCollision(Collision collision)
        {
            if (collision == null || settings == null)
            {
                return;
            }

            GameObject other = collision.gameObject;
            Rigidbody otherBody = collision.rigidbody;

            // 일반 MovingPlatform뿐 아니라 되감기 때문에 움직이는 Rigidbody도 플랫폼 속도 전달 대상으로 취급
            RewindRecorder rewindPlatform = other.GetComponentInParent<RewindRecorder>();
            bool transfersPlatformMotion =
                other.GetComponentInParent<MovingPlatform>() != null ||
                rewindPlatform?.IsRewinding == true;

            if (transfersPlatformMotion && otherBody != null)
            {
                foreach (ContactPoint contact in collision.contacts)
                {
                    Vector3 candidateVelocity = otherBody.GetPointVelocity(contact.point);
                    Vector3 referencePosition = modelRoot != null ? modelRoot.position : transform.position;
                    // 발 아래쪽 접점만 플랫폼으로 보고, 여러 접점 중 가장 큰 속도를 사용해 중복 전달을 방지
                    if (contact.point.y < referencePosition.y + 0.5f &&
                        candidateVelocity.sqrMagnitude > platformVelocity.sqrMagnitude)
                    {
                        platformVelocity = candidateVelocity;
                    }
                }
            }

            // 충돌 상대가 명시적인 충격 소스가 아니면 일반 지형 충돌이므로 비행 상태로 전환하지 않음
            if (other.GetComponentInParent<PlayerImpulseSource>() == null)
            {
                return;
            }

            Vector3 impulse = collision.impulse;
            // 임계값을 넘는 충격 중 현재 예약된 값보다 강한 충격만 다음 FixedUpdate에 적용
            if (impulse.magnitude >= settings.impactThreshold &&
                impulse.sqrMagnitude > pendingFlyingVelocity.sqrMagnitude)
            {
                pendingFlyingVelocity = impulse;
                flyingTimeRemaining = settings.flyingDuration;
            }
        }
    }
}
