using System.Collections;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class AttachmentServicePlayModeTests
    {
        private GameObject serviceObject;
        private GameObject firstObject;
        private GameObject secondObject;
        private AttachSettings settings;
        private AttachmentService service;
        private AttachableObject first;
        private AttachableObject second;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<AttachSettings>();
            settings.selectedMass = 0.1f;
            settings.selectedInertiaTensor = new Vector3(100f, 100f, 100f);

            serviceObject = new GameObject("Attachment Service");
            service = serviceObject.AddComponent<AttachmentService>();
            service.Configure(settings);

            first = CreateAttachable("First", Vector3.zero, 3f, out firstObject);
            second = CreateAttachable("Second", Vector3.right, 5f, out secondObject);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(firstObject);
            Object.Destroy(secondObject);
            Object.Destroy(serviceObject);
            Object.Destroy(settings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttachAndDetach_CreateJointAndSplitIsland()
        {
            Assert.That(service.Attach(first, second, Vector3.right * 0.5f), Is.True);
            Assert.That(service.GetIsland(first).Count, Is.EqualTo(2));
            Assert.That(first.GetComponent<FixedJoint>(), Is.Not.Null);

            Assert.That(service.Detach(first), Is.True);
            yield return null;

            Assert.That(service.GetIsland(first).Count, Is.EqualTo(1));
            Assert.That(service.GetIsland(second).Count, Is.EqualTo(1));
            Assert.That(first.GetComponent<FixedJoint>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SelectAndDeselectIsland_RestoresRigidBodyProperties()
        {
            service.Attach(first, second, Vector3.right * 0.5f);
            Rigidbody firstBody = first.Body;
            Rigidbody secondBody = second.Body;

            service.SelectIsland(first);
            Assert.That(firstBody.mass, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(secondBody.mass, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(firstBody.useGravity, Is.False);
            Assert.That(secondBody.useGravity, Is.False);

            service.DeselectIsland(first);
            yield return null;

            Assert.That(firstBody.mass, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(secondBody.mass, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(firstBody.useGravity, Is.True);
            Assert.That(secondBody.useGravity, Is.True);
        }

        [UnityTest]
        public IEnumerator Attach_ScaledObjects_RemainAtContactPose()
        {
            firstObject.transform.position = Vector3.zero;
            secondObject.transform.position = Vector3.right * 2f;
            firstObject.transform.localScale = Vector3.one * 2f;
            secondObject.transform.localScale = Vector3.one * 2f;
            first.Body.useGravity = false;
            second.Body.useGravity = false;
            Physics.SyncTransforms();

            float initialSeparation = Vector3.Distance(first.Body.position, second.Body.position);
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);

            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float finalSeparation = Vector3.Distance(first.Body.position, second.Body.position);
            Assert.That(finalSeparation, Is.EqualTo(initialSeparation).Within(0.01f));
        }

        private AttachableObject CreateAttachable(
            string name,
            Vector3 position,
            float mass,
            out GameObject targetObject)
        {
            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = name;
            targetObject.transform.position = position;
            Rigidbody body = targetObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = true;
            AttachableObject attachable = targetObject.AddComponent<AttachableObject>();
            attachable.Configure(service);
            return attachable;
        }
    }
}
