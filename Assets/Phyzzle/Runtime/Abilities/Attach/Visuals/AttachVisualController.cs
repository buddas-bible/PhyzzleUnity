using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 부착 능력 상태에 맞춰 대상 렌더링 레이어, 투영, 테더와 접촉 프리뷰 시각 효과를 통합 제어한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachVisualController : MonoBehaviour
    {
        [SerializeField] private AttachAbilityController ability;
        [SerializeField] private AttachTargeting targeting;
        [SerializeField] private AttachHoldController holdController;
        [SerializeField] private AttachmentService attachmentService;
        [SerializeField] private AttachProjectionRenderer projectionRenderer;
        [SerializeField] private AttachTetherRenderer tetherRenderer;
        [SerializeField] private AttachContactPreviewRenderer contactPreviewRenderer;
        [SerializeField] private AttachSettings settings;
        [SerializeField] private RenderPipelineAsset supportedPipeline;

        private readonly Dictionary<AttachableObject, uint> desiredObjectRoles = new();
        private readonly Dictionary<AttachableObject, uint> appliedObjectRoles = new();
        private readonly Dictionary<Renderer, uint> desiredRendererRoles = new();
        private readonly Dictionary<Renderer, uint> appliedRendererRoles = new();
        private readonly List<Renderer> rendererScratch = new();
        private readonly List<AttachableObject> islandMembers = new();
        private readonly List<AttachableObject> projectionIsland = new();

        private float visualBlend;
        private int islandRefreshCount;
        private AttachableObject observedHeldRoot;
        private int observedTopologyVersion = -1;

        internal float VisualBlend => visualBlend;
        internal int IslandRefreshCount => islandRefreshCount;

        /// <summary>
        /// 부착 시각 효과에서 사용할 능력·타게팅·부착 서비스·렌더러와 설정 참조를 구성한다.
        /// </summary>
        public void Configure(
            AttachAbilityController attachAbility,
            AttachTargeting attachTargeting,
            AttachHoldController attachHoldController,
            AttachmentService attachAttachmentService,
            AttachProjectionRenderer attachProjectionRenderer,
            AttachSettings attachSettings,
            RenderPipelineAsset attachSupportedPipeline,
            AttachTetherRenderer attachTetherRenderer = null,
            AttachContactPreviewRenderer attachContactPreviewRenderer = null)
        {
            HardCleanup();
            ability = attachAbility;
            targeting = attachTargeting;
            holdController = attachHoldController;
            attachmentService = attachAttachmentService;
            projectionRenderer = attachProjectionRenderer;
            tetherRenderer = attachTetherRenderer;
            contactPreviewRenderer = attachContactPreviewRenderer;
            settings = attachSettings;
            supportedPipeline = attachSupportedPipeline;
        }

        /// <summary>
        /// LateUpdate에서 시간 배율과 무관하게 현재 부착 시각 상태를 갱신한다.
        /// </summary>
        private void LateUpdate()
        {
            TickVisual(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 비활성화될 때 적용한 렌더링 레이어와 모든 임시 시각 효과를 정리한다.
        /// </summary>
        private void OnDisable()
        {
            HardCleanup();
        }

        /// <summary>
        /// 파괴될 때 적용한 렌더링 레이어와 모든 임시 시각 효과를 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            HardCleanup();
        }

        /// <summary>
        /// 현재 부착 능력 상태에 따라 선택·들기·퇴장 시각 효과와 전역 셰이더 값을 갱신한다.
        /// </summary>
        internal void TickVisual(float unscaledDeltaTime)
        {
            // 필요한 시스템이 비활성/누락되거나 다른 RenderPipeline이면 이전 Rendering Layer와 전역 Shader 값까지 즉시 정리
            if (ability == null || targeting == null || holdController == null || attachmentService == null ||
                projectionRenderer == null || settings == null || !ability.isActiveAndEnabled ||
                !targeting.isActiveAndEnabled || !holdController.isActiveAndEnabled ||
                !SupportsPipeline(supportedPipeline, GraphicsSettings.currentRenderPipeline))
            {
                HardCleanup();
                return;
            }

            // 선택 상태에서는 Eligible/Focused만 사용하므로 들기 전용 Projection/Tether/Contact Preview는 제거
            if (ability.State == AttachAbilityController.AbilityState.Selecting)
            {
                BuildSelectingRoles();
                UpdateRoles();
                ClearProjection();
                tetherRenderer?.Clear();
                contactPreviewRenderer?.Clear();
                visualBlend = MoveBlend(visualBlend, 1f, settings.visualEnterDuration, unscaledDeltaTime);
                PushGlobals();
                return;
            }

            if (ability.State == AttachAbilityController.AbilityState.Holding)
            {
                AttachableObject heldRoot = holdController.HeldObject;
                // 들던 Root가 파괴/비활성화되면 캐시된 Renderer 역할이 남지 않도록 전체 시각 상태를 정리
                if (heldRoot == null || !heldRoot.isActiveAndEnabled)
                {
                    HardCleanup();
                    return;
                }

                // Root가 바뀌거나 Attach/Detach로 그래프 구조가 변한 경우에만 섬과 Renderer 목록을 다시 수집
                if (heldRoot != observedHeldRoot || observedTopologyVersion != attachmentService.TopologyVersion)
                {
                    BuildHoldingRoles(heldRoot);
                    UpdateRoles();
                    UpdateProjectionIsland();
                    observedHeldRoot = heldRoot;
                    observedTopologyVersion = attachmentService.TopologyVersion;
                }
                visualBlend = MoveBlend(visualBlend, 1f, settings.visualEnterDuration, unscaledDeltaTime);
                PushGlobals();
                projectionRenderer.Submit(unscaledDeltaTime);
                tetherRenderer?.Submit(heldRoot, visualBlend);
                UpdateContactPreview();
                return;
            }

            FadeOut(unscaledDeltaTime);
        }

        /// <summary>
        /// 현재 들기 섬의 첫 유효 접촉 후보를 찾아 접촉 글루 프리뷰를 갱신한다.
        /// </summary>
        private void UpdateContactPreview()
        {
            if (contactPreviewRenderer == null)
            {
                return;
            }

            if (attachmentService.TryGetPreviewContact(islandMembers,
                out AttachableObject member, out AttachableObject other, out Vector3 anchor))
            {
                contactPreviewRenderer.Submit(member, other, anchor, visualBlend);
            }
            else
            {
                contactPreviewRenderer.Clear();
            }
        }

        /// <summary>
        /// 선택 모드의 주변 후보와 현재 대상에 Eligible·Focused 역할을 배정한다.
        /// </summary>
        private void BuildSelectingRoles()
        {
            desiredObjectRoles.Clear();
            IReadOnlyList<AttachableObject> nearby = targeting.Nearby;
            for (int i = 0; i < nearby.Count; i++)
            {
                AddObjectRole(nearby[i], AttachVisualLayers.Eligible);
            }

            AddObjectRole(targeting.CurrentTarget, AttachVisualLayers.Focused);
        }

        /// <summary>
        /// 들고 있는 루트가 속한 전체 부착 섬에 Held 역할을 배정하고 구성원을 캐시한다.
        /// </summary>
        private void BuildHoldingRoles(AttachableObject heldRoot)
        {
            desiredObjectRoles.Clear();
            islandMembers.Clear();
            islandRefreshCount++;
            foreach (AttachableObject member in attachmentService.GetIsland(heldRoot))
            {
                if (member != null)
                {
                    islandMembers.Add(member);
                    AddObjectRole(member, AttachVisualLayers.Held);
                }
            }
        }

        /// <summary>
        /// 오브젝트에 현재보다 우선순위가 높은 시각 역할만 기록한다.
        /// </summary>
        private void AddObjectRole(AttachableObject attachable, uint role)
        {
            // 한 오브젝트가 여러 상태에 걸리면 Held > Focused > Eligible 우선순위가 높은 역할 하나만 유지
            if (attachable == null ||
                (desiredObjectRoles.TryGetValue(attachable, out uint current) && RolePriority(current) >= RolePriority(role)))
            {
                return;
            }

            desiredObjectRoles[attachable] = role;
        }

        /// <summary>
        /// 원하는 오브젝트 역할이 바뀌었을 때 하위 Renderer 역할을 다시 계산하고 적용한다.
        /// </summary>
        private void UpdateRoles()
        {
            // 오브젝트 역할이 지난 프레임과 같으면 GetComponentsInChildren과 Rendering Layer 재적용을 생략
            if (SameObjectRoles(desiredObjectRoles, appliedObjectRoles))
            {
                return;
            }

            desiredRendererRoles.Clear();
            foreach (KeyValuePair<AttachableObject, uint> pair in desiredObjectRoles)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                rendererScratch.Clear();
                pair.Key.GetComponentsInChildren(true, rendererScratch);
                for (int i = 0; i < rendererScratch.Count; i++)
                {
                    AddRendererRole(rendererScratch[i], pair.Value);
                }
            }

            ApplyRendererRoles();
            appliedObjectRoles.Clear();
            foreach (KeyValuePair<AttachableObject, uint> pair in desiredObjectRoles)
            {
                appliedObjectRoles.Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Renderer에 현재보다 우선순위가 높은 시각 역할만 기록한다.
        /// </summary>
        private void AddRendererRole(Renderer renderer, uint role)
        {
            if (renderer == null ||
                (desiredRendererRoles.TryGetValue(renderer, out uint current) && RolePriority(current) >= RolePriority(role)))
            {
                return;
            }

            desiredRendererRoles[renderer] = role;
        }

        /// <summary>
        /// 변경된 Renderer의 부착 전용 Rendering Layer 비트를 제거하거나 새 역할로 갱신한다.
        /// </summary>
        private void ApplyRendererRoles()
        {
            rendererScratch.Clear();
            foreach (KeyValuePair<Renderer, uint> pair in appliedRendererRoles)
            {
                if (pair.Key == null || !desiredRendererRoles.TryGetValue(pair.Key, out uint desired) || desired != pair.Value)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.renderingLayerMask &= ~AttachVisualLayers.Owned;
                    }

                    rendererScratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < rendererScratch.Count; i++)
            {
                appliedRendererRoles.Remove(rendererScratch[i]);
            }

            foreach (KeyValuePair<Renderer, uint> pair in desiredRendererRoles)
            {
                if (pair.Key == null || appliedRendererRoles.ContainsKey(pair.Key))
                {
                    continue;
                }

                // 다른 시스템의 Rendering Layer는 보존하고 Attach가 소유한 비트만 지운 뒤 새 역할 비트를 적용
                pair.Key.renderingLayerMask = (pair.Key.renderingLayerMask & ~AttachVisualLayers.Owned) | pair.Value;
                appliedRendererRoles.Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// 들기 섬 구성원이 변경된 경우에만 투영 렌더러의 소스 섬을 다시 설정한다.
        /// </summary>
        private void UpdateProjectionIsland()
        {
            if (SameMembers(islandMembers, projectionIsland))
            {
                return;
            }

            projectionRenderer.SetIsland(islandMembers);
            projectionIsland.Clear();
            projectionIsland.AddRange(islandMembers);
        }

        /// <summary>
        /// 능력이 기본 상태로 돌아간 뒤 기존 들기 시각 효과를 설정된 시간 동안 페이드아웃한다.
        /// </summary>
        private void FadeOut(float unscaledDeltaTime)
        {
            // A preview promises an available action; unlike the holding glow, it must not linger.
            contactPreviewRenderer?.Clear();
            // Unity Object의 == null은 Destroy된 객체도 true가 되므로 ReferenceEquals로 '한 번이라도 들었던 Root'인지 먼저 구분
            if (!ReferenceEquals(observedHeldRoot, null) &&
                (observedHeldRoot == null || !observedHeldRoot.isActiveAndEnabled))
            {
                HardCleanup();
                return;
            }

            // 이미 완전히 사라진 상태라면 남은 캐시와 Rendering Layer를 즉시 정리하고 더 이상 Draw하지 않음
            if (visualBlend <= 0f)
            {
                HardCleanup();
                return;
            }

            visualBlend = MoveBlend(visualBlend, 0f, settings.visualExitDuration, unscaledDeltaTime);
            PushGlobals();
            if (visualBlend > 0f)
            {
                if (projectionIsland.Count > 0)
                {
                    projectionRenderer.Submit(unscaledDeltaTime);
                }

                tetherRenderer?.Submit(observedHeldRoot, visualBlend);

                return;
            }

            HardCleanup();
        }

        /// <summary>
        /// 관찰 중인 들기 루트와 투영 섬 캐시를 초기화하고 투영 렌더링을 지운다.
        /// </summary>
        private void ClearProjection()
        {
            observedHeldRoot = null;
            observedTopologyVersion = -1;
            if (projectionIsland.Count == 0)
            {
                return;
            }

            projectionRenderer.Clear();
            projectionIsland.Clear();
        }

        /// <summary>
        /// 모든 역할, 투영·테더·접촉 프리뷰와 전역 셰이더 상태를 즉시 초기화한다.
        /// </summary>
        private void HardCleanup()
        {
            desiredObjectRoles.Clear();
            appliedObjectRoles.Clear();
            desiredRendererRoles.Clear();
            ClearAppliedRendererRoles();
            islandMembers.Clear();
            projectionIsland.Clear();
            projectionRenderer?.Clear();
            tetherRenderer?.Clear();
            contactPreviewRenderer?.Clear();
            visualBlend = 0f;
            islandRefreshCount = 0;
            observedHeldRoot = null;
            observedTopologyVersion = -1;
            ResetGlobals();
        }

        /// <summary>
        /// 이전에 수정한 모든 Renderer에서 부착 전용 Rendering Layer 비트를 제거한다.
        /// </summary>
        private void ClearAppliedRendererRoles()
        {
            foreach (KeyValuePair<Renderer, uint> pair in appliedRendererRoles)
            {
                if (pair.Key != null)
                {
                    pair.Key.renderingLayerMask &= ~AttachVisualLayers.Owned;
                }
            }

            appliedRendererRoles.Clear();
            rendererScratch.Clear();
        }

        /// <summary>
        /// 현재 블렌드와 색상·윤곽선·펄스 설정을 전역 셰이더 프로퍼티에 반영한다.
        /// </summary>
        private void PushGlobals()
        {
            Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, visualBlend);
            Shader.SetGlobalColor(AttachVisualShaderIds.EligibleColor, settings.eligibleVisualColor.linear);
            Shader.SetGlobalColor(AttachVisualShaderIds.FocusedColor, settings.focusedVisualColor.linear);
            Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, settings.heldVisualColor);
            Shader.SetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels, Mathf.Max(0f, settings.eligibleOutlinePixels));
            Shader.SetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels, Mathf.Max(0f, settings.focusedOutlinePixels));
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, Mathf.Max(0f, settings.heldOutlinePixels));
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, Mathf.Max(0f, settings.heldPulseSpeed));
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, Mathf.Clamp01(settings.heldPulseStrength));
        }

        /// <summary>
        /// 부착 시각 효과가 사용하는 모든 전역 셰이더 값을 비활성 상태로 초기화한다.
        /// </summary>
        private static void ResetGlobals()
        {
            Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, 0f);
            Shader.SetGlobalColor(AttachVisualShaderIds.EligibleColor, Color.clear);
            Shader.SetGlobalColor(AttachVisualShaderIds.FocusedColor, Color.clear);
            Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, Color.clear);
            Shader.SetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, 0f);
        }

        /// <summary>
        /// 지정한 지속 시간을 기준으로 블렌드 값을 목표값 쪽으로 일정 속도로 이동시킨다.
        /// </summary>
        private static float MoveBlend(float current, float target, float duration, float deltaTime)
        {
            // deltaTime / duration을 한 프레임 이동량으로 사용하면 총 duration 동안 0~1 전체 구간을 일정하게 이동
            return duration <= 0f
                ? target
                : Mathf.MoveTowards(current, target, Mathf.Max(0f, deltaTime) / duration);
        }

        /// <summary>
        /// 현재 렌더 파이프라인이 이 시각 효과가 구성된 동일 파이프라인인지 확인한다.
        /// </summary>
        internal static bool SupportsPipeline(
            RenderPipelineAsset configured,
            RenderPipelineAsset current) =>
            configured != null && ReferenceEquals(configured, current);

        /// <summary>
        /// Held, Focused, Eligible 순서로 시각 역할의 우선순위를 반환한다.
        /// </summary>
        private static int RolePriority(uint role)
        {
            if (role == AttachVisualLayers.Held)
            {
                return 3;
            }

            return role == AttachVisualLayers.Focused ? 2 : 1;
        }

        /// <summary>
        /// 두 오브젝트 역할 딕셔너리가 동일한 키와 역할 값을 갖는지 비교한다.
        /// </summary>
        private static bool SameObjectRoles(
            Dictionary<AttachableObject, uint> first,
            Dictionary<AttachableObject, uint> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }

            foreach (KeyValuePair<AttachableObject, uint> pair in first)
            {
                if (!second.TryGetValue(pair.Key, out uint role) || role != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 두 부착 오브젝트 목록이 순서와 무관하게 같은 구성원을 포함하는지 비교한다.
        /// </summary>
        private static bool SameMembers(List<AttachableObject> first, List<AttachableObject> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }

            for (int i = 0; i < first.Count; i++)
            {
                if (!second.Contains(first[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
