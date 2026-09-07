using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 되감기 대상에 적용할 선형·각속도 명령을 함께 보관한다.
    /// </summary>
    internal readonly struct RewindDrive
    {
        /// <summary>
        /// 선형 속도와 각속도로 하나의 되감기 구동 명령을 생성한다.
        /// </summary>
        public RewindDrive(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }

        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }
    }

    /// <summary>
    /// 현재 자세에서 기록된 목표 자세로 한 물리 프레임에 접근할 속도 명령을 계산한다.
    /// </summary>
    internal static class RewindVelocityServo
    {
        /// <summary>
        /// 목표 위치·회전까지 필요한 선형·각속도를 계산하고 설정된 최대 속도로 제한한다.
        /// </summary>
        public static RewindDrive Calculate(
            Vector3 currentPosition,
            Quaternion currentRotation,
            RewindPoseSample target,
            float fixedDeltaTime,
            float maxLinearSpeed,
            float maxAngularSpeedDegrees)
        {
            if (fixedDeltaTime <= 0f)
            {
                return new RewindDrive(Vector3.zero, Vector3.zero);
            }

            Vector3 linearVelocity = Vector3.ClampMagnitude(
                (target.Position - currentPosition) / fixedDeltaTime,
                maxLinearSpeed);

            Quaternion rotationError = Quaternion.Normalize(target.Rotation * Quaternion.Inverse(currentRotation));
            if (rotationError.w < 0f)
            {
                rotationError = new Quaternion(
                    -rotationError.x,
                    -rotationError.y,
                    -rotationError.z,
                    -rotationError.w);
            }

            rotationError.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            Vector3 angularVelocity = Vector3.ClampMagnitude(
                axis * (angleDegrees * Mathf.Deg2Rad / fixedDeltaTime),
                maxAngularSpeedDegrees * Mathf.Deg2Rad);
            return new RewindDrive(linearVelocity, angularVelocity);
        }
    }
}
