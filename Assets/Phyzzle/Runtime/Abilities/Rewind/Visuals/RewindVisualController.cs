using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 되감기 대상 선택 강조, 과거 경로와 고스트 미리보기, 셰이더 전역 상태를 조율한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewindVisualController : MonoBehaviour
    {
        /// <summary>
        /// 대상 리지드바디 기준으로 고스트에 재사용할 메시와 로컬 변환 정보를 보관한다.
        /// </summary>
        private readonly struct MeshSource
        {
            /// <summary>
            /// 고스트 복제에 필요한 메시와 로컬 변환 정보를 생성한다.
            /// </summary>
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

        /// <summary>
        /// 하나의 고스트 구성 메시 오브젝트와 렌더링 자원을 보관한다.
        /// </summary>
        private sealed class GhostMesh
        {
            internal GameObject Object;
            internal MeshFilter Filter;
            internal MeshRenderer Renderer;
            internal Material[] Materials;
        }

        /// <summary>
        /// 한 시점의 고스트 루트와 그 아래 복제 메시 목록을 보관한다.
        /// </summary>
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

        /// <summary>
        /// 되감기 시각화에 필요한 능력, 타게팅, 플레이어, 설정, 재질과 렌더 파이프라인을 구성한다.
        /// </summary>
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

        /// <summary>
        /// LateUpdate마다 시간 배율과 무관한 deltaTime으로 선택 시각 효과를 갱신한다.
        /// </summary>
        private void LateUpdate()
        {
            TickVisual(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 컴포넌트 비활성화 시 렌더 레이어와 미리보기 상태를 모두 정리한다.
        /// </summary>
        private void OnDisable()
        {
            HardCleanup();
        }

        /// <summary>
        /// 컴포넌트 파괴 시 런타임에 생성한 미리보기 루트 오브젝트를 제거한다.
        /// </summary>
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

        /// <summary>
        /// 선택 상태에 따라 렌더 마스크, 경로·고스트 프리뷰와 셰이더 블렌드를 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 지정한 지속 시간 동안 현재 블렌드 값을 목표값으로 일정 속도로 이동시킨다.
        /// </summary>
        private static float MoveBlend(float current, float target, float duration, float deltaTime)
        {
            if (duration <= 0f)
            {
                return target;
            }

            return Mathf.MoveTowards(current, target, Mathf.Max(0f, deltaTime) / duration);
        }

        /// <summary>
        /// 구성된 렌더 파이프라인이 현재 실행 중인 파이프라인과 동일한지 확인한다.
        /// </summary>
        internal static bool SupportsPipeline(
            RenderPipelineAsset configured,
            RenderPipelineAsset current) =>
            configured != null && ReferenceEquals(configured, current);

        /// <summary>
        /// 플레이어와 선택 가능·현재 대상의 렌더링 레이어 역할을 현재 타게팅 상태에 맞춰 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 지정한 루트 아래 렌더러에 되감기 시각 역할 레이어를 추가하고 추적 목록에 기록한다.
        /// </summary>
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

        /// <summary>
        /// 이전 프레임에 적용했던 되감기 전용 렌더링 레이어를 모두 제거한다.
        /// </summary>
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

        /// <summary>
        /// 현재 대상의 기록이 바뀌었을 때 경로와 고스트 샘플을 다시 만들고 미리보기 형상을 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 되감기 대상 아래의 활성 메시를 수집해 대상 루트 기준 로컬 변환으로 저장한다.
        /// </summary>
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

        /// <summary>
        /// 샘플된 과거 자세마다 고스트 풀을 배치하고 필요한 메시 복제 상태를 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 지정한 인덱스까지 고스트 풀을 확장하고 해당 고스트를 반환한다.
        /// </summary>
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

        /// <summary>
        /// 고스트가 현재 대상의 모든 소스 메시를 표시할 수 있도록 복제 오브젝트와 재질을 구성한다.
        /// </summary>
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

        /// <summary>
        /// 0에 가까운 축을 안전하게 처리하면서 두 스케일 벡터를 성분별로 나눈다.
        /// </summary>
        private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > 0.000001f ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > 0.000001f ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > 0.000001f ? value.z / divisor.z : value.z);
        }

        /// <summary>
        /// 현재 선택 블렌드와 설정 색상·폭을 경로와 고스트 렌더러에 적용한다.
        /// </summary>
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

        /// <summary>
        /// 선택 블렌드, 채도와 강조 색상·외곽선 값을 전역 셰이더 프로퍼티에 반영한다.
        /// </summary>
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

        /// <summary>
        /// 런타임 경로와 고스트를 담을 숨김 루트와 LineRenderer를 한 번 생성한다.
        /// </summary>
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

        /// <summary>
        /// 캐시한 대상과 샘플 데이터를 초기화하고 모든 미리보기 형상을 숨긴다.
        /// </summary>
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

        /// <summary>
        /// 경로와 고스트 오브젝트를 비활성화해 현재 미리보기 형상을 숨긴다.
        /// </summary>
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

        /// <summary>
        /// 렌더 레이어, 프리뷰 캐시와 전역 셰이더 상태를 기본값으로 완전히 복원한다.
        /// </summary>
        private void HardCleanup()
        {
            RemoveOwnedLayers();
            ClearPreview();
            selectionBlend = 0f;
            RewindVisualShaderIds.ResetGlobals();
        }
    }
}
