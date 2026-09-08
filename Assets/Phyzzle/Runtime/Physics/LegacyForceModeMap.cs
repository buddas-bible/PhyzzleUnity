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

    /// <summary>
    /// 기존 엔진의 힘 모드 열거형 값을 Unity ForceMode 의미에 맞게 변환한다.
    /// </summary>
    public static class LegacyForceModeMap
    {
        // The original engine cast its enum directly to PhysX PxForceMode.
        // PxForceMode values 2 and 3 are VelocityChange and Acceleration,
        // while the legacy enum named those two values in the opposite order.
        /// <summary>
        /// 레거시 enum 값의 실제 PhysX 의미를 보존해 대응하는 Unity ForceMode를 반환한다.
        /// </summary>
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
