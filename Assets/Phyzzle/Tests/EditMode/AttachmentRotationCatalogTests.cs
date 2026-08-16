using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class AttachmentRotationCatalogTests
    {
        [Test]
        public void Poses_PreservesEightOrderedGroupsOfTwentyFour()
        {
            Assert.That(AttachmentRotationCatalog.Poses.Count, Is.EqualTo(192));
            AttachRotationPoseType[] groups =
            {
                AttachRotationPoseType.None,
                AttachRotationPoseType.RotateX,
                AttachRotationPoseType.RotateY,
                AttachRotationPoseType.RotateXY,
                AttachRotationPoseType.RotateX_Y,
                AttachRotationPoseType.RotateYX,
                AttachRotationPoseType.RotateY_X,
                AttachRotationPoseType.RotateZ
            };

            for (int group = 0; group < groups.Length; group++)
            {
                for (int entry = 0; entry < 24; entry++)
                {
                    Assert.That(
                        AttachmentRotationCatalog.Poses[group * 24 + entry].Type,
                        Is.EqualTo(groups[group]),
                        $"group={group}, entry={entry}");
                }
            }
        }

        [TestCase(0f, 0f, 0f, (int)AttachRotationPoseType.None)]
        [TestCase(45f, 0f, 0f, (int)AttachRotationPoseType.RotateX)]
        [TestCase(0f, 45f, 0f, (int)AttachRotationPoseType.RotateY)]
        [TestCase(0f, 0f, 45f, (int)AttachRotationPoseType.RotateZ)]
        public void FindNearest_ReturnsLegacyPoseType(
            float x, float y, float z, int expectedTypeValue)
        {
            AttachRotationPoseType expectedType = (AttachRotationPoseType)expectedTypeValue;
            AttachRotationPose pose = AttachmentRotationCatalog.FindNearest(
                Quaternion.Euler(x, y, z));

            Assert.That(pose.Type, Is.EqualTo(expectedType));
            Assert.That(Quaternion.Angle(pose.Rotation, Quaternion.Euler(x, y, z)),
                Is.LessThan(0.01f));
        }

        [Test]
        public void FindNearest_OppositeQuaternionSign_ReturnsSameCatalogEntry()
        {
            Quaternion input = Quaternion.Euler(45f, 90f, 0f);
            AttachRotationPose positive = AttachmentRotationCatalog.FindNearest(input);
            AttachRotationPose negative = AttachmentRotationCatalog.FindNearest(
                new Quaternion(-input.x, -input.y, -input.z, -input.w));

            Assert.That(negative.Type, Is.EqualTo(positive.Type));
            Assert.That(Quaternion.Angle(negative.Rotation, positive.Rotation), Is.LessThan(0.001f));
        }

        [Test]
        public void FindNearest_ExactTieUsesLaterLegacyCatalogEntry()
        {
            AttachRotationPose pose = AttachmentRotationCatalog.FindNearest(
                Quaternion.AngleAxis(22.5f, Vector3.right));

            Assert.That(pose.Type, Is.EqualTo(AttachRotationPoseType.RotateX));
            Assert.That(Quaternion.Angle(pose.Rotation,
                Quaternion.AngleAxis(45f, Vector3.right)), Is.LessThan(0.001f));
        }
    }
}
