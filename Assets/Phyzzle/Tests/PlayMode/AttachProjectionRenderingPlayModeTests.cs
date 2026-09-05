using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Tests
{
    public sealed class AttachProjectionRenderingPlayModeTests
    {
        private const int TextureSize = 128;
        private static readonly Color PlatformColor = new(0.12f, 0.08f, 0.10f, 1f);

        private Camera primaryCamera;
        private Camera secondaryCamera;
        private RenderTexture primaryTarget;
        private RenderTexture secondaryTarget;
        private Material platformMaterial;
        private Material projectionMaterial;
        private GameObject platform;
        private GameObject source;
        private Mesh sourceMesh;
        private Matrix4x4 sourceMatrix;

        [SetUp]
        public void SetUp()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader projection = Shader.Find("Phyzzle/AttachProjection");
            Assert.That(unlit, Is.Not.Null);
            Assert.That(projection, Is.Not.Null);

            platformMaterial = new Material(unlit);
            platformMaterial.SetColor("_BaseColor", PlatformColor);
            projectionMaterial = new Material(projection);
            ConfigureProjectionMaterial();
            Shader.SetGlobalFloat("_AttachVisualBlend", 1f);
            Shader.SetGlobalFloat("_AttachWorldSaturation", 1f);
            Shader.SetGlobalFloat("_AttachWorldBrightness", 1f);

            primaryTarget = CreateTarget();
            secondaryTarget = CreateTarget();
            primaryCamera = CreateCamera("Attach Projection Primary", primaryTarget);
            secondaryCamera = CreateCamera("Attach Projection Secondary", secondaryTarget);
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Attach Projection Receiver";
            platform.transform.position = new Vector3(0f, -0.1f, 0f);
            platform.transform.localScale = new Vector3(2f, 0.2f, 2f);
            platform.GetComponent<Renderer>().sharedMaterial = platformMaterial;

            source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.name = "Attach Projection Source";
            source.transform.position = new Vector3(0f, 2f, 0f);
            source.transform.localScale = new Vector3(4f, 2f, 4f);
            sourceMesh = source.GetComponent<MeshFilter>().sharedMesh;
            sourceMatrix = source.transform.localToWorldMatrix;
            source.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (primaryCamera != null)
            {
                Object.DestroyImmediate(primaryCamera.gameObject);
            }

            if (secondaryCamera != null)
            {
                Object.DestroyImmediate(secondaryCamera.gameObject);
            }

            Object.DestroyImmediate(platform);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(platformMaterial);
            Object.DestroyImmediate(projectionMaterial);
            ReleaseTarget(primaryTarget);
            ReleaseTarget(secondaryTarget);
            Shader.SetGlobalColor("_AttachHeldColor", Color.clear);
            Shader.SetGlobalFloat("_AttachVisualBlend", 0f);
            Shader.SetGlobalFloat("_AttachWorldSaturation", 0f);
            Shader.SetGlobalFloat("_AttachWorldBrightness", 0f);
        }

        [Test]
        public void Projection_ClipsPixelsBeyondReceiverDepth()
        {
            SubmitToPrimaryCamera();
            primaryCamera.Render();

            Color receiverPixel = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);
            Color beyondReceiverPixel = ReadPixel(primaryTarget, TextureSize * 3 / 4, TextureSize / 2);
            Debug.Log($"Attach projection depth pixels: receiver={receiverPixel}, beyond={beyondReceiverPixel}");

            Assert.That(receiverPixel.g, Is.GreaterThan(receiverPixel.r + 0.12f),
                "The silhouette must add green where it overlays the opaque receiver.");
            Assert.That(receiverPixel.g, Is.GreaterThan(PlatformColor.g + 0.12f));
            Assert.That(beyondReceiverPixel.r, Is.LessThan(0.06f));
            Assert.That(beyondReceiverPixel.g, Is.LessThan(0.06f),
                "Receiver-depth clipping must discard the infinite plane outside the platform.");
            Assert.That(beyondReceiverPixel.b, Is.LessThan(0.06f));
        }

        [Test]
        public void Projection_RenderParamsCameraScopesDrawToGameplayCamera()
        {
            SubmitToPrimaryCamera();
            primaryCamera.Render();
            secondaryCamera.Render();

            Color primaryPixel = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);
            Color secondaryPixel = ReadPixel(secondaryTarget, TextureSize / 2, TextureSize / 2);
            Debug.Log($"Attach projection camera pixels: primary={primaryPixel}, secondary={secondaryPixel}");

            Assert.That(primaryPixel.g, Is.GreaterThan(primaryPixel.r + 0.12f));
            Assert.That(secondaryPixel.g, Is.LessThan(secondaryPixel.r + 0.06f),
                "The explicit RenderParams camera must keep the projection out of other cameras.");
            Assert.That(secondaryPixel.g, Is.LessThan(PlatformColor.g + 0.06f));
        }

        [Test]
        public void Projection_OrthographicDepthSeparatedReceiverWithinToleranceMatchesProjectionPlane()
        {
            primaryCamera.farClipPlane = 8.2f;
            platform.SetActive(false);
            GameObject deeperReceiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material deeperMaterial = new(platformMaterial)
            {
                color = new Color(0.5f, 0.08f, 0.04f, 1f)
            };
            try
            {
                deeperReceiver.transform.position = new Vector3(0f, -0.054f, 0f);
                deeperReceiver.transform.localScale = new Vector3(0.8f, 0.02f, 0.8f);
                deeperReceiver.GetComponent<Renderer>().sharedMaterial = deeperMaterial;

                primaryCamera.Render();
                Color baseline = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);
                Assert.That(baseline.r, Is.GreaterThan(0.2f),
                    "The test must sample a real depth-separated opaque receiver, not the background.");
                SubmitToPrimaryCamera();
                primaryCamera.Render();

                Color pixel = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);
                Debug.Log($"Attach projection orthographic depth-tolerance pixel: {pixel}");
                Assert.That(pixel.g, Is.GreaterThan(pixel.r + 0.12f),
                    "An orthographic receiver inside the configured depth tolerance must retain the projection.");
            }
            finally
            {
                Object.DestroyImmediate(deeperReceiver);
                Object.DestroyImmediate(deeperMaterial);
            }
        }

        [Test]
        public void Projection_GlobalVisualBlendScalesIntermediateFade()
        {
            Shader.SetGlobalFloat("_AttachVisualBlend", 1f);
            SubmitToPrimaryCamera();
            primaryCamera.Render();
            Color fullBlend = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);

            Shader.SetGlobalFloat("_AttachVisualBlend", 0.5f);
            SubmitToPrimaryCamera();
            primaryCamera.Render();
            Color halfBlend = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);

            float fullGreenContribution = fullBlend.g - PlatformColor.g;
            float halfGreenContribution = halfBlend.g - PlatformColor.g;
            Assert.That(fullGreenContribution, Is.GreaterThan(0.12f));
            Assert.That(halfGreenContribution, Is.GreaterThan(fullGreenContribution * 0.3f));
            Assert.That(halfGreenContribution, Is.LessThan(fullGreenContribution * 0.7f),
                "A Holding exit at half blend must not render the projection at full opacity.");
        }

        [Test]
        public void Projection_ZeroGlobalVisualBlendSuppressesTheNextDraw()
        {
            Shader.SetGlobalFloat("_AttachVisualBlend", 1f);
            primaryCamera.Render();
            Color baseline = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);

            SubmitToPrimaryCamera();
            primaryCamera.Render();
            Color visible = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);
            Assert.That(visible.g, Is.GreaterThan(PlatformColor.g + 0.12f));

            Shader.SetGlobalFloat("_AttachVisualBlend", 0f);
            SubmitToPrimaryCamera();
            primaryCamera.Render();
            Color cleaned = ReadPixel(primaryTarget, TextureSize / 2, TextureSize / 2);
            Assert.That(cleaned.r, Is.EqualTo(baseline.r).Within(0.02f));
            Assert.That(cleaned.g, Is.EqualTo(baseline.g).Within(0.02f),
                "Hard cleanup must not leak a visible projection into the next draw.");
            Assert.That(cleaned.b, Is.EqualTo(baseline.b).Within(0.02f));
        }

        private void SubmitToPrimaryCamera()
        {
            MaterialPropertyBlock properties = new();
            properties.SetVector("_AttachProjectionPlane", new Vector4(0f, 1f, 0f, 0f));
            properties.SetVector("_AttachProjectionDirection", Vector3.down);
            properties.SetFloat("_AttachProjectionOpacity", 0.8f);
            properties.SetFloat("_AttachProjectionBias", 0.015f);
            properties.SetFloat("_AttachProjectionDepthTolerance", 0.06f);
            Graphics.RenderMesh(new RenderParams(projectionMaterial)
            {
                camera = primaryCamera,
                matProps = properties,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 8f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            }, sourceMesh, 0, sourceMatrix);
        }

        private static Camera CreateCamera(string name, RenderTexture target)
        {
            GameObject cameraObject = new(name);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 8f, 0f), Quaternion.Euler(90f, 0f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.targetTexture = target;
            return camera;
        }

        private static RenderTexture CreateTarget()
        {
            RenderTexture target = new(
                TextureSize,
                TextureSize,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            target.Create();
            return target;
        }

        private static void ReleaseTarget(RenderTexture target)
        {
            if (target == null)
            {
                return;
            }

            target.Release();
            Object.DestroyImmediate(target);
        }

        private static Color ReadPixel(RenderTexture target, int x, int y)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D pixels = new(TextureSize, TextureSize, TextureFormat.RGBA32, false, true);
            pixels.ReadPixels(new Rect(0f, 0f, TextureSize, TextureSize), 0, 0);
            pixels.Apply(false, false);
            Color pixel = pixels.GetPixel(x, y);
            Object.DestroyImmediate(pixels);
            RenderTexture.active = previous;
            return pixel;
        }

        private void ConfigureProjectionMaterial()
        {
            Shader.SetGlobalColor("_AttachHeldColor", new Color(0.05f, 0.95f, 0.25f, 1f));
        }
    }
}
