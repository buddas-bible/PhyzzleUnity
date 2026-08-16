using UnityEngine;
using static Phyzzle.Abilities.Attach.AttachRotationPoseType;

namespace Phyzzle.Abilities.Attach
{
    internal sealed class AttachRotationStateMachine
    {
        private enum RotationOperation { XPositive, XNegative, YPositive, YNegative }

        private readonly struct Transition
        {
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

        internal void Begin(AttachRotationPose pose)
        {
            TargetRotation = AttachmentRotationCatalog.Normalize(pose.Rotation);
            PoseType = pose.Type;
        }

        internal void Step(AttachRotationDirection direction, float degrees)
        {
            Transition transition = transitions[(int)direction, (int)PoseType];
            foreach (RotationOperation operation in transition.Operations)
            {
                Apply(operation, degrees);
            }

            PoseType = transition.Next;
        }

        internal void Adjust(Vector3 axis, float degrees)
        {
            TargetRotation = AttachmentRotationCatalog.Normalize(
                Quaternion.AngleAxis(degrees, axis) * TargetRotation);
        }

        internal void Reset()
        {
            TargetRotation = Quaternion.identity;
            PoseType = None;
        }

        private static Transition T(AttachRotationPoseType next, params RotationOperation[] operations)
            => new(next, operations);

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
