using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    internal enum AttachRotationPoseType
    {
        None,
        RotateX,
        RotateY,
        RotateX_Y,
        RotateXY,
        RotateY_X,
        RotateYX,
        RotateZ
    }

    internal enum AttachRotationDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    internal readonly struct AttachRotationPose
    {
        public AttachRotationPose(Quaternion rotation, AttachRotationPoseType type)
        {
            Rotation = rotation;
            Type = type;
        }

        public Quaternion Rotation { get; }
        public AttachRotationPoseType Type { get; }
    }
}
