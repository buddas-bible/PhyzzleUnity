using UnityEngine;

namespace Phyzzle.Player
{
    /// <summary>
    /// 플레이어 카메라의 회전, 충돌 회피, 능력·들기 상태 자세와 모델 가시성을 제어한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform cameraArm;
        [SerializeField] private Transform cameraCore;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private PlayerCameraSettings settings;

        private Vector3 armDefaultLocalPosition;
        private Quaternion armDefaultLocalRotation;
        private Vector3 coreDefaultLocalPosition;
        private Quaternion coreDefaultLocalRotation;
        private Vector3 coreTargetLocalPosition;
        private Quaternion coreTargetLocalRotation;
        private Renderer[] modelRenderers = System.Array.Empty<Renderer>();
        private float pitch;
        private float previousCollisionDistance = float.PositiveInfinity;
        private Transform holdingTarget;
        private bool abilityCamera;
        private bool holdingCamera;
        private bool initialized;

        public Transform MovementReference => cameraArm;
        public float Pitch => pitch;

        /// <summary>
        /// 런타임 시작 시 카메라 기본 자세와 렌더러 참조를 초기화한다.
        /// </summary>
        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// 카메라 리그에 사용할 Transform과 설정을 구성하고 기본 자세를 다시 초기화한다.
        /// </summary>
        public void Configure(
            Transform arm,
            Transform core,
            Transform playerModelRoot,
            PlayerCameraSettings cameraSettings)
        {
            cameraArm = arm;
            cameraCore = core;
            modelRoot = playerModelRoot;
            settings = cameraSettings;
            initialized = false;
            Initialize();
        }

        /// <summary>
        /// 능력 선택 중 사용할 추가 카메라 오프셋의 활성 여부를 설정한다.
        /// </summary>
        public void SetAbilityCamera(bool enabled)
        {
            abilityCamera = enabled;
        }

        /// <summary>
        /// 현재 카메라 월드 자세를 유지한 채 들고 있는 대상을 추적하는 카메라 상태로 전환한다.
        /// </summary>
        public void EnterHoldingCamera(Transform heldTarget)
        {
            if (!Initialize() || heldTarget == null)
            {
                return;
            }

            Vector3 coreWorldPosition = cameraCore.position;
            Quaternion coreWorldRotation = cameraCore.rotation;
            cameraArm.localPosition = armDefaultLocalPosition;
            cameraArm.localRotation = modelRoot != null
                ? modelRoot.localRotation
                : armDefaultLocalRotation;
            pitch = 0f;
            cameraCore.SetPositionAndRotation(coreWorldPosition, coreWorldRotation);
            coreTargetLocalPosition = cameraCore.localPosition;
            coreTargetLocalRotation = cameraCore.localRotation;
            previousCollisionDistance = float.PositiveInfinity;
            holdingTarget = heldTarget;
            holdingCamera = true;
        }

        /// <summary>
        /// 들기 카메라 상태를 종료하고 기본 카메라 목표 자세로 복귀한다.
        /// </summary>
        public void ExitHoldingCamera()
        {
            if (!Initialize())
            {
                return;
            }

            holdingCamera = false;
            holdingTarget = null;
            coreTargetLocalPosition = coreDefaultLocalPosition;
            coreTargetLocalRotation = coreDefaultLocalRotation;
            previousCollisionDistance = float.PositiveInfinity;
        }

        /// <summary>
        /// 일반 축 입력으로 카메라의 LateUpdate 처리를 갱신한다.
        /// </summary>
        public void TickLate(Vector2 lookInput, bool allowInput)
            => TickLate(lookInput, allowInput, false);

        /// <summary>
        /// 시점 입력 또는 들기 상태를 반영하고 카메라 충돌과 모델 가시성을 갱신한다.
        /// </summary>
        public void TickLate(Vector2 lookInput, bool allowInput, bool lookIsPointerDelta)
        {
            if (!Initialize())
            {
                return;
            }

            float deltaTime = Time.timeScale <= Mathf.Epsilon
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            if (holdingCamera)
            {
                cameraArm.localPosition = armDefaultLocalPosition;
                if (modelRoot != null)
                {
                    cameraArm.localRotation = modelRoot.localRotation;
                }
            }
            else if (allowInput)
            {
                RotateArm(
                    lookIsPointerDelta ? lookInput : Vector2.ClampMagnitude(lookInput, 1f),
                    lookIsPointerDelta ? 1f : deltaTime);
            }

            UpdateCameraTarget(deltaTime);
            UpdateModelVisibility();
        }

        /// <summary>
        /// 카메라 리그의 회전, 위치와 들기 상태를 초기 기본값으로 되돌린다.
        /// </summary>
        public void ResetRig()
        {
            if (!Initialize())
            {
                return;
            }

            pitch = 0f;
            cameraArm.localPosition = armDefaultLocalPosition;
            cameraArm.localRotation = armDefaultLocalRotation;
            cameraCore.localPosition = coreDefaultLocalPosition;
            cameraCore.localRotation = coreDefaultLocalRotation;
            coreTargetLocalPosition = coreDefaultLocalPosition;
            coreTargetLocalRotation = coreDefaultLocalRotation;
            previousCollisionDistance = float.PositiveInfinity;
            holdingTarget = null;
            holdingCamera = false;
        }

        /// <summary>
        /// 카메라 기본 자세와 플레이어 모델 렌더러를 한 번만 캐시한다.
        /// </summary>
        private bool Initialize()
        {
            if (initialized)
            {
                return true;
            }

            if (cameraArm == null || cameraCore == null || settings == null)
            {
                return false;
            }

            armDefaultLocalPosition = cameraArm.localPosition;
            armDefaultLocalRotation = cameraArm.localRotation;
            coreDefaultLocalPosition = cameraCore.localPosition;
            coreDefaultLocalRotation = cameraCore.localRotation;
            coreTargetLocalPosition = coreDefaultLocalPosition;
            coreTargetLocalRotation = coreDefaultLocalRotation;
            modelRenderers = modelRoot != null
                ? modelRoot.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            initialized = true;
            return true;
        }

        /// <summary>
        /// 시점 입력으로 카메라 암의 수평 회전과 제한된 피치 회전을 적용한다.
        /// </summary>
        private void RotateArm(Vector2 lookInput, float deltaTime)
        {
            float yaw = lookInput.x * settings.sensitivity * deltaTime;
            float requestedPitch = -lookInput.y * settings.sensitivity * deltaTime;

            cameraArm.Rotate(Vector3.up, yaw, Space.World);

            float nextPitch = Mathf.Clamp(
                pitch + requestedPitch,
                settings.lowPitchLimit,
                settings.highPitchLimit);
            float appliedPitch = nextPitch - pitch;
            pitch = nextPitch;
            cameraArm.Rotate(cameraArm.right, appliedPitch, Space.World);
        }

        /// <summary>
        /// 상태별 목표 자세를 계산하고 SphereCast로 장애물 충돌을 해결한 카메라 위치를 갱신한다.
        /// </summary>
        private void UpdateCameraTarget(float deltaTime)
        {
            Vector3 desiredLocal;
            Quaternion desiredLocalRotation;
            if (holdingCamera && holdingTarget != null)
            {
                Vector3 playerPosition = modelRoot != null
                    ? modelRoot.position
                    : transform.position;
                PlayerCameraMath.EvaluateHoldingPose(
                    holdingTarget.position - playerPosition,
                    settings,
                    out desiredLocal,
                    out desiredLocalRotation);
            }
            else
            {
                desiredLocal = coreDefaultLocalPosition;
                if (abilityCamera)
                {
                    desiredLocal += settings.abilityCameraOffset;
                }

                desiredLocal.z = PlayerCameraMath.EvaluateLocalZ(
                    pitch,
                    coreDefaultLocalPosition.z,
                    settings.highPitchLimit,
                    settings.lowPitchLimit,
                    settings.highPitchLocalZ,
                    settings.lowPitchLocalZ);
                desiredLocalRotation = coreDefaultLocalRotation;
            }

            coreTargetLocalRotation = desiredLocalRotation;

            Vector3 armWorldPosition = cameraArm.position;
            Vector3 desiredWorldPosition = cameraArm.TransformPoint(desiredLocal);
            Vector3 worldDirection = desiredWorldPosition - armWorldPosition;
            float desiredDistance = worldDirection.magnitude;

            if (desiredDistance <= 0.000001f)
            {
                coreTargetLocalPosition = desiredLocal;
                ApplyCoreLerp(deltaTime);
                return;
            }

            worldDirection /= desiredDistance;
            bool hit = UnityEngine.Physics.SphereCast(
                armWorldPosition,
                settings.collisionRadius,
                worldDirection,
                out RaycastHit hitInfo,
                desiredDistance,
                settings.collisionMask,
                QueryTriggerInteraction.Ignore);

            float targetDistance = hit ? hitInfo.distance : desiredDistance;
            bool obstructionMovedCloser = hit && targetDistance <= previousCollisionDistance;

            if (obstructionMovedCloser)
            {
                previousCollisionDistance = targetDistance;
                Vector3 collisionWorldPosition = armWorldPosition + worldDirection * previousCollisionDistance;
                Vector3 collisionLocalPosition = cameraArm.InverseTransformPoint(collisionWorldPosition);
                cameraCore.localPosition = collisionLocalPosition;
                coreTargetLocalPosition = collisionLocalPosition;
            }
            else
            {
                if (float.IsPositiveInfinity(previousCollisionDistance))
                {
                    previousCollisionDistance = targetDistance;
                }
                else
                {
                    previousCollisionDistance = Mathf.MoveTowards(
                        previousCollisionDistance,
                        targetDistance,
                        settings.collisionReturnSpeed * deltaTime);
                }

                Vector3 resolvedWorldPosition = armWorldPosition + worldDirection * previousCollisionDistance;
                coreTargetLocalPosition = cameraArm.InverseTransformPoint(resolvedWorldPosition);
            }

            ApplyCoreLerp(deltaTime);
        }

        /// <summary>
        /// 카메라 코어를 계산된 목표 로컬 위치와 회전으로 부드럽게 보간한다.
        /// </summary>
        private void ApplyCoreLerp(float deltaTime)
        {
            float t = Mathf.Clamp01(deltaTime / settings.positionLerpTime);
            cameraCore.localPosition = Vector3.Lerp(cameraCore.localPosition, coreTargetLocalPosition, t);
            cameraCore.localRotation = Quaternion.Slerp(cameraCore.localRotation, coreTargetLocalRotation, t);
        }

        /// <summary>
        /// 카메라가 플레이어에 너무 가까워지면 모델 렌더러를 숨긴다.
        /// </summary>
        private void UpdateModelVisibility()
        {
            float distance = Vector3.Distance(cameraArm.position, cameraCore.position);
            bool visible = distance >= settings.hideModelDistance;
            foreach (Renderer modelRenderer in modelRenderers)
            {
                if (modelRenderer != null)
                {
                    modelRenderer.enabled = visible;
                }
            }
        }
    }
}
