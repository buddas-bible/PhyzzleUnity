using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Abilities.Attach
{
    public sealed class AttachRenderFeature : ScriptableRendererFeature
    {
        private sealed class AttachRenderPass : ScriptableRenderPass
        {
            private sealed class MaskPassData
            {
                internal RendererListHandle Eligible;
                internal RendererListHandle Focused;
                internal RendererListHandle Held;
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

            internal void Setup(Material attachMaskMaterial, Material attachCompositeMaterial)
            {
                maskMaterial = attachMaskMaterial;
                compositeMaterial = attachCompositeMaterial;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (maskMaterial == null || compositeMaterial == null || resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureDesc maskDescriptor = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                maskDescriptor.name = "Attach Selection Mask";
                maskDescriptor.colorFormat = RequiredMaskFormat;
                maskDescriptor.msaaSamples = MSAASamples.None;
                maskDescriptor.bindTextureMS = false;
                maskDescriptor.filterMode = FilterMode.Point;
                maskDescriptor.useMipMap = false;
                maskDescriptor.autoGenerateMips = false;
                maskDescriptor.clearBuffer = true;
                maskDescriptor.clearColor = Color.clear;
                TextureHandle mask = renderGraph.CreateTexture(maskDescriptor);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           "Attach Selection Mask", out MaskPassData passData))
                {
                    passData.Eligible = CreateRendererList(renderGraph, frameData, AttachVisualLayers.Eligible, 0);
                    passData.Focused = CreateRendererList(renderGraph, frameData, AttachVisualLayers.Focused, 1);
                    passData.Held = CreateRendererList(renderGraph, frameData, AttachVisualLayers.Held, 2);
                    builder.UseRendererList(passData.Eligible);
                    builder.UseRendererList(passData.Focused);
                    builder.UseRendererList(passData.Held);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(mask, AttachVisualShaderIds.MaskTexture);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.Eligible);
                        context.cmd.DrawRendererList(data.Focused);
                        context.cmd.DrawRendererList(data.Held);
                    });
                }

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "Attach Composite Color";
                destinationDescriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);
                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, compositeMaterial, 0);
                IBaseRenderGraphBuilder compositeBuilder = renderGraph.AddBlitPass(
                    parameters, "Attach Selection Composite", returnBuilder: true);
                compositeBuilder.UseGlobalTexture(AttachVisualShaderIds.MaskTexture);
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
                    shaderTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
                drawingSettings.overrideMaterial = maskMaterial;
                drawingSettings.overrideMaterialPassIndex = materialPass;
                FilteringSettings filteringSettings = new(RenderQueueRange.all, -1)
                {
                    renderingLayerMask = renderingLayerMask
                };
                return renderGraph.CreateRendererList(new RendererListParams(
                    renderingData.cullResults, drawingSettings, filteringSettings));
            }
        }

        [SerializeField] private Material maskMaterial;
        [SerializeField] private Material compositeMaterial;

        private AttachRenderPass pass;

        internal bool IsConfigured => pass != null && maskMaterial != null && compositeMaterial != null;
        internal RenderPassEvent PassEvent => pass != null ? pass.renderPassEvent : RenderPassEvent.BeforeRendering;
        internal bool RequiresIntermediateTexture => pass?.HasIntermediateRequirement == true;
        internal GraphicsFormat MaskFormat => AttachRenderPass.RequiredMaskFormat;

        public void Configure(Material attachMaskMaterial, Material attachCompositeMaterial)
        {
            maskMaterial = attachMaskMaterial;
            compositeMaterial = attachCompositeMaterial;
            pass?.Setup(maskMaterial, compositeMaterial);
        }

        public override void Create()
        {
            pass = new AttachRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
            if (maskMaterial != null && compositeMaterial != null)
            {
                pass.Setup(maskMaterial, compositeMaterial);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool materialsValid = maskMaterial != null && compositeMaterial != null;
            if (!ShouldEnqueue(
                    renderingData.cameraData.cameraType,
                    renderingData.cameraData.renderType,
                    materialsValid,
                    Shader.GetGlobalFloat(AttachVisualShaderIds.VisualBlend)))
            {
                return;
            }

            pass ??= new AttachRenderPass
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
            float visualBlend)
        {
            return materialsValid && visualBlend > 0f &&
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
                if (feature is AttachRenderFeature attach && attach.isActive &&
                    attach.maskMaterial != null && attach.compositeMaterial != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
