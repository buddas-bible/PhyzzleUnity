using UnityEngine;

namespace Phyzzle.Player
{
    /// <summary>
    /// 플레이어 이동 방향과 속도 제한에 필요한 순수 계산을 제공한다.
    /// </summary>
    public static class PlayerMovementMath
    {
        private const float DirectionEpsilon = 0.000001f;

        /// <summary>
        /// 입력을 카메라의 수평 방향 기준 월드 이동 방향으로 변환한다.
        /// </summary>
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

        /// <summary>
        /// 카메라 기준 입력 방향을 지면 경사면에 투영한 이동 방향으로 변환한다.
        /// </summary>
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

        /// <summary>
        /// 수직 속도를 설정된 최대 상승·하강 속도 범위로 제한한다.
        /// </summary>
        public static Vector3 ClampVerticalSpeed(Vector3 velocity, float maxVerticalSpeed)
        {
            velocity.y = Mathf.Clamp(velocity.y, -maxVerticalSpeed, maxVerticalSpeed);
            return velocity;
        }
    }
}
