using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// URP RenderGraph에서 부착 대상 마스크를 만들고 화면 합성 효과를 적용하는 Renderer Feature다.
    /// </summary>
    public sealed class AttachRenderFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// 렌더링 레이어별 부착 마스크 생성과 후처리 합성을 기록하는 URP 렌더 패스다.
        /// </summary>
        private sealed class AttachRenderPass : ScriptableRenderPass
        {
            /// <summary>
            /// 마스크 패스에서 사용할 Eligible, Focused, Held RendererList 핸들을 보관한다.
            /// </summary>
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

            /// <summary>
            /// 마스크·합성 재질을 설정하고 중간 컬러 텍스처 사용을 요구한다.
            /// </summary>
            internal void Setup(Material attachMaskMaterial, Material attachCompositeMaterial)
            {
                maskMaterial = attachMaskMaterial;
                compositeMaterial = attachCompositeMaterial;
                requiresIntermediateTexture = true;
            }

            /// <summary>
            /// 부착 마스크 렌더 패스와 화면 합성 블릿 패스를 RenderGraph에 기록한다.
            /// </summary>
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                // 재질이 없거나 카메라가 BackBuffer에 직접 그리는 경우 중간 텍스처를 안전하게 합성할 수 없으므로 패스를 만들지 않음
                if (maskMaterial == null || compositeMaterial == null || resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                // R/G/B 채널에 Eligible/Focused/Held 상태를 각각 기록하므로 3채널 이상인 정규화 포맷을 사용
                // 마스크 경계가 섞이지 않도록 MSAA와 선형 필터링, MipMap은 사용하지 않음
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
                    // 같은 Mask Material의 0/1/2번 Pass가 각각 R/G/B 상태값을 출력
                    passData.Eligible = CreateRendererList(renderGraph, frameData, AttachVisualLayers.Eligible, 0);
                    passData.Focused = CreateRendererList(renderGraph, frameData, AttachVisualLayers.Focused, 1);
                    passData.Held = CreateRendererList(renderGraph, frameData, AttachVisualLayers.Held, 2);
                    builder.UseRendererList(passData.Eligible);
                    builder.UseRendererList(passData.Focused);
                    builder.UseRendererList(passData.Held);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    // 씬 Depth를 읽어 가려진 오브젝트의 마스크가 앞쪽 지형을 뚫고 나오지 않게 함
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    // 다음 Composite Pass가 별도 핸들 전달 없이 같은 마스크를 샘플링할 수 있도록 전역 텍스처로 등록
                    builder.SetGlobalTextureAfterPass(mask, AttachVisualShaderIds.MaskTexture);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.Eligible);
                        context.cmd.DrawRendererList(data.Focused);
                        context.cmd.DrawRendererList(data.Held);
                    });
                }

                TextureHandle source = resourceData.activeColorTexture;
                // 같은 Color Texture를 동시에 읽고 쓸 수 없으므로 별도 destination을 만든 뒤 cameraColor를 교체
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
                    shaderTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
                // 원본 Material 대신 Mask Material의 지정 Pass로 렌더해 상태 채널만 기록
                drawingSettings.overrideMaterial = maskMaterial;
                drawingSettings.overrideMaterialPassIndex = materialPass;
                FilteringSettings filteringSettings = new(RenderQueueRange.all, -1)
                {
                    // GameObject Layer가 아니라 Rendering Layer로 상태 그룹을 분리
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

        /// <summary>
        /// Renderer Feature에서 사용할 마스크와 합성 재질을 구성한다.
        /// </summary>
        public void Configure(Material attachMaskMaterial, Material attachCompositeMaterial)
        {
            maskMaterial = attachMaskMaterial;
            compositeMaterial = attachCompositeMaterial;
            pass?.Setup(maskMaterial, compositeMaterial);
        }

        /// <summary>
        /// 후처리 이후 실행될 부착 렌더 패스를 생성하고 재질이 있으면 설정한다.
        /// </summary>
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

        /// <summary>
        /// 현재 카메라와 시각 블렌드 조건이 유효할 때 부착 렌더 패스를 큐에 추가한다.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool materialsValid = maskMaterial != null && compositeMaterial != null;
            // SceneView/Overlay 카메라나 효과 Blend가 0인 프레임에서는 불필요한 마스크/Blit 패스를 만들지 않음
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

        /// <summary>
        /// 게임 Base 카메라에서 유효한 재질과 시각 블렌드가 있을 때만 패스를 실행하도록 판정한다.
        /// </summary>
        internal static bool ShouldEnqueue(
            CameraType cameraType,
            CameraRenderType renderType,
            bool materialsValid,
            float visualBlend)
        {
            return materialsValid && visualBlend > 0f &&
                   cameraType == CameraType.Game && renderType == CameraRenderType.Base;
        }

        /// <summary>
        /// RendererData에 활성화되고 완전히 구성된 AttachRenderFeature가 있는지 확인한다.
        /// </summary>
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
