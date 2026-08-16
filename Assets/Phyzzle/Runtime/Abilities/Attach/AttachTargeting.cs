using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
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

        public void Configure(Transform arm, Transform core, AttachSettings attachSettings)
        {
            cameraArm = arm;
            cameraCore = core;
            settings = attachSettings;
            EnsureBuffer();
        }

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

        public void Clear()
        {
            CurrentTarget = null;
            nearby.Clear();
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
