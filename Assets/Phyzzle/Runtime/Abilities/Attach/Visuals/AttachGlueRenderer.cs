using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// AttachmentService가 소유한 실제 FixedJoint 연결마다 글루 메시를 생성해 렌더링한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachGlueRenderer : MonoBehaviour
    {
        [SerializeField] private Camera camera;
        [SerializeField] private AttachmentService attachmentService;
        [SerializeField] private Material material;
        [SerializeField] private AttachSettings settings;
        [SerializeField] private RenderPipelineAsset supportedPipeline;
        [SerializeField, Min(0.01f)] private float radius = 0.22f;

        private readonly List<FixedJoint> joints = new();
        private readonly List<FixedJoint> removedJoints = new();
        private readonly Dictionary<FixedJoint, AttachGlueMesh> blobs = new();

        internal int LastSubmittedDrawCount { get; private set; }

        /// <summary>
        /// 글루 렌더링에 사용할 카메라, 부착 서비스, 재질, 설정과 렌더 파이프라인을 구성한다.
        /// </summary>
        public void Configure(Camera renderCamera, AttachmentService service, Material glueMaterial,
            AttachSettings attachSettings, RenderPipelineAsset pipeline)
        {
            Clear();
            camera = renderCamera;
            attachmentService = service;
            material = glueMaterial;
            settings = attachSettings;
            supportedPipeline = pipeline;
        }

        /// <summary>
        /// LateUpdate에서 현재 부착 연결의 글루 시각 효과를 갱신한다.
        /// </summary>
        private void LateUpdate() => TickVisual();

        /// <summary>
        /// 비활성화될 때 생성한 글루 렌더링 리소스를 정리한다.
        /// </summary>
        private void OnDisable() => Clear();

        /// <summary>
        /// 파괴될 때 생성한 글루 렌더링 리소스를 정리한다.
        /// </summary>
        private void OnDestroy() => Clear();

        /// <summary>
        /// 현재 실제 조인트 목록을 읽고 각 연결 앵커 사이에 글루 드로우를 제출한다.
        /// </summary>
        internal void TickVisual()
        {
            LastSubmittedDrawCount = 0;
            if (!isActiveAndEnabled || attachmentService == null || !attachmentService.isActiveAndEnabled ||
                camera == null || !camera.isActiveAndEnabled || material == null || settings == null ||
                !AttachVisualController.SupportsPipeline(supportedPipeline, GraphicsSettings.currentRenderPipeline))
            {
                Clear();
                return;
            }

            // Copy actual service-owned joints, not contact candidates or the currently held island.
            attachmentService.CopyConnectionJoints(joints);
            RemoveMissingConnections();
            for (int i = 0; i < joints.Count; i++)
            {
                FixedJoint joint = joints[i];
                if (!blobs.TryGetValue(joint, out AttachGlueMesh blob))
                {
                    blob = new AttachGlueMesh();
                    blobs.Add(joint, blob);
                }

                if (!joint.gameObject.activeInHierarchy || !joint.connectedBody.gameObject.activeInHierarchy)
                {
                    blob.Clear();
                    continue;
                }

                // Joint anchors are already local and include each object's scale convention.
                // TransformPoint follows the rendered poses without writing back to either Rigidbody.
                Vector3 firstAnchor = joint.transform.TransformPoint(joint.anchor);
                Vector3 secondAnchor = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
                if (blob.Submit(camera, material, firstAnchor, secondAnchor, radius,
                    settings.heldVisualColor, Time.unscaledTime))
                {
                    LastSubmittedDrawCount++;
                }
            }
        }

        /// <summary>
        /// 더 이상 실제 조인트 목록에 없는 연결의 글루 메시를 찾아 해제하고 제거한다.
        /// </summary>
        private void RemoveMissingConnections()
        {
            removedJoints.Clear();
            foreach (KeyValuePair<FixedJoint, AttachGlueMesh> pair in blobs)
            {
                // ponytail: small scene connection lists; reuse a membership set if profiling needs it.
                if (!joints.Contains(pair.Key))
                {
                    pair.Value.Dispose();
                    removedJoints.Add(pair.Key);
                }
            }

            for (int i = 0; i < removedJoints.Count; i++)
            {
                blobs.Remove(removedJoints[i]);
            }
        }

        /// <summary>
        /// 모든 글루 메시와 캐시된 연결 목록, 제출 횟수를 초기화한다.
        /// </summary>
        private void Clear()
        {
            foreach (AttachGlueMesh blob in blobs.Values)
            {
                blob.Dispose();
            }

            blobs.Clear();
            joints.Clear();
            removedJoints.Clear();
            LastSubmittedDrawCount = 0;
        }
    }
}
