using UnityEngine;

namespace Phyzzle.Player
{
    /// <summary>
    /// 들기 상태에서 사용할 카메라의 로컬 위치와 회전 값을 보관한다.
    /// </summary>
    [System.Serializable]
    public struct HoldingCameraPose
    {
        public Vector3 localPosition;
        public Vector3 localEulerAngles;

        /// <summary>
        /// 로컬 위치와 오일러 각으로 들기 카메라 자세를 생성한다.
        /// </summary>
        public HoldingCameraPose(Vector3 position, Vector3 eulerAngles)
        {
            localPosition = position;
            localEulerAngles = eulerAngles;
        }

        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
    }

    /// <summary>
    /// 플레이어 카메라 회전, 충돌 회피, 표시와 들기 상태 카메라에 사용할 튜닝 값을 보관한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerCameraSettings", menuName = "Phyzzle/Player/Camera Settings")]
    public sealed class PlayerCameraSettings : ScriptableObject
    {
        [Header("Original Phyzzle values")]
        [Min(0f)] public float sensitivity = 90f;
        [Range(0f, 89f)] public float highPitchLimit = 80f;
        [Range(-89f, 0f)] public float lowPitchLimit = -70f;
        public float highPitchLocalZ = -10f;
        public float lowPitchLocalZ = -2f;
        [Min(0.001f)] public float positionLerpTime = 0.5f;

        [Header("Collision")]
        [Min(0.01f)] public float collisionRadius = 0.2f;
        [Min(0f)] public float collisionReturnSpeed = 12f;
        public LayerMask collisionMask = UnityEngine.Physics.DefaultRaycastLayers;

        [Header("Presentation")]
        [Min(0f)] public float hideModelDistance = 3f;
        public Vector3 abilityCameraOffset = new(0.5f, 0.5f, 0f);

        [Header("Attach Holding Camera")]
        [Min(0f)] public float holdingPositionNearDistance = 5f;
        [Min(0f)] public float holdingPositionFarDistance = 20f;
        [Min(0f)] public float holdingRotationNearDistance = 10f;
        [Min(0f)] public float holdingRotationFarDistance = 20f;
        [Min(0.001f)] public float holdingHighHeight = 10f;
        [Min(0.001f)] public float holdingLowHeight = 7f;
        public HoldingCameraPose holdingLowCamera0 = new(
            new Vector3(0f, 4f, -1f),
            new Vector3(70f, 0f, 0f));
        public HoldingCameraPose holdingLowCamera1 = new(
            new Vector3(0f, 2.5f, -4.5f),
            new Vector3(20f, 0f, 0f));
        public HoldingCameraPose holdingDefaultCamera0 = new(
            new Vector3(0f, 2.8f, -4.2f),
            new Vector3(31f, 0f, 0f));
        public HoldingCameraPose holdingDefaultCamera1 = new(
            new Vector3(0f, 2.3f, -7f),
            new Vector3(10f, 0f, 0f));
        public HoldingCameraPose holdingHighCamera0 = new(
            new Vector3(0f, 7.9f, -11.5f),
            new Vector3(20f, 0f, 0f));
        public HoldingCameraPose holdingHighCamera1 = new(
            new Vector3(0f, 8f, -12f),
            new Vector3(14.5f, 0f, 0f));
    }
}
