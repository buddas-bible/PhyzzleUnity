using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Rewind
{
    [DisallowMultipleComponent]
    public sealed class RewindVisualController : MonoBehaviour
    {
        private readonly struct MeshSource
        {
            internal MeshSource(
                Mesh mesh,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                Mesh = mesh;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            internal Mesh Mesh { get; }
            internal Vector3 LocalPosition { get; }
            internal Quaternion LocalRotation { get; }
            internal Vector3 LocalScale { get; }
        }

        private sealed class GhostMesh
        {
            internal GameObject Object;
            internal MeshFilter Filter;
            internal MeshRenderer Renderer;
            internal Material[] Materials;
        }

        private sealed class Ghost
        {
            internal GameObject Object;
            internal readonly List<GhostMesh> Meshes = new();
        }

        [SerializeField] private RewindAbilityController ability;
        [SerializeField] private RewindTargeting targeting;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private RewindSettings settings;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private RenderPipelineAsset supportedPipeline;

        private readonly HashSet<Renderer> touchedRenderers = new();
        private readonly List<RewindPoseSample> history = new();
        private readonly List<RewindPoseSample> pathSamples = new();
        private readonly List<RewindPoseSample> ghostSamples = new();
        private readonly List<MeshSource> meshSources = new();
        private readonly List<Ghost> ghostPool = new();
        private MaterialPropertyBlock previewProperties;

        private Transform previewRoot;
        private LineRenderer previewPath;
        private RewindRecorder cachedTarget;
        private int cachedSnapshotCount = -1;
        private float selectionBlend;
        private int activeGhostCount;

        internal float SelectionBlend => selectionBlend;
        internal int ActiveGhostCount => activeGhostCount;
        internal LineRenderer PreviewPath
        {
            get
            {
                EnsurePreviewRoot();
                return previewPath;
            }
        }

        public void Configure(
            RewindAbilityController rewindAbility,
            RewindTargeting rewindTargeting,
            Transform rewindPlayerRoot,
            RewindSettings rewindSettings,
            Material rewindPreviewMaterial,
            RenderPipelineAsset rewindSupportedPipeline)
        {
            HardCleanup();
            ability = rewindAbility;
            targeting = rewindTargeting;
            playerRoot = rewindPlayerRoot;
            settings = rewindSettings;
            previewMaterial = rewindPreviewMaterial;
            supportedPipeline = rewindSupportedPipeline;
            EnsurePreviewRoot();
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
            if (previewRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(previewRoot.gameObject);
            }
            else
            {
                DestroyImmediate(previewRoot.gameObject);
            }
        }

        internal void TickVisual(float unscaledDeltaTime)
        {
            if (ability == null || targeting == null || playerRoot == null || settings == null)
            {
                HardCleanup();
                return;
            }

            if (!SupportsPipeline(supportedPipeline, GraphicsSettings.currentRenderPipeline) ||
                !ability.isActiveAndEnabled || !targeting.isActiveAndEnabled)
            {
                HardCleanup();
                return;
            }

            bool selecting = ability.State == RewindAbilityController.AbilityState.Selecting;
            if (selecting)
            {
                selectionBlend = MoveBlend(
                    selectionBlend,
                    1f,
                    settings.selectionVisualEnterDuration,
                    unscaledDeltaTime);
                RefreshRendererMasks();
                RefreshPreview(targeting.CurrentTarget);
                ApplyPreviewStyle();
                PushGlobals();
                return;
            }

            if (selectionBlend > 0f)
            {
                if (!ReferenceEquals(cachedTarget, null) &&
                    (cachedTarget == null || !cachedTarget.isActiveAndEnabled))
                {
                    HardCleanup();
                    return;
                }

                selectionBlend = MoveBlend(
                    selectionBlend,
                    0f,
                    settings.selectionVisualExitDuration,
                    unscaledDeltaTime);
                ApplyPreviewStyle();
                PushGlobals();
                if (selectionBlend > 0f)
                {
                    return;
                }
            }

            HardCleanup();
        }

        private static float MoveBlend(float current, float target, float duration, float deltaTime)
        {
            if (duration <= 0f)
            {
                return target;
            }

            return Mathf.MoveTowards(current, target, Mathf.Max(0f, deltaTime) / duration);
        }

        internal static bool SupportsPipeline(
            RenderPipelineAsset configured,
            RenderPipelineAsset current) =>
            configured != null && ReferenceEquals(configured, current);

        private void RefreshRendererMasks()
        {
            RemoveOwnedLayers();
            AddRole(playerRoot, RewindVisualLayers.PlayerPreserve, excludePreview: true);

            IReadOnlyList<RewindRecorder> nearby = targeting.Nearby;
            for (int i = 0; i < nearby.Count; i++)
            {
                RewindRecorder recorder = nearby[i];
                if (recorder != null && recorder.isActiveAndEnabled)
                {
                    AddRole(recorder.transform, RewindVisualLayers.Eligible, excludePreview: false);
                }
            }

            RewindRecorder current = targeting.CurrentTarget;
            if (current != null && current.isActiveAndEnabled)
            {
                AddRole(
                    current.transform,
                    RewindVisualLayers.Eligible | RewindVisualLayers.Active,
                    excludePreview: false);
            }
        }

        private void AddRole(Transform root, uint role, bool excludePreview)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    (excludePreview && previewRoot != null && renderer.transform.IsChildOf(previewRoot)))
                {
                    continue;
                }

                renderer.renderingLayerMask |= role;
                touchedRenderers.Add(renderer);
            }
        }

        private void RemoveOwnedLayers()
        {
            foreach (Renderer renderer in touchedRenderers)
            {
                if (renderer != null)
                {
                    renderer.renderingLayerMask &= ~RewindVisualLayers.Owned;
                }
            }

            touchedRenderers.Clear();
        }

        private void RefreshPreview(RewindRecorder current)
        {
            if (current == null || !current.isActiveAndEnabled || current.Body == null)
            {
                ClearPreview();
                return;
            }

            int snapshotCount = current.SnapshotCount;
            if (current == cachedTarget && snapshotCount == cachedSnapshotCount)
            {
                return;
            }

            cachedTarget = current;
            cachedSnapshotCount = snapshotCount;
            current.CopyHistoryNewestFirst(history);
            RewindPreviewSampler.Build(
                history,
                settings.previewPathMaxPoints,
                settings.previewGhostCount,
                pathSamples,
                ghostSamples);
            if (pathSamples.Count < 2 || previewMaterial == null)
            {
                HidePreviewGeometry();
                return;
            }

            EnsurePreviewRoot();
            previewPath.sharedMaterial = previewMaterial;
            previewPath.positionCount = pathSamples.Count;
            for (int i = 0; i < pathSamples.Count; i++)
            {
                previewPath.SetPosition(i, pathSamples[i].Position);
            }

            previewPath.enabled = true;
            CollectMeshSources(current.Body.transform);
            BuildGhosts(current.Body.transform);
        }

        private void CollectMeshSources(Transform bodyRoot)
        {
            meshSources.Clear();
            MeshFilter[] filters = bodyRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
                {
                    continue;
                }

                meshSources.Add(new MeshSource(
                    filter.sharedMesh,
                    bodyRoot.InverseTransformPoint(filter.transform.position),
                    Quaternion.Inverse(bodyRoot.rotation) * filter.transform.rotation,
                    DivideScale(filter.transform.lossyScale, bodyRoot.lossyScale)));
            }
        }

        private void BuildGhosts(Transform bodyRoot)
        {
            activeGhostCount = ghostSamples.Count;
            for (int i = 0; i < ghostSamples.Count; i++)
            {
                Ghost ghost = EnsureGhost(i);
                RewindPoseSample sample = ghostSamples[i];
                ghost.Object.transform.SetPositionAndRotation(sample.Position, sample.Rotation);
                ghost.Object.transform.localScale = DivideScale(bodyRoot.lossyScale, previewRoot.lossyScale);
                ghost.Object.SetActive(true);
                EnsureGhostMeshes(ghost);
            }

            for (int i = ghostSamples.Count; i < ghostPool.Count; i++)
            {
                ghostPool[i].Object.SetActive(false);
            }
        }

        private Ghost EnsureGhost(int index)
        {
            while (ghostPool.Count <= index)
            {
                GameObject root = new($"Recall Ghost {ghostPool.Count}");
                root.transform.SetParent(previewRoot, false);
                root.hideFlags = HideFlags.DontSave;
                ghostPool.Add(new Ghost { Object = root });
            }

            return ghostPool[index];
        }

        private void EnsureGhostMeshes(Ghost ghost)
        {
            while (ghost.Meshes.Count < meshSources.Count)
            {
                GameObject meshObject = new($"Mesh {ghost.Meshes.Count}");
                meshObject.transform.SetParent(ghost.Object.transform, false);
                meshObject.hideFlags = HideFlags.DontSave;
                GhostMesh mesh = new()
                {
                    Object = meshObject,
                    Filter = meshObject.AddComponent<MeshFilter>(),
                    Renderer = meshObject.AddComponent<MeshRenderer>()
                };
                mesh.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                mesh.Renderer.receiveShadows = false;
                mesh.Renderer.lightProbeUsage = LightProbeUsage.Off;
                mesh.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                ghost.Meshes.Add(mesh);
            }

            for (int i = 0; i < meshSources.Count; i++)
            {
                MeshSource source = meshSources[i];
                GhostMesh mesh = ghost.Meshes[i];
                mesh.Object.SetActive(true);
                mesh.Object.transform.SetLocalPositionAndRotation(
                    source.LocalPosition,
                    source.LocalRotation);
                mesh.Object.transform.localScale = source.LocalScale;
                mesh.Filter.sharedMesh = source.Mesh;
                mesh.Renderer.renderingLayerMask = RewindVisualLayers.Active;
                int materialCount = Mathf.Max(1, source.Mesh.subMeshCount);
                if (mesh.Materials == null || mesh.Materials.Length != materialCount)
                {
                    mesh.Materials = new Material[materialCount];
                }

                for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                {
                    mesh.Materials[materialIndex] = previewMaterial;
                }

                mesh.Renderer.sharedMaterials = mesh.Materials;
            }

            for (int i = meshSources.Count; i < ghost.Meshes.Count; i++)
            {
                ghost.Meshes[i].Object.SetActive(false);
            }
        }

        private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > 0.000001f ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > 0.000001f ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > 0.000001f ? value.z / divisor.z : value.z);
        }

        private void ApplyPreviewStyle()
        {
            if (previewPath == null || settings == null)
            {
                return;
            }

            previewProperties ??= new MaterialPropertyBlock();
            previewPath.startWidth = Mathf.Max(0f, settings.previewPathWidth);
            previewPath.endWidth = Mathf.Max(0f, settings.previewPathWidth);
            previewProperties.Clear();
            Color pathColor = settings.activeVisualColor;
            pathColor.a = selectionBlend;
            previewProperties.SetColor(RewindVisualShaderIds.BaseColor, pathColor);
            previewProperties.SetFloat(RewindVisualShaderIds.PreviewMaskWeight, 1f);
            previewPath.SetPropertyBlock(previewProperties);

            Color ghostColor = settings.activeVisualColor;
            ghostColor.a = Mathf.Clamp01(settings.previewGhostAlpha) * selectionBlend;
            previewProperties.Clear();
            previewProperties.SetColor(RewindVisualShaderIds.BaseColor, ghostColor);
            previewProperties.SetFloat(
                RewindVisualShaderIds.PreviewMaskWeight,
                Mathf.Clamp01(settings.previewGhostAlpha));
            for (int i = 0; i < activeGhostCount; i++)
            {
                foreach (GhostMesh mesh in ghostPool[i].Meshes)
                {
                    if (mesh.Object.activeSelf)
                    {
                        mesh.Renderer.SetPropertyBlock(previewProperties);
                    }
                }
            }
        }

        private void PushGlobals()
        {
            Shader.SetGlobalFloat(RewindVisualShaderIds.SelectionBlend, selectionBlend);
            Shader.SetGlobalFloat(
                RewindVisualShaderIds.WorldSaturation,
                Mathf.Clamp01(settings.selectionWorldSaturation));
            Shader.SetGlobalColor(RewindVisualShaderIds.EligibleColor, settings.eligibleVisualColor);
            Shader.SetGlobalColor(RewindVisualShaderIds.ActiveColor, settings.activeVisualColor);
            Shader.SetGlobalFloat(
                RewindVisualShaderIds.EligibleOutlinePixels,
                Mathf.Max(0f, settings.eligibleOutlinePixels));
            Shader.SetGlobalFloat(
                RewindVisualShaderIds.ActiveOutlinePixels,
                Mathf.Max(0f, settings.activeOutlinePixels));
        }

        private void EnsurePreviewRoot()
        {
            if (previewRoot != null && previewPath != null)
            {
                return;
            }

            GameObject root = new("RecallVisualPreview");
            root.hideFlags = HideFlags.DontSave;
            previewRoot = root.transform;

            GameObject pathObject = new("Recall Path");
            pathObject.transform.SetParent(previewRoot, false);
            pathObject.hideFlags = HideFlags.DontSave;
            previewPath = pathObject.AddComponent<LineRenderer>();
            previewPath.useWorldSpace = true;
            previewPath.loop = false;
            previewPath.shadowCastingMode = ShadowCastingMode.Off;
            previewPath.receiveShadows = false;
            previewPath.lightProbeUsage = LightProbeUsage.Off;
            previewPath.reflectionProbeUsage = ReflectionProbeUsage.Off;
            previewPath.renderingLayerMask = RewindVisualLayers.Active;
            previewPath.startColor = Color.white;
            previewPath.endColor = Color.white;
            previewPath.enabled = false;
        }

        private void ClearPreview()
        {
            cachedTarget = null;
            cachedSnapshotCount = -1;
            history.Clear();
            pathSamples.Clear();
            ghostSamples.Clear();
            meshSources.Clear();
            HidePreviewGeometry();
        }

        private void HidePreviewGeometry()
        {
            activeGhostCount = 0;
            if (previewPath != null)
            {
                previewPath.enabled = false;
                previewPath.positionCount = 0;
            }

            foreach (Ghost ghost in ghostPool)
            {
                if (ghost.Object != null)
                {
                    ghost.Object.SetActive(false);
                }
            }
        }

        private void HardCleanup()
        {
            RemoveOwnedLayers();
            ClearPreview();
            selectionBlend = 0f;
            RewindVisualShaderIds.ResetGlobals();
        }
    }
}
