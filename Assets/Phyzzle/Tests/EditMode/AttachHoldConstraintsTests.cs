using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class AttachHoldConstraintsTests
    {
        [Test]
        public void ClampTargetForBounds_PushesMinimumZThenAppliesCppScalarLimits()
        {
            Vector3 result = AttachHoldConstraints.ClampTargetForBounds(
                new Vector3(0f, 12f, 2f),
                0.25f,
                true,
                -4f,
                10f,
                1f,
                2.5f);

            Assert.That(result.y, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void TryComputeIslandLocalBounds_IncludesAttachedMember()
        {
            AttachSettings settings = ScriptableObject.CreateInstance<AttachSettings>();
            GameObject playerObject = new GameObject("Player");
            GameObject serviceObject = new GameObject("AttachmentService");
            GameObject rootObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject rearObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                AttachmentService service = serviceObject.AddComponent<AttachmentService>();
                service.Configure(settings);

                rootObject.transform.position = new Vector3(0f, 0f, 2f);
                rootObject.AddComponent<Rigidbody>().useGravity = false;
                AttachableObject root = rootObject.AddComponent<AttachableObject>();
                root.Configure(service);

                rearObject.transform.position = Vector3.zero;
                rearObject.AddComponent<Rigidbody>().useGravity = false;
                AttachableObject rear = rearObject.AddComponent<AttachableObject>();
                rear.Configure(service);
                Physics.SyncTransforms();
                Assert.That(service.Attach(root, rear, Vector3.forward), Is.True);

                bool found = AttachHoldConstraints.TryComputeIslandLocalBounds(
                    root,
                    service.GetIsland(root),
                    playerObject.transform,
                    new Vector3(0f, 0f, 2f),
                    Quaternion.identity,
                    out Bounds bounds);

                Assert.That(found, Is.True);
                Assert.That(bounds.min.z, Is.EqualTo(-0.5f).Within(0.01f));
                Assert.That(bounds.max.z, Is.EqualTo(2.5f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(rearObject);
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void LimitOrbit_TargetWithinOffset_ReturnsDesiredRotation()
        {
            Quaternion desired = Quaternion.Euler(0f, 15f, 0f);
            Vector3 target = desired * new Vector3(0f, 0f, 5f);
            Quaternion result = AttachHoldConstraints.LimitOrbit(
                Quaternion.identity, desired, Vector3.zero, new Vector3(0f, 0f, 5f),
                target - Vector3.forward * 0.25f, 1f);

            Assert.That(Quaternion.Angle(result, desired), Is.LessThan(0.001f));
        }

        [Test]
        public void LimitOrbit_TargetBeyondOffset_UsesCppOffsetDirection()
        {
            Quaternion current = Quaternion.identity;
            Quaternion desired = Quaternion.Euler(0f, 90f, 0f);
            Vector3 objectPosition = new Vector3(0f, 0f, 3f);
            Vector3 desiredWorldTarget = desired * new Vector3(0f, 0f, 5f);
            Vector3 planarDirection = desiredWorldTarget - objectPosition;
            planarDirection.y = 0f;
            Vector3 offsetTarget = objectPosition + planarDirection.normalized;
            Quaternion expected = Quaternion.LookRotation(offsetTarget.normalized, Vector3.up);

            Quaternion result = AttachHoldConstraints.LimitOrbit(
                current, desired, Vector3.zero, new Vector3(0f, 0f, 5f), objectPosition, 1f);

            Assert.That(Quaternion.Angle(result, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void LimitOrbit_ZeroDirection_ReturnsFiniteCurrentRotation()
        {
            Quaternion current = Quaternion.Euler(0f, 37f, 0f);
            Quaternion result = AttachHoldConstraints.LimitOrbit(
                current, current, Vector3.zero, Vector3.zero, Vector3.zero, 0f);

            Assert.That(float.IsFinite(result.x), Is.True);
            Assert.That(float.IsFinite(result.y), Is.True);
            Assert.That(float.IsFinite(result.z), Is.True);
            Assert.That(float.IsFinite(result.w), Is.True);
            Assert.That(Quaternion.Angle(result, current), Is.LessThan(0.001f));
        }

        [Test]
        public void LimitLift_TargetBeyondOffset_ClampsWorldHeightFromObject()
        {
            Vector3 result = AttachHoldConstraints.LimitLift(
                new Vector3(0f, 4f, 5f),
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0f, 0f, 5f),
                1f);

            Assert.That(result.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void LimitLift_TargetWithinOffset_PreservesCandidate()
        {
            Vector3 candidate = new Vector3(0f, 0.5f, 5f);
            Vector3 result = AttachHoldConstraints.LimitLift(
                candidate, Vector3.zero, Quaternion.identity, new Vector3(0f, 0f, 5f), 1f);

            Assert.That(Vector3.Distance(result, candidate), Is.LessThan(0.0001f));
        }
    }
}
