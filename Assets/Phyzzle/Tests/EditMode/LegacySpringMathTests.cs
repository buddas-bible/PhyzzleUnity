using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class LegacySpringMathTests
    {
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
