using UnityEngine;

namespace Phyzzle.Player
{
    public static class PlayerMovementMath
    {
        private const float DirectionEpsilon = 0.000001f;

        public static Vector3 CameraRelativeDirection(Vector2 input, Vector3 cameraForward)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (forward.sqrMagnitude <= DirectionEpsilon)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            return forward * input.y + right * input.x;
        }

        public static Vector3 ProjectDirectionOnSlope(
            Vector2 input,
            Vector3 cameraForward,
            Vector3 groundNormal)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (flatForward.sqrMagnitude <= DirectionEpsilon)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();
            Vector3 slopeRight = Vector3.Cross(groundNormal, flatForward);
            if (slopeRight.sqrMagnitude <= DirectionEpsilon)
            {
                return CameraRelativeDirection(input, cameraForward);
            }

            slopeRight.Normalize();
            Vector3 slopeForward = Vector3.Cross(slopeRight, groundNormal).normalized;
            return slopeForward * input.y + slopeRight * input.x;
        }

        public static Vector3 ClampVerticalSpeed(Vector3 velocity, float maxVerticalSpeed)
        {
            velocity.y = Mathf.Clamp(velocity.y, -maxVerticalSpeed, maxVerticalSpeed);
            return velocity;
        }
    }
}
