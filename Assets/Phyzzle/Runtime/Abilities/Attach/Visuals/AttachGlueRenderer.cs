using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Abilities.Attach
{
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

        private void LateUpdate() => TickVisual();
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();

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
