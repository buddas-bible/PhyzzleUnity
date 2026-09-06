using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class AttachmentJointSnapshotPlayModeTests
    {
        private readonly List<GameObject> objects = new();
        private readonly List<FixedJoint> snapshot = new();
        private AttachmentService service;
        private AttachableObject first;
        private AttachableObject second;
        private AttachableObject third;

        [SetUp]
        public void SetUp()
        {
            GameObject serviceObject = new("Attachment Service");
            objects.Add(serviceObject);
            service = serviceObject.AddComponent<AttachmentService>();

            first = CreateAttachable("First", Vector3.zero);
            second = CreateAttachable("Second", Vector3.right);
            third = CreateAttachable("Third", Vector3.right * 2f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject target in objects)
            {
                Object.Destroy(target);
            }

            objects.Clear();
            snapshot.Clear();
            yield return null;
        }

        [Test]
        public void CopyConnectionJoints_ClearsDestinationAndCopiesOnlyServiceJointsOnce()
        {
            FixedJoint arbitraryJoint = first.gameObject.AddComponent<FixedJoint>();
            arbitraryJoint.connectedBody = third.Body;
            Assert.That(service.Attach(first, second, Vector3.right * 0.5f), Is.True);
            Assert.That(service.Attach(second, third, Vector3.right * 1.5f), Is.True);
            FixedJoint firstServiceJoint = FindJoint(first, second.Body, arbitraryJoint);
            FixedJoint secondServiceJoint = FindJoint(second, third.Body);
            snapshot.Add(arbitraryJoint);

            service.CopyConnectionJoints(snapshot);

            Assert.That(snapshot, Is.EquivalentTo(new[] { firstServiceJoint, secondServiceJoint }));
            CollectionAssert.AllItemsAreUnique(snapshot);
            CollectionAssert.DoesNotContain(snapshot, arbitraryJoint);
        }

        [Test]
        public void CopyConnectionJoints_AfterDetachExcludesJointBeforeDeferredDestroy()
        {
            Assert.That(service.Attach(first, second, Vector3.right * 0.5f), Is.True);
            FixedJoint joint = FindJoint(first, second.Body);
            Assert.That(service.Detach(first), Is.True);
            Assert.That(joint, Is.Not.Null, "Destroy should still be deferred in play mode.");

            service.CopyConnectionJoints(snapshot);

            Assert.That(snapshot, Is.Empty);
        }

        [Test]
        public void CopyConnectionJoints_ExcludesDestroyedOrDisconnectedJoints()
        {
            Assert.That(service.Attach(first, second, Vector3.right * 0.5f), Is.True);
            FixedJoint destroyedJoint = FindJoint(first, second.Body);
            Assert.That(service.Attach(second, third, Vector3.right * 1.5f), Is.True);
            FixedJoint disconnectedJoint = FindJoint(second, third.Body);
            Object.DestroyImmediate(destroyedJoint);
            disconnectedJoint.connectedBody = null;

            service.CopyConnectionJoints(snapshot);

            Assert.That(snapshot, Is.Empty);
        }

        [Test]
        public void CopyConnectionJoints_DoesNotMutateTopologyOrJointPhysics()
        {
            Vector3 anchor = new(0.5f, 0.25f, -0.125f);
            Assert.That(service.Attach(first, second, anchor), Is.True);
            FixedJoint joint = FindJoint(first, second.Body);
            int topologyVersion = service.TopologyVersion;
            Vector3 localAnchor = joint.anchor;
            Vector3 connectedAnchor = joint.connectedAnchor;
            Rigidbody connectedBody = joint.connectedBody;

            service.CopyConnectionJoints(snapshot);

            Assert.That(service.TopologyVersion, Is.EqualTo(topologyVersion));
            Assert.That(service.AreInSameIsland(first, second), Is.True);
            Assert.That(joint.connectedBody, Is.SameAs(connectedBody));
            Assert.That(joint.anchor, Is.EqualTo(localAnchor));
            Assert.That(joint.connectedAnchor, Is.EqualTo(connectedAnchor));
        }

        private AttachableObject CreateAttachable(string name, Vector3 position)
        {
            GameObject target = new(name);
            objects.Add(target);
            target.transform.position = position;
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            AttachableObject attachable = target.AddComponent<AttachableObject>();
            attachable.Configure(service);
            return attachable;
        }

        private static FixedJoint FindJoint(
            AttachableObject owner,
            Rigidbody connectedBody,
            FixedJoint excluded = null)
        {
            foreach (FixedJoint joint in owner.GetComponents<FixedJoint>())
            {
                if (joint != excluded && joint.connectedBody == connectedBody)
                {
                    return joint;
                }
            }

            Assert.Fail("Expected service-created joint was not found.");
            return null;
        }
    }
}
