using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 카메라 주변의 부착 가능 오브젝트를 수집하고 조준선상의 현재 대상을 선택한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachTargeting : MonoBehaviour
    {
        [SerializeField] private Transform cameraArm;
        [SerializeField] private Transform cameraCore;
        [SerializeField] private AttachSettings settings;

        private Collider[] overlapBuffer = System.Array.Empty<Collider>();
        private readonly List<AttachableObject> nearby = new();

        public AttachableObject CurrentTarget { get; private set; }
        public IReadOnlyList<AttachableObject> Nearby => nearby;

        /// <summary>
        /// 타게팅에 사용할 카메라 기준 Transform과 설정을 구성한다.
        /// </summary>
        public void Configure(Transform arm, Transform core, AttachSettings attachSettings)
        {
            cameraArm = arm;
            cameraCore = core;
            settings = attachSettings;
            EnsureBuffer();
        }

        /// <summary>
        /// 주변 후보 목록과 카메라 조준선상의 현재 부착 대상을 다시 계산한다.
        /// </summary>
        public AttachableObject Refresh()
        {
            CurrentTarget = null;
            nearby.Clear();
            if (cameraArm == null || cameraCore == null || settings == null)
            {
                return null;
            }

            EnsureBuffer();
            Vector3 nearbyCenter = cameraArm.TransformPoint(new Vector3(
                cameraCore.localPosition.x,
                cameraCore.localPosition.y,
                0f));
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(
                nearbyCenter,
                settings.nearbySearchRadius,
                overlapBuffer,
                settings.nearbyMask,
                QueryTriggerInteraction.Collide);

            HashSet<AttachableObject> unique = new();
            for (int i = 0; i < count; i++)
            {
                AttachableObject candidate = overlapBuffer[i] != null
                    ? overlapBuffer[i].GetComponentInParent<AttachableObject>()
                    : null;
                if (candidate != null && candidate.Body != null && !candidate.Body.isKinematic && unique.Add(candidate))
                {
                    nearby.Add(candidate);
                }
            }

            float rayDistance = settings.targetRayDistance + Mathf.Abs(cameraCore.localPosition.z);
            if (UnityEngine.Physics.Raycast(
                    cameraCore.position,
                    cameraCore.forward,
                    out RaycastHit hit,
                    rayDistance,
                    settings.targetMask,
                    QueryTriggerInteraction.Collide))
            {
                AttachableObject target = hit.collider.GetComponentInParent<AttachableObject>();
                if (target != null && target.Body != null && !target.Body.isKinematic)
                {
                    CurrentTarget = target;
                }
            }

            return CurrentTarget;
        }

        /// <summary>
        /// 현재 대상과 주변 후보 목록을 모두 초기화한다.
        /// </summary>
        public void Clear()
        {
            CurrentTarget = null;
            nearby.Clear();
        }

        /// <summary>
        /// 설정된 최대 검색 개수에 맞춰 비할당 오버랩 버퍼 크기를 준비한다.
        /// </summary>
        private void EnsureBuffer()
        {
            int requiredSize = settings != null ? Mathf.Max(1, settings.overlapBufferSize) : 1;
            if (overlapBuffer.Length != requiredSize)
            {
                overlapBuffer = new Collider[requiredSize];
            }
        }
    }
}
