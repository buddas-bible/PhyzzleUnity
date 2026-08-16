using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using static Phyzzle.Abilities.Attach.AttachRotationDirection;
using static Phyzzle.Abilities.Attach.AttachRotationPoseType;

namespace Phyzzle.Tests
{
    public sealed class AttachRotationStateMachineTests
    {
        private static readonly object[] Cases =
        {
            C(None, Up, RotateX, "X+"), C(RotateX, Up, None, "X+"),
            C(RotateY, Up, RotateYX, "X+"),
            C(RotateX_Y, Up, RotateXY, "Y+ X+ Y- Y- X+ Y+"),
            C(RotateXY, Up, RotateX_Y, "Y- X+ Y+ Y+ X+ Y-"),
            C(RotateY_X, Up, RotateY, "X+"), C(RotateYX, Up, RotateZ, "X+"),
            C(RotateZ, Up, RotateY_X, "X+"),

            C(None, Down, RotateX, "X-"), C(RotateX, Down, None, "X-"),
            C(RotateY, Down, RotateY_X, "X-"),
            C(RotateX_Y, Down, RotateXY, "Y+ X- Y- Y- X- Y+"),
            C(RotateXY, Down, RotateX_Y, "Y- X- Y+ Y+ X- Y-"),
            C(RotateY_X, Down, RotateZ, "X-"), C(RotateYX, Down, RotateY, "X-"),
            C(RotateZ, Down, RotateYX, "X-"),

            C(None, Left, RotateY, "Y+"), C(RotateX, Left, RotateXY, "Y+"),
            C(RotateY, Left, None, "Y+"), C(RotateX_Y, Left, RotateX, "Y+"),
            C(RotateXY, Left, RotateZ, "Y+"),
            C(RotateY_X, Left, RotateYX, "X+ Y+ X- X- Y+ X+"),
            C(RotateYX, Left, RotateY_X, "X- Y+ X+ X+ Y+ X-"),
            C(RotateZ, Left, RotateX_Y, "Y+"),

            C(None, Right, RotateY, "Y-"), C(RotateX, Right, RotateX_Y, "Y-"),
            C(RotateY, Right, None, "Y-"), C(RotateX_Y, Right, RotateZ, "Y-"),
            C(RotateXY, Right, RotateX, "Y-"),
            C(RotateY_X, Right, RotateYX, "X+ Y- X- X- Y- X+"),
            C(RotateYX, Right, RotateY_X, "X- Y- X+ X+ Y- X-"),
            C(RotateZ, Right, RotateXY, "Y-")
        };

        [TestCaseSource(nameof(Cases))]
        public void Step_MatchesCppTransition(
            int startValue,
            int directionValue,
            int expectedTypeValue,
            string operations)
        {
            AttachRotationPoseType start = (AttachRotationPoseType)startValue;
            AttachRotationPoseType expectedType = (AttachRotationPoseType)expectedTypeValue;
            Quaternion seed = Quaternion.Euler(13f, 27f, -19f);
            AttachRotationStateMachine machine = new();
            machine.Begin(new AttachRotationPose(seed, start));

            machine.Step((AttachRotationDirection)directionValue, 45f);

            Assert.That(machine.PoseType, Is.EqualTo(expectedType));
            Assert.That(Quaternion.Angle(machine.TargetRotation,
                ApplyLiteralOperations(seed, operations, 45f)), Is.LessThan(0.001f));
        }

        [Test]
        public void Begin_NormalizesRotationWhilePreservingPoseType()
        {
            AttachRotationStateMachine machine = new();
            Quaternion input = new(1f, -2f, 3f, -4f);

            machine.Begin(new AttachRotationPose(input, RotateYX));

            Assert.That(machine.PoseType, Is.EqualTo(RotateYX));
            Assert.That(Mathf.Abs(machine.TargetRotation.x * machine.TargetRotation.x +
                machine.TargetRotation.y * machine.TargetRotation.y +
                machine.TargetRotation.z * machine.TargetRotation.z +
                machine.TargetRotation.w * machine.TargetRotation.w - 1f), Is.LessThan(0.0001f));
        }

        [Test]
        public void Reset_ReturnsIdentityAndNonePose()
        {
            AttachRotationStateMachine machine = new();
            machine.Begin(new AttachRotationPose(Quaternion.Euler(13f, 27f, -19f), RotateXY));

            machine.Reset();

            Assert.That(machine.PoseType, Is.EqualTo(None));
            Assert.That(Quaternion.Angle(machine.TargetRotation, Quaternion.identity), Is.LessThan(0.001f));
        }

        [Test]
        public void Adjust_PreMultipliesWithoutReclassifyingPoseType()
        {
            Quaternion seed = Quaternion.Euler(13f, 27f, -19f);
            AttachRotationStateMachine machine = new();
            machine.Begin(new AttachRotationPose(seed, RotateX_Y));

            machine.Adjust(Vector3.up, 17f);

            Assert.That(machine.PoseType, Is.EqualTo(RotateX_Y));
            Assert.That(Quaternion.Angle(machine.TargetRotation,
                Quaternion.AngleAxis(17f, Vector3.up) * seed), Is.LessThan(0.001f));
        }

        [Test]
        public void Begin_InvalidQuaternion_RemainsFinite()
        {
            AttachRotationStateMachine machine = new();

            machine.Begin(new AttachRotationPose(new Quaternion(float.NaN, 0f, 0f, 0f), RotateZ));

            Assert.That(float.IsFinite(machine.TargetRotation.x), Is.True);
            Assert.That(float.IsFinite(machine.TargetRotation.y), Is.True);
            Assert.That(float.IsFinite(machine.TargetRotation.z), Is.True);
            Assert.That(float.IsFinite(machine.TargetRotation.w), Is.True);
            Assert.That(Quaternion.Angle(machine.TargetRotation, Quaternion.identity), Is.LessThan(0.001f));
        }

        private static object[] C(
            AttachRotationPoseType start,
            AttachRotationDirection direction,
            AttachRotationPoseType expectedType,
            string operations) => new object[] { (int)start, (int)direction, (int)expectedType, operations };

        private static Quaternion ApplyLiteralOperations(Quaternion rotation, string operations, float degrees)
        {
            foreach (string operation in operations.Split(' '))
            {
                rotation = operation switch
                {
                    "X+" => Quaternion.AngleAxis(degrees, Vector3.right) * rotation,
                    "X-" => Quaternion.AngleAxis(-degrees, Vector3.right) * rotation,
                    "Y+" => Quaternion.AngleAxis(degrees, Vector3.up) * rotation,
                    "Y-" => Quaternion.AngleAxis(-degrees, Vector3.up) * rotation,
                    _ => throw new AssertionException($"Unknown literal operation: {operation}")
                };
            }

            return rotation;
        }
    }
}
