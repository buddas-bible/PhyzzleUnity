using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 부착 대상을 들고 있는 동안 입력, 회전, 위치 제한과 물리 추종을 조율한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachHoldController : MonoBehaviour
    {
        [SerializeField] private Transform playerModel;
        [SerializeField] private Rigidbody playerBody;
        [SerializeField] private AttachmentService attachmentService;
        [SerializeField] private AttachSettings settings;

        private readonly AttachHoldSession session = new();
        private readonly AttachHoldPhysics holdPhysics = new();

        public bool IsHolding => session.IsHolding;
        public AttachableObject HeldObject => session.HeldObject;

        /// <summary>
        /// 들기 처리에 필요한 플레이어, 부착 서비스와 설정 참조를 구성한다.
        /// </summary>
        public void Configure(
            Transform model,
            Rigidbody playerRigidbody,
            AttachmentService service,
            AttachSettings attachSettings)
        {
            playerModel = model;
            playerBody = playerRigidbody;
            attachmentService = service;
            settings = attachSettings;
        }

        /// <summary>
        /// 지정된 대상을 들기 세션으로 전환하고 초기 목표 위치를 제한 범위에 맞춘다.
        /// </summary>
        public bool Begin(AttachableObject target)
        {
            if (target == null || target.Body == null || target.Body.isKinematic ||
                playerModel == null || attachmentService == null || settings == null)
            {
                return false;
            }

            if (!session.Begin(target, playerModel, attachmentService))
            {
                return false;
            }

            ClampTargetPosition();
            return true;
        }

        /// <summary>
        /// 플레이어 입력으로 들고 있는 대상의 이동과 회전 목표를 갱신한다.
        /// </summary>
        public void TickUpdate(Player.PlayerInputReader input, float deltaTime)
        {
            if (!IsHolding || session.HeldObject.Body == null || input == null || settings == null)
            {
                return;
            }

            bool rotateMode = input.RotateHeld;
            float fineAdjustment = input.LeftTrigger;

            if (!rotateMode)
            {
                OrbitAndLift(input.Look, input.LookIsPointerDelta ? 1f : deltaTime);
                if (fineAdjustment <= 0f && Mathf.Abs(input.DpadPressed.y) > 0.5f)
                {
                    session.TargetLocalPosition += new Vector3(
                        0f, 0f, input.DpadPressed.y * settings.targetDepthStep);
                }
                else if (fineAdjustment > 0f && Mathf.Abs(input.Dpad.y) > 0.5f)
                {
                    session.TargetLocalPosition += new Vector3(
                        0f, 0f, input.Dpad.y * 5f * fineAdjustment * deltaTime);
                }
            }
            else if (fineAdjustment <= 0f)
            {
                StepRotation(input.DpadPressed);
            }
            else
            {
                AdjustRotation(input.Dpad, fineAdjustment, deltaTime);
            }

            ClampTargetPosition();
        }

        /// <summary>
        /// 현재 들기 세션의 목표 자세를 따라가도록 물리 힘을 적용한다.
        /// </summary>
        public void TickFixed(float fixedDeltaTime)
        {
            holdPhysics.Tick(session, playerModel, playerBody, settings, fixedDeltaTime);
        }

        /// <summary>
        /// 현재 들고 있는 대상을 주변 오브젝트에 부착하려고 시도한다.
        /// </summary>
        public bool TryAttach()
        {
            return IsHolding && attachmentService != null && attachmentService.TryAttach(session.HeldObject);
        }

        /// <summary>
        /// 현재 들고 있는 대상의 기존 부착 연결을 해제한다.
        /// </summary>
        public bool DetachHeldObject()
        {
            return IsHolding && attachmentService != null && attachmentService.Detach(session.HeldObject);
        }

        /// <summary>
        /// 현재 들기 세션을 종료하고 선택 상태를 정리한다.
        /// </summary>
        public void Release()
        {
            session.Release(attachmentService);
        }

        /// <summary>
        /// 방향 패드의 단발 입력에 맞춰 목표 회전을 단계적으로 변경한다.
        /// </summary>
        private void StepRotation(Vector2 dpad)
        {
            if (dpad.y > 0.5f)
            {
                session.StepRotation(AttachRotationDirection.Up, settings.rotationStepDegrees);
            }
            else if (dpad.y < -0.5f)
            {
                session.StepRotation(AttachRotationDirection.Down, settings.rotationStepDegrees);
            }

            if (dpad.x < -0.5f)
            {
                session.StepRotation(AttachRotationDirection.Left, settings.rotationStepDegrees);
            }
            else if (dpad.x > 0.5f)
            {
                session.StepRotation(AttachRotationDirection.Right, settings.rotationStepDegrees);
            }
        }

        /// <summary>
        /// 방향 패드와 트리거 입력으로 목표 회전을 연속적으로 미세 조정한다.
        /// </summary>
        private void AdjustRotation(Vector2 dpad, float trigger, float deltaTime)
        {
            float degrees = settings.fineAdjustmentSpeed * trigger * deltaTime;
            if (dpad.y > 0.5f)
            {
                session.AdjustRotation(Vector3.right, degrees);
            }
            else if (dpad.y < -0.5f)
            {
                session.AdjustRotation(Vector3.right, -degrees);
            }

            if (dpad.x < -0.5f)
            {
                session.AdjustRotation(Vector3.up, degrees);
            }
            else if (dpad.x > 0.5f)
            {
                session.AdjustRotation(Vector3.up, -degrees);
            }
        }

        /// <summary>
        /// 시점 입력에 따라 플레이어를 선회시키고 대상의 높이를 조정한다.
        /// </summary>
        private void OrbitAndLift(Vector2 look, float deltaTime)
        {
            float radius = Mathf.Max(settings.minTargetZ, session.TargetLocalPosition.z);
            float orbitSpeed = settings.orbitDegreesPerSecond / radius * settings.orbitArcRatio;
            Quaternion currentRotation = playerModel.rotation;
            Quaternion desiredRotation = Quaternion.AngleAxis(
                look.x * orbitSpeed * deltaTime,
                Vector3.up) * currentRotation;
            playerModel.rotation = AttachHoldConstraints.LimitOrbit(
                currentRotation,
                desiredRotation,
                playerModel.position,
                session.TargetLocalPosition,
                session.HeldObject.Body.position,
                settings.targetPositionOffset);

            Vector3 candidate = session.TargetLocalPosition;
            candidate.y += settings.targetVerticalSpeed * look.y * deltaTime;
            session.TargetLocalPosition = AttachHoldConstraints.LimitLift(
                candidate,
                playerModel.position,
                playerModel.rotation,
                session.HeldObject.Body.position,
                settings.targetPositionOffset);
        }

        /// <summary>
        /// 현재 부착 섬의 경계를 고려해 목표 위치를 허용 범위 안으로 보정한다.
        /// </summary>
        private void ClampTargetPosition()
        {
            bool hasBounds = AttachHoldConstraints.TryComputeIslandLocalBounds(
                session.HeldObject,
                attachmentService.GetIsland(session.HeldObject),
                playerModel,
                session.TargetLocalPosition,
                session.TargetLocalRotation,
                out Bounds bounds);

            session.TargetLocalPosition = AttachHoldConstraints.ClampTargetForBounds(
                session.TargetLocalPosition,
                hasBounds ? bounds.min.z : 0f,
                hasBounds,
                settings.minTargetY,
                settings.maxTargetY,
                settings.minTargetZ,
                settings.maxTargetZ);
        }
    }
}
