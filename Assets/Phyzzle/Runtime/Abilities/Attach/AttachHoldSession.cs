using Phyzzle.Compatibility;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 하나의 부착 대상에 대한 들기 상태와 목표 자세, 스프링 속도를 보관한다.
    /// </summary>
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

        /// <summary>
        /// 지정한 방향으로 목표 회전을 한 단계 변경한다.
        /// </summary>
        internal void StepRotation(AttachRotationDirection direction, float degrees)
            => rotation.Step(direction, degrees);

        /// <summary>
        /// 지정한 축을 기준으로 목표 회전을 연속적으로 조정한다.
        /// </summary>
        internal void AdjustRotation(Vector3 axis, float degrees)
            => rotation.Adjust(axis, degrees);

        /// <summary>
        /// 지정한 대상을 선택하고 플레이어 기준의 초기 들기 위치와 회전을 설정한다.
        /// </summary>
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

        /// <summary>
        /// 선택 상태와 목표 자세, 스프링 속도를 초기화해 들기 세션을 종료한다.
        /// </summary>
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
