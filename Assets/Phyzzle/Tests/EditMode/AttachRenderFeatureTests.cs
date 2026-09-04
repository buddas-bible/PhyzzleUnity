using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Tests
{
    public sealed class AttachRenderFeatureTests
    {
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
    }
}
