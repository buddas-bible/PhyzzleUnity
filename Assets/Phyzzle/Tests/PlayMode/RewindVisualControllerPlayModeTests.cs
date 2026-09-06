using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class RewindVisualControllerPlayModeTests
    {
        private sealed class TargetFixture
        {
            internal GameObject Object;
            internal Rigidbody Body;
            internal RewindRecorder Recorder;
            internal MeshRenderer Renderer;
            internal uint OriginalRenderingLayers;
        }

        private readonly List<TargetFixture> targets = new();
        private GameObject playerObject;
        private GameObject coordinatorObject;
        private RewindSettings settings;
        private RewindCoordinator coordinator;
        private RewindTargeting targeting;
        private RewindAbilityController ability;
        private RewindVisualController visuals;
        private MeshRenderer playerRenderer;
        private uint playerOriginalRenderingLayers;
        private Material previewMaterial;
        private Mesh customMesh;
        private float initialTimeScale;

        private TargetFixture Current => targets[0];
        private TargetFixture Nearby => targets[1];

        [SetUp]
        public void SetUp()
        {
            initialTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            settings = ScriptableObject.CreateInstance<RewindSettings>();
            settings.pauseWorldWhileTargeting = true;
            settings.nearbySearchRadius = 40f;
            settings.targetRayDistance = 20f;
            settings.targetMask = UnityEngine.Physics.DefaultRaycastLayers;

            coordinatorObject = new GameObject("Rewind Coordinator");
            coordinator = coordinatorObject.AddComponent<RewindCoordinator>();
            coordinator.Configure(settings);
            coordinator.enabled = false;

            playerObject = new GameObject("Player");
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Player Model";
            model.transform.SetParent(playerObject.transform, false);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            playerRenderer = model.GetComponent<MeshRenderer>();
            playerOriginalRenderingLayers = 1u | (1u << 5);
            playerRenderer.renderingLayerMask = playerOriginalRenderingLayers;

            GameObject cameraArm = new("Camera Arm");
            cameraArm.transform.SetParent(playerObject.transform, false);
            GameObject cameraCore = new("Camera Core");
            cameraCore.transform.SetParent(cameraArm.transform, false);

            targeting = playerObject.AddComponent<RewindTargeting>();
            targeting.Configure(
                cameraArm.transform,
                cameraCore.transform,
                settings,
                coordinator);
            ability = playerObject.AddComponent<RewindAbilityController>();
            ability.Configure(null, targeting, coordinator, settings);

            Shader previewShader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(previewShader, Is.Not.Null);
            previewMaterial = new Material(previewShader);

            visuals = playerObject.AddComponent<RewindVisualController>();
            visuals.Configure(
                ability,
                targeting,
                playerObject.transform,
                settings,
                previewMaterial,
                GraphicsSettings.currentRenderPipeline);

            targets.Add(CreateTarget("Current Target", new Vector3(0f, 0f, 5f), 6));
            targets.Add(CreateTarget("Nearby Target", new Vector3(4f, 0f, 5f), 7));
            UnityEngine.Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = initialTimeScale;
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }

            foreach (TargetFixture target in targets)
            {
                if (target.Object != null)
                {
                    Object.DestroyImmediate(target.Object);
                }
            }

            targets.Clear();
            if (coordinatorObject != null)
            {
                Object.DestroyImmediate(coordinatorObject);
            }

            if (customMesh != null)
            {
                Object.DestroyImmediate(customMesh);
            }

            if (previewMaterial != null)
            {
                Object.DestroyImmediate(previewMaterial);
            }

            if (settings != null)
            {
                Object.DestroyImmediate(settings);
            }

            Shader.SetGlobalFloat(Shader.PropertyToID("_RewindSelectionBlend"), 0f);
            Shader.SetGlobalTexture(
                Shader.PropertyToID("_RewindMaskTexture"),
                Texture2D.blackTexture);
            yield return null;
        }

        [Test]
        public void Selecting_UsesUnscaledBlendAndAppliesThreeMaskRolesWithoutPhysicsChanges()
        {
            int snapshots = Current.Recorder.SnapshotCount;
            Vector3 position = Current.Body.position;
            Quaternion rotation = Current.Body.rotation;
            bool useGravity = Current.Body.useGravity;

            EnterSelection();
            visuals.TickVisual(0.07f);

            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(visuals.SelectionBlend, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(Shader.GetGlobalFloat(RewindVisualShaderIds.SelectionBlend),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(playerRenderer.renderingLayerMask,
                Is.EqualTo(playerOriginalRenderingLayers | RewindVisualLayers.PlayerPreserve));
            Assert.That(Current.Renderer.renderingLayerMask,
                Is.EqualTo(Current.OriginalRenderingLayers |
                    RewindVisualLayers.Eligible | RewindVisualLayers.Active));
            Assert.That(Nearby.Renderer.renderingLayerMask,
                Is.EqualTo(Nearby.OriginalRenderingLayers | RewindVisualLayers.Eligible));
            Assert.That(Current.Recorder.SnapshotCount, Is.EqualTo(snapshots));
            Assert.That(Current.Body.position, Is.EqualTo(position));
            Assert.That(Current.Body.rotation, Is.EqualTo(rotation));
            Assert.That(Current.Body.useGravity, Is.EqualTo(useGravity));
            Assert.That(Current.Body.isKinematic, Is.False);
            Assert.That(coordinator.IsRewindingAny, Is.False);
        }

        [Test]
        public void LeavingSelection_KeepsPresentationDuringExitThenRestoresExactLayers()
        {
            EnterSelection();
            visuals.TickVisual(0.14f);
            Assert.That(visuals.PreviewPath.enabled, Is.True);

            ability.ReturnToDefault();
            visuals.TickVisual(0.08f);

            Assert.That(visuals.SelectionBlend, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(visuals.PreviewPath.enabled, Is.True);
            Assert.That(Current.Renderer.renderingLayerMask,
                Is.EqualTo(Current.OriginalRenderingLayers |
                    RewindVisualLayers.Eligible | RewindVisualLayers.Active));

            visuals.TickVisual(0.08f);

            Assert.That(visuals.SelectionBlend, Is.Zero);
            Assert.That(visuals.ActiveGhostCount, Is.Zero);
            Assert.That(visuals.PreviewPath.enabled, Is.False);
            Assert.That(playerRenderer.renderingLayerMask, Is.EqualTo(playerOriginalRenderingLayers));
            Assert.That(Current.Renderer.renderingLayerMask,
                Is.EqualTo(Current.OriginalRenderingLayers));
            Assert.That(Nearby.Renderer.renderingLayerMask,
                Is.EqualTo(Nearby.OriginalRenderingLayers));
        }

        [Test]
        public void LeavingSelection_KeepsGhostsAtRecordedWorldPosesWhenPlayerMoves()
        {
            EnterSelection();
            visuals.TickVisual(0.14f);
            List<Transform> ghosts = ActiveGhostRoots();
            Vector3[] positions = ghosts.Select(ghost => ghost.position).ToArray();
            Quaternion[] rotations = ghosts.Select(ghost => ghost.rotation).ToArray();

            ability.ReturnToDefault();
            playerObject.transform.SetPositionAndRotation(
                new Vector3(3f, 2f, -4f),
                Quaternion.Euler(15f, 80f, -10f));
            visuals.TickVisual(0.08f);

            Assert.That(ghosts.Select(ghost => ghost.position), Is.EqualTo(positions));
            for (int i = 0; i < ghosts.Count; i++)
            {
                Assert.That(Quaternion.Angle(ghosts[i].rotation, rotations[i]),
                    Is.LessThan(0.001f));
            }
        }

        [Test]
        public void Retargeting_MovesActiveRoleInTheSameVisualTick()
        {
            EnterSelection();
            visuals.TickVisual(0.14f);
            Current.Body.position = new Vector3(-4f, 0f, 5f);
            Nearby.Body.position = new Vector3(0f, 0f, 5f);
            UnityEngine.Physics.SyncTransforms();

            Assert.That(targeting.Refresh(), Is.SameAs(Nearby.Recorder));
            visuals.TickVisual(0f);

            Assert.That(Current.Renderer.renderingLayerMask,
                Is.EqualTo(Current.OriginalRenderingLayers | RewindVisualLayers.Eligible));
            Assert.That(Nearby.Renderer.renderingLayerMask,
                Is.EqualTo(Nearby.OriginalRenderingLayers |
                    RewindVisualLayers.Eligible | RewindVisualLayers.Active));
            Assert.That(visuals.PreviewPath.GetPosition(0),
                Is.EqualTo(new Vector3(4f, 0f, 5f)));
        }

        [Test]
        public void DisabledCurrentRecorder_ClearsItsPreviewAndOwnedLayersImmediately()
        {
            EnterSelection();
            visuals.TickVisual(0.14f);
            Assert.That(visuals.ActiveGhostCount, Is.GreaterThan(0));

            Current.Recorder.enabled = false;
            targeting.Refresh();
            visuals.TickVisual(0f);

            Assert.That(Current.Renderer.renderingLayerMask,
                Is.EqualTo(Current.OriginalRenderingLayers));
            Assert.That(visuals.ActiveGhostCount, Is.Zero);
            Assert.That(visuals.PreviewPath.enabled, Is.False);
            Assert.That(playerRenderer.renderingLayerMask,
                Is.EqualTo(playerOriginalRenderingLayers | RewindVisualLayers.PlayerPreserve));
            Assert.That(Nearby.Renderer.renderingLayerMask,
                Is.EqualTo(Nearby.OriginalRenderingLayers | RewindVisualLayers.Eligible));
        }

        [Test]
        public void DisabledTargetingOwner_HardCleansImmediately()
        {
            EnterSelection();
            visuals.TickVisual(0.14f);

            targeting.enabled = false;
            visuals.TickVisual(0f);

            AssertVisualsHardClean();
        }

        [Test]
        public void DisabledCurrentRecorderDuringExit_HardCleansImmediately()
        {
            EnterSelection();
            visuals.TickVisual(0.14f);
            ability.ReturnToDefault();
            visuals.TickVisual(0.04f);
            Assert.That(visuals.SelectionBlend, Is.GreaterThan(0f));

            Current.Recorder.enabled = false;
            visuals.TickVisual(0f);

            AssertVisualsHardClean();
        }

        [Test]
        public void GhostMaskWeight_TracksConfiguredOpacityIncludingZero()
        {
            settings.previewGhostAlpha = 0.28f;
            EnterSelection();
            visuals.TickVisual(0.14f);
            MeshRenderer ghost = visuals.PreviewPath.transform.parent
                .GetComponentsInChildren<MeshRenderer>(true)
                .First(renderer => renderer.gameObject.name.StartsWith("Mesh "));
            MaterialPropertyBlock properties = new();
            ghost.GetPropertyBlock(properties);
            int maskWeight = Shader.PropertyToID("_RewindPreviewMaskWeight");
            Assert.That(properties.GetFloat(maskWeight), Is.EqualTo(0.28f).Within(0.0001f));

            settings.previewGhostAlpha = 0f;
            visuals.TickVisual(0f);
            ghost.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(maskWeight), Is.Zero);
        }

        [Test]
        public void DisablingController_HardCleansMasksGeometryAndGlobalBlend()
        {
            EnterSelection();
            visuals.TickVisual(0.14f);

            visuals.enabled = false;

            Assert.That(visuals.SelectionBlend, Is.Zero);
            Assert.That(visuals.ActiveGhostCount, Is.Zero);
            Assert.That(visuals.PreviewPath.enabled, Is.False);
            Assert.That(Shader.GetGlobalFloat(RewindVisualShaderIds.SelectionBlend), Is.Zero);
            Assert.That(playerRenderer.renderingLayerMask, Is.EqualTo(playerOriginalRenderingLayers));
            Assert.That(Current.Renderer.renderingLayerMask,
                Is.EqualTo(Current.OriginalRenderingLayers));
            Assert.That(Nearby.Renderer.renderingLayerMask,
                Is.EqualTo(Nearby.OriginalRenderingLayers));
        }

        [Test]
        public void Preview_PreservesEndpointPosesAndUsesWiderGhostGapsForFastMotion()
        {
            Vector3[] oldestToNewest =
            {
                new(0f, 0f, -38f),
                new(0f, 0f, -28f),
                new(0f, 0f, -18f),
                new(0f, 0f, -8f),
                new(0f, 0f, 2f),
                new(0f, 0f, 3f),
                new(0f, 0f, 4f),
                new(0f, 0f, 5f)
            };
            Current.Recorder.ClearHistory();
            for (int i = 0; i < oldestToNewest.Length; i++)
            {
                Current.Body.position = oldestToNewest[i];
                Current.Body.rotation = Quaternion.Euler(0f, 70f - i * 10f, 0f);
                Current.Recorder.CaptureNow();
            }

            UnityEngine.Physics.SyncTransforms();
            EnterSelection();
            visuals.TickVisual(0.14f);

            Assert.That(visuals.PreviewPath.positionCount, Is.EqualTo(8));
            Assert.That(visuals.PreviewPath.GetPosition(0), Is.EqualTo(new Vector3(0f, 0f, 5f)));
            Assert.That(visuals.PreviewPath.GetPosition(7), Is.EqualTo(new Vector3(0f, 0f, -38f)));
            List<Transform> ghosts = ActiveGhostRoots();
            Assert.That(ghosts, Has.Count.EqualTo(8));
            Assert.That(ghosts[0].position, Is.EqualTo(new Vector3(0f, 0f, 5f)));
            Assert.That(ghosts[7].position, Is.EqualTo(new Vector3(0f, 0f, -38f)));
            Assert.That(Quaternion.Angle(ghosts[0].rotation, Quaternion.identity),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(ghosts[7].rotation, Quaternion.Euler(0f, 70f, 0f)),
                Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(ghosts[3].position, ghosts[4].position),
                Is.GreaterThan(Vector3.Distance(ghosts[0].position, ghosts[1].position)));
            Assert.That(visuals.PreviewPath.renderingLayerMask,
                Is.EqualTo(RewindVisualLayers.Active));
            Transform previewRoot = visuals.PreviewPath.transform.parent;
            Assert.That(previewRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(previewRoot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(previewRoot.GetComponentsInChildren<RewindRecorder>(true), Is.Empty);

            Transform[] roots = ghosts.ToArray();
            visuals.TickVisual(0f);
            Assert.That(ActiveGhostRoots(), Is.EqualTo(roots));
        }

        [Test]
        public void Preview_CopiesAllMeshSubmeshesButDoesNotFabricateSkinnedGhosts()
        {
            customMesh = new Mesh { name = "Two Submesh Test Mesh" };
            customMesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up
            };
            customMesh.subMeshCount = 2;
            customMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            customMesh.SetTriangles(new[] { 0, 2, 1 }, 1);

            GameObject meshChild = new("Multi Submesh");
            meshChild.transform.SetParent(Current.Object.transform, false);
            MeshFilter meshFilter = meshChild.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = customMesh;
            MeshRenderer meshRenderer = meshChild.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new[] { previewMaterial, previewMaterial };

            GameObject skinnedChild = new("Skinned Highlight Only");
            skinnedChild.transform.SetParent(Current.Object.transform, false);
            SkinnedMeshRenderer skinned = skinnedChild.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = customMesh;
            skinned.sharedMaterial = previewMaterial;

            EnterSelection();
            visuals.TickVisual(0.14f);

            Assert.That(skinned.renderingLayerMask,
                Is.EqualTo(1u | RewindVisualLayers.Eligible | RewindVisualLayers.Active));
            Transform previewRoot = visuals.PreviewPath.transform.parent;
            Assert.That(previewRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true), Is.Empty);
            MeshFilter[] copied = previewRoot.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh == customMesh)
                .ToArray();
            Assert.That(copied, Has.Length.EqualTo(visuals.ActiveGhostCount));
            Assert.That(copied.All(filter =>
                    filter.GetComponent<MeshRenderer>().sharedMaterials.Length == 2),
                Is.True);
            Assert.That(copied.All(filter =>
                    filter.GetComponent<MeshRenderer>().renderingLayerMask ==
                    RewindVisualLayers.Active),
                Is.True);
        }

        private void EnterSelection()
        {
            UnityEngine.Physics.SyncTransforms();
            ability.EnterSelecting();
            Assert.That(targeting.CurrentTarget, Is.SameAs(Current.Recorder));
            CollectionAssert.Contains((ICollection)targeting.Nearby, Current.Recorder);
            CollectionAssert.Contains((ICollection)targeting.Nearby, Nearby.Recorder);
        }

        private TargetFixture CreateTarget(string name, Vector3 position, int unrelatedLayerBit)
        {
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = name;
            Rigidbody body = targetObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            RewindRecorder recorder = targetObject.AddComponent<RewindRecorder>();
            recorder.Configure(settings, coordinator);
            recorder.ClearHistory();
            body.position = position - Vector3.forward;
            recorder.CaptureNow();
            body.position = position;
            recorder.CaptureNow();
            MeshRenderer renderer = targetObject.GetComponent<MeshRenderer>();
            uint originalLayers = 1u | (1u << unrelatedLayerBit);
            renderer.renderingLayerMask = originalLayers;
            return new TargetFixture
            {
                Object = targetObject,
                Body = body,
                Recorder = recorder,
                Renderer = renderer,
                OriginalRenderingLayers = originalLayers
            };
        }

        private List<Transform> ActiveGhostRoots()
        {
            Transform previewRoot = visuals.PreviewPath.transform.parent;
            List<Transform> result = new();
            for (int i = 0; i < previewRoot.childCount; i++)
            {
                Transform child = previewRoot.GetChild(i);
                if (child != visuals.PreviewPath.transform && child.gameObject.activeSelf)
                {
                    result.Add(child);
                }
            }

            return result;
        }

        private void AssertVisualsHardClean()
        {
            Assert.That(visuals.SelectionBlend, Is.Zero);
            Assert.That(visuals.ActiveGhostCount, Is.Zero);
            Assert.That(visuals.PreviewPath.enabled, Is.False);
            Assert.That(Shader.GetGlobalFloat(RewindVisualShaderIds.SelectionBlend), Is.Zero);
            Assert.That(playerRenderer.renderingLayerMask, Is.EqualTo(playerOriginalRenderingLayers));
            Assert.That(Current.Renderer.renderingLayerMask,
                Is.EqualTo(Current.OriginalRenderingLayers));
            Assert.That(Nearby.Renderer.renderingLayerMask,
                Is.EqualTo(Nearby.OriginalRenderingLayers));
        }
    }
}
