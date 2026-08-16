using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    internal static class AttachmentRotationCatalog
    {
        private static readonly IReadOnlyList<AttachRotationPose> poses = Build();
        internal static IReadOnlyList<AttachRotationPose> Poses => poses;

        internal static AttachRotationPose FindNearest(Quaternion rotation)
        {
            Quaternion normalized = Normalize(rotation);
            AttachRotationPose best = poses[0];
            float bestDot = -1f;
            foreach (AttachRotationPose pose in poses)
            {
                float dot = Mathf.Abs(Quaternion.Dot(normalized, pose.Rotation));
                if (dot >= bestDot)
                {
                    bestDot = dot;
                    best = pose;
                }
            }

            return best;
        }

        private static IReadOnlyList<AttachRotationPose> Build()
        {
            List<Quaternion> majorAxes = new();
            for (int y = 0; y < 360; y += 90)
            {
                majorAxes.Add(Quaternion.AngleAxis(y, Vector3.up));
            }
            majorAxes.Add(Quaternion.AngleAxis(-90f, Vector3.right));
            majorAxes.Add(Quaternion.AngleAxis(90f, Vector3.right));

            List<Quaternion> none = new(24);
            foreach (Quaternion majorAxis in majorAxes)
            {
                for (int z = 0; z < 360; z += 90)
                {
                    none.Add(Quaternion.AngleAxis(z, Vector3.forward) * majorAxis);
                }
            }

            List<AttachRotationPose> result = new(192);
            Add(result, none, AttachRotationPoseType.None);
            List<Quaternion> rotateX = Multiply(none, 45f, Vector3.right);
            Add(result, rotateX, AttachRotationPoseType.RotateX);
            List<Quaternion> rotateY = Multiply(none, 45f, Vector3.up);
            Add(result, rotateY, AttachRotationPoseType.RotateY);
            Add(result, Multiply(rotateX, 45f, Vector3.up), AttachRotationPoseType.RotateXY);
            Add(result, Multiply(rotateX, -45f, Vector3.up), AttachRotationPoseType.RotateX_Y);
            Add(result, Multiply(rotateY, 45f, Vector3.right), AttachRotationPoseType.RotateYX);
            Add(result, Multiply(rotateY, -45f, Vector3.right), AttachRotationPoseType.RotateY_X);
            Add(result, Multiply(none, 45f, Vector3.forward), AttachRotationPoseType.RotateZ);
            return result.AsReadOnly();
        }

        private static void Add(
            ICollection<AttachRotationPose> output,
            IEnumerable<Quaternion> rotations,
            AttachRotationPoseType type)
        {
            foreach (Quaternion rotation in rotations)
            {
                output.Add(new AttachRotationPose(Normalize(rotation), type));
            }
        }

        private static List<Quaternion> Multiply(
            IEnumerable<Quaternion> source, float degrees, Vector3 axis)
        {
            Quaternion step = Quaternion.AngleAxis(degrees, axis);
            List<Quaternion> output = new();
            foreach (Quaternion rotation in source)
            {
                output.Add(step * rotation);
            }
            return output;
        }

        internal static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y +
                                         value.z * value.z + value.w * value.w);
            if (!float.IsFinite(magnitude) || magnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            float inverse = 1f / magnitude;
            return new Quaternion(value.x * inverse, value.y * inverse,
                value.z * inverse, value.w * inverse);
        }
    }
}
