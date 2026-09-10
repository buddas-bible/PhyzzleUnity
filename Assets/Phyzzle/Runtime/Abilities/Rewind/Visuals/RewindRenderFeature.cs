using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// URP RenderGraph에서 되감기 선택 마스크를 만들고 화면 합성 효과를 적용하는 Renderer Feature다.
    /// </summary>
    public sealed class RewindRenderFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// 플레이어 보존, 선택 가능, 활성 대상 마스크와 후처리 합성을 기록하는 URP 렌더 패스다.
        /// </summary>
        private sealed class RewindRenderPass : ScriptableRenderPass
        {
            /// <summary>
            /// 마스크 패스에서 사용할 렌더러 목록 핸들을 보관한다.
            /// </summary>
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

            /// <summary>
            /// 마스크와 합성 재질을 설정하고 중간 컬러 텍스처 사용을 요구한다.
            /// </summary>
            internal void Setup(Material rewindMaskMaterial, Material rewindCompositeMaterial)
            {
                maskMaterial = rewindMaskMaterial;
                compositeMaterial = rewindCompositeMaterial;
                requiresIntermediateTexture = true;
            }

            /// <summary>
            /// 되감기 마스크 렌더 패스와 화면 합성 블릿 패스를 RenderGraph에 기록한다.
            /// </summary>
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                // BackBuffer를 직접 사용하는 카메라는 source/destination 분리가 안 되므로 후처리 패스를 생략
                if (maskMaterial == null || compositeMaterial == null ||
                    resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                // R/G/B에 Player Preserve/Eligible/Active 값을 저장할 독립 마스크를 생성
                // 경계의 채널 값이 섞이지 않도록 Point 필터와 비 MSAA 텍스처를 사용
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
                    // 동일 Mask Shader의 세 Pass를 Rendering Layer별로 나눠 각각 다른 채널에 기록
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
                    // 기존 씬 Depth를 읽어 화면 앞쪽에 가려진 대상은 마스크에도 가려지도록 함
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
                // 원본 화면을 읽으면서 같은 텍스처에 쓸 수 없으므로 합성 결과용 Color Texture를 따로 생성
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

            /// <summary>
            /// 지정한 렌더링 레이어를 마스크 재질의 특정 패스로 그릴 RendererList를 생성한다.
            /// </summary>
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
                // 대상의 원래 Material은 무시하고 상태 채널을 기록하는 Mask Material로 교체
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

        /// <summary>
        /// Renderer Feature에서 사용할 마스크와 합성 재질을 구성한다.
        /// </summary>
        public void Configure(Material rewindMaskMaterial, Material rewindCompositeMaterial)
        {
            maskMaterial = rewindMaskMaterial;
            compositeMaterial = rewindCompositeMaterial;
            pass?.Setup(maskMaterial, compositeMaterial);
        }

        /// <summary>
        /// 후처리 이후 실행될 되감기 렌더 패스를 생성하고 재질이 있으면 설정한다.
        /// </summary>
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

        /// <summary>
        /// 현재 카메라와 선택 블렌드 조건이 유효할 때 되감기 렌더 패스를 큐에 추가한다.
        /// </summary>
        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            bool materialsValid = maskMaterial != null && compositeMaterial != null;
            // 게임의 Base 카메라에서 효과가 실제 보일 때만 RenderGraph 작업을 추가
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

        /// <summary>
        /// 게임 Base 카메라에서 유효한 재질과 선택 블렌드가 있을 때만 패스를 실행하도록 판정한다.
        /// </summary>
        internal static bool ShouldEnqueue(
            CameraType cameraType,
            CameraRenderType renderType,
            bool materialsValid,
            float selectionBlend)
        {
            return materialsValid && selectionBlend > 0f &&
                   cameraType == CameraType.Game && renderType == CameraRenderType.Base;
        }

        /// <summary>
        /// 지정한 URP RendererData에 활성화되고 완전히 구성된 되감기 렌더 피처가 있는지 확인한다.
        /// </summary>
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
