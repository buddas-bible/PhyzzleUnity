using UnityEngine;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Compatibility;

namespace Phyzzle.Player
{
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

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
        }

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

        public void SetSpeedOverride(float speed)
        {
            speedOverride = speed;
        }

        public void ClearSpeedOverride()
        {
            speedOverride = -1f;
        }

        public void SetMovementFacingEnabled(bool enabled)
        {
            movementFacingEnabled = enabled;
        }

        public void TickFixed(Vector2 moveInput, bool jumpPressed, bool allowControl)
        {
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
                IsMoving = false;
                platformVelocity = Vector3.zero;
                ClampVerticalVelocity();
            }
        }

        private bool IsConfigured()
        {
            return body != null && movementReference != null && settings != null;
        }

        private void UpdateGroundState()
        {
            IsGrounded = groundSensor != null && groundSensor.IsGrounded;
            IsOnStandableSlope = false;

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

            if (!hitGround)
            {
                return;
            }

            lastGroundNormal = hit.normal;
            float slope = Vector3.Angle(Vector3.up, hit.normal);
            IsOnStandableSlope = slope <= settings.slopeLimitDegrees;
        }

        private bool TryJump()
        {
            if (!IsGrounded || !IsOnStandableSlope)
            {
                return false;
            }

            body.AddForce(
                Vector3.up * settings.jumpAcceleration,
                LegacyForceModeMap.ToUnity(LegacyForceType.Accelration));
            return true;
        }

        private void ApplyMovement(Vector2 input)
        {
            Vector3 originDirection = PlayerMovementMath.CameraRelativeDirection(input, movementReference.forward);
            Vector3 direction = !IsFlying && IsOnStandableSlope
                ? PlayerMovementMath.ProjectDirectionOnSlope(input, movementReference.forward, lastGroundNormal)
                : originDirection;

            float inputMagnitude = input.magnitude;
            float movementSpeed = speedOverride >= 0f ? speedOverride : settings.moveSpeed;
            Vector3 targetVelocity = Vector3.zero;
            if (direction.sqrMagnitude > DirectionEpsilon)
            {
                float alignment = Vector3.Dot(direction, originDirection);
                targetVelocity = direction * (movementSpeed * inputMagnitude * alignment);
            }

            Vector3 currentVelocity = PlayerMovementMath.ClampVerticalSpeed(
                body.linearVelocity,
                settings.maxVerticalSpeed);
            body.linearVelocity = currentVelocity;

            Vector3 horizontalVelocity = currentVelocity;
            horizontalVelocity.y = 0f;
            Vector3 additionalVelocity = targetVelocity - horizontalVelocity;

            if (IsFlying)
            {
                body.AddForce(targetVelocity, ForceMode.Force);
            }
            else if (IsGrounded)
            {
                additionalVelocity.y = 0f;
                body.AddForce(
                    additionalVelocity + platformVelocity,
                    ForceMode.VelocityChange);
                    // LegacyForceModeMap.ToUnity(LegacyForceType.Accelration));
            }
            else
            {
                body.AddForce(additionalVelocity * body.mass, ForceMode.Force);
            }

            platformVelocity = Vector3.zero;
            IsMoving = inputMagnitude >= 0.000001f;

            if (IsMoving && movementFacingEnabled)
            {
                FaceMovementDirection(body.linearVelocity);
            }
        }

        private void FaceMovementDirection(Vector3 worldVelocity)
        {
            if (modelRoot == null)
            {
                return;
            }

            worldVelocity.y = 0f;
            if (worldVelocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            modelRoot.rotation = Quaternion.LookRotation(worldVelocity.normalized, Vector3.up);
        }

        private void ClampVerticalVelocity()
        {
            body.linearVelocity = PlayerMovementMath.ClampVerticalSpeed(
                body.linearVelocity,
                settings.maxVerticalSpeed);
        }

        private void ApplyPendingImpulse()
        {
            if (pendingFlyingVelocity.sqrMagnitude > 0f)
            {
                body.AddForce(
                    pendingFlyingVelocity,
                    LegacyForceModeMap.ToUnity(LegacyForceType.Accelration));
                pendingFlyingVelocity = Vector3.zero;
            }
        }

        private void UpdateFlyingTimer()
        {
            if (flyingTimeRemaining > 0f)
            {
                flyingTimeRemaining = Mathf.Max(0f, flyingTimeRemaining - Time.fixedDeltaTime);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            ReadCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            ReadCollision(collision);
        }

        private void ReadCollision(Collision collision)
        {
            if (collision == null || settings == null)
            {
                return;
            }

            GameObject other = collision.gameObject;
            Rigidbody otherBody = collision.rigidbody;

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
                    if (contact.point.y < referencePosition.y + 0.5f &&
                        candidateVelocity.sqrMagnitude > platformVelocity.sqrMagnitude)
                    {
                        platformVelocity = candidateVelocity;
                    }
                }
            }

            if (other.GetComponentInParent<PlayerImpulseSource>() == null)
            {
                return;
            }

            Vector3 impulse = collision.impulse;
            if (impulse.magnitude >= settings.impactThreshold &&
                impulse.sqrMagnitude > pendingFlyingVelocity.sqrMagnitude)
            {
                pendingFlyingVelocity = impulse;
                flyingTimeRemaining = settings.flyingDuration;
            }
        }
    }
}
