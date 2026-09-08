using NUnit.Framework;
using Phyzzle.Player;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerCameraMathTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerCameraMathTests
    {
        /// <summary>
        /// <c>EvaluateLocalZ_MatchesOriginalCameraEndpoints</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(0f, -4f)]
        [TestCase(80f, -10f)]
        [TestCase(-70f, -2f)]
        public void EvaluateLocalZ_MatchesOriginalCameraEndpoints(float pitch, float expected)
        {
            float result = PlayerCameraMath.EvaluateLocalZ(pitch, -4f, 80f, -70f, -10f, -2f);

            Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
        }

        /// <summary>
        /// <c>EvaluateLocalZ_ClampsPitchOutsideOriginalLimits</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void EvaluateLocalZ_ClampsPitchOutsideOriginalLimits()
        {
            float above = PlayerCameraMath.EvaluateLocalZ(100f, -4f, 80f, -70f, -10f, -2f);
            float below = PlayerCameraMath.EvaluateLocalZ(-100f, -4f, 80f, -70f, -10f, -2f);

            Assert.That(above, Is.EqualTo(-10f).Within(0.0001f));
            Assert.That(below, Is.EqualTo(-2f).Within(0.0001f));
        }

        /// <summary>
        /// <c>EvaluateHoldingPose_UsesAuthoredDistanceRanges</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(5f, 2.8f, -4.2f, 31f)]
        [TestCase(10f, 2.6333334f, -5.133333f, 31f)]
        [TestCase(12.5f, 2.55f, -5.6f, 25.75f)]
        [TestCase(15f, 2.4666667f, -6.0666666f, 20.5f)]
        [TestCase(20f, 2.3f, -7f, 10f)]
        public void EvaluateHoldingPose_UsesAuthoredDistanceRanges(
            float distance,
            float expectedY,
            float expectedZ,
            float expectedPitch)
        {
            PlayerCameraSettings settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
            try
            {
                PlayerCameraMath.EvaluateHoldingPose(
                    new Vector3(0f, 0f, distance),
                    settings,
                    out Vector3 position,
                    out Quaternion rotation);

                Assert.That(position.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(position.y, Is.EqualTo(expectedY).Within(0.0001f));
                Assert.That(position.z, Is.EqualTo(expectedZ).Within(0.0001f));
                Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(expectedPitch, 0f, 0f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        /// <summary>
        /// <c>EvaluateHoldingPose_ReachesAuthoredHeightEndpoints</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(10f, 7.9f, -11.5f, 20f)]
        [TestCase(-7f, 4f, -1f, 70f)]
        public void EvaluateHoldingPose_ReachesAuthoredHeightEndpoints(
            float height,
            float expectedY,
            float expectedZ,
            float expectedPitch)
        {
            PlayerCameraSettings settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
            try
            {
                PlayerCameraMath.EvaluateHoldingPose(
                    new Vector3(0f, height, 5f),
                    settings,
                    out Vector3 position,
                    out Quaternion rotation);

                Assert.That(position.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(position.y, Is.EqualTo(expectedY).Within(0.0001f));
                Assert.That(position.z, Is.EqualTo(expectedZ).Within(0.0001f));
                Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(expectedPitch, 0f, 0f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
