using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Abilities.Rewind
{
    public sealed class RewindRenderFeature : ScriptableRendererFeature
    {
        private sealed class RewindRenderPass : ScriptableRenderPass
        {
            private sealed class MaskPassData
            {
                internal RendererListHandle Player;
                internal RendererListHandle Eligible;
                internal RendererListHandle Active;
            }

            internal const GraphicsFormat RequiredMaskFormat = GraphicsFormat.R8G8B8A8_UNorm;
            private static readonly ShaderTagId[] ShaderTags =
            {
                new("UniversalForwardOnly"),
                new("UniversalForward"),
                new("SRPDefaultUnlit"),
                new("LightweightForward")
            };

            private readonly List<ShaderTagId> shaderTags = new(ShaderTags);
            private Material maskMaterial;
            private Material compositeMaterial;

            internal bool HasIntermediateRequirement => requiresIntermediateTexture;

            internal void Setup(Material rewindMaskMaterial, Material rewindCompositeMaterial)
            {
                maskMaterial = rewindMaskMaterial;
                compositeMaterial = rewindCompositeMaterial;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (maskMaterial == null || compositeMaterial == null ||
                    resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureDesc maskDescriptor =
                    renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                maskDescriptor.name = "Rewind Selection Mask";
                maskDescriptor.colorFormat = RequiredMaskFormat;
                maskDescriptor.msaaSamples = MSAASamples.None;
                maskDescriptor.bindTextureMS = false;
                maskDescriptor.filterMode = FilterMode.Point;
                maskDescriptor.useMipMap = false;
                maskDescriptor.autoGenerateMips = false;
                maskDescriptor.clearBuffer = true;
                maskDescriptor.clearColor = Color.clear;
                TextureHandle mask = renderGraph.CreateTexture(maskDescriptor);

                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<MaskPassData>(
                           "Rewind Selection Mask",
                           out MaskPassData passData))
                {
                    passData.Player = CreateRendererList(
                        renderGraph,
                        frameData,
                        RewindVisualLayers.PlayerPreserve,
                        0);
                    passData.Eligible = CreateRendererList(
                        renderGraph,
                        frameData,
                        RewindVisualLayers.Eligible,
                        1);
                    passData.Active = CreateRendererList(
                        renderGraph,
                        frameData,
                        RewindVisualLayers.Active,
                        2);

                    builder.UseRendererList(passData.Player);
                    builder.UseRendererList(passData.Eligible);
                    builder.UseRendererList(passData.Active);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(
                        resourceData.activeDepthTexture,
                        AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(mask, RewindVisualShaderIds.MaskTexture);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.Player);
                        context.cmd.DrawRendererList(data.Eligible);
                        context.cmd.DrawRendererList(data.Active);
                    });
                }

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "Rewind Composite Color";
                destinationDescriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);
                RenderGraphUtils.BlitMaterialParameters parameters =
                    new(source, destination, compositeMaterial, 0);
                IBaseRenderGraphBuilder compositeBuilder = renderGraph.AddBlitPass(
                    parameters,
                    "Rewind Selection Composite",
                    returnBuilder: true);
                compositeBuilder.UseGlobalTexture(RewindVisualShaderIds.MaskTexture);
                compositeBuilder.Dispose();
                resourceData.cameraColor = destination;
            }

            private RendererListHandle CreateRendererList(
                RenderGraph renderGraph,
                ContextContainer frameData,
                uint renderingLayerMask,
                int materialPass)
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    shaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    cameraData.defaultOpaqueSortFlags);
                drawingSettings.overrideMaterial = maskMaterial;
                drawingSettings.overrideMaterialPassIndex = materialPass;
                FilteringSettings filteringSettings = new(RenderQueueRange.all, -1)
                {
                    renderingLayerMask = renderingLayerMask
                };
                return renderGraph.CreateRendererList(new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings));
            }
        }

        [SerializeField] private Material maskMaterial;
        [SerializeField] private Material compositeMaterial;

        private RewindRenderPass pass;

        internal bool IsConfigured =>
            pass != null && maskMaterial != null && compositeMaterial != null;
        internal RenderPassEvent PassEvent =>
            pass != null ? pass.renderPassEvent : RenderPassEvent.BeforeRendering;
        internal bool RequiresIntermediateTexture =>
            pass?.HasIntermediateRequirement == true;
        internal GraphicsFormat MaskFormat => RewindRenderPass.RequiredMaskFormat;
        internal Material MaskMaterial => maskMaterial;
        internal Material CompositeMaterial => compositeMaterial;

        public void Configure(Material rewindMaskMaterial, Material rewindCompositeMaterial)
        {
            maskMaterial = rewindMaskMaterial;
            compositeMaterial = rewindCompositeMaterial;
            pass?.Setup(maskMaterial, compositeMaterial);
        }

        public override void Create()
        {
            pass = new RewindRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
            if (maskMaterial != null && compositeMaterial != null)
            {
                pass.Setup(maskMaterial, compositeMaterial);
            }
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            bool materialsValid = maskMaterial != null && compositeMaterial != null;
            if (!ShouldEnqueue(
                    renderingData.cameraData.cameraType,
                    renderingData.cameraData.renderType,
                    materialsValid,
                    Shader.GetGlobalFloat(RewindVisualShaderIds.SelectionBlend)))
            {
                return;
            }

            pass ??= new RewindRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
            pass.Setup(maskMaterial, compositeMaterial);
            renderer.EnqueuePass(pass);
        }

        internal static bool ShouldEnqueue(
            CameraType cameraType,
            CameraRenderType renderType,
            bool materialsValid,
            float selectionBlend)
        {
            return materialsValid && selectionBlend > 0f &&
                   cameraType == CameraType.Game && renderType == CameraRenderType.Base;
        }

        internal static bool SupportsRendererData(ScriptableRendererData rendererData)
        {
            if (rendererData == null)
            {
                return false;
            }

            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature is RewindRenderFeature rewind && rewind.isActive &&
                    rewind.maskMaterial != null && rewind.compositeMaterial != null)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
