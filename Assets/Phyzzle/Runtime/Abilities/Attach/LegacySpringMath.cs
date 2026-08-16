using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    public static class LegacySpringMath
    {
        public static Vector3 UpdatePositionVelocity(
            Vector3 currentPosition,
            Vector3 currentSpringVelocity,
            Vector3 targetPosition,
            float dampingRatio,
            float frequency,
            float deltaTime)
        {
            float f = 1f + 2f * deltaTime * dampingRatio * frequency;
            float omegaSquared = frequency * frequency;
            float stepOmegaSquared = deltaTime * omegaSquared;
            float stepSquaredOmegaSquared = deltaTime * stepOmegaSquared;
            float determinantInverse = 1f / (f + stepSquaredOmegaSquared);
            Vector3 determinantVelocity = currentSpringVelocity +
                                          stepOmegaSquared * (targetPosition - currentPosition);
            return determinantVelocity * determinantInverse;
        }

        public static Vector3 UpdateAngularVelocity(
            Quaternion currentRotation,
            Vector3 currentSpringVelocity,
            Quaternion targetRotation,
            float dampingRatio,
            float frequency,
            float deltaTime)
        {
            Quaternion goal = targetRotation;
            if (Quaternion.Dot(currentRotation, goal) < 0f)
            {
                goal = new Quaternion(-goal.x, -goal.y, -goal.z, -goal.w);
            }

            Quaternion relative = goal * Quaternion.Inverse(currentRotation);
            relative = Normalize(relative);
            Vector3 relativeAxis = new(relative.x, relative.y, relative.z);

            float f = 1f + 2f * deltaTime * dampingRatio * frequency;
            float omegaSquared = frequency * frequency;
            float stepOmegaSquared = deltaTime * omegaSquared;
            float stepSquaredOmegaSquared = deltaTime * stepOmegaSquared;
            float determinantInverse = 1f / (f + stepSquaredOmegaSquared);
            Vector3 determinantVelocity = currentSpringVelocity + stepOmegaSquared * relativeAxis;
            return determinantVelocity * determinantInverse;
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }
    }
}
