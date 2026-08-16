using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    internal readonly struct RewindDrive
    {
        public RewindDrive(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }

        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }
    }

    internal static class RewindVelocityServo
    {
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
