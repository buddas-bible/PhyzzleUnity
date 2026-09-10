using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 기존 프로젝트의 감쇠 스프링 방식으로 위치와 회전 추종 속도를 계산한다.
    /// </summary>
    public static class LegacySpringMath
    {
        /// <summary>
        /// 현재 위치와 목표 위치 사이의 감쇠 스프링 선형 속도를 한 단계 갱신한다.
        /// </summary>
        public static Vector3 UpdatePositionVelocity(
            Vector3 currentPosition,
            Vector3 currentSpringVelocity,
            Vector3 targetPosition,
            float dampingRatio,
            float frequency,
            float deltaTime)
        {
            // x'' + 2ζωx' + ω²x = 0 형태의 감쇠 스프링을 implicit 방식으로 한 스텝 적분
            // f는 감쇠항, omegaSquared는 복원력 항이며 determinantInverse가 큰 deltaTime에서도 발산을 억제
            float f = 1f + 2f * deltaTime * dampingRatio * frequency;
            float omegaSquared = frequency * frequency;
            float stepOmegaSquared = deltaTime * omegaSquared;
            float stepSquaredOmegaSquared = deltaTime * stepOmegaSquared;
            float determinantInverse = 1f / (f + stepSquaredOmegaSquared);

            // 현재 스프링 속도에 목표까지의 위치 오차 * ω² * dt를 더해 목표 방향의 새 속도를 계산
            Vector3 determinantVelocity = currentSpringVelocity +
                                          stepOmegaSquared * (targetPosition - currentPosition);
            return determinantVelocity * determinantInverse;
        }

        /// <summary>
        /// 현재 회전과 목표 회전 사이의 감쇠 스프링 각속도를 한 단계 갱신한다.
        /// </summary>
        public static Vector3 UpdateAngularVelocity(
            Quaternion currentRotation,
            Vector3 currentSpringVelocity,
            Quaternion targetRotation,
            float dampingRatio,
            float frequency,
            float deltaTime)
        {
            Quaternion goal = targetRotation;
            // q와 -q는 같은 회전을 나타내므로 Dot이 음수면 부호를 뒤집어 더 짧은 회전 경로를 선택
            if (Quaternion.Dot(currentRotation, goal) < 0f)
            {
                goal = new Quaternion(-goal.x, -goal.y, -goal.z, -goal.w);
            }

            // 현재 회전의 역을 곱해 목표 회전을 현재 기준의 상대 회전으로 변환
            Quaternion relative = goal * Quaternion.Inverse(currentRotation);
            relative = Normalize(relative);
            Vector3 relativeAxis = new(relative.x, relative.y, relative.z);

            // 위치 스프링과 같은 implicit 감쇠식을 상대 회전 벡터에 적용해 목표 각속도를 계산
            float f = 1f + 2f * deltaTime * dampingRatio * frequency;
            float omegaSquared = frequency * frequency;
            float stepOmegaSquared = deltaTime * omegaSquared;
            float stepSquaredOmegaSquared = deltaTime * stepOmegaSquared;
            float determinantInverse = 1f / (f + stepSquaredOmegaSquared);
            Vector3 determinantVelocity = currentSpringVelocity + stepOmegaSquared * relativeAxis;
            return determinantVelocity * determinantInverse;
        }

        /// <summary>
        /// 쿼터니언을 단위 크기로 정규화하고 너무 작은 값은 항등 회전으로 대체한다.
        /// </summary>
        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            // 거의 0인 쿼터니언은 나눗셈 시 수치가 불안정해지므로 회전 없음으로 처리
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
