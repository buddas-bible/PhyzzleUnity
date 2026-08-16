using UnityEngine;

namespace Phyzzle.Player
{
    public static class PlayerCameraMath
    {
        public static float EvaluateLocalZ(
            float pitch,
            float defaultLocalZ,
            float highPitchLimit,
            float lowPitchLimit,
            float highPitchLocalZ,
            float lowPitchLocalZ)
        {
            if (pitch >= 0f)
            {
                float ratio = highPitchLimit <= 0f ? 0f : Mathf.Clamp01(pitch / highPitchLimit);
                float eased = 1f - (1f - ratio) * (1f - ratio);
                return defaultLocalZ + (highPitchLocalZ - defaultLocalZ) * eased;
            }

            float lowRatio = lowPitchLimit >= 0f ? 0f : Mathf.Clamp01(pitch / lowPitchLimit);
            float lowEased = 1f - Mathf.Pow(1f - lowRatio, 5f);
            return defaultLocalZ + (lowPitchLocalZ - defaultLocalZ) * lowEased;
        }

        public static void EvaluateHoldingPose(
            Vector3 relativePosition,
            PlayerCameraSettings settings,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            if (settings == null)
            {
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                return;
            }

            float dy = relativePosition.y;
            float dz = new Vector2(relativePosition.x, relativePosition.z).magnitude;
            bool isHigh = dy > 0f;
            float heightLimit = isHigh ? settings.holdingHighHeight : settings.holdingLowHeight;
            float heightRatio = Mathf.Clamp01(Mathf.Abs(dy) / heightLimit);
            float positionDistanceRatio = Mathf.InverseLerp(
                settings.holdingPositionNearDistance,
                settings.holdingPositionFarDistance,
                dz);
            float rotationDistanceRatio = Mathf.InverseLerp(
                settings.holdingRotationNearDistance,
                settings.holdingRotationFarDistance,
                dz);

            HoldingCameraPose active0 = isHigh
                ? settings.holdingHighCamera0
                : settings.holdingLowCamera0;
            HoldingCameraPose active1 = isHigh
                ? settings.holdingHighCamera1
                : settings.holdingLowCamera1;

            Vector3 defaultPosition = Vector3.Lerp(
                settings.holdingDefaultCamera0.localPosition,
                settings.holdingDefaultCamera1.localPosition,
                positionDistanceRatio);
            Vector3 activePosition = Vector3.Lerp(
                active0.localPosition,
                active1.localPosition,
                positionDistanceRatio);
            localPosition = Vector3.Lerp(defaultPosition, activePosition, heightRatio);

            Quaternion defaultRotation = Quaternion.Slerp(
                settings.holdingDefaultCamera0.LocalRotation,
                settings.holdingDefaultCamera1.LocalRotation,
                rotationDistanceRatio);
            Quaternion activeRotation = Quaternion.Slerp(
                active0.LocalRotation,
                active1.LocalRotation,
                rotationDistanceRatio);
            localRotation = Quaternion.Slerp(defaultRotation, activeRotation, heightRatio);
        }
    }
}
