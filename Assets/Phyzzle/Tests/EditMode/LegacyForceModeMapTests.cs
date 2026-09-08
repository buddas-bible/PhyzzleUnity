using NUnit.Framework;
using Phyzzle.Compatibility;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>LegacyForceModeMapTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class LegacyForceModeMapTests
    {
        /// <summary>
        /// <c>OriginalMisspelledAccelerationActuallyMappedToVelocityChange</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void OriginalMisspelledAccelerationActuallyMappedToVelocityChange()
        {
            Assert.That(
                LegacyForceModeMap.ToUnity(LegacyForceType.Accelration),
                Is.EqualTo(ForceMode.VelocityChange));
        }

        /// <summary>
        /// <c>OriginalVelocityChangeActuallyMappedToAcceleration</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void OriginalVelocityChangeActuallyMappedToAcceleration()
        {
            Assert.That(
                LegacyForceModeMap.ToUnity(LegacyForceType.VelocityChange),
                Is.EqualTo(ForceMode.Acceleration));
        }
    }
}
