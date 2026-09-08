using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>AttachRenderFeatureTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class AttachRenderFeatureTests
    {
        private const int RenderSize = 64;
        private static readonly Color SourceColor = new(0.8f, 0.4f, 0.2f, 1f);
        private Material maskMaterial;
        private Material compositeMaterial;
        private Material projectionMaterial;
        private AttachRenderFeature feature;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
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

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(maskMaterial);
            Object.DestroyImmediate(compositeMaterial);
            Object.DestroyImmediate(projectionMaterial);
        }

        /// <summary>
        /// <c>Shaders_ExposeRolePassesAndTransparentProjection</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Shaders_ExposeRolePassesAndTransparentProjection()
        {
            Assert.That(maskMaterial.passCount, Is.EqualTo(3));
            Assert.That(maskMaterial.FindPass("Eligible"), Is.EqualTo(0));
            Assert.That(maskMaterial.FindPass("Focused"), Is.EqualTo(1));
            Assert.That(maskMaterial.FindPass("Held"), Is.EqualTo(2));
            Assert.That(projectionMaterial.renderQueue, Is.InRange(3000, 3999));
        }

        /// <summary>
        /// <c>Configure_UsesPostProcessingIntermediateMask</c> 테스트 시나리오를 검증한다.
        /// </summary>
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

        /// <summary>
        /// <c>ShouldEnqueue_AcceptsOnlyActiveBaseGameCamera</c> 테스트 시나리오를 검증한다.
        /// </summary>
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

        /// <summary>
        /// <c>SupportsRendererData_RejectsMissingFeature</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void SupportsRendererData_RejectsMissingFeature()
        {
            Assert.That(AttachRenderFeature.SupportsRendererData(null), Is.False);
        }

        /// <summary>
        /// <c>Feature_RenderingUsesDepthCullRolePrecedenceBlendAndHeldPulse</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Feature_RenderingUsesDepthCullRolePrecedenceBlendAndHeldPulse()
        {
            AttachGlobals globals = AttachGlobals.Capture();
            RenderPipelineAsset originalGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset originalQualityPipeline = QualitySettings.renderPipeline;
            UniversalRendererData rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AttachRenderFeature renderFeature = ScriptableObject.CreateInstance<AttachRenderFeature>();
            MaskProbeFeature maskProbeFeature = ScriptableObject.CreateInstance<MaskProbeFeature>();
            DepthProbeFeature depthProbeFeature = ScriptableObject.CreateInstance<DepthProbeFeature>();
            Material opaqueMaterial = null;
            Material roleWithoutDepthMaterial = null;
            Material depthProbeMaterial = null;
            Material maskProbeMaterial = null;
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
                roleWithoutDepthMaterial = new Material(opaqueMaterial);
                roleWithoutDepthMaterial.SetFloat("_ZWrite", 0f);
                depthProbeMaterial = new Material(opaqueShader);
                depthProbeMaterial.SetColor("_BaseColor", new Color(0.02f, 0.15f, 1f, 1f));
                maskProbeMaterial = new Material(Shader.Find("Hidden/Phyzzle/Tests/AttachMaskProbe"));
                renderFeature.Configure(maskMaterial, compositeMaterial);
                maskProbeFeature.Configure(maskProbeMaterial);
                depthProbeFeature.Configure(depthProbeMaterial, Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
                rendererData.rendererFeatures.Add(renderFeature);
                rendererData.rendererFeatures.Add(maskProbeFeature);
                rendererData.rendererFeatures.Add(depthProbeFeature);
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
                Color zeroBlendRole = Render(camera, target, opaqueMaterial, null, AttachVisualLayers.Held, 0f, 0f, false);
                Color world = Render(camera, target, opaqueMaterial, null, 0u, 1f, 0f, false);
                Color occludedEligible = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Eligible, 1f, 0f, true);
                Color backfaceEligible = Render(
                    camera, target, opaqueMaterial, backfaceTriangle, AttachVisualLayers.Eligible, 1f, 0f, false);
                Color allRoles = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Owned, 1f, 0f, false);
                Color focusedOnly = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Focused, 1f, 0f, false);
                Color eligibleFocused = Render(
                    camera, target, opaqueMaterial,
                    null, AttachVisualLayers.Eligible | AttachVisualLayers.Focused, 1f, 0f, false);
                Color eligiblePulseOff = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Eligible, 1f, 0f, false);
                Color eligiblePulseOn = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Eligible, 1f, 1f, false);
                Color heldPulseOff = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Held, 1f, 0f, false);
                Color heldPulseOn = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Held, 1f, 1f, false);
                Color heldMaskOff = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Held, 1f, 0f, false, maskProbeFeature);
                Color heldMaskOn = Render(
                    camera, target, opaqueMaterial, null, AttachVisualLayers.Held, 1f, 1f, false, maskProbeFeature);
                Color depthProbe = RenderDepthProbe(camera, target, roleWithoutDepthMaterial, depthProbeFeature);
                Debug.Log($"Attach feature pixels: zero={zeroBlend}, occluded={occludedEligible}, " +
                    $"backface={backfaceEligible}, all={allRoles}, eligibleOff={eligiblePulseOff}, " +
                    $"eligibleOn={eligiblePulseOn}, heldOff={heldPulseOff}, heldOn={heldPulseOn}, " +
                    $"heldMaskOff={heldMaskOff}, heldMaskOn={heldMaskOn}, depthProbe={depthProbe}");

                Assert.That(ColorDistance(zeroBlend, zeroBlendRole), Is.LessThan(0.04f),
                    "Zero blend must preserve the true source render even when a role is present.");
                Assert.That(ColorDistance(zeroBlend, world), Is.LessThan(0.02f),
                    "Attach must preserve the color and brightness of unmarked world pixels.");
                Assert.That(ColorDistance(occludedEligible, world), Is.LessThan(0.06f),
                    "The feature mask must read active scene depth so hidden roles do not composite.");
                Assert.That(ColorDistance(backfaceEligible, world), Is.LessThan(0.06f),
                    "Back-facing role geometry must be removed by the mask shader's real raster culling.");
                Assert.That(allRoles.g, Is.GreaterThan(allRoles.r + 0.35f),
                    "A renderer carrying every role must resolve to Held after Eligible then Focused.");
                Assert.That(ColorDistance(eligibleFocused, focusedOnly), Is.LessThan(0.04f),
                    "Eligible plus Focused must resolve to Focused before Held is present.");
                Assert.That(ColorDistance(eligibleFocused, allRoles), Is.GreaterThan(0.20f),
                    "Adding Held must replace the Focused output with the distinct Held output.");
                Assert.That(ColorDistance(eligiblePulseOff, eligiblePulseOn), Is.LessThan(0.04f),
                    "Held pulse settings must not alter eligible mask emission.");
                Assert.That(ColorDistance(heldPulseOff, heldPulseOn), Is.GreaterThan(0.08f),
                    "Held pulse settings must alter held emission while the role mask stays held.");
                Assert.That(heldMaskOff.b, Is.GreaterThan(0.5f),
                    "The live global mask capture must encode a nonzero held role.");
                Assert.That(heldMaskOff.b, Is.GreaterThan(heldMaskOff.r + 0.4f),
                    "The live global mask capture must be held-channel dominant.");
                Assert.That(ColorDistance(heldMaskOff, heldMaskOn), Is.LessThan(0.01f),
                    "Pulse must not alter the feature's live held mask capture.");
                Assert.That(depthProbe.b, Is.GreaterThan(depthProbe.r + 0.25f),
                    "A later depth-tested probe must pass through a role whose feature mask only reads scene depth.");
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
                Object.DestroyImmediate(roleWithoutDepthMaterial);
                Object.DestroyImmediate(depthProbeMaterial);
                Object.DestroyImmediate(maskProbeMaterial);
                Object.DestroyImmediate(temporaryPipeline);
                Object.DestroyImmediate(rendererData);
                Object.DestroyImmediate(renderFeature);
                Object.DestroyImmediate(maskProbeFeature);
                Object.DestroyImmediate(depthProbeFeature);
                globals.Restore();
            }
        }

        /// <summary>
        /// <c>Render</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static Color Render(
            Camera camera,
            RenderTexture target,
            Material opaqueMaterial,
            Mesh mesh,
            uint role,
            float blend,
            float pulseStrength,
            bool blocked,
            MaskProbeFeature maskProbeFeature = null)
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
                Shader.SetGlobalColor(AttachVisualShaderIds.EligibleColor, new Color(1f, 0.7f, 0.05f, 1f));
                Shader.SetGlobalColor(AttachVisualShaderIds.FocusedColor, new Color(1f, 0.9f, 0.2f, 1f));
                Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, new Color(0.05f, 0.95f, 0.25f, 1f));
                Shader.SetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, pulseStrength);
                if (maskProbeFeature != null)
                {
                    maskProbeFeature.Enabled = true;
                }

                RenderPipeline.StandardRequest request = new() { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                return ReadCenterPixel(target);
            }
            finally
            {
                if (maskProbeFeature != null)
                {
                    maskProbeFeature.Enabled = false;
                }

                Object.DestroyImmediate(roleObject);
                Object.DestroyImmediate(blocker);
            }
        }

        /// <summary>
        /// <c>RenderDepthProbe</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static Color RenderDepthProbe(
            Camera camera,
            RenderTexture target,
            Material roleMaterial,
            DepthProbeFeature depthProbeFeature)
        {
            GameObject roleObject = null;
            try
            {
                roleObject = new GameObject("Attach Feature Depth Probe Role");
                roleObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                Renderer renderer = roleObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = roleMaterial;
                renderer.renderingLayerMask = AttachVisualLayers.Held;
                roleObject.transform.position = Vector3.forward;

                Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, 1f);
                Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, new Color(0.05f, 0.95f, 0.25f, 1f));
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, 0f);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, 0f);
                depthProbeFeature.Enabled = true;
                RenderPipeline.StandardRequest request = new() { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                return ReadCenterPixel(target);
            }
            finally
            {
                depthProbeFeature.Enabled = false;
                Object.DestroyImmediate(roleObject);
            }
        }

        /// <summary>
        /// <c>CreateBackfaceTriangle</c> 테스트 지원 동작을 수행한다.
        /// </summary>
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

        /// <summary>
        /// <c>ReadCenterPixel</c> 테스트 지원 동작을 수행한다.
        /// </summary>
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

        /// <summary>
        /// <c>ColorDistance</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static float ColorDistance(Color first, Color second) =>
            Vector3.Distance(new Vector3(first.r, first.g, first.b), new Vector3(second.r, second.g, second.b));

        /// <summary>
        /// 테스트에서 사용하는 <c>AttachGlobals</c> 보조 타입이다.
        /// </summary>
        private readonly struct AttachGlobals
        {
            private readonly float blend, eligibleWidth, focusedWidth, heldWidth, pulseSpeed, pulseStrength;
            private readonly Color eligible, focused, held;
            private readonly Texture mask;

            private AttachGlobals(float blend, Color eligible, Color focused, Color held,
                float eligibleWidth, float focusedWidth, float heldWidth, float pulseSpeed, float pulseStrength, Texture mask)
            {
                this.blend = blend;
                this.eligible = eligible; this.focused = focused; this.held = held;
                this.eligibleWidth = eligibleWidth; this.focusedWidth = focusedWidth; this.heldWidth = heldWidth;
                this.pulseSpeed = pulseSpeed; this.pulseStrength = pulseStrength; this.mask = mask;
            }

            /// <summary>
            /// <c>Capture</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            internal static AttachGlobals Capture() => new(
                Shader.GetGlobalFloat(AttachVisualShaderIds.VisualBlend), Shader.GetGlobalColor(AttachVisualShaderIds.EligibleColor),
                Shader.GetGlobalColor(AttachVisualShaderIds.FocusedColor), Shader.GetGlobalColor(AttachVisualShaderIds.HeldColor),
                Shader.GetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels), Shader.GetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels),
                Shader.GetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels), Shader.GetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed),
                Shader.GetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength), Shader.GetGlobalTexture(AttachVisualShaderIds.MaskTexture));

            /// <summary>
            /// <c>Restore</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            internal void Restore()
            {
                Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, blend); Shader.SetGlobalColor(AttachVisualShaderIds.EligibleColor, eligible);
                Shader.SetGlobalColor(AttachVisualShaderIds.FocusedColor, focused); Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, held);
                Shader.SetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels, eligibleWidth); Shader.SetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels, focusedWidth);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, heldWidth); Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, pulseSpeed);
                Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, pulseStrength); Shader.SetGlobalTexture(AttachVisualShaderIds.MaskTexture, mask);
            }
        }

        /// <summary>
        /// 테스트에서 사용하는 <c>MaskProbeFeature</c> 보조 타입이다.
        /// </summary>
        private sealed class MaskProbeFeature : ScriptableRendererFeature
        {
            /// <summary>
            /// 테스트에서 사용하는 <c>PassData</c> 보조 타입이다.
            /// </summary>
            private sealed class PassData
            {
                internal TextureHandle Source;
                internal Material Material;
            }

            private Material material;
            private MaskProbePass pass;

            internal bool Enabled { get; set; }

            /// <summary>
            /// <c>Configure</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            internal void Configure(Material sourceMaterial)
            {
                material = sourceMaterial;
            }

            /// <summary>
            /// <c>Create</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            public override void Create()
            {
                pass = new MaskProbePass(this)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
                };
            }

            /// <summary>
            /// <c>AddRenderPasses</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                if (Enabled && material != null)
                {
                    renderer.EnqueuePass(pass);
                }
            }

            /// <summary>
            /// 테스트에서 사용하는 <c>MaskProbePass</c> 보조 타입이다.
            /// </summary>
            private sealed class MaskProbePass : ScriptableRenderPass
            {
                private readonly MaskProbeFeature feature;

                internal MaskProbePass(MaskProbeFeature sourceFeature)
                {
                    feature = sourceFeature;
                }

                /// <summary>
                /// <c>RecordRenderGraph</c> 테스트 지원 동작을 수행한다.
                /// </summary>
                public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
                {
                    if (!feature.Enabled || feature.material == null)
                    {
                        return;
                    }

                    UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                    TextureHandle source = resources.activeColorTexture;
                    TextureDesc descriptor = renderGraph.GetTextureDesc(source);
                    descriptor.name = "Attach Test Live Mask Capture";
                    TextureHandle destination = renderGraph.CreateTexture(descriptor);
                    using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                        "Attach Test Live Mask Capture", out PassData passData);
                    passData.Source = source;
                    passData.Material = feature.material;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.UseGlobalTexture(AttachVisualShaderIds.MaskTexture);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, (RTHandle)data.Source, new Vector4(1f, 1f, 0f, 0f),
                            data.Material, 0);
                    });
                    resources.cameraColor = destination;
                }
            }
        }

        /// <summary>
        /// 테스트에서 사용하는 <c>DepthProbeFeature</c> 보조 타입이다.
        /// </summary>
        private sealed class DepthProbeFeature : ScriptableRendererFeature
        {
            /// <summary>
            /// 테스트에서 사용하는 <c>PassData</c> 보조 타입이다.
            /// </summary>
            private sealed class PassData
            {
                internal Material Material;
                internal Mesh Mesh;
            }

            private DepthProbePass pass;

            internal bool Enabled
            {
                get => pass != null && pass.Enabled;
                set
                {
                    pass ??= new DepthProbePass();
                    pass.Enabled = value;
                }
            }

            /// <summary>
            /// <c>Configure</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            internal void Configure(Material material, Mesh mesh)
            {
                pass ??= new DepthProbePass();
                pass.Setup(material, mesh);
            }

            /// <summary>
            /// <c>Create</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            public override void Create()
            {
                pass ??= new DepthProbePass();
                pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            /// <summary>
            /// <c>AddRenderPasses</c> 테스트 지원 동작을 수행한다.
            /// </summary>
            public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                if (pass != null && pass.Enabled)
                {
                    renderer.EnqueuePass(pass);
                }
            }

            /// <summary>
            /// 테스트에서 사용하는 <c>DepthProbePass</c> 보조 타입이다.
            /// </summary>
            private sealed class DepthProbePass : ScriptableRenderPass
            {
                private Material material;
                private Mesh mesh;

                internal bool Enabled { get; set; }

                /// <summary>
                /// <c>Setup</c> 테스트 지원 동작을 수행한다.
                /// </summary>
                internal void Setup(Material sourceMaterial, Mesh sourceMesh)
                {
                    material = sourceMaterial;
                    mesh = sourceMesh;
                }

                /// <summary>
                /// <c>RecordRenderGraph</c> 테스트 지원 동작을 수행한다.
                /// </summary>
                public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
                {
                    if (!Enabled || material == null || mesh == null)
                    {
                        return;
                    }

                    UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                    using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                        "Attach Test Depth Probe", out PassData passData);
                    passData.Material = material;
                    passData.Mesh = mesh;
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawMesh(data.Mesh, Matrix4x4.TRS(Vector3.forward * 2f, Quaternion.identity, Vector3.one),
                            data.Material, 0, 0);
                    });
                }
            }
        }

    }
}
