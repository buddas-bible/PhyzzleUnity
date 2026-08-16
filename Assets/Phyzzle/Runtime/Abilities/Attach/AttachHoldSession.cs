using Phyzzle.Compatibility;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    internal sealed class AttachHoldSession
    {
        private readonly AttachRotationStateMachine rotation = new();

        public AttachableObject HeldObject { get; private set; }
        public bool IsHolding => HeldObject != null;
        public Vector3 TargetLocalPosition { get; set; }
        public Quaternion TargetLocalRotation => rotation.TargetRotation;
        internal AttachRotationPoseType RotationPoseType => rotation.PoseType;
        public Vector3 LinearSpringVelocity { get; set; }
        public Vector3 AngularSpringVelocity { get; set; }

        internal void StepRotation(AttachRotationDirection direction, float degrees)
            => rotation.Step(direction, degrees);

        internal void AdjustRotation(Vector3 axis, float degrees)
            => rotation.Adjust(axis, degrees);

        public bool Begin(AttachableObject target, Transform playerModel, AttachmentService service)
        {
            if (target == null || target.Body == null || target.Body.isKinematic ||
                playerModel == null || service == null)
            {
                return false;
            }

            HeldObject = target;
            service.SelectIsland(target);

            Vector3 playerToObject = target.Body.position - playerModel.position;
            float verticalOffset = playerToObject.y;
            playerToObject.y = 0f;
            if (playerToObject.sqrMagnitude > 0.000001f)
            {
                playerModel.rotation = Quaternion.LookRotation(playerToObject.normalized, Vector3.up);
            }

            TargetLocalPosition = new Vector3(0f, verticalOffset, playerToObject.magnitude);
            rotation.Begin(AttachmentRotationCatalog.FindNearest(
                Quaternion.Inverse(playerModel.rotation) * target.Body.rotation));
            LinearSpringVelocity = Vector3.zero;
            AngularSpringVelocity = Vector3.zero;
            return true;
        }

        public void Release(AttachmentService service)
        {
            if (HeldObject != null)
            {
                service?.DeselectIsland(HeldObject);
            }

            HeldObject = null;
            TargetLocalPosition = Vector3.zero;
            rotation.Reset();
            LinearSpringVelocity = Vector3.zero;
            AngularSpringVelocity = Vector3.zero;
        }
    }
}
