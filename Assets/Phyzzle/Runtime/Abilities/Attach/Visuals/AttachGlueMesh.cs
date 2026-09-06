using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
    internal sealed class AttachGlueMesh : IDisposable
    {
        private const int Rings = 12;
        private const int Sectors = 20;
        private static readonly int ColorId = Shader.PropertyToID("_ContactColor");
        private static readonly int TimeId = Shader.PropertyToID("_PreviewTime");

        private readonly Vector3[] directions = new Vector3[(Rings + 1) * (Sectors + 1)];
        private readonly Vector3[] vertices = new Vector3[(Rings + 1) * (Sectors + 1)];
        private readonly Vector3[] normals = new Vector3[(Rings + 1) * (Sectors + 1)];
        private readonly int[] triangles = new int[Rings * Sectors * 6];
        private Mesh mesh;
        private MaterialPropertyBlock properties;

        internal Mesh Mesh => mesh;

        internal bool Submit(Camera camera, Material material, Vector3 start, Vector3 end, float radius,
            Color color, float animationTime)
        {
            if (camera == null || !camera.isActiveAndEnabled || material == null ||
                !IsFinite(start) || !IsFinite(end) || !float.IsFinite(radius) ||
                !IsFinite(color) || !float.IsFinite(animationTime))
            {
                Clear();
                return false;
            }

            EnsureMesh();
            UpdateBlob(start, end, radius);
            properties.SetColor(ColorId, color);
            properties.SetFloat(TimeId, animationTime);
            Graphics.RenderMesh(new RenderParams(material)
            {
                camera = camera,
                matProps = properties,
                worldBounds = mesh.bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off
            }, mesh, 0, Matrix4x4.identity);
            return true;
        }

        internal void Clear()
        {
            // Clear the retained mesh so a draw queued earlier this frame cannot leave a stale cue.
            if (mesh != null && mesh.vertexCount > 0)
            {
                mesh.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(mesh);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }

            mesh = null;
            properties = null;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFinite(Color value) =>
            float.IsFinite(value.r) && float.IsFinite(value.g) &&
            float.IsFinite(value.b) && float.IsFinite(value.a);

        private void EnsureMesh()
        {
            if (mesh != null)
            {
                return;
            }

            mesh = new Mesh { name = "Attach Glue", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            properties = new MaterialPropertyBlock();
            for (int ring = 0; ring <= Rings; ring++)
            {
                float latitude = ring * Mathf.PI / Rings;
                for (int sector = 0; sector <= Sectors; sector++)
                {
                    float longitude = sector * Mathf.PI * 2f / Sectors;
                    int vertex = ring * (Sectors + 1) + sector;
                    directions[vertex] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude),
                        Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    if (ring == Rings || sector == Sectors)
                    {
                        continue;
                    }

                    int nextRing = vertex + Sectors + 1;
                    int index = (ring * Sectors + sector) * 6;
                    triangles[index] = vertex;
                    triangles[index + 1] = vertex + 1;
                    triangles[index + 2] = nextRing;
                    triangles[index + 3] = vertex + 1;
                    triangles[index + 4] = nextRing + 1;
                    triangles[index + 5] = nextRing;
                }
            }
        }

        private void UpdateBlob(Vector3 start, Vector3 end, float radius)
        {
            Vector3 difference = end - start;
            Quaternion orientation = difference.sqrMagnitude > 0.000001f
                ? Quaternion.FromToRotation(Vector3.forward, difference.normalized)
                : Quaternion.identity;
            Vector3 center = (start + end) * 0.5f;
            float thickness = Mathf.Max(0.01f, radius);
            Vector3 extent = new(thickness, thickness, thickness + difference.magnitude * 0.5f);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 direction = directions[i];
                vertices[i] = center + orientation * Vector3.Scale(direction, extent);
                normals[i] = orientation * new Vector3(direction.x / extent.x,
                    direction.y / extent.y, direction.z / extent.z).normalized;
            }

            bool needsTopology = mesh.vertexCount == 0;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            if (needsTopology)
            {
                mesh.SetTriangles(triangles, 0, false);
            }

            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(0.04f); // Allow the shader's small surface ripple without culling its silhouette.
            mesh.bounds = bounds;
        }
    }
}
