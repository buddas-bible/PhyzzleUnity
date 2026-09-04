using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    [DisallowMultipleComponent]
    public sealed class AttachVisualController : MonoBehaviour
    {
        [SerializeField] private AttachAbilityController ability;
        [SerializeField] private AttachTargeting targeting;
        [SerializeField] private AttachHoldController holdController;
        [SerializeField] private AttachmentService attachmentService;
        [SerializeField] private AttachProjectionRenderer projectionRenderer;
        [SerializeField] private AttachSettings settings;

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

        public void Configure(
            AttachAbilityController attachAbility,
            AttachTargeting attachTargeting,
            AttachHoldController attachHoldController,
            AttachmentService attachAttachmentService,
            AttachProjectionRenderer attachProjectionRenderer,
            AttachSettings attachSettings)
        {
            HardCleanup();
            ability = attachAbility;
            targeting = attachTargeting;
            holdController = attachHoldController;
            attachmentService = attachAttachmentService;
            projectionRenderer = attachProjectionRenderer;
            settings = attachSettings;
        }

        private void LateUpdate()
        {
            TickVisual(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            HardCleanup();
        }

        private void OnDestroy()
        {
            HardCleanup();
        }

        internal void TickVisual(float unscaledDeltaTime)
        {
            if (ability == null || targeting == null || holdController == null || attachmentService == null ||
                projectionRenderer == null || settings == null || !ability.isActiveAndEnabled ||
                !targeting.isActiveAndEnabled || !holdController.isActiveAndEnabled)
            {
                HardCleanup();
                return;
            }

            if (ability.State == AttachAbilityController.AbilityState.Selecting)
            {
                BuildSelectingRoles();
                UpdateRoles();
                ClearProjection();
                visualBlend = MoveBlend(visualBlend, 1f, settings.visualEnterDuration, unscaledDeltaTime);
                PushGlobals();
                return;
            }

            if (ability.State == AttachAbilityController.AbilityState.Holding)
            {
                AttachableObject heldRoot = holdController.HeldObject;
                if (heldRoot == null)
                {
                    HardCleanup();
                    return;
                }

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
                projectionRenderer.Submit();
                return;
            }

            FadeOut(unscaledDeltaTime);
        }

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

        private void AddObjectRole(AttachableObject attachable, uint role)
        {
            if (attachable == null ||
                (desiredObjectRoles.TryGetValue(attachable, out uint current) && RolePriority(current) >= RolePriority(role)))
            {
                return;
            }

            desiredObjectRoles[attachable] = role;
        }

        private void UpdateRoles()
        {
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

        private void AddRendererRole(Renderer renderer, uint role)
        {
            if (renderer == null ||
                (desiredRendererRoles.TryGetValue(renderer, out uint current) && RolePriority(current) >= RolePriority(role)))
            {
                return;
            }

            desiredRendererRoles[renderer] = role;
        }

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

                pair.Key.renderingLayerMask = (pair.Key.renderingLayerMask & ~AttachVisualLayers.Owned) | pair.Value;
                appliedRendererRoles.Add(pair.Key, pair.Value);
            }
        }

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

        private void FadeOut(float unscaledDeltaTime)
        {
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
                    projectionRenderer.Submit();
                }

                return;
            }

            HardCleanup();
        }

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

        private void HardCleanup()
        {
            desiredObjectRoles.Clear();
            appliedObjectRoles.Clear();
            desiredRendererRoles.Clear();
            ClearAppliedRendererRoles();
            islandMembers.Clear();
            projectionIsland.Clear();
            projectionRenderer?.Clear();
            visualBlend = 0f;
            islandRefreshCount = 0;
            observedHeldRoot = null;
            observedTopologyVersion = -1;
            ResetGlobals();
        }

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

        private void PushGlobals()
        {
            Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, visualBlend);
            Shader.SetGlobalFloat(AttachVisualShaderIds.WorldSaturation, Mathf.Clamp01(settings.visualWorldSaturation));
            Shader.SetGlobalFloat(AttachVisualShaderIds.WorldBrightness, Mathf.Max(0f, settings.visualWorldBrightness));
            Shader.SetGlobalColor(AttachVisualShaderIds.EligibleColor, settings.eligibleVisualColor);
            Shader.SetGlobalColor(AttachVisualShaderIds.FocusedColor, settings.focusedVisualColor);
            Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, settings.heldVisualColor);
            Shader.SetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels, Mathf.Max(0f, settings.eligibleOutlinePixels));
            Shader.SetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels, Mathf.Max(0f, settings.focusedOutlinePixels));
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, Mathf.Max(0f, settings.heldOutlinePixels));
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, Mathf.Max(0f, settings.heldPulseSpeed));
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, Mathf.Clamp01(settings.heldPulseStrength));
        }

        private static void ResetGlobals()
        {
            Shader.SetGlobalFloat(AttachVisualShaderIds.VisualBlend, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.WorldSaturation, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.WorldBrightness, 0f);
            Shader.SetGlobalColor(AttachVisualShaderIds.EligibleColor, Color.clear);
            Shader.SetGlobalColor(AttachVisualShaderIds.FocusedColor, Color.clear);
            Shader.SetGlobalColor(AttachVisualShaderIds.HeldColor, Color.clear);
            Shader.SetGlobalFloat(AttachVisualShaderIds.EligibleOutlinePixels, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.FocusedOutlinePixels, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldOutlinePixels, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseSpeed, 0f);
            Shader.SetGlobalFloat(AttachVisualShaderIds.HeldPulseStrength, 0f);
        }

        private static float MoveBlend(float current, float target, float duration, float deltaTime)
        {
            return duration <= 0f
                ? target
                : Mathf.MoveTowards(current, target, Mathf.Max(0f, deltaTime) / duration);
        }

        private static int RolePriority(uint role)
        {
            if (role == AttachVisualLayers.Held)
            {
                return 3;
            }

            return role == AttachVisualLayers.Focused ? 2 : 1;
        }

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
