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
            // 한 물리 프레임의 시간이 없으면 거리 / 시간 계산이 불가능하므로 속도를 주지 않음
            if (fixedDeltaTime <= 0f)
            {
                return new RewindDrive(Vector3.zero, Vector3.zero);
            }

            // 이번 FixedUpdate 한 번에 목표 위치까지 도달하는 속도 = 위치 오차 / dt
            // 기록 간격이 크더라도 순간이동하지 않도록 최대 선형 속도로 제한
            Vector3 linearVelocity = Vector3.ClampMagnitude(
                (target.Position - currentPosition) / fixedDeltaTime,
                maxLinearSpeed);

            // 현재 회전의 역을 곱해 목표 회전을 현재 기준의 상대 회전으로 변환
            Quaternion rotationError = Quaternion.Normalize(target.Rotation * Quaternion.Inverse(currentRotation));
            // q와 -q는 같은 회전이므로 w가 음수면 부호를 뒤집어 180도 이하의 짧은 회전 경로를 선택
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

            // 각속도(rad/s) = 회전축 * (남은 각도(rad) / dt)
            // 물리적으로 지나치게 빠른 회전이 생기지 않도록 설정 최대 각속도로 제한
            Vector3 angularVelocity = Vector3.ClampMagnitude(
                axis * (angleDegrees * Mathf.Deg2Rad / fixedDeltaTime),
                maxAngularSpeedDegrees * Mathf.Deg2Rad);
            return new RewindDrive(linearVelocity, angularVelocity);
        }
    }
}
