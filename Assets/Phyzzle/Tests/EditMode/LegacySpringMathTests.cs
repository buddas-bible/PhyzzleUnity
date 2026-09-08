using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>LegacySpringMathTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class LegacySpringMathTests
    {
        /// <summary>
        /// <c>PositionSpring_AcceleratesTowardTarget</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void PositionSpring_AcceleratesTowardTarget()
        {
            Vector3 result = LegacySpringMath.UpdatePositionVelocity(
                Vector3.zero,
                Vector3.zero,
                Vector3.forward * 10f,
                3f,
                500f,
                0.02f);

            Assert.That(result.z, Is.GreaterThan(0f));
            Assert.That(result.x, Is.EqualTo(0f));
            Assert.That(result.y, Is.EqualTo(0f));
        }

        /// <summary>
        /// <c>QuaternionSpring_UsesShortestTargetDirection</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void QuaternionSpring_UsesShortestTargetDirection()
        {
            Vector3 positive = LegacySpringMath.UpdateAngularVelocity(
                Quaternion.identity,
                Vector3.zero,
                Quaternion.AngleAxis(90f, Vector3.up),
                3.6f,
                500f,
                0.02f);
            Vector3 equivalentNegativeQuaternion = LegacySpringMath.UpdateAngularVelocity(
                Quaternion.identity,
                Vector3.zero,
                new Quaternion(0f, -0.7071068f, 0f, -0.7071068f),
                3.6f,
                500f,
                0.02f);

            Assert.That(positive.y, Is.GreaterThan(0f));
            Assert.That(equivalentNegativeQuaternion.y, Is.EqualTo(positive.y).Within(0.0001f));
        }
    }
}
