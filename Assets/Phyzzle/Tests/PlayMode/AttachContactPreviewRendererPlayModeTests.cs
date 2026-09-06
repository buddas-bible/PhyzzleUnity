using System.Collections;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class AttachContactPreviewRendererPlayModeTests
    {
        private GameObject root;
        private Camera camera;
        private AttachableObject member;
        private AttachableObject other;
        private AttachSettings settings;
        private Material material;
        private AttachContactPreviewRenderer preview;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Attach Contact Preview Test");
            camera = CreateChild("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            member = CreateChild("Member").AddComponent<AttachableObject>();
            other = CreateChild("Other").AddComponent<AttachableObject>();
            member.Body.useGravity = other.Body.useGravity = false;
            member.transform.position = Vector3.left;
            other.transform.position = Vector3.right;
            Physics.SyncTransforms();
            settings = ScriptableObject.CreateInstance<AttachSettings>();
            // A stock material lets the geometry/lifecycle checks fail independently of shader import.
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            preview = root.AddComponent<AttachContactPreviewRenderer>();
            preview.Configure(camera, material, settings);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CoincidentContactAnchors_StillProduceVisibleGeometry()
        {
            preview.Submit(member, other, Vector3.zero, 1f);

            Assert.That(preview.LastSubmittedDrawCount, Is.EqualTo(1));
            Assert.That(preview.PreviewMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(preview.PreviewMesh.bounds.size.x, Is.GreaterThan(0.1f));
            Assert.That(preview.PreviewMesh.bounds.size.y, Is.GreaterThan(0.1f));
            Assert.That(preview.PreviewMesh.bounds.size.z, Is.GreaterThan(0.1f));
            Assert.That(preview.RenderedMemberAnchor.magnitude, Is.LessThan(0.0001f));
            Assert.That(preview.RenderedOtherAnchor.magnitude, Is.LessThan(0.0001f));
            Assert.That(preview.PreviewMesh.bounds.size.x, Is.LessThan(0.6f));
            Assert.That(preview.PreviewMesh.bounds.size.y, Is.LessThan(0.6f));
            Assert.That(preview.PreviewMesh.bounds.size.z, Is.LessThan(0.6f));
            Assert.That(preview.PreviewMesh.bounds.center.magnitude, Is.LessThan(0.0001f));
        }

        [Test]
        public void ContactAnchors_FollowBothRenderedPosesWithoutApplyingScaleTwice()
        {
            bool previousAutoSync = Physics.autoSyncTransforms;
            try
            {
                Physics.autoSyncTransforms = false;
                member.transform.localScale = new Vector3(2f, 3f, 4f);
                other.transform.localScale = new Vector3(0.5f, 2f, 3f);
                member.Body.interpolation = other.Body.interpolation = RigidbodyInterpolation.Interpolate;
                Physics.SyncTransforms();
                Vector3 memberPhysics = member.Body.position;
                Vector3 otherPhysics = other.Body.position;
                Quaternion quarterTurn = Quaternion.Euler(0f, 0f, 90f);
                member.transform.SetPositionAndRotation(new Vector3(-1f, 2f, 0f), quarterTurn);
                other.transform.SetPositionAndRotation(new Vector3(2f, 0f, 0f), quarterTurn);
                Assert.That(Vector3.Distance(member.transform.position, member.Body.position), Is.GreaterThan(1f));

                preview.Submit(member, other, Vector3.zero, 1f);

                Assert.That(Vector3.Distance(preview.RenderedMemberAnchor, new Vector3(-1f, 3f, 0f)),
                    Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(preview.RenderedOtherAnchor, new Vector3(2f, -1f, 0f)),
                    Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(preview.PreviewMesh.bounds.center, new Vector3(0.5f, 1f, 0f)),
                    Is.LessThan(0.0001f));
                Assert.That(member.Body.position, Is.EqualTo(memberPhysics));
                Assert.That(other.Body.position, Is.EqualTo(otherPhysics));
            }
            finally
            {
                Physics.autoSyncTransforms = previousAutoSync;
            }
        }

        [Test]
        public void Submit_DoesNotSelectMoveOrConnectEitherBody()
        {
            foreach (AttachableObject body in new[] { member, other })
            {
                body.Body.mass = 3f;
                body.Body.linearVelocity = new Vector3(1f, 2f, 3f);
                body.Body.angularVelocity = Vector3.up;
            }

            preview.Submit(member, other, Vector3.zero, 1f);

            Assert.That(member.Body.position, Is.EqualTo(Vector3.left));
            Assert.That(other.Body.position, Is.EqualTo(Vector3.right));
            foreach (AttachableObject body in new[] { member, other })
            {
                Assert.That(body.Body.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(body.Body.mass, Is.EqualTo(3f));
                Assert.That(body.Body.linearVelocity, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(body.Body.angularVelocity, Is.EqualTo(Vector3.up));
                Assert.That(body.Body.useGravity, Is.False);
                Assert.That(body.Body.isKinematic, Is.False);
                Assert.That(body.IsSelected, Is.False);
                Assert.That(body.GetComponents<Joint>(), Is.Empty);
            }
        }

        [Test]
        public void ClearAndDisable_RemoveQueuedGeometryAndReuseTheMesh()
        {
            preview.Submit(member, other, Vector3.zero, 1f);
            Mesh mesh = preview.PreviewMesh;
            Assert.That(mesh, Is.Not.Null);
            preview.Clear();
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(preview.LastSubmittedDrawCount, Is.Zero);

            preview.Submit(member, other, Vector3.zero, 1f);
            Assert.That(preview.PreviewMesh, Is.SameAs(mesh));
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            preview.enabled = false;
            preview.Submit(member, other, Vector3.zero, 1f);
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(preview.LastSubmittedDrawCount, Is.Zero);
        }

        [Test]
        public void InvalidContactCameraOrBlend_RemovesThePreviousPreview()
        {
            preview.Submit(member, other, Vector3.zero, 1f);
            Mesh mesh = preview.PreviewMesh;
            Assert.That(mesh, Is.Not.Null);
            foreach (float blend in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                preview.Submit(member, other, Vector3.zero, 1f);
                preview.Submit(member, other, Vector3.zero, blend);
                Assert.That(mesh.vertexCount, Is.Zero);
            }
            foreach (Vector3 anchor in new[] { new Vector3(float.NaN, 0f, 0f), new Vector3(0f, float.PositiveInfinity, 0f) })
            {
                preview.Submit(member, other, Vector3.zero, 1f);
                preview.Submit(member, other, anchor, 1f);
                Assert.That(mesh.vertexCount, Is.Zero);
            }

            preview.Submit(member, other, Vector3.zero, 1f);
            camera.enabled = false;
            preview.Submit(member, other, Vector3.zero, 1f);
            Assert.That(mesh.vertexCount, Is.Zero);
            camera.enabled = true;
            preview.Submit(member, other, Vector3.zero, 1f);
            member.enabled = false;
            preview.Submit(member, other, Vector3.zero, 1f);
            Assert.That(mesh.vertexCount, Is.Zero);
            member.enabled = true;
            preview.Submit(member, other, Vector3.zero, 1f);
            other.gameObject.SetActive(false);
            preview.Submit(member, other, Vector3.zero, 1f);
            Assert.That(mesh.vertexCount, Is.Zero);
            other.gameObject.SetActive(true);
            preview.Submit(member, other, Vector3.zero, 1f);
            Object.DestroyImmediate(other.gameObject);
            preview.Submit(member, other, Vector3.zero, 1f);
            Assert.That(mesh.vertexCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Destroy_ReleasesTheOwnedMesh()
        {
            preview.Submit(member, other, Vector3.zero, 1f);
            Mesh mesh = preview.PreviewMesh;
            Assert.That(mesh, Is.Not.Null);
            Object.Destroy(preview);
            yield return null;
            yield return null;
            Assert.That(mesh == null, Is.True);
        }

        [Test]
        public void Draw_IsGreenCameraScopedAndSubtlyVisibleAtBuriedContacts()
        {
            Shader shader = Shader.Find("Phyzzle/AttachContactPreview");
            Assert.That(shader, Is.Not.Null);
            material.shader = shader;
            RenderTexture primary = new(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture secondary = new(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Camera secondCamera = CreateChild("Other Camera").AddComponent<Camera>();
            Material blockerMaterial = null;
            GameObject blocker = null;
            try
            {
                ConfigureRenderCamera(camera, primary);
                ConfigureRenderCamera(secondCamera, secondary);
                preview.Submit(member, other, Vector3.zero, 1f);
                camera.Render();
                secondCamera.Render();
                float visibleGreen = MaxGreen(primary);
                Assert.That(visibleGreen, Is.GreaterThan(0.15f));
                Assert.That(MaxGreen(secondary), Is.LessThan(0.03f), "The preview must not leak to another camera.");

                blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.SetParent(root.transform);
                blocker.transform.position = new Vector3(0f, 0f, -0.5f);
                blocker.transform.localScale = new Vector3(2f, 2f, 0.1f);
                blockerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                blockerMaterial.SetColor("_BaseColor", Color.black);
                blocker.GetComponent<Renderer>().sharedMaterial = blockerMaterial;
                preview.Submit(member, other, Vector3.zero, 1f);
                camera.Render();
                float buriedGreen = MaxGreen(primary);
                Assert.That(buriedGreen, Is.GreaterThan(0.005f), "A face-center contact needs a faint cue even inside opaque surfaces.");
                Assert.That(buriedGreen, Is.LessThan(visibleGreen * 0.4f), "The occluded cue must be much fainter than visible glue.");
                preview.Clear();
                camera.Render();
                Assert.That(MaxGreen(primary), Is.LessThan(0.03f));
            }
            finally
            {
                camera.targetTexture = null;
                secondCamera.targetTexture = null;
                primary.Release();
                secondary.Release();
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(secondary);
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(blockerMaterial);
            }
        }

        private GameObject CreateChild(string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(root.transform);
            return child;
        }

        private static void ConfigureRenderCamera(Camera target, RenderTexture texture)
        {
            target.transform.position = new Vector3(0f, 0f, -5f);
            target.orthographic = true;
            target.orthographicSize = 1.5f;
            target.clearFlags = CameraClearFlags.SolidColor;
            target.backgroundColor = Color.black;
            target.depthTextureMode = DepthTextureMode.Depth;
            target.targetTexture = texture;
            texture.Create();
        }

        private static float MaxGreen(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D pixels = new(target.width, target.height, TextureFormat.RGBA32, false, true);
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            pixels.Apply();
            float maximum = 0f;
            foreach (Color pixel in pixels.GetPixels())
            {
                maximum = Mathf.Max(maximum, pixel.g - pixel.r);
            }
            Object.DestroyImmediate(pixels);
            RenderTexture.active = previous;
            return maximum;
        }

    }
}
