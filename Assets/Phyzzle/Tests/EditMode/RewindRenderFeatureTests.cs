using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Tests
{
    public sealed class RewindRenderFeatureTests
    {
        private Material maskMaterial;
        private Material compositeMaterial;
        private Material previewMaterial;
        private RewindRenderFeature feature;

        [SetUp]
        public void SetUp()
        {
            Shader mask = Shader.Find("Hidden/Phyzzle/RewindMask");
            Shader composite = Shader.Find("Hidden/Phyzzle/RewindComposite");
            Shader preview = Shader.Find("Phyzzle/RewindPreview");
            Assert.That(mask, Is.Not.Null);
            Assert.That(composite, Is.Not.Null);
            Assert.That(preview, Is.Not.Null);
            maskMaterial = new Material(mask);
            compositeMaterial = new Material(composite);
            previewMaterial = new Material(preview);
            feature = ScriptableObject.CreateInstance<RewindRenderFeature>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(maskMaterial);
            Object.DestroyImmediate(compositeMaterial);
            Object.DestroyImmediate(previewMaterial);
        }

        [Test]
        public void MaskAndPreviewShaders_ExposeRequiredRenderContracts()
        {
            Assert.That(maskMaterial.passCount, Is.EqualTo(3));
            Assert.That(maskMaterial.FindPass("Player Preserve"), Is.EqualTo(0));
            Assert.That(maskMaterial.FindPass("Eligible"), Is.EqualTo(1));
            Assert.That(maskMaterial.FindPass("Active"), Is.EqualTo(2));
            Assert.That(previewMaterial.HasProperty("_BaseColor"), Is.True);
            Assert.That(maskMaterial.HasProperty("_RewindPreviewMaskWeight"), Is.True);
            Assert.That(previewMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000));
            string previewSource = System.IO.File.ReadAllText(
                AssetDatabase.GetAssetPath(previewMaterial.shader));
            StringAssert.Contains("Blend SrcAlpha One", previewSource);
        }

        [Test]
        public void SupportedRenderer_IsPcOnly()
        {
            UniversalRendererData pc = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/Settings/PC_Renderer.asset");
            UniversalRendererData mobile = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/Settings/Mobile_Renderer.asset");

            Assert.That(RewindRenderFeature.SupportsRendererData(pc), Is.True);
            Assert.That(RewindRenderFeature.SupportsRendererData(mobile), Is.False);

            UniversalRenderPipelineAsset pcPipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    "Assets/Settings/PC_RPAsset.asset");
            UniversalRenderPipelineAsset mobilePipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    "Assets/Settings/Mobile_RPAsset.asset");
            Assert.That(RewindVisualController.SupportsPipeline(pcPipeline, pcPipeline), Is.True);
            Assert.That(RewindVisualController.SupportsPipeline(pcPipeline, mobilePipeline), Is.False);
            Assert.That(RewindVisualController.SupportsPipeline(null, pcPipeline), Is.False);
        }

        [Test]
        public void Configure_CreatesOnePostProcessingPassThatRequiresIntermediateColor()
        {
            feature.Configure(maskMaterial, compositeMaterial);
            feature.Create();

            Assert.That(feature.IsConfigured, Is.True);
            Assert.That(feature.PassEvent, Is.EqualTo(RenderPassEvent.AfterRenderingPostProcessing));
            Assert.That(feature.RequiresIntermediateTexture, Is.True);
            Assert.That(feature.MaskFormat, Is.EqualTo(GraphicsFormat.R8G8B8A8_UNorm));
        }

        [Test]
        public void Configure_MissingMaterialFailsClosed()
        {
            feature.Configure(null, compositeMaterial);
            feature.Create();

            Assert.That(feature.IsConfigured, Is.False);
            Assert.That(RewindRenderFeature.ShouldEnqueue(
                CameraType.Game,
                CameraRenderType.Base,
                materialsValid: false,
                selectionBlend: 1f),
                Is.False);
        }

        [Test]
        public void ShouldEnqueue_AcceptsOnlyActiveBaseGameCamera()
        {
            Assert.That(RewindRenderFeature.ShouldEnqueue(
                CameraType.Game,
                CameraRenderType.Base,
                materialsValid: true,
                selectionBlend: 1f),
                Is.True);
            Assert.That(RewindRenderFeature.ShouldEnqueue(
                CameraType.Game,
                CameraRenderType.Overlay,
                materialsValid: true,
                selectionBlend: 1f),
                Is.False);
            Assert.That(RewindRenderFeature.ShouldEnqueue(
                CameraType.SceneView,
                CameraRenderType.Base,
                materialsValid: true,
                selectionBlend: 1f),
                Is.False);
            Assert.That(RewindRenderFeature.ShouldEnqueue(
                CameraType.Preview,
                CameraRenderType.Base,
                materialsValid: true,
                selectionBlend: 1f),
                Is.False);
            Assert.That(RewindRenderFeature.ShouldEnqueue(
                CameraType.Reflection,
                CameraRenderType.Base,
                materialsValid: true,
                selectionBlend: 1f),
                Is.False);
            Assert.That(RewindRenderFeature.ShouldEnqueue(
                CameraType.Game,
                CameraRenderType.Base,
                materialsValid: true,
                selectionBlend: 0f),
                Is.False);
        }
    }
}
