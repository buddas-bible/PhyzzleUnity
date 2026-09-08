using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>AttachContactPreviewPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class AttachContactPreviewPlayModeTests
    {
        private Scene scene;
        private AttachmentService service;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.CreateScene(
                "Attach Contact Preview Test", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            GameObject serviceObject = new("Contact Preview Service");
            SceneManager.MoveGameObjectToScene(serviceObject, scene);
            service = serviceObject.AddComponent<AttachmentService>();
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        /// <summary>
        /// <c>ContactOnNonRootIslandMember_PreviewsTheActualJointAnchorWithoutMutatingBodies</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ContactOnNonRootIslandMember_PreviewsTheActualJointAnchorWithoutMutatingBodies()
        {
            AttachableObject root = CreateAttachable("Held Root", 0f);
            AttachableObject touchingMember = CreateAttachable("Touching Member", 3f);
            AttachableObject target = CreateAttachable("Target", 3.99f);
            Assert.That(service.Attach(root, touchingMember, new Vector3(1.5f, 0f, 0f)), Is.True);
            SimulateContacts();
            Assert.That(root.ContactCandidate, Is.Null);
            Assert.That(touchingMember.ContactCandidate, Is.SameAs(target));

            List<AttachableObject> island = new(service.GetIsland(root));
            int topologyBefore = service.TopologyVersion;
            Vector3 positionBefore = touchingMember.Body.position;
            Vector3 velocityBefore = touchingMember.Body.linearVelocity;

            Assert.That(service.TryGetPreviewContact(island, out AttachableObject member,
                out AttachableObject other, out Vector3 anchor), Is.True);
            Assert.That(member, Is.SameAs(touchingMember));
            Assert.That(other, Is.SameAs(target));
            Assert.That(anchor.x, Is.EqualTo(3.5f).Within(0.02f));
            Assert.That(service.TopologyVersion, Is.EqualTo(topologyBefore));
            Assert.That(touchingMember.Body.position, Is.EqualTo(positionBefore));
            Assert.That(touchingMember.Body.linearVelocity, Is.EqualTo(velocityBefore));
            Assert.That(touchingMember.ContactCandidate, Is.SameAs(target));
            Assert.That(touchingMember.GetComponent<FixedJoint>(), Is.Null);

            Assert.That(service.TryAttach(root), Is.True);
            FixedJoint joint = touchingMember.GetComponent<FixedJoint>();
            Assert.That(joint, Is.Not.Null);
            Assert.That(joint.connectedBody, Is.SameAs(target.Body));
            Assert.That(Vector3.Distance(touchingMember.transform.TransformPoint(joint.anchor), anchor),
                Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(target.transform.TransformPoint(joint.connectedAnchor), anchor),
                Is.LessThan(0.0001f));
            AssertNoContact(new List<AttachableObject>(service.GetIsland(root)));
        }

        /// <summary>
        /// <c>ContactAlreadyJoinedIntoHeldIsland_DoesNotPreviewASecondAttachment</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ContactAlreadyJoinedIntoHeldIsland_DoesNotPreviewASecondAttachment()
        {
            AttachableObject held = CreateAttachable("Held", 0f);
            AttachableObject target = CreateAttachable("Target", 0.99f);
            SimulateContacts();
            Assert.That(held.ContactCandidate, Is.SameAs(target));
            Assert.That(service.Attach(held, target, held.ContactAnchor), Is.True);

            // Attach itself leaves the old contact until the next physics callback.
            Assert.That(held.ContactCandidate, Is.SameAs(target));
            AssertNoContact(new List<AttachableObject>(service.GetIsland(held)));
        }

        /// <summary>
        /// <c>DisabledFirstCandidate_SuppressesPreviewUntilTheActionCanChooseNextMember</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void DisabledFirstCandidate_SuppressesPreviewUntilTheActionCanChooseNextMember()
        {
            AttachableObject root = CreateAttachable("Held Root", 0f);
            AttachableObject firstTarget = CreateAttachable("First Target", 0.99f);
            AttachableObject secondMember = CreateAttachable("Second Member", 3f);
            AttachableObject secondTarget = CreateAttachable("Second Target", 3.99f);
            service.Attach(root, secondMember, new Vector3(1.5f, 0f, 0f));
            SimulateContacts();
            Assert.That(root.ContactCandidate, Is.SameAs(firstTarget));
            Assert.That(secondMember.ContactCandidate, Is.SameAs(secondTarget));
            List<AttachableObject> island = new(service.GetIsland(root));

            Assert.That(service.TryGetPreviewContact(island, out AttachableObject firstMember,
                out AttachableObject firstOther, out _), Is.True);
            Assert.That(firstMember, Is.SameAs(root));
            Assert.That(firstOther, Is.SameAs(firstTarget));
            firstTarget.enabled = false;

            AssertNoContact(island);
            root.ClearContact(firstTarget);

            Assert.That(service.TryGetPreviewContact(island, out AttachableObject member,
                out AttachableObject other, out _), Is.True);
            Assert.That(member, Is.SameAs(secondMember));
            Assert.That(other, Is.SameAs(secondTarget));
        }

        /// <summary>
        /// <c>InactiveTarget_SuppressesTheStillCachedContact</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void InactiveTarget_SuppressesTheStillCachedContact()
        {
            AttachableObject held = CreateAttachable("Held", 0f);
            AttachableObject target = CreateAttachable("Target", 0.99f);
            SimulateContacts();
            List<AttachableObject> island = new(service.GetIsland(held));
            Assert.That(held.ContactCandidate, Is.SameAs(target));

            target.gameObject.SetActive(false);

            AssertNoContact(island);
        }

        /// <summary>
        /// <c>DestroyedTarget_SuppressesTheStillCachedContact</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyedTarget_SuppressesTheStillCachedContact()
        {
            AttachableObject held = CreateAttachable("Held", 0f);
            AttachableObject target = CreateAttachable("Target", 0.99f);
            SimulateContacts();
            List<AttachableObject> island = new(service.GetIsland(held));
            Assert.That(held.ContactCandidate, Is.SameAs(target));

            Object.Destroy(target.gameObject);
            yield return null;

            AssertNoContact(island);
        }

        /// <summary>
        /// <c>EmptyOrUncontactedIsland_ReturnsNoPreviewAndClearsOutputs</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void EmptyOrUncontactedIsland_ReturnsNoPreviewAndClearsOutputs()
        {
            AttachableObject held = CreateAttachable("Held", 0f);
            CreateAttachable("Nearby But Not Touching", 1.1f);
            SimulateContacts();

            AssertNoContact(null);
            AssertNoContact(new List<AttachableObject>());
            AssertNoContact(new List<AttachableObject> { null, held });
        }

        /// <summary>
        /// <c>AssertNoContact</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void AssertNoContact(IReadOnlyList<AttachableObject> island)
        {
            Assert.That(service.TryGetPreviewContact(island, out AttachableObject member,
                out AttachableObject other, out Vector3 anchor), Is.False);
            Assert.That(member, Is.Null);
            Assert.That(other, Is.Null);
            Assert.That(anchor, Is.EqualTo(Vector3.zero));
        }

        /// <summary>
        /// <c>CreateAttachable</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private AttachableObject CreateAttachable(string name, float x)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            SceneManager.MoveGameObjectToScene(target, scene);
            target.transform.position = new Vector3(x, 0f, 0f);
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            AttachableObject attachable = target.AddComponent<AttachableObject>();
            attachable.Configure(service);
            return attachable;
        }

        /// <summary>
        /// <c>SimulateContacts</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void SimulateContacts()
        {
            Physics.SyncTransforms();
            scene.GetPhysicsScene().Simulate(0.02f);
        }
    }
}
