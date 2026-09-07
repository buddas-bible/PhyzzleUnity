using UnityEngine;

namespace Phyzzle.Player
{
    /// <summary>
    /// 플레이어 카메라 위치와 들기 상태 카메라 자세를 계산하는 순수 함수를 제공한다.
    /// </summary>
    public static class PlayerCameraMath
    {
        /// <summary>
        /// 카메라 피치에 따라 보간된 로컬 Z 위치를 계산한다.
        /// </summary>
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

        /// <summary>
        /// 들고 있는 대상의 상대 위치를 기준으로 카메라 로컬 위치와 회전을 계산한다.
        /// </summary>
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
