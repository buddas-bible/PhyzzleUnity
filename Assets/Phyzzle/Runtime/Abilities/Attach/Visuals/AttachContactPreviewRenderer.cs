using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 부착 후보 두 오브젝트의 접촉 지점 사이에 글루 형태의 연결 프리뷰를 렌더링한다.
    /// </summary>
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

        /// <summary>
        /// 접촉 프리뷰에 사용할 카메라, 재질과 부착 설정을 구성한다.
        /// </summary>
        public void Configure(Camera renderCamera, Material previewMaterial, AttachSettings attachSettings)
        {
            Clear();
            camera = renderCamera;
            material = previewMaterial;
            settings = attachSettings;
        }

        /// <summary>
        /// 두 부착 오브젝트의 렌더 자세 기준 앵커를 계산해 글루 프리뷰 드로우를 제출한다.
        /// </summary>
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

        /// <summary>
        /// 현재 프리뷰 드로우 상태와 임시 글루 메시를 초기화한다.
        /// </summary>
        internal void Clear()
        {
            LastSubmittedDrawCount = 0;
            RenderedMemberAnchor = RenderedOtherAnchor = Vector3.zero;
            glueMesh.Clear();
        }

        /// <summary>
        /// 비활성화될 때 남아 있는 접촉 프리뷰를 제거한다.
        /// </summary>
        private void OnDisable() => Clear();

        /// <summary>
        /// 파괴될 때 내부 글루 메시 리소스를 해제한다.
        /// </summary>
        private void OnDestroy() => glueMesh.Dispose();

        /// <summary>
        /// 물리 앵커를 보간된 Rigidbody 렌더 자세 기준 월드 위치로 변환한다.
        /// </summary>
        private static Vector3 ToRenderedAnchor(Rigidbody body, Vector3 worldAnchor)
        {
            // The collision point already includes scale. Applying TransformPoint would scale it twice.
            Vector3 offset = Quaternion.Inverse(body.rotation) * (worldAnchor - body.position);
            return body.transform.position + body.transform.rotation * offset;
        }

        /// <summary>
        /// 벡터의 모든 성분이 유한한 값인지 확인한다.
        /// </summary>
        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    }
}
