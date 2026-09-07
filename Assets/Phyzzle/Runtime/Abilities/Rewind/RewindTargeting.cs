using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 카메라 주변의 되감기 가능 기록기를 수집하고 조준선상의 현재 대상을 선택한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewindTargeting : MonoBehaviour
    {
        [SerializeField] private Transform cameraArm;
        [SerializeField] private Transform cameraCore;
        [SerializeField] private RewindSettings settings;
        [SerializeField] private RewindCoordinator coordinator;

        private Collider[] overlapBuffer = System.Array.Empty<Collider>();
        private readonly List<RewindRecorder> nearby = new();

        public RewindRecorder CurrentTarget { get; private set; }
        public IReadOnlyList<RewindRecorder> Nearby => nearby;

        /// <summary>
        /// 타게팅에 사용할 카메라 기준 Transform, 설정과 되감기 조율자를 구성한다.
        /// </summary>
        public void Configure(
            Transform arm,
            Transform core,
            RewindSettings rewindSettings,
            RewindCoordinator rewindCoordinator)
        {
            cameraArm = arm;
            cameraCore = core;
            settings = rewindSettings;
            coordinator = rewindCoordinator;
            EnsureBuffer();
        }

        /// <summary>
        /// 주변 후보 목록과 카메라 조준선상의 현재 되감기 대상을 다시 계산한다.
        /// </summary>
        public RewindRecorder Refresh()
        {
            CurrentTarget = null;
            nearby.Clear();
            if (cameraArm == null || cameraCore == null || settings == null || coordinator == null)
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
                settings.targetMask,
                QueryTriggerInteraction.Collide);

            HashSet<RewindRecorder> unique = new();
            for (int i = 0; i < count; i++)
            {
                RewindRecorder candidate = overlapBuffer[i] != null
                    ? overlapBuffer[i].GetComponentInParent<RewindRecorder>()
                    : null;
                if (IsUsable(candidate) && unique.Add(candidate))
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
                RewindRecorder candidate = hit.collider.GetComponentInParent<RewindRecorder>();
                if (IsUsable(candidate))
                {
                    CurrentTarget = candidate;
                }
            }

            return CurrentTarget;
        }

        /// <summary>
        /// 현재 대상과 주변 되감기 후보 목록을 초기화한다.
        /// </summary>
        public void Clear()
        {
            CurrentTarget = null;
            nearby.Clear();
        }

        /// <summary>
        /// 기록기가 물리 상태와 되감기 조건을 모두 만족하는 유효한 대상인지 검사한다.
        /// </summary>
        private bool IsUsable(RewindRecorder candidate)
        {
            return candidate != null && candidate.Body != null &&
                   !candidate.Body.isKinematic && coordinator.CanRewind(candidate);
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
