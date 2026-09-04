using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Tests
{
    public sealed class AttachRenderFeatureTests
    {
        private const int RenderSize = 64;
        private static readonly Color SourceColor = new(0.16f, 0.08f, 0.05f, 1f);
        private Material maskMaterial;
        private Material compositeMaterial;
        private Material projectionMaterial;
        private AttachRenderFeature feature;

        [SetUp]
        public void SetUp()
        {
            Shader mask = Shader.Find("Hidden/Phyzzle/AttachMask");
            Shader composite = Shader.Find("Hidden/Phyzzle/AttachComposite");
            Shader projection = Shader.Find("Phyzzle/AttachProjection");
            Assert.That(mask, Is.Not.Null);
            Assert.That(composite, Is.Not.Null);
            Assert.That(projection, Is.Not.Null);

            maskMaterial = new Material(mask);
            compositeMaterial = new Material(composite);
            projectionMaterial = new Material(projection);
            feature = ScriptableObject.CreateInstance<AttachRenderFeature>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(maskMaterial);
            Object.DestroyImmediate(compositeMaterial);
            Object.DestroyImmediate(projectionMaterial);
        }

        [Test]
        public void Shaders_ExposeRolePassesAndTransparentProjection()
        {
            Assert.That(maskMaterial.passCount, Is.EqualTo(3));
            Assert.That(maskMaterial.FindPass("Eligible"), Is.EqualTo(0));
            Assert.That(maskMaterial.FindPass("Focused"), Is.EqualTo(1));
            Assert.That(maskMaterial.FindPass("Held"), Is.EqualTo(2));
            Assert.That(projectionMaterial.renderQueue, Is.InRange(3000, 3999));
        }

        [Test]
        public void Configure_UsesPostProcessingIntermediateMask()
        {
            feature.Configure(maskMaterial, compositeMaterial);
            feature.Create();

            Assert.That(feature.IsConfigured, Is.True);
            Assert.That(feature.PassEvent, Is.EqualTo(RenderPassEvent.AfterRenderingPostProcessing));
            Assert.That(feature.RequiresIntermediateTexture, Is.True);
            Assert.That(feature.MaskFormat, Is.EqualTo(GraphicsFormat.R8G8B8A8_UNorm));
        }

        [Test]
        public void ShouldEnqueue_AcceptsOnlyActiveBaseGameCamera()
        {
            Assert.That(AttachRenderFeature.ShouldEnqueue(
                CameraType.Game, CameraRenderType.Base, true, 1f), Is.True);
            Assert.That(AttachRenderFeature.ShouldEnqueue(
                CameraType.SceneView, CameraRenderType.Base, true, 1f), Is.False);
            Assert.That(AttachRenderFeature.ShouldEnqueue(
                CameraType.Game, CameraRenderType.Overlay, true, 1f), Is.False);
            Assert.That(AttachRenderFeature.ShouldEnqueue(
                CameraType.Game, CameraRenderType.Base, true, 0f), Is.False);
            Assert.That(AttachRenderFeature.ShouldEnqueue(
                CameraType.Game, CameraRenderType.Base, false, 1f), Is.False);
        }

        [Test]
        public void SupportsRendererData_RejectsMissingFeature()
        {
            Assert.That(AttachRenderFeature.SupportsRendererData(null), Is.False);
        }

        [Test]
        public void Feature_RenderingUsesDepthCullRolePrecedenceBlendAndHeldPulse()
        {
            RenderPipelineAsset originalGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset originalQualityPipeline = QualitySettings.renderPipeline;
            UniversalRendererData rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AttachRenderFeature renderFeature = ScriptableObject.CreateInstance<AttachRenderFeature>();
            Material opaqueMaterial = null;
            GameObject cameraObject = null;
            RenderTexture target = null;
            Mesh backfaceTriangle = null;
            UniversalRenderPipelineAsset temporaryPipeline = null;
            try
            {
                Shader opaqueShader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(opaqueShader, Is.Not.Null);
                opaqueMaterial = new Material(opaqueShader);
                opaqueMaterial.SetColor("_BaseColor", SourceColor);
                renderFeature.Configure(maskMaterial, compositeMaterial);
                rendererData.rendererFeatures.Add(renderFeature);
                temporaryPipeline = UniversalRenderPipelineAsset.Create(rendererData);
                GraphicsSettings.defaultRenderPipeline = temporaryPipeline;
                QualitySettings.renderPipeline = temporaryPipeline;

                cameraObject = new GameObject("Attach Feature Test Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 1.5f;
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -5f), Quaternion.identity);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = SourceColor;
                camera.depthTextureMode = DepthTextureMode.Depth;
                target = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                target.Create();
                backfaceTriangle = CreateBackfaceTriangle();

                Color zeroBlend = Render(camera, target, opaqueMaterial, null, 0u, 0f, 0f, false);
                Color world = Render(camera, target, opaqueMaterial, null, 0u, 1f, 0f, false);
                Color occludedEligible = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Eligible, 1f, 0f, true);
                Color backfaceEligible = Render(
                    camera, target, opaqueMaterial, backfaceTriangle, AttachVisualLayers.Eligible, 1f, 0f, false);
                Color allRoles = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Owned, 1f, 0f, false);
                Color eligiblePulseOff = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Eligible, 1f, 0f, false);
                Color eligiblePulseOn = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Eligible, 1f, 1f, false);
                Color heldPulseOff = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Held, 1f, 0f, false);
                Color heldPulseOn = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Held, 1f, 1f, false);
                Debug.Log($"Attach feature pixels: zero={zeroBlend}, occluded={occludedEligible}, " +
                    $"backface={backfaceEligible}, all={allRoles}, eligibleOff={eligiblePulseOff}, " +
                    $"eligibleOn={eligiblePulseOn}, heldOff={heldPulseOff}, heldOn={heldPulseOn}");

                Assert.That(ColorDistance(zeroBlend, world), Is.LessThan(0.04f),
                    "Zero blend must preserve the actual source-camera color without applying the feature treatment.");
                Assert.That(ColorDistance(occludedEligible, zeroBlend), Is.LessThan(0.06f),
                    "The feature mask must read active scene depth so hidden roles do not composite.");
                Assert.That(ColorDistance(backfaceEligible, zeroBlend), Is.LessThan(0.06f),
                    "Back-facing role geometry must be removed by the mask shader's real raster culling.");
                Assert.That(allRoles.g, Is.GreaterThan(allRoles.r + 0.35f),
                    "A renderer carrying every role must resolve to Held after Eligible then Focused.");
                Assert.That(ColorDistance(eligiblePulseOff, eligiblePulseOn), Is.LessThan(0.04f),
                    "Held pulse settings must not alter eligible mask emission.");
                Assert.That(ColorDistance(heldPulseOff, heldPulseOn), Is.GreaterThan(0.08f),
                    "Held pulse settings must alter held emission while the role mask stays held.");
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = originalGraphicsPipeline;
                QualitySettings.renderPipeline = originalQualityPipeline;
                Object.DestroyImmediate(backfaceTriangle);
                if (target != null)
                {
                    target.Release();
                }

                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(opaqueMaterial);
                Object.DestroyImmediate(temporaryPipeline);
                Object.DestroyImmediate(rendererData);
                Object.DestroyImmediate(renderFeature);
                Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, 0f);
                Shader.SetGlobalTexture(AttachVisualShaderIds.MaskTexture, Texture2D.blackTexture);
            }
        }

        private static Color Render(
            Camera camera,
            RenderTexture target,
            Material opaqueMaterial,
            Mesh mesh,
            uint role,
            float blend,
            float pulseStrength,
            bool blocked)
        {
            GameObject roleObject = null;
            GameObject blocker = null;
            try
            {
                if (role != 0u)
                {
                    roleObject = new GameObject("Attach Feature Role");
                    MeshFilter filter = roleObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh == null ? Resources.GetBuiltinResource<Mesh>("Cube.fbx") : mesh;
                    Renderer renderer = roleObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = opaqueMaterial;
                    renderer.renderingLayerMask = role;
                    roleObject.transform.position = Vector3.forward;
                }

                if (blocked)
                {
                    blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blocker.transform.position = Vector3.forward * 0.5f;
                    blocker.transform.localScale = Vector3.one * 1.2f;
                    blocker.GetComponent<Renderer>().sharedMaterial = opaqueMaterial;
                }

                Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, blend);
                Shader.SetGlobalFloat(AttachVisualShaderIds.WorldSaturation, 1f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.WorldBrightness, 1f);
                Shader.SetGlobalColor(AttachVisualShaderIds.EligibleColor, new Color(1f, 0.7f, 0.05f, 1f));
                Shader.SetGlobalColor(AttachVisualShaderIds.FocusedColor, new Color(1f, 0.9f, 0.2f, 1f));
                Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, new Color(0.05f, 0.95f, 0.25f, 1f));
                Shader.SetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, 1f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, pulseStrength);
                RenderPipeline.StandardRequest request = new() { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                return ReadCenterPixel(target);
            }
            finally
            {
                Object.DestroyImmediate(roleObject);
                Object.DestroyImmediate(blocker);
            }
        }

        private static Mesh CreateBackfaceTriangle()
        {
            Mesh mesh = new();
            mesh.vertices = new[]
            {
                new Vector3(-0.8f, -0.8f, 0f),
                new Vector3(0f, 0.8f, 0f),
                new Vector3(0.8f, -0.8f, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1 };
            return mesh;
        }

        private static Color ReadCenterPixel(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D pixels = new(RenderSize, RenderSize, TextureFormat.RGBA32, false, true);
            pixels.ReadPixels(new Rect(0f, 0f, RenderSize, RenderSize), 0, 0);
            pixels.Apply(false, false);
            Color pixel = pixels.GetPixel(RenderSize / 2, RenderSize / 2);
            Object.DestroyImmediate(pixels);
            RenderTexture.active = previous;
            return pixel;
        }

        private static float ColorDistance(Color first, Color second) =>
            Vector3.Distance(new Vector3(first.r, first.g, first.b), new Vector3(second.r, second.g, second.b));

    }
}
