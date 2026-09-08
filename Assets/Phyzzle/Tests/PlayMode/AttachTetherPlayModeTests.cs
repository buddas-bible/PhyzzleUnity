using System.Collections;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>AttachTetherPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class AttachTetherPlayModeTests
    {
        private GameObject root;
        private Camera camera;
        private Transform hand;
        private AttachableObject held;
        private AttachSettings settings;
        private Material material;
        private AttachTetherRenderer tether;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Attach Tether Test");
            camera = CreateChild("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            hand = CreateChild("Hand").transform;
            hand.position = new Vector3(-1f, 0f, 0f);
            held = CreateChild("Held").AddComponent<AttachableObject>();
            held.Body.useGravity = false;
            held.Body.position = new Vector3(1f, 0f, 0f);
            settings = ScriptableObject.CreateInstance<AttachSettings>();
            Shader shader = Shader.Find("Phyzzle/AttachTether");
            Assert.That(shader, Is.Not.Null);
            material = new Material(shader);
            tether = root.AddComponent<AttachTetherRenderer>();
            tether.Configure(camera, hand, material, settings);
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(settings);
        }

        /// <summary>
        /// <c>Submit_FollowsBothEndpointsWithoutChangingPhysics</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Submit_FollowsBothEndpointsWithoutChangingPhysics()
        {
            held.Body.mass = 3f;
            held.Body.linearVelocity = new Vector3(1f, 2f, 3f);
            held.Body.angularVelocity = new Vector3(0f, 1f, 0f);
            Quaternion rotation = held.Body.rotation;
            tether.Submit(held, 1f);
            AssertEndpoints();

            hand.position += Vector3.up;
            held.Body.position += new Vector3(2f, 1f, 0f);
            Vector3 position = held.Body.position;
            tether.Submit(held, 1f);
            AssertEndpoints();
            Assert.That(held.Body.position, Is.EqualTo(position));
            Assert.That(held.Body.rotation, Is.EqualTo(rotation));
            Assert.That(held.Body.linearVelocity, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(held.Body.angularVelocity, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(held.Body.mass, Is.EqualTo(3f));
            Assert.That(held.Body.useGravity, Is.False);
            Assert.That(held.Body.isKinematic, Is.False);
            Assert.That(held.IsSelected, Is.False);
        }

        /// <summary>
        /// <c>Submit_UsesTheRenderedPoseWhenItDiffersFromThePhysicsPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Submit_UsesTheRenderedPoseWhenItDiffersFromThePhysicsPose()
        {
            bool previousAutoSync = Physics.autoSyncTransforms;
            try
            {
                Physics.autoSyncTransforms = false;
                held.Body.interpolation = RigidbodyInterpolation.Interpolate;
                Physics.SyncTransforms();
                Vector3 physicsPosition = held.Body.position;

                // Keep the two poses distinct without depending on the frame's interpolation fraction.
                held.Body.transform.position = physicsPosition + Vector3.up * 2f;
                Assert.That(Vector3.Distance(held.Body.transform.position, held.Body.position),
                    Is.GreaterThan(1f), "This regression must exercise distinct rendered and physics poses.");
                tether.Submit(held, 1f);
                AssertEndpoints();
                Assert.That(held.Body.position, Is.EqualTo(physicsPosition),
                    "Following the rendered pose must not move the physics body.");
            }
            finally
            {
                Physics.autoSyncTransforms = previousAutoSync;
            }
        }

        /// <summary>
        /// <c>ClearAndDisable_RemoveGeometryAndAllowReuse</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ClearAndDisable_RemoveGeometryAndAllowReuse()
        {
            tether.Submit(held, 1f);
            Mesh mesh = tether.RibbonMesh;
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            tether.Clear();
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(tether.LastSubmittedDrawCount, Is.Zero);

            tether.Submit(held, 1f);
            Assert.That(tether.RibbonMesh, Is.SameAs(mesh));
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            tether.enabled = false;
            tether.Submit(held, 1f);
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(tether.LastSubmittedDrawCount, Is.Zero);
            tether.enabled = true;
            tether.Submit(held, 1f);
            AssertEndpoints();
        }

        /// <summary>
        /// <c>InvalidInputsAndZeroBlend_RemoveThePreviousTether</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void InvalidInputsAndZeroBlend_RemoveThePreviousTether()
        {
            tether.Submit(held, 1f);
            held.enabled = false;
            tether.Submit(held, 1f);
            Assert.That(tether.RibbonMesh.vertexCount, Is.Zero);
            held.enabled = true;

            tether.Submit(held, 1f);
            hand.gameObject.SetActive(false);
            tether.Submit(held, 1f);
            Assert.That(tether.LastSubmittedDrawCount, Is.Zero);
            hand.gameObject.SetActive(true);

            tether.Submit(held, 1f);
            tether.Submit(held, 0f);
            Assert.That(tether.RibbonMesh.vertexCount, Is.Zero);
            tether.Submit(held, 1f);
            Object.DestroyImmediate(held.gameObject);
            tether.Submit(held, 1f);
            Assert.That(tether.RibbonMesh.vertexCount, Is.Zero);
        }

        /// <summary>
        /// <c>Destroy_ReleasesTheRetainedMesh</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Destroy_ReleasesTheRetainedMesh()
        {
            tether.Submit(held, 1f);
            Mesh mesh = tether.RibbonMesh;
            Object.Destroy(tether);
            yield return null;
            yield return null;
            Assert.That(mesh == null, Is.True);
        }

        /// <summary>
        /// <c>Draw_IsGreenAndScopedToTheConfiguredCamera</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Draw_IsGreenAndScopedToTheConfiguredCamera()
        {
            RenderTexture primary = new(128, 128, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture secondary = new(128, 128, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            Camera other = CreateChild("Other Camera").AddComponent<Camera>();
            GameObject blocker = null;
            Material blockerMaterial = null;
            try
            {
                ConfigureRenderCamera(camera, primary);
                ConfigureRenderCamera(other, secondary);
                tether.Submit(held, 1f);
                camera.Render();
                other.Render();
                Assert.That(MaxGreen(primary), Is.GreaterThan(0.15f),
                    "The configured camera must see an actual green ribbon.");
                Assert.That(MaxGreen(secondary), Is.LessThan(0.03f),
                    "A camera-scoped tether must not leak to another camera.");

                blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.SetParent(root.transform);
                blocker.transform.position = new Vector3(0f, 0f, -1f);
                blocker.transform.localScale = new Vector3(4f, 4f, 0.2f);
                blockerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                blockerMaterial.SetColor("_BaseColor", Color.black);
                blocker.GetComponent<Renderer>().sharedMaterial = blockerMaterial;
                tether.Submit(held, 1f);
                camera.Render();
                Assert.That(MaxGreen(primary), Is.LessThan(0.03f),
                    "An opaque wall in front of the tether must occlude it.");
                blocker.SetActive(false);
                tether.Clear();
                camera.Render();
                Assert.That(MaxGreen(primary), Is.LessThan(0.03f),
                    "Clear must remove geometry, including draws queued earlier this frame.");
            }
            finally
            {
                camera.targetTexture = null;
                other.targetTexture = null;
                primary.Release();
                secondary.Release();
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(secondary);
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(blockerMaterial);
            }
        }

        /// <summary>
        /// <c>CreateChild</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private GameObject CreateChild(string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(root.transform);
            return child;
        }

        /// <summary>
        /// <c>AssertEndpoints</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void AssertEndpoints()
        {
            Assert.That(tether.LastSubmittedDrawCount, Is.EqualTo(1));
            Vector3[] vertices = tether.RibbonMesh.vertices;
            Assert.That(Vector3.Distance((vertices[0] + vertices[1]) * 0.5f, hand.position),
                Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance((vertices[^2] + vertices[^1]) * 0.5f, held.Body.transform.position),
                Is.LessThan(0.0001f));
        }

        /// <summary>
        /// <c>ConfigureRenderCamera</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static void ConfigureRenderCamera(Camera target, RenderTexture texture)
        {
            target.transform.position = new Vector3(0f, 0f, -5f);
            target.orthographic = true;
            target.orthographicSize = 1.5f;
            target.clearFlags = CameraClearFlags.SolidColor;
            target.backgroundColor = Color.black;
            target.targetTexture = texture;
            texture.Create();
        }

        /// <summary>
        /// <c>MaxGreen</c> 테스트 지원 동작을 수행한다.
        /// </summary>
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
