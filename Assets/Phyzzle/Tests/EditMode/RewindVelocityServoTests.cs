using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>RewindVelocityServoTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class RewindVelocityServoTests
    {
        /// <summary>
        /// <c>Calculate_UsesShortestQuaternionArc</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Calculate_UsesShortestQuaternionArc()
        {
            RewindPoseSample target = new(1, Vector3.zero, Quaternion.Euler(0f, 350f, 0f));
            RewindDrive drive = RewindVelocityServo.Calculate(
                Vector3.zero, Quaternion.identity, target, 0.02f, 100f, 1000f);

            Assert.That(drive.AngularVelocity.y, Is.EqualTo(-Mathf.Deg2Rad * 10f / 0.02f).Within(0.001f));
        }

        /// <summary>
        /// <c>Calculate_ClampsLinearAndAngularVelocity</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Calculate_ClampsLinearAndAngularVelocity()
        {
            RewindPoseSample target = new(1, Vector3.right * 10f, Quaternion.Euler(0f, 180f, 0f));
            RewindDrive drive = RewindVelocityServo.Calculate(
                Vector3.zero, Quaternion.identity, target, 0.02f, 3f, 90f);

            Assert.That(drive.LinearVelocity.magnitude, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(drive.AngularVelocity.magnitude, Is.EqualTo(90f * Mathf.Deg2Rad).Within(0.0001f));
        }

        /// <summary>
        /// <c>Calculate_UsesPositionErrorPerFixedDeltaForUncappedLinearVelocity</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Calculate_UsesPositionErrorPerFixedDeltaForUncappedLinearVelocity()
        {
            RewindPoseSample target = new(1, new Vector3(0.4f, -0.2f, 0.1f), Quaternion.identity);
            RewindDrive drive = RewindVelocityServo.Calculate(
                Vector3.zero, Quaternion.identity, target, 0.02f, 1000f, 1000f);

            Assert.That(drive.LinearVelocity, Is.EqualTo(new Vector3(20f, -10f, 5f)));
        }

        /// <summary>
        /// <c>Calculate_NonPositiveFixedDeltaTime_ReturnsZeroVelocities</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Calculate_NonPositiveFixedDeltaTime_ReturnsZeroVelocities()
        {
            RewindPoseSample target = new(1, Vector3.right, Quaternion.Euler(0f, 90f, 0f));
            RewindDrive drive = RewindVelocityServo.Calculate(
                Vector3.zero, Quaternion.identity, target, 0f, 100f, 1000f);

            Assert.That(drive.LinearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(drive.AngularVelocity, Is.EqualTo(Vector3.zero));
        }

        /// <summary>
        /// <c>Calculate_NegativeFixedDeltaTime_ReturnsZeroVelocities</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Calculate_NegativeFixedDeltaTime_ReturnsZeroVelocities()
        {
            RewindPoseSample target = new(1, Vector3.right, Quaternion.Euler(0f, 90f, 0f));
            RewindDrive drive = RewindVelocityServo.Calculate(
                Vector3.zero, Quaternion.identity, target, -0.02f, 100f, 1000f);

            Assert.That(drive.LinearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(drive.AngularVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
