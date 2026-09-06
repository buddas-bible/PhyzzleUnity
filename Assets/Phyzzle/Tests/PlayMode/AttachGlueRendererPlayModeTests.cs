using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class AttachGlueRendererPlayModeTests
    {
        private GameObject root;
        private AttachmentService service;
        private AttachGlueRenderer glue;
        private Camera camera;
        private AttachSettings settings;
        private Material material;
        private AttachableObject first;
        private AttachableObject second;
        private RenderPipelineAsset pipeline;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Persistent Glue Test");
            settings = ScriptableObject.CreateInstance<AttachSettings>();
            service = root.AddComponent<AttachmentService>();
            service.Configure(settings);
            camera = new GameObject("Glue Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0f, 0f, -5f);
            material = new Material(Shader.Find("Phyzzle/AttachContactPreview"));
            pipeline = GraphicsSettings.currentRenderPipeline;
            Assert.That(pipeline, Is.Not.Null);
            glue = root.AddComponent<AttachGlueRenderer>();
            glue.Configure(camera, service, material, settings, pipeline);
            first = CreateAttachable("First", Vector3.left);
            second = CreateAttachable("Second", Vector3.right);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(material);
            Object.Destroy(settings);
            yield return null;
        }

        [Test]
        public void ConfirmedConnection_RendersWithoutHoldingAndDoesNotChangePhysics()
        {
            FixedJoint joint = Connect(first, second, Vector3.zero);
            int topology = service.TopologyVersion;
            Vector3 firstPosition = first.Body.position;
            Vector3 firstAnchor = joint.anchor;
            Vector3 secondAnchor = joint.connectedAnchor;
            first.Body.mass = 3f;
            first.Body.constraints = RigidbodyConstraints.FreezeRotation;
            first.Body.linearVelocity = Vector3.up;
            Assert.That(first.Body.linearVelocity, Is.EqualTo(Vector3.up), "The non-mutation check needs a nonzero input velocity.");

            glue.TickVisual();

            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
            Mesh mesh = GetMesh(joint);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            Assert.That(mesh.bounds.center.magnitude, Is.LessThan(0.0001f));
            Assert.That(first.Body.position, Is.EqualTo(firstPosition));
            Assert.That(first.Body.mass, Is.EqualTo(3f));
            Assert.That(first.Body.linearVelocity, Is.EqualTo(Vector3.up));
            Assert.That(first.IsSelected, Is.False);
            Assert.That(second.IsSelected, Is.False);
            Assert.That(service.TopologyVersion, Is.EqualTo(topology));
            Assert.That(joint.anchor, Is.EqualTo(firstAnchor));
            Assert.That(joint.connectedAnchor, Is.EqualTo(secondAnchor));
            Assert.That(joint.connectedBody, Is.SameAs(second.Body));
            Assert.That(joint.enableCollision, Is.False);
            glue.TickVisual();
            Assert.That(GetMesh(joint), Is.SameAs(mesh));
        }

        [Test]
        public void LocalJointAnchors_FollowScaledAndRotatedRenderedPoses()
        {
            bool autoSync = Physics.autoSyncTransforms;
            try
            {
                Physics.autoSyncTransforms = false;
                first.transform.localScale = new Vector3(2f, 3f, 4f);
                second.transform.localScale = new Vector3(0.5f, 2f, 3f);
                first.Body.interpolation = second.Body.interpolation = RigidbodyInterpolation.Interpolate;
                Physics.SyncTransforms();
                FixedJoint joint = Connect(first, second, Vector3.zero);
                Assert.That(joint.anchor.x, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(joint.connectedAnchor.x, Is.EqualTo(-2f).Within(0.0001f));
                Vector3 physicsPosition = first.Body.position;
                first.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0f), Quaternion.Euler(0f, 0f, 90f));
                second.transform.SetPositionAndRotation(new Vector3(3f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));

                glue.TickVisual();

                Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
                // First local anchor -> (0,3,0); second -> (3,-1,0), midpoint -> (1.5,1,0).
                Assert.That(Vector3.Distance(GetMesh(joint).bounds.center, new Vector3(1.5f, 1f, 0f)),
                    Is.LessThan(0.0001f));
                Assert.That(first.Body.position, Is.EqualTo(physicsPosition), "Visuals may read but never synchronize the bodies.");
            }
            finally
            {
                Physics.autoSyncTransforms = autoSync;
            }
        }

        [UnityTest]
        public IEnumerator DetachAndObjectDestruction_RemoveOnlyTheirGlue()
        {
            FixedJoint removed = Connect(first, second, Vector3.zero);
            AttachableObject third = CreateAttachable("Third", Vector3.right * 5f);
            AttachableObject fourth = CreateAttachable("Fourth", Vector3.right * 7f);
            FixedJoint surviving = Connect(third, fourth, Vector3.right * 6f);
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(2));
            Mesh removedMesh = GetMesh(removed);
            Mesh survivingMesh = GetMesh(surviving);

            Assert.That(service.Detach(first), Is.True);
            Assert.That(removed != null, Is.True, "Unity defers Destroy, but the service already removed this connection.");
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
            Assert.That(removedMesh.vertexCount, Is.Zero);
            Assert.That(GetMesh(surviving), Is.SameAs(survivingMesh));

            Object.Destroy(fourth.gameObject);
            yield return null;
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.Zero);
            Assert.That(survivingMesh == null || survivingMesh.vertexCount == 0, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyedJoint_IsRemovedEvenWithoutATopologyChange()
        {
            FixedJoint joint = Connect(first, second, Vector3.zero);
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
            Mesh mesh = GetMesh(joint);
            int topology = service.TopologyVersion;

            Object.Destroy(joint);
            yield return null;
            Assert.That(service.TopologyVersion, Is.EqualTo(topology));
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.Zero);
            Assert.That(mesh == null || mesh.vertexCount == 0, Is.True);
        }

        [Test]
        public void DisableAndUnsupportedPipeline_ClearAndRecoverFromTheActualConnections()
        {
            FixedJoint joint = Connect(first, second, Vector3.zero);
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
            Mesh mesh = GetMesh(joint);
            glue.enabled = false;
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(glue.LastSubmittedDrawCount, Is.Zero);
            glue.enabled = true;
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
            service.enabled = false;
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.Zero);
            service.enabled = true;
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
            camera.enabled = false;
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.Zero);
            camera.enabled = true;
            glue.Configure(camera, service, material, settings, null);
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.Zero);
            glue.Configure(camera, service, material, settings, pipeline);
            glue.TickVisual();
            Assert.That(glue.LastSubmittedDrawCount, Is.EqualTo(1));
            Assert.That(joint != null, Is.True, "Visual cleanup must not destroy the actual connection.");
        }

        private FixedJoint Connect(AttachableObject a, AttachableObject b, Vector3 anchor)
        {
            Assert.That(service.Attach(a, b, anchor), Is.True);
            FixedJoint[] joints = a.GetComponents<FixedJoint>();
            return joints[joints.Length - 1];
        }

        private Mesh GetMesh(FixedJoint joint)
        {
            FieldInfo field = typeof(AttachGlueRenderer).GetField("blobs", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            IDictionary blobs = (IDictionary)field.GetValue(glue);
            object blob = blobs[joint];
            Assert.That(blob, Is.Not.Null);
            return (Mesh)blob.GetType().GetProperty("Mesh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(blob);
        }

        private AttachableObject CreateAttachable(string name, Vector3 position)
        {
            GameObject target = new(name);
            target.transform.SetParent(root.transform);
            target.transform.position = position;
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            AttachableObject attachable = target.AddComponent<AttachableObject>();
            attachable.Configure(service);
            return attachable;
        }
    }
}
