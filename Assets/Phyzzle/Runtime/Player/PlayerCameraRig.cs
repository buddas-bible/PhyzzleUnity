using UnityEngine;

namespace Phyzzle.Player
{
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

        private void Awake()
        {
            Initialize();
        }

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

        public void SetAbilityCamera(bool enabled)
        {
            abilityCamera = enabled;
        }

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

        public void TickLate(Vector2 lookInput, bool allowInput)
            => TickLate(lookInput, allowInput, false);

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

        private void ApplyCoreLerp(float deltaTime)
        {
            float t = Mathf.Clamp01(deltaTime / settings.positionLerpTime);
            cameraCore.localPosition = Vector3.Lerp(cameraCore.localPosition, coreTargetLocalPosition, t);
            cameraCore.localRotation = Quaternion.Slerp(cameraCore.localRotation, coreTargetLocalRotation, t);
        }

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
