using NUnit.Framework;
using Phyzzle.Player;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Phyzzle.Tests
{
    public sealed class PlayerMovementMathTests
    {
        [Test]
        public void CameraRelativeDirection_UsesFlattenedCameraAxes()
        {
            Vector3 result = PlayerMovementMath.CameraRelativeDirection(
                new Vector2(1f, 1f).normalized,
                new Vector3(0f, 0.5f, 1f));

            Assert.That(result.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.normalized, Is.EqualTo(new Vector3(1f, 0f, 1f).normalized).Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void ProjectDirectionOnSlope_IsTangentToGround()
        {
            Vector3 normal = Quaternion.AngleAxis(30f, Vector3.right) * Vector3.up;
            Vector3 result = PlayerMovementMath.ProjectDirectionOnSlope(
                Vector2.up,
                Vector3.forward,
                normal);

            Assert.That(Vector3.Dot(result.normalized, normal), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ClampVerticalSpeed_PreservesHorizontalVelocity()
        {
            Vector3 result = PlayerMovementMath.ClampVerticalSpeed(new Vector3(2f, 80f, -3f), 30f);

            Assert.That(result, Is.EqualTo(new Vector3(2f, 30f, -3f)).Using(Vector3ComparerWithEqualsOperator.Instance));
        }
    }
}
