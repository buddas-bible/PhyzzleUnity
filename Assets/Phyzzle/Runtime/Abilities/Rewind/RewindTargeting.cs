using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
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

        public void Clear()
        {
            CurrentTarget = null;
            nearby.Clear();
        }

        private bool IsUsable(RewindRecorder candidate)
        {
            return candidate != null && candidate.Body != null &&
                   !candidate.Body.isKinematic && coordinator.CanRewind(candidate);
        }

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
