using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
    [DisallowMultipleComponent]
    public sealed class AttachTetherRenderer : MonoBehaviour
    {
        private const int Segments = 32;
        private static readonly int ColorId = Shader.PropertyToID("_TetherColor");
        private static readonly int LengthId = Shader.PropertyToID("_TetherLength");

        [SerializeField] private Camera camera;
        [SerializeField] private Transform source;
        [SerializeField] private Material material;
        [SerializeField] private AttachSettings settings;
        [SerializeField, Min(0.001f)] private float width = 0.16f;
        [SerializeField, Min(0f)] private float waveAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float curveHeight = 0.25f;

        private readonly Vector3[] centers = new Vector3[Segments + 1];
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        private readonly Vector2[] uvs = new Vector2[(Segments + 1) * 2];
        private readonly int[] triangles = new int[Segments * 6];
        private Mesh mesh;
        private MaterialPropertyBlock properties;

        internal Mesh RibbonMesh => mesh;
        internal int LastSubmittedDrawCount { get; private set; }

        public void Configure(Camera targetCamera, Transform handOrigin, Material tetherMaterial,
            AttachSettings attachSettings)
        {
            Clear();
            camera = targetCamera;
            source = handOrigin;
            material = tetherMaterial;
            settings = attachSettings;
        }

        internal void Submit(AttachableObject held, float blend)
        {
            if (!isActiveAndEnabled || camera == null || !camera.isActiveAndEnabled ||
                source == null || !source.gameObject.activeInHierarchy || material == null || settings == null ||
                held == null || !held.isActiveAndEnabled || held.Body == null || blend <= 0f)
            {
                Clear();
                return;
            }

            Vector3 start = source.position;
            // Match the visible object, including any Rigidbody interpolation between physics steps.
            Vector3 end = held.Body.transform.position;
            float length = Vector3.Distance(start, end);
            if (length < 0.001f)
            {
                Clear();
                return;
            }

            EnsureMesh();
            UpdateRibbon(start, end, length);
            Color color = settings.heldVisualColor;
            color.a *= Mathf.Clamp01(blend);
            properties.SetColor(ColorId, color);
            properties.SetFloat(LengthId, length);
            Graphics.RenderMesh(new RenderParams(material)
            {
                camera = camera,
                matProps = properties,
                worldBounds = mesh.bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off
            }, mesh, 0, Matrix4x4.identity);
            LastSubmittedDrawCount = 1;
        }

        internal void Clear()
        {
            LastSubmittedDrawCount = 0;
            if (mesh != null && mesh.vertexCount > 0)
            {
                mesh.Clear();
            }
        }

        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            Clear();
            if (mesh != null)
            {
                Destroy(mesh);
                mesh = null;
            }
        }

        private void EnsureMesh()
        {
            if (mesh != null)
            {
                return;
            }

            mesh = new Mesh { name = "Attach Holding Tether", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            properties = new MaterialPropertyBlock();
            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
                if (i == Segments)
                {
                    continue;
                }

                int vertex = i * 2;
                int index = i * 6;
                triangles[index] = vertex;
                triangles[index + 1] = vertex + 2;
                triangles[index + 2] = vertex + 1;
                triangles[index + 3] = vertex + 1;
                triangles[index + 4] = vertex + 2;
                triangles[index + 5] = vertex + 3;
            }
        }

        private void UpdateRibbon(Vector3 start, Vector3 end, float length)
        {
            Vector3 side = Vector3.Cross((end - start) / length, Vector3.up);
            side = side.sqrMagnitude > 0.0001f ? side.normalized : camera.transform.right;
            float wave = Mathf.Min(waveAmplitude, length * 0.06f);
            float arc = Mathf.Min(curveHeight, length * 0.1f);
            float phase = Time.unscaledTime * 3f;
            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;
                float envelope = 4f * t * (1f - t);
                centers[i] = Vector3.Lerp(start, end, t) + envelope *
                    (Vector3.up * arc + side * (Mathf.Sin(t * Mathf.PI * 4f - phase) * wave));
            }

            float halfWidth = Mathf.Max(0.001f, width) * 0.5f;
            for (int i = 0; i <= Segments; i++)
            {
                Vector3 tangent = centers[Mathf.Min(i + 1, Segments)] - centers[Mathf.Max(i - 1, 0)];
                Vector3 facing = camera.orthographic ? -camera.transform.forward : camera.transform.position - centers[i];
                Vector3 ribbonSide = Vector3.Cross(tangent, facing);
                ribbonSide = ribbonSide.sqrMagnitude > 0.0001f ? ribbonSide.normalized : camera.transform.right;
                vertices[i * 2] = centers[i] - ribbonSide * halfWidth;
                vertices[i * 2 + 1] = centers[i] + ribbonSide * halfWidth;
            }

            bool needsTopology = mesh.vertexCount == 0;
            mesh.SetVertices(vertices);
            if (needsTopology)
            {
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0, false);
            }
            mesh.RecalculateBounds();
        }
    }
}
