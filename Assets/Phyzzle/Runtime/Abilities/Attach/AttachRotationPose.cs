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

    /// <summary>
    /// 부착 오브젝트의 정규화된 회전과 해당 회전 자세 유형을 함께 보관한다.
    /// </summary>
    internal readonly struct AttachRotationPose
    {
        /// <summary>
        /// 회전 값과 자세 유형으로 부착 회전 자세를 생성한다.
        /// </summary>
        public AttachRotationPose(Quaternion rotation, AttachRotationPoseType type)
        {
            Rotation = rotation;
            Type = type;
        }

        public Quaternion Rotation { get; }
        public AttachRotationPoseType Type { get; }
    }
}
