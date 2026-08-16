using NUnit.Framework;
using Phyzzle.Compatibility;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class LegacyForceModeMapTests
    {
        [Test]
        public void OriginalMisspelledAccelerationActuallyMappedToVelocityChange()
        {
            Assert.That(
                LegacyForceModeMap.ToUnity(LegacyForceType.Accelration),
                Is.EqualTo(ForceMode.VelocityChange));
        }

        [Test]
        public void OriginalVelocityChangeActuallyMappedToAcceleration()
        {
            Assert.That(
                LegacyForceModeMap.ToUnity(LegacyForceType.VelocityChange),
                Is.EqualTo(ForceMode.Acceleration));
        }
    }
}
