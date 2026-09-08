using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 들고 있는 부착 섬의 메시를 주변 여섯 방향 표면에 투영해 공간 관계를 시각화한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachProjectionRenderer : MonoBehaviour
    {
        private const float PlanarEpsilon = 0.0001f;
        private const float SurfaceFadeSeconds = 0.16f;
        private const int DirectionCount = 6;

        private readonly List<MeshFilter> meshFilters = new();
        private readonly List<Renderer> renderers = new();
        private readonly List<Collider> colliders = new();
        private readonly List<MeshFilter> meshFilterScratch = new();
        private readonly List<Renderer> rendererScratch = new();
        private readonly List<Collider> colliderScratch = new();

        [SerializeField] private Camera camera;
        [SerializeField] private AttachSettings settings;
        [SerializeField] private Material projectionMaterial;
        private ReceiverSurface[] currentSurfaces;
        private ReceiverSurface[] retiringSurfaces;
        private Vector3 lastPlanarForward;

        internal int LastSubmittedDrawCount { get; private set; }

        /// <summary>
        /// 런타임 시작 시 방향별 현재·퇴장 표면 상태 배열을 준비한다.
        /// </summary>
        private void Awake() => InitializeSurfaces();

        /// <summary>
        /// 여섯 투영 방향에 사용할 현재 표면과 페이드아웃 표면 상태를 한 번 생성한다.
        /// </summary>
        private void InitializeSurfaces()
        {
            if (currentSurfaces != null)
            {
                return;
            }

            currentSurfaces = new ReceiverSurface[DirectionCount];
            retiringSurfaces = new ReceiverSurface[DirectionCount];
            for (int i = 0; i < DirectionCount; i++)
            {
                currentSurfaces[i] = new ReceiverSurface();
                retiringSurfaces[i] = new ReceiverSurface();
            }
        }

        /// <summary>
        /// 투영 렌더링에 사용할 카메라, 설정과 투영 재질을 구성한다.
        /// </summary>
        public void Configure(Camera targetCamera, AttachSettings targetSettings, Material targetProjectionMaterial)
        {
            InitializeSurfaces();
            camera = targetCamera;
            settings = targetSettings;
            projectionMaterial = targetProjectionMaterial;
            lastPlanarForward = Vector3.zero;
        }

        /// <summary>
        /// 지정한 부착 섬에서 투영에 사용할 메시, 렌더러와 콜라이더 목록을 다시 수집한다.
        /// </summary>
        internal void SetIsland(IReadOnlyCollection<AttachableObject> island)
        {
            Clear();
            if (island == null)
            {
                return;
            }

            foreach (AttachableObject member in island)
            {
                if (member == null)
                {
                    continue;
                }

                meshFilterScratch.Clear();
                member.GetComponentsInChildren(true, meshFilterScratch);
                meshFilters.AddRange(meshFilterScratch);

                rendererScratch.Clear();
                member.GetComponentsInChildren(true, rendererScratch);
                renderers.AddRange(rendererScratch);

                colliderScratch.Clear();
                member.GetComponentsInChildren(true, colliderScratch);
                colliders.AddRange(colliderScratch);
            }

            RemoveDuplicates(meshFilters);
            RemoveDuplicates(renderers);
            RemoveDuplicates(colliders);
        }

        /// <summary>
        /// 현재 프레임의 deltaTime으로 투영 드로우 제출을 갱신한다.
        /// </summary>
        internal void Submit() => Submit(Time.deltaTime);

        /// <summary>
        /// 섬의 전체 경계를 기준으로 상하·좌우·전후 표면 투영을 갱신하고 드로우를 제출한다.
        /// </summary>
        internal void Submit(float deltaTime)
        {
            LastSubmittedDrawCount = 0;
            if (camera == null || settings == null || projectionMaterial == null || currentSurfaces == null ||
                !HasSourceMesh())
            {
                return;
            }

            if (!TryGetCombinedBounds(out Bounds combinedBounds))
            {
                return;
            }

            Vector3 fallbackForward = lastPlanarForward.sqrMagnitude > PlanarEpsilon
                ? lastPlanarForward
                : camera.transform.root.forward;
            Vector3 planarForward = ResolvePlanarForward(camera.transform.forward, fallbackForward);
            lastPlanarForward = planarForward;
            Vector3 right = Vector3.Cross(Vector3.up, planarForward);

            float fadeStep = Mathf.Max(0f, deltaTime) / SurfaceFadeSeconds;
            SubmitDirection(combinedBounds, Vector3.down, 0, fadeStep);
            SubmitDirection(combinedBounds, -right, 1, fadeStep);
            SubmitDirection(combinedBounds, right, 2, fadeStep);
            SubmitDirection(combinedBounds, planarForward, 3, fadeStep);
            SubmitDirection(combinedBounds, -planarForward, 4, fadeStep);
            SubmitDirection(combinedBounds, Vector3.up, 5, fadeStep);
        }

        /// <summary>
        /// 수집한 섬 구성 요소와 모든 표면 페이드 상태를 초기화한다.
        /// </summary>
        internal void Clear()
        {
            meshFilters.Clear();
            renderers.Clear();
            colliders.Clear();
            meshFilterScratch.Clear();
            rendererScratch.Clear();
            colliderScratch.Clear();
            LastSubmittedDrawCount = 0;
            if (currentSurfaces != null)
            {
                for (int i = 0; i < DirectionCount; i++)
                {
                    currentSurfaces[i].Clear();
                    retiringSurfaces[i].Clear();
                }
            }
        }

        /// <summary>
        /// 카메라 전방을 수평면에 투영하고 수직 시점에서는 이전 전방 방향으로 대체한다.
        /// </summary>
        internal static Vector3 ResolvePlanarForward(Vector3 cameraForward, Vector3 fallbackForward)
        {
            Vector3 planar = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (planar.sqrMagnitude > PlanarEpsilon)
            {
                return planar.normalized;
            }

            planar = Vector3.ProjectOnPlane(fallbackForward, Vector3.up);
            return planar.sqrMagnitude > PlanarEpsilon ? planar.normalized : Vector3.forward;
        }

        /// <summary>
        /// 경계 상자에서 지정 방향으로 레이캐스트를 시작할 바깥쪽 위치를 계산한다.
        /// </summary>
        internal static Vector3 GetCastOrigin(Bounds bounds, Vector3 direction, float skin)
        {
            Vector3 absoluteDirection = new(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            float support = Vector3.Dot(bounds.extents, absoluteDirection);
            return bounds.center + direction * (support + skin);
        }

        /// <summary>
        /// 레이캐스트 히트가 투영 방향과 충분히 교차하면 월드 평면 방정식을 계산한다.
        /// </summary>
        internal static bool TryGetProjectionPlane(
            RaycastHit hit,
            Vector3 direction,
            float parallelThreshold,
            out Vector4 plane)
        {
            Vector3 normal = hit.normal.normalized;
            if (Mathf.Abs(Vector3.Dot(direction, normal)) < parallelThreshold)
            {
                plane = default;
                return false;
            }

            plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, hit.point));
            return true;
        }

        /// <summary>
        /// 지정한 점을 방향 벡터를 따라 목표 평면 위로 투영한다.
        /// </summary>
        internal static Vector3 ProjectPoint(Vector3 point, Vector3 direction, Vector4 plane)
        {
            Vector3 normal = new(plane.x, plane.y, plane.z);
            float denominator = Vector3.Dot(normal, direction);
            return point - direction * ((Vector3.Dot(normal, point) + plane.w) / denominator);
        }

        /// <summary>
        /// 경계 상자의 모든 꼭짓점을 평면에 투영해 투영 결과의 월드 경계를 계산한다.
        /// </summary>
        internal static Bounds ProjectBounds(Bounds source, Vector3 direction, Vector4 plane)
        {
            Vector3 min = source.min;
            Vector3 max = source.max;
            Bounds projected = new(ProjectPoint(new Vector3(min.x, min.y, min.z), direction, plane), Vector3.zero);
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        projected.Encapsulate(ProjectPoint(new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z), direction, plane));
                    }
                }
            }

            return projected;
        }

        /// <summary>
        /// 현재 섬에 실제 투영할 수 있는 공유 메시가 하나 이상 존재하는지 확인한다.
        /// </summary>
        private bool HasSourceMesh()
        {
            for (int i = 0; i < meshFilters.Count; i++)
            {
                if (meshFilters[i] != null && meshFilters[i].sharedMesh != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 섬의 렌더러와 콜라이더 경계를 하나의 월드 Bounds로 합친다.
        /// </summary>
        private bool TryGetCombinedBounds(out Bounds combinedBounds)
        {
            bool hasBounds = false;
            combinedBounds = default;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (hasBounds)
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (hasBounds)
                {
                    combinedBounds.Encapsulate(collider.bounds);
                }
                else
                {
                    combinedBounds = collider.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        /// <summary>
        /// 한 방향으로 수신 표면을 탐색하고 현재·퇴장 표면의 페이드와 드로우를 갱신한다.
        /// </summary>
        private void SubmitDirection(Bounds combinedBounds, Vector3 direction, int index, float fadeStep)
        {
            Vector3 origin = GetCastOrigin(combinedBounds, direction, settings.projectionSurfaceBias);
            bool hasHit = Physics.Raycast(origin, direction, out RaycastHit hit, settings.projectionMaxDistance,
                settings.targetMask, QueryTriggerInteraction.Ignore) &&
                TryGetProjectionPlane(hit, direction, settings.projectionParallelThreshold, out _);

            ReceiverSurface current = currentSurfaces[index];
            ReceiverSurface retiring = retiringSurfaces[index];
            if (hasHit && !current.Matches(hit, settings.projectionDepthTolerance))
            {
                // Keep the stronger old surface when several receivers change inside one fade.
                if (retiring.Matches(hit, settings.projectionDepthTolerance) || current.Opacity >= retiring.Opacity)
                {
                    (current, retiring) = (retiring, current);
                    currentSurfaces[index] = current;
                    retiringSurfaces[index] = retiring;
                }

                if (!current.Matches(hit, settings.projectionDepthTolerance))
                {
                    current.Clear();
                }
            }

            if (hasHit)
            {
                current.Capture(hit, direction);
            }

            current.Fade(hasHit, fadeStep);
            retiring.Fade(false, fadeStep);
            DrawSurface(combinedBounds, current);
            DrawSurface(combinedBounds, retiring);
        }

        /// <summary>
        /// 활성 수신 표면의 평면 정보를 셰이더에 전달해 섬의 모든 서브메시 투영 드로우를 제출한다.
        /// </summary>
        private void DrawSurface(Bounds combinedBounds, ReceiverSurface surface)
        {
            if (surface.Opacity <= 0f || !surface.TryGetPlane(out Vector4 plane) ||
                Mathf.Abs(Vector3.Dot(new Vector3(plane.x, plane.y, plane.z), surface.Direction)) <
                settings.projectionParallelThreshold)
            {
                return;
            }

            MaterialPropertyBlock properties = surface.Properties;
            properties.Clear();
            properties.SetVector(AttachVisualShaderIds.ProjectionPlane, plane);
            properties.SetVector(AttachVisualShaderIds.ProjectionDirection, surface.Direction);
            properties.SetFloat(AttachVisualShaderIds.ProjectionOpacity,
                settings.projectionOpacity * Mathf.SmoothStep(0f, 1f, surface.Opacity));
            properties.SetFloat(AttachVisualShaderIds.ProjectionBias, settings.projectionSurfaceBias);
            properties.SetFloat(AttachVisualShaderIds.ProjectionDepthTolerance, settings.projectionDepthTolerance);
            RenderParams renderParams = new(projectionMaterial)
            {
                camera = camera,
                matProps = properties,
                worldBounds = ProjectBounds(combinedBounds, surface.Direction, plane),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };

            for (int i = 0; i < meshFilters.Count; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Graphics.RenderMesh(renderParams, mesh, submesh, meshFilter.transform.localToWorldMatrix);
                    LastSubmittedDrawCount++;
                }
            }
        }

        /// <summary>
        /// 하나의 투영 방향에서 선택된 수신 콜라이더, 평면과 페이드 상태를 보관한다.
        /// </summary>
        internal sealed class ReceiverSurface
        {
            internal readonly MaterialPropertyBlock Properties = new();
            internal Vector3 Direction { get; private set; }
            internal float Opacity { get; private set; }
            private Collider receiver;
            private Vector3 localPoint;
            private Vector3 localNormal;

            /// <summary>
            /// 새 히트가 현재 수신 표면과 같은 콜라이더·평면을 나타내는지 비교한다.
            /// </summary>
            internal bool Matches(RaycastHit hit, float tolerance)
            {
                if (receiver != hit.collider || !TryGetPlane(out Vector4 plane))
                {
                    return false;
                }

                Vector3 normal = new(plane.x, plane.y, plane.z);
                return Vector3.Dot(normal, hit.normal) > 0.95f &&
                    Mathf.Abs(Vector3.Dot(normal, hit.point) + plane.w) <= Mathf.Max(0.01f, tolerance);
            }

            /// <summary>
            /// 새 레이캐스트 히트를 수신 오브젝트 로컬 공간으로 저장한다.
            /// </summary>
            internal void Capture(RaycastHit hit, Vector3 direction)
            {
                receiver = hit.collider;
                Transform surface = receiver.transform;
                localPoint = surface.InverseTransformPoint(hit.point);
                localNormal = surface.localToWorldMatrix.transpose.MultiplyVector(hit.normal).normalized;
                Direction = direction;
            }

            /// <summary>
            /// 저장된 로컬 접촉 정보를 현재 수신 오브젝트 자세 기준 월드 평면으로 복원한다.
            /// </summary>
            internal bool TryGetPlane(out Vector4 plane)
            {
                if (receiver == null || !receiver.enabled || !receiver.gameObject.activeInHierarchy)
                {
                    plane = default;
                    return false;
                }

                Transform surface = receiver.transform;
                Vector3 normal = surface.worldToLocalMatrix.transpose.MultiplyVector(localNormal).normalized;
                Vector3 point = surface.TransformPoint(localPoint);
                plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
                return true;
            }

            /// <summary>
            /// 표면 표시 여부에 따라 불투명도를 이동시키고 완전히 사라지면 상태를 지운다.
            /// </summary>
            internal void Fade(bool visible, float step)
            {
                if (!TryGetPlane(out _))
                {
                    Clear();
                    return;
                }

                Opacity = Mathf.MoveTowards(Opacity, visible ? 1f : 0f, step);
                if (!visible && Opacity <= 0f)
                {
                    Clear();
                }
            }

            /// <summary>
            /// 현재 수신 표면 참조와 불투명도를 초기화한다.
            /// </summary>
            internal void Clear()
            {
                receiver = null;
                Opacity = 0f;
            }
        }

        /// <summary>
        /// 컴포넌트 목록에서 null과 중복 참조를 제거한다.
        /// </summary>
        private static void RemoveDuplicates<T>(List<T> components) where T : Component
        {
            for (int i = components.Count - 1; i >= 0; i--)
            {
                if (components[i] == null)
                {
                    components.RemoveAt(i);
                    continue;
                }

                for (int earlier = 0; earlier < i; earlier++)
                {
                    if (components[i] == components[earlier])
                    {
                        components.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }
}
