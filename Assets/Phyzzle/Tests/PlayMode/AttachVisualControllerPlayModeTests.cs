using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class AttachVisualControllerPlayModeTests
    {
        private GameObject playerObject;
        private GameObject firstObject;
        private GameObject secondObject;
        private GameObject serviceObject;
        private GameObject projectionObject;
        private AttachSettings settings;
        private AttachmentService service;
        private AttachTargeting targeting;
        private AttachHoldController hold;
        private AttachAbilityController ability;
        private AttachProjectionRenderer projection;
        private AttachVisualController controller;
        private AttachableObject first;
        private AttachableObject second;
        private readonly List<GameObject> extraObjects = new();
        private Material projectionMaterial;
        private RenderPipelineAsset originalGraphicsPipeline;
        private RenderPipelineAsset originalQualityPipeline;
        private bool pipelineOverridden;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<AttachSettings>();
            settings.visualEnterDuration = 0f;
            settings.visualExitDuration = 0.16f;

            serviceObject = new GameObject("Attachment Service");
            service = serviceObject.AddComponent<AttachmentService>();
            service.Configure(settings);

            playerObject = new GameObject("Attach Visual Player");
            GameObject modelObject = new("Model");
            GameObject armObject = new("Camera Arm");
            GameObject coreObject = new("Camera Core");
            modelObject.transform.SetParent(playerObject.transform, false);
            armObject.transform.SetParent(playerObject.transform, false);
            coreObject.transform.SetParent(armObject.transform, false);

            targeting = playerObject.AddComponent<AttachTargeting>();
            targeting.Configure(armObject.transform, coreObject.transform, settings);
            hold = playerObject.AddComponent<AttachHoldController>();
            hold.Configure(modelObject.transform, null, service, settings);
            ability = playerObject.AddComponent<AttachAbilityController>();
            ability.Configure(null, null, targeting, hold, settings);
            projectionObject = new GameObject("Attach Projection");
            projection = projectionObject.AddComponent<AttachProjectionRenderer>();
            controller = playerObject.AddComponent<AttachVisualController>();
            controller.Configure(
                ability,
                targeting,
                hold,
                service,
                projection,
                settings,
                GraphicsSettings.currentRenderPipeline);

            first = CreateAttachableCube("First", Vector3.zero, out firstObject);
            second = CreateAttachableCube("Second", Vector3.right * 2f, out secondObject);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pipelineOverridden)
            {
                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                yield return null;
                GraphicsSettings.defaultRenderPipeline = originalGraphicsPipeline;
                QualitySettings.renderPipeline = originalQualityPipeline;
                pipelineOverridden = false;
                yield return null;
            }

            Object.Destroy(playerObject);
            Object.Destroy(firstObject);
            Object.Destroy(secondObject);
            Object.Destroy(projectionObject);
            Object.Destroy(serviceObject);
            Object.Destroy(settings);
            for (int i = 0; i < extraObjects.Count; i++)
            {
                Object.Destroy(extraObjects[i]);
            }

            if (projectionMaterial != null)
            {
                Object.Destroy(projectionMaterial);
            }
            yield return null;
        }

        [Test]
        public void Selecting_AssignsFocusedOverEligibleAndPreservesUnrelatedBits()
        {
            const uint unrelated = 1u << 3;
            firstObject.GetComponent<Renderer>().renderingLayerMask = unrelated;
            secondObject.GetComponent<Renderer>().renderingLayerMask = unrelated;
            EnterSelecting(first, first, second);

            controller.TickVisual(1f);

            Assert.That(firstObject.GetComponent<Renderer>().renderingLayerMask,
                Is.EqualTo(unrelated | AttachVisualLayers.Focused));
            Assert.That(secondObject.GetComponent<Renderer>().renderingLayerMask,
                Is.EqualTo(unrelated | AttachVisualLayers.Eligible));
        }

        [Test]
        public void Holding_AssignsHeldToEveryIslandRendererAndClearsSelectionRoles()
        {
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);
            EnterSelecting(first, first, second);
            controller.TickVisual(1f);
            Assert.That(ability.TryBeginHolding(), Is.True);

            controller.TickVisual(1f);

            AssertRole(firstObject.GetComponent<Renderer>(), AttachVisualLayers.Held);
            AssertRole(GetChildRenderer(firstObject), AttachVisualLayers.Held);
            AssertRole(secondObject.GetComponent<Renderer>(), AttachVisualLayers.Held);
            AssertRole(GetChildRenderer(secondObject), AttachVisualLayers.Held);
        }

        [Test]
        public void ExitFade_UsesUnscaledDeltaThenClearsOwnedBits()
        {
            EnterSelecting(first, first, second);
            controller.TickVisual(1f);
            ability.ReturnToDefault();

            controller.TickVisual(0.08f);

            Assert.That(controller.VisualBlend, Is.EqualTo(0.5f).Within(0.001f));
            AssertRole(firstObject.GetComponent<Renderer>(), AttachVisualLayers.Focused);
            controller.TickVisual(0.08f);
            Assert.That(controller.VisualBlend, Is.Zero);
            AssertRole(firstObject.GetComponent<Renderer>(), 0u);
        }

        [Test]
        public void Disable_HardCleansGlobalsLayersAndProjection()
        {
            EnterSelecting(first, first, second);
            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            Assert.That(GetProjectionMeshFilters().Count, Is.GreaterThan(0));
            playerObject.SetActive(false);

            AssertRole(firstObject.GetComponent<Renderer>(), 0u);
            Assert.That(Shader.GetGlobalFloat(AttachVisualShaderIds.VisualBlend), Is.Zero);
            Assert.That(GetProjectionMeshFilters().Count, Is.Zero);
        }

        [Test]
        public void VisualTick_DoesNotChangePhysicsMaterialsTransformsOrAttachState()
        {
            EnterSelecting(first, first, second);
            VisualSnapshot selecting = CaptureVisualSnapshot(AttachAbilityController.AbilityState.Selecting);

            controller.TickVisual(1f);

            AssertVisualSnapshotUnchanged(selecting);
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);
            Assert.That(ability.TryBeginHolding(), Is.True);
            VisualSnapshot holding = CaptureVisualSnapshot(AttachAbilityController.AbilityState.Holding);

            controller.TickVisual(1f);

            AssertVisualSnapshotUnchanged(holding);
            ability.ReturnToDefault();
            VisualSnapshot exit = CaptureVisualSnapshot(AttachAbilityController.AbilityState.Default);
            controller.TickVisual(0.08f);
            AssertVisualSnapshotUnchanged(exit);
            controller.TickVisual(0.08f);
            AssertVisualSnapshotUnchanged(exit);
        }

        [Test]
        public void Retargeting_ReplacesFocusedRoleInSameTick()
        {
            EnterSelecting(first, first, second);
            controller.TickVisual(1f);
            SetTargets(second, first, second);

            controller.TickVisual(1f);

            AssertRole(firstObject.GetComponent<Renderer>(), AttachVisualLayers.Eligible);
            AssertRole(secondObject.GetComponent<Renderer>(), AttachVisualLayers.Focused);
        }

        [Test]
        public void IslandGrowth_AddsHeldRoleOnNextTick()
        {
            EnterSelecting(first, first);
            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);

            controller.TickVisual(1f);

            AssertRole(secondObject.GetComponent<Renderer>(), AttachVisualLayers.Held);
        }

        [Test]
        public void HoldingSteadyState_RefreshesIslandOnlyWhenTopologyChanges()
        {
            EnterSelecting(first, first);
            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            int refreshCount = controller.IslandRefreshCount;

            for (int i = 0; i < 10; i++)
            {
                controller.TickVisual(1f);
            }

            Assert.That(controller.IslandRefreshCount, Is.EqualTo(refreshCount));
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);
            controller.TickVisual(1f);
            Assert.That(controller.IslandRefreshCount, Is.EqualTo(refreshCount + 1));
            AssertRole(secondObject.GetComponent<Renderer>(), AttachVisualLayers.Held);
        }

        [UnityTest]
        public IEnumerator MobilePipeline_HardCleansSelectingAndHoldingThenPcPipelineRecovers()
        {
            originalGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            originalQualityPipeline = QualitySettings.renderPipeline;
            RenderPipelineAsset supportedPipeline = LoadPipelineAsset("Assets/Settings/PC_RPAsset.asset");
            RenderPipelineAsset mobilePipeline = LoadPipelineAsset("Assets/Settings/Mobile_RPAsset.asset");
            Assert.That(supportedPipeline, Is.Not.Null);
            Assert.That(mobilePipeline, Is.Not.SameAs(supportedPipeline));
            pipelineOverridden = true;

            GraphicsSettings.defaultRenderPipeline = supportedPipeline;
            QualitySettings.renderPipeline = supportedPipeline;
            yield return null;
            controller.Configure(ability, targeting, hold, service, projection, settings, supportedPipeline);

            EnterSelecting(first, first, second);
            controller.TickVisual(1f);
            AssertRole(firstObject.GetComponent<Renderer>(), AttachVisualLayers.Focused);

            GraphicsSettings.defaultRenderPipeline = mobilePipeline;
            QualitySettings.renderPipeline = mobilePipeline;
            yield return null;
            Assert.That(QualitySettings.renderPipeline, Is.Not.SameAs(supportedPipeline));
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(QualitySettings.renderPipeline));
            controller.TickVisual(1f);
            AssertVisualsHardClean();

            GraphicsSettings.defaultRenderPipeline = supportedPipeline;
            QualitySettings.renderPipeline = supportedPipeline;
            yield return null;
            controller.TickVisual(1f);
            AssertRole(firstObject.GetComponent<Renderer>(), AttachVisualLayers.Focused);

            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            AssertRole(firstObject.GetComponent<Renderer>(), AttachVisualLayers.Held);
            Assert.That(GetProjectionMeshFilters(), Is.Not.Empty);

            GraphicsSettings.defaultRenderPipeline = mobilePipeline;
            QualitySettings.renderPipeline = mobilePipeline;
            yield return null;
            controller.TickVisual(1f);
            AssertVisualsHardClean();

            GraphicsSettings.defaultRenderPipeline = supportedPipeline;
            QualitySettings.renderPipeline = supportedPipeline;
            yield return null;
            controller.TickVisual(1f);
            AssertRole(firstObject.GetComponent<Renderer>(), AttachVisualLayers.Held);
        }

        [UnityTest]
        public IEnumerator DestroyedHeldRoot_HardCleansVisuals()
        {
            EnterSelecting(first, first);
            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            Object.Destroy(firstObject);
            yield return null;

            controller.TickVisual(1f);

            Assert.That(controller.VisualBlend, Is.Zero);
            Assert.That(Shader.GetGlobalFloat(AttachVisualShaderIds.VisualBlend), Is.Zero);
            Assert.That(GetProjectionMeshFilters().Count, Is.Zero);
        }

        [Test]
        public void DestroyedHeldRootDuringFade_HardCleansTheSurvivingIslandImmediately()
        {
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);
            EnterSelecting(first, first, second);
            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            ability.ReturnToDefault();
            Object.DestroyImmediate(firstObject);

            controller.TickVisual(0f);

            AssertRole(secondObject.GetComponent<Renderer>(), 0u);
            Assert.That(controller.VisualBlend, Is.Zero);
            Assert.That(Shader.GetGlobalFloat(AttachVisualShaderIds.VisualBlend), Is.Zero);
            Assert.That(GetProjectionMeshFilters(), Is.Empty);
            Assert.That(projection.LastSubmittedDrawCount, Is.Zero);
        }

        [Test]
        public void InactiveHeldRootDuringFade_HardCleansTheSurvivingIslandImmediately()
        {
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);
            EnterSelecting(first, first, second);
            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            ability.ReturnToDefault();
            firstObject.SetActive(false);

            controller.TickVisual(0f);

            AssertRole(secondObject.GetComponent<Renderer>(), 0u);
            Assert.That(controller.VisualBlend, Is.Zero);
            Assert.That(Shader.GetGlobalFloat(AttachVisualShaderIds.VisualBlend), Is.Zero);
            Assert.That(GetProjectionMeshFilters(), Is.Empty);
            Assert.That(projection.LastSubmittedDrawCount, Is.Zero);
        }

        [Test]
        public void SkinnedRenderer_IsHighlightedButNeverAddedToProjectionMeshes()
        {
            Mesh mesh = firstObject.GetComponent<MeshFilter>().sharedMesh;
            AttachableObject skinnedOnly = CreateSkinnedAttachable(mesh, out SkinnedMeshRenderer skinned);
            Camera camera = CreateProjectionCamera();
            projectionMaterial = new Material(FindProjectionTestShader());
            projection.Configure(camera, settings, projectionMaterial);
            EnterSelecting(skinnedOnly, skinnedOnly);
            Assert.That(ability.TryBeginHolding(), Is.True);
            controller.TickVisual(1f);
            SetLastSubmittedDrawCount(17);

            controller.TickVisual(1f);

            AssertRole(skinned, AttachVisualLayers.Held);
            Assert.That(GetProjectionMeshFilters(), Is.Empty);
            Assert.That(GetProjectionRenderers(), Has.Member(skinned));
            Assert.That(projection.LastSubmittedDrawCount, Is.Zero,
                "Submit resets the sentinel, then safely returns because a skinned-only island has no MeshFilter source.");
        }

        [Test]
        public void TopologyVersion_ChangesOnlyForGraphMutations()
        {
            int version = service.TopologyVersion;
            service.Register(first);
            Assert.That(service.TopologyVersion, Is.EqualTo(version));

            AttachableObject unknown = CreateInactiveAttachable("Unknown");
            service.Unregister(unknown);
            Assert.That(service.TopologyVersion, Is.EqualTo(version));

            AttachableObject registered = CreateInactiveAttachable("Registered");
            service.Register(registered);
            Assert.That(service.TopologyVersion, Is.EqualTo(++version));
            service.Register(registered);
            Assert.That(service.TopologyVersion, Is.EqualTo(version));

            Assert.That(service.Attach(first, first, Vector3.zero), Is.False);
            Assert.That(service.TopologyVersion, Is.EqualTo(version));
            Assert.That(service.Attach(first, second, Vector3.right), Is.True);
            Assert.That(service.TopologyVersion, Is.EqualTo(++version));
            Assert.That(service.GetIsland(first).Count, Is.EqualTo(2));
            Assert.That(service.Attach(first, second, Vector3.right), Is.False);
            Assert.That(service.TopologyVersion, Is.EqualTo(version));

            Assert.That(service.Detach(registered), Is.False);
            Assert.That(service.TopologyVersion, Is.EqualTo(version));
            Assert.That(service.Detach(first), Is.True);
            Assert.That(service.TopologyVersion, Is.EqualTo(++version));
            Assert.That(service.GetIsland(first).Count, Is.EqualTo(1));
            Assert.That(service.GetIsland(second).Count, Is.EqualTo(1));

            service.Unregister(registered);
            Assert.That(service.TopologyVersion, Is.EqualTo(++version));
        }

        private AttachableObject CreateAttachableCube(string name, Vector3 position, out GameObject target)
        {
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = position;
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            AttachableObject attachable = target.AddComponent<AttachableObject>();
            attachable.Configure(service);
            GameObject child = new("Renderer Child");
            child.transform.SetParent(target.transform, false);
            MeshFilter childFilter = child.AddComponent<MeshFilter>();
            childFilter.sharedMesh = target.GetComponent<MeshFilter>().sharedMesh;
            child.AddComponent<MeshRenderer>();
            return attachable;
        }

        private static Renderer GetChildRenderer(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            return renderers[1];
        }

        private AttachableObject CreateSkinnedAttachable(Mesh mesh, out SkinnedMeshRenderer skinned)
        {
            GameObject target = new("Skinned Only");
            extraObjects.Add(target);
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            AttachableObject attachable = target.AddComponent<AttachableObject>();
            attachable.Configure(service);
            skinned = target.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            return attachable;
        }

        private Camera CreateProjectionCamera()
        {
            GameObject cameraObject = new("Projection Camera");
            extraObjects.Add(cameraObject);
            return cameraObject.AddComponent<Camera>();
        }

        private AttachableObject CreateInactiveAttachable(string name)
        {
            GameObject target = new(name);
            target.SetActive(false);
            extraObjects.Add(target);
            target.AddComponent<Rigidbody>();
            return target.AddComponent<AttachableObject>();
        }

        private static Shader FindProjectionTestShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Assert.That(shader, Is.Not.Null);
            return shader;
        }

        private void EnterSelecting(AttachableObject current, params AttachableObject[] nearby)
        {
            ability.EnterSelecting();
            SetTargets(current, nearby);
        }

        private void SetTargets(AttachableObject current, params AttachableObject[] nearby)
        {
            FieldInfo nearbyField = typeof(AttachTargeting).GetField("nearby",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo currentField = typeof(AttachTargeting).GetField("<CurrentTarget>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(nearbyField, Is.Not.Null);
            Assert.That(currentField, Is.Not.Null);
            List<AttachableObject> targets = (List<AttachableObject>)nearbyField.GetValue(targeting);
            targets.Clear();
            targets.AddRange(nearby);
            currentField.SetValue(targeting, current);
        }

        private List<MeshFilter> GetProjectionMeshFilters()
        {
            FieldInfo field = typeof(AttachProjectionRenderer).GetField("meshFilters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (List<MeshFilter>)field.GetValue(projection);
        }

        private List<Renderer> GetProjectionRenderers()
        {
            FieldInfo field = typeof(AttachProjectionRenderer).GetField("renderers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (List<Renderer>)field.GetValue(projection);
        }

        private void SetLastSubmittedDrawCount(int value)
        {
            FieldInfo field = typeof(AttachProjectionRenderer).GetField("<LastSubmittedDrawCount>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(projection, value);
        }

        private VisualSnapshot CaptureVisualSnapshot(AttachAbilityController.AbilityState state)
        {
            return new VisualSnapshot(first, second, state);
        }

        private void AssertVisualSnapshotUnchanged(VisualSnapshot snapshot)
        {
            Assert.That(ability.State, Is.EqualTo(snapshot.State));
            snapshot.AssertUnchanged();
        }

        private static void AssertRole(Renderer renderer, uint expectedRole)
        {
            Assert.That(renderer.renderingLayerMask & AttachVisualLayers.Owned, Is.EqualTo(expectedRole));
        }

        private void AssertVisualsHardClean()
        {
            AssertRole(firstObject.GetComponent<Renderer>(), 0u);
            AssertRole(secondObject.GetComponent<Renderer>(), 0u);
            Assert.That(controller.VisualBlend, Is.Zero);
            Assert.That(Shader.GetGlobalFloat(AttachVisualShaderIds.VisualBlend), Is.Zero);
            Assert.That(GetProjectionMeshFilters(), Is.Empty);
            Assert.That(projection.LastSubmittedDrawCount, Is.Zero);
        }

        private static RenderPipelineAsset LoadPipelineAsset(string path)
        {
            System.Type assetDatabase = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            MethodInfo loadMainAsset = assetDatabase?.GetMethod("LoadMainAssetAtPath",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(loadMainAsset, Is.Not.Null);
            return loadMainAsset.Invoke(null, new object[] { path }) as RenderPipelineAsset;
        }

        private sealed class VisualSnapshot
        {
            private readonly RigidbodySnapshot[] bodies;
            private readonly RendererSnapshot[] renderers;
            private readonly ColliderSnapshot[] colliders;

            internal VisualSnapshot(
                AttachableObject firstAttachable,
                AttachableObject secondAttachable,
                AttachAbilityController.AbilityState state)
            {
                State = state;
                AttachableObject[] attachables = { firstAttachable, secondAttachable };
                bodies = new RigidbodySnapshot[attachables.Length];
                List<RendererSnapshot> rendererStates = new();
                List<ColliderSnapshot> colliderStates = new();
                for (int i = 0; i < attachables.Length; i++)
                {
                    bodies[i] = new RigidbodySnapshot(attachables[i].Body);
                    Renderer[] childRenderers = attachables[i].GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < childRenderers.Length; rendererIndex++)
                    {
                        rendererStates.Add(new RendererSnapshot(childRenderers[rendererIndex]));
                    }

                    Collider[] childColliders = attachables[i].GetComponentsInChildren<Collider>(true);
                    for (int colliderIndex = 0; colliderIndex < childColliders.Length; colliderIndex++)
                    {
                        colliderStates.Add(new ColliderSnapshot(childColliders[colliderIndex]));
                    }
                }

                renderers = rendererStates.ToArray();
                colliders = colliderStates.ToArray();
            }

            internal AttachAbilityController.AbilityState State { get; }

            internal void AssertUnchanged()
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    bodies[i].AssertUnchanged();
                }

                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].AssertUnchanged();
                }

                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].AssertUnchanged();
                }
            }
        }

        private readonly struct RigidbodySnapshot
        {
            private readonly Rigidbody body;
            private readonly float mass;
            private readonly bool useGravity;
            private readonly bool isKinematic;
            private readonly Vector3 linearVelocity;
            private readonly Vector3 angularVelocity;
            private readonly RigidbodyConstraints constraints;
            private readonly Vector3 inertiaTensor;
            private readonly Quaternion inertiaTensorRotation;
            private readonly float linearDamping;
            private readonly float angularDamping;
            private readonly RigidbodyInterpolation interpolation;
            private readonly CollisionDetectionMode collisionDetectionMode;
            private readonly bool detectCollisions;

            internal RigidbodySnapshot(Rigidbody source)
            {
                body = source;
                mass = source.mass;
                useGravity = source.useGravity;
                isKinematic = source.isKinematic;
                linearVelocity = source.linearVelocity;
                angularVelocity = source.angularVelocity;
                constraints = source.constraints;
                inertiaTensor = source.inertiaTensor;
                inertiaTensorRotation = source.inertiaTensorRotation;
                linearDamping = source.linearDamping;
                angularDamping = source.angularDamping;
                interpolation = source.interpolation;
                collisionDetectionMode = source.collisionDetectionMode;
                detectCollisions = source.detectCollisions;
            }

            internal void AssertUnchanged()
            {
                Assert.That(body.mass, Is.EqualTo(mass));
                Assert.That(body.useGravity, Is.EqualTo(useGravity));
                Assert.That(body.isKinematic, Is.EqualTo(isKinematic));
                Assert.That(body.linearVelocity, Is.EqualTo(linearVelocity));
                Assert.That(body.angularVelocity, Is.EqualTo(angularVelocity));
                Assert.That(body.constraints, Is.EqualTo(constraints));
                Assert.That(body.inertiaTensor, Is.EqualTo(inertiaTensor));
                Assert.That(body.inertiaTensorRotation, Is.EqualTo(inertiaTensorRotation));
                Assert.That(body.linearDamping, Is.EqualTo(linearDamping));
                Assert.That(body.angularDamping, Is.EqualTo(angularDamping));
                Assert.That(body.interpolation, Is.EqualTo(interpolation));
                Assert.That(body.collisionDetectionMode, Is.EqualTo(collisionDetectionMode));
                Assert.That(body.detectCollisions, Is.EqualTo(detectCollisions));
            }
        }

        private readonly struct RendererSnapshot
        {
            private readonly Renderer renderer;
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;
            private readonly uint unrelatedLayers;
            private readonly Material[] materials;

            internal RendererSnapshot(Renderer source)
            {
                renderer = source;
                position = source.transform.position;
                rotation = source.transform.rotation;
                scale = source.transform.localScale;
                unrelatedLayers = source.renderingLayerMask & ~AttachVisualLayers.Owned;
                materials = source.sharedMaterials;
            }

            internal void AssertUnchanged()
            {
                Assert.That(renderer.transform.position, Is.EqualTo(position));
                Assert.That(renderer.transform.rotation, Is.EqualTo(rotation));
                Assert.That(renderer.transform.localScale, Is.EqualTo(scale));
                Assert.That(renderer.renderingLayerMask & ~AttachVisualLayers.Owned, Is.EqualTo(unrelatedLayers));
                Material[] current = renderer.sharedMaterials;
                Assert.That(current.Length, Is.EqualTo(materials.Length));
                for (int i = 0; i < materials.Length; i++)
                {
                    Assert.That(current[i], Is.SameAs(materials[i]));
                }
            }
        }

        private readonly struct ColliderSnapshot
        {
            private readonly Collider collider;
            private readonly PhysicsMaterial material;

            internal ColliderSnapshot(Collider source)
            {
                collider = source;
                material = source.sharedMaterial;
            }

            internal void AssertUnchanged()
            {
                Assert.That(collider.sharedMaterial, Is.SameAs(material));
            }
        }
    }
}
