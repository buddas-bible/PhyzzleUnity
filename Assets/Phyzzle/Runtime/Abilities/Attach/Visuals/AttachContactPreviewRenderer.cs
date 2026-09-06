using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    [DisallowMultipleComponent]
    public sealed class AttachContactPreviewRenderer : MonoBehaviour
    {
        [SerializeField] private Camera camera;
        [SerializeField] private Material material;
        [SerializeField] private AttachSettings settings;
        [SerializeField, Min(0.01f)] private float radius = 0.22f;

        private readonly AttachGlueMesh glueMesh = new();

        internal Mesh PreviewMesh => glueMesh.Mesh;
        internal int LastSubmittedDrawCount { get; private set; }
        internal Vector3 RenderedMemberAnchor { get; private set; }
        internal Vector3 RenderedOtherAnchor { get; private set; }

        public void Configure(Camera renderCamera, Material previewMaterial, AttachSettings attachSettings)
        {
            Clear();
            camera = renderCamera;
            material = previewMaterial;
            settings = attachSettings;
        }

        internal void Submit(AttachableObject member, AttachableObject other, Vector3 worldAnchor, float blend)
        {
            if (!isActiveAndEnabled || settings == null ||
                member == null || !member.isActiveAndEnabled || member.Body == null ||
                other == null || !other.isActiveAndEnabled || other.Body == null || member == other ||
                !IsFinite(worldAnchor) || !float.IsFinite(blend) || blend <= 0f || !float.IsFinite(radius))
            {
                Clear();
                return;
            }

            RenderedMemberAnchor = ToRenderedAnchor(member.Body, worldAnchor);
            RenderedOtherAnchor = ToRenderedAnchor(other.Body, worldAnchor);
            if (!IsFinite(RenderedMemberAnchor) || !IsFinite(RenderedOtherAnchor))
            {
                Clear();
                return;
            }

            Color color = settings.heldVisualColor;
            color.a *= Mathf.Clamp01(blend);
            if (!glueMesh.Submit(camera, material, RenderedMemberAnchor, RenderedOtherAnchor, radius, color,
                    Time.unscaledTime))
            {
                Clear();
                return;
            }

            LastSubmittedDrawCount = 1;
        }

        internal void Clear()
        {
            LastSubmittedDrawCount = 0;
            RenderedMemberAnchor = RenderedOtherAnchor = Vector3.zero;
            glueMesh.Clear();
        }

        private void OnDisable() => Clear();

        private void OnDestroy() => glueMesh.Dispose();

        private static Vector3 ToRenderedAnchor(Rigidbody body, Vector3 worldAnchor)
        {
            // The collision point already includes scale. Applying TransformPoint would scale it twice.
            Vector3 offset = Quaternion.Inverse(body.rotation) * (worldAnchor - body.position);
            return body.transform.position + body.transform.rotation * offset;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    }
}
