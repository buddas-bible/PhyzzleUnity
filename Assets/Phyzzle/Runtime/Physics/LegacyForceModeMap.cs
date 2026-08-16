using UnityEngine;

namespace Phyzzle.Compatibility
{
    public enum LegacyForceType
    {
        Force = 0,
        Impulse = 1,
        Accelration = 2,
        VelocityChange = 3
    }

    public static class LegacyForceModeMap
    {
        // The original engine cast its enum directly to PhysX PxForceMode.
        // PxForceMode values 2 and 3 are VelocityChange and Acceleration,
        // while the legacy enum named those two values in the opposite order.
        public static ForceMode ToUnity(LegacyForceType legacyType)
        {
            return legacyType switch
            {
                LegacyForceType.Force => ForceMode.Force,
                LegacyForceType.Impulse => ForceMode.Impulse,
                LegacyForceType.Accelration => ForceMode.VelocityChange,
                LegacyForceType.VelocityChange => ForceMode.Acceleration,
                _ => ForceMode.Force
            };
        }
    }
}
