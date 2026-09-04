using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
    [DisallowMultipleComponent]
    public sealed class AttachProjectionRenderer : MonoBehaviour
    {
        private const float PlanarEpsilon = 0.0001f;

        private readonly List<MeshFilter> meshFilters = new();
        private readonly List<Renderer> renderers = new();
        private readonly List<Collider> colliders = new();
        private readonly List<MeshFilter> meshFilterScratch = new();
        private readonly List<Renderer> rendererScratch = new();
        private readonly List<Collider> colliderScratch = new();

        [SerializeField] private Camera camera;
        [SerializeField] private AttachSettings settings;
        [SerializeField] private Material projectionMaterial;
        private MaterialPropertyBlock[] projectionProperties;
        private Vector3 lastPlanarForward;

        internal int LastSubmittedDrawCount { get; private set; }

        private void Awake()
        {
            projectionProperties = new[] { new MaterialPropertyBlock(), new MaterialPropertyBlock(),
                new MaterialPropertyBlock(), new MaterialPropertyBlock(), new MaterialPropertyBlock() };
        }

        public void Configure(Camera targetCamera, AttachSettings targetSettings, Material targetProjectionMaterial)
        {
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

        internal void Submit()
        {
            LastSubmittedDrawCount = 0;
            if (camera == null || settings == null || projectionMaterial == null || projectionProperties == null ||
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

            SubmitDirection(combinedBounds, Vector3.down, projectionProperties[0]);
            SubmitDirection(combinedBounds, -right, projectionProperties[1]);
            SubmitDirection(combinedBounds, right, projectionProperties[2]);
            SubmitDirection(combinedBounds, planarForward, projectionProperties[3]);
            SubmitDirection(combinedBounds, -planarForward, projectionProperties[4]);
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

        private void SubmitDirection(Bounds combinedBounds, Vector3 direction, MaterialPropertyBlock properties)
        {
            Vector3 origin = GetCastOrigin(combinedBounds, direction, settings.projectionSurfaceBias);
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, settings.projectionMaxDistance,
                    settings.targetMask, QueryTriggerInteraction.Ignore) ||
                !TryGetProjectionPlane(hit, direction, settings.projectionParallelThreshold, out Vector4 plane))
            {
                return;
            }

            properties.Clear();
            properties.SetVector(AttachVisualShaderIds.ProjectionPlane, plane);
            properties.SetVector(AttachVisualShaderIds.ProjectionDirection, direction);
            properties.SetFloat(AttachVisualShaderIds.ProjectionOpacity, settings.projectionOpacity);
            properties.SetFloat(AttachVisualShaderIds.ProjectionBias, settings.projectionSurfaceBias);
            properties.SetFloat(AttachVisualShaderIds.ProjectionDepthTolerance, settings.projectionDepthTolerance);
            RenderParams renderParams = new(projectionMaterial)
            {
                camera = camera,
                matProps = properties,
                worldBounds = ProjectBounds(combinedBounds, direction, plane),
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
