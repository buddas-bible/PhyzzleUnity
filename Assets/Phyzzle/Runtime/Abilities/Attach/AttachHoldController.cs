using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
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

        public void TickFixed(float fixedDeltaTime)
        {
            holdPhysics.Tick(session, playerModel, playerBody, settings, fixedDeltaTime);
        }

        public bool TryAttach()
        {
            return IsHolding && attachmentService != null && attachmentService.TryAttach(session.HeldObject);
        }

        public bool DetachHeldObject()
        {
            return IsHolding && attachmentService != null && attachmentService.Detach(session.HeldObject);
        }

        public void Release()
        {
            session.Release(attachmentService);
        }

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
