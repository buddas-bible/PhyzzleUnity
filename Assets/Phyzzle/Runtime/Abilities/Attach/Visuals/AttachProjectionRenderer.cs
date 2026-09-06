using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
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

        private void Awake() => InitializeSurfaces();

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

        public void Configure(Camera targetCamera, AttachSettings targetSettings, Material targetProjectionMaterial)
        {
            InitializeSurfaces();
            camera = targetCamera;
            settings = targetSettings;
            projectionMaterial = targetProjectionMaterial;
            lastPlanarForward = Vector3.zero;
        }

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

        internal void Submit() => Submit(Time.deltaTime);

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

        internal static Vector3 GetCastOrigin(Bounds bounds, Vector3 direction, float skin)
        {
            Vector3 absoluteDirection = new(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            float support = Vector3.Dot(bounds.extents, absoluteDirection);
            return bounds.center + direction * (support + skin);
        }

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

        internal static Vector3 ProjectPoint(Vector3 point, Vector3 direction, Vector4 plane)
        {
            Vector3 normal = new(plane.x, plane.y, plane.z);
            float denominator = Vector3.Dot(normal, direction);
            return point - direction * ((Vector3.Dot(normal, point) + plane.w) / denominator);
        }

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

        internal sealed class ReceiverSurface
        {
            internal readonly MaterialPropertyBlock Properties = new();
            internal Vector3 Direction { get; private set; }
            internal float Opacity { get; private set; }
            private Collider receiver;
            private Vector3 localPoint;
            private Vector3 localNormal;

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

            internal void Capture(RaycastHit hit, Vector3 direction)
            {
                receiver = hit.collider;
                Transform surface = receiver.transform;
                localPoint = surface.InverseTransformPoint(hit.point);
                localNormal = surface.localToWorldMatrix.transpose.MultiplyVector(hit.normal).normalized;
                Direction = direction;
            }

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

            internal void Clear()
            {
                receiver = null;
                Opacity = 0f;
            }
        }

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
