using UnityEngine;
using static Phyzzle.Abilities.Attach.AttachRotationPoseType;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 단계 회전 입력을 부착 회전 자세 전이와 목표 회전 값으로 변환한다.
    /// </summary>
    internal sealed class AttachRotationStateMachine
    {
        private enum RotationOperation { XPositive, XNegative, YPositive, YNegative }

        /// <summary>
        /// 하나의 회전 입력에서 다음 자세와 적용할 회전 연산 순서를 정의한다.
        /// </summary>
        private readonly struct Transition
        {
            /// <summary>
            /// 다음 자세와 순차 적용할 회전 연산으로 전이 정보를 생성한다.
            /// </summary>
            internal Transition(AttachRotationPoseType next, params RotationOperation[] operations)
            {
                Next = next;
                Operations = operations;
            }

            internal AttachRotationPoseType Next { get; }
            internal RotationOperation[] Operations { get; }
        }

        private const RotationOperation Xp = RotationOperation.XPositive;
        private const RotationOperation Xn = RotationOperation.XNegative;
        private const RotationOperation Yp = RotationOperation.YPositive;
        private const RotationOperation Yn = RotationOperation.YNegative;

        private static readonly Transition[,] transitions =
        {
            { T(RotateX, Xp), T(None, Xp), T(RotateYX, Xp),
              T(RotateXY, Yp, Xp, Yn, Yn, Xp, Yp),
              T(RotateX_Y, Yn, Xp, Yp, Yp, Xp, Yn),
              T(RotateY, Xp), T(RotateZ, Xp), T(RotateY_X, Xp) },
            { T(RotateX, Xn), T(None, Xn), T(RotateY_X, Xn),
              T(RotateXY, Yp, Xn, Yn, Yn, Xn, Yp),
              T(RotateX_Y, Yn, Xn, Yp, Yp, Xn, Yn),
              T(RotateZ, Xn), T(RotateY, Xn), T(RotateYX, Xn) },
            { T(RotateY, Yp), T(RotateXY, Yp), T(None, Yp),
              T(RotateX, Yp), T(RotateZ, Yp),
              T(RotateYX, Xp, Yp, Xn, Xn, Yp, Xp),
              T(RotateY_X, Xn, Yp, Xp, Xp, Yp, Xn), T(RotateX_Y, Yp) },
            { T(RotateY, Yn), T(RotateX_Y, Yn), T(None, Yn),
              T(RotateZ, Yn), T(RotateX, Yn),
              T(RotateYX, Xp, Yn, Xn, Xn, Yn, Xp),
              T(RotateY_X, Xn, Yn, Xp, Xp, Yn, Xn), T(RotateXY, Yn) }
        };

        internal Quaternion TargetRotation { get; private set; } = Quaternion.identity;
        internal AttachRotationPoseType PoseType { get; private set; }

        /// <summary>
        /// 가장 가까운 기준 자세를 초기 목표 회전과 현재 자세로 설정한다.
        /// </summary>
        internal void Begin(AttachRotationPose pose)
        {
            TargetRotation = AttachmentRotationCatalog.Normalize(pose.Rotation);
            PoseType = pose.Type;
        }

        /// <summary>
        /// 방향 입력에 대응하는 전이를 찾아 목표 회전을 단계적으로 변경한다.
        /// </summary>
        internal void Step(AttachRotationDirection direction, float degrees)
        {
            Transition transition = transitions[(int)direction, (int)PoseType];
            foreach (RotationOperation operation in transition.Operations)
            {
                Apply(operation, degrees);
            }

            PoseType = transition.Next;
        }

        /// <summary>
        /// 지정한 축을 기준으로 목표 회전을 연속적으로 조정한다.
        /// </summary>
        internal void Adjust(Vector3 axis, float degrees)
        {
            TargetRotation = AttachmentRotationCatalog.Normalize(
                Quaternion.AngleAxis(degrees, axis) * TargetRotation);
        }

        /// <summary>
        /// 목표 회전과 자세 유형을 초기 상태로 되돌린다.
        /// </summary>
        internal void Reset()
        {
            TargetRotation = Quaternion.identity;
            PoseType = None;
        }

        /// <summary>
        /// 전이 테이블 초기화에 사용할 전이 정보를 간단히 생성한다.
        /// </summary>
        private static Transition T(AttachRotationPoseType next, params RotationOperation[] operations)
            => new(next, operations);

        /// <summary>
        /// 하나의 회전 연산을 목표 회전에 적용하고 결과를 정규화한다.
        /// </summary>
        private void Apply(RotationOperation operation, float degrees)
        {
            (float signedDegrees, Vector3 axis) = operation switch
            {
                RotationOperation.XPositive => (degrees, Vector3.right),
                RotationOperation.XNegative => (-degrees, Vector3.right),
                RotationOperation.YPositive => (degrees, Vector3.up),
                RotationOperation.YNegative => (-degrees, Vector3.up),
                _ => throw new System.ArgumentOutOfRangeException(nameof(operation))
            };
            TargetRotation = AttachmentRotationCatalog.Normalize(
                Quaternion.AngleAxis(signedDegrees, axis) * TargetRotation);
        }
    }
}
