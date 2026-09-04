using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;
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
            controller.Configure(ability, targeting, hold, service, projection, settings);

            first = CreateAttachableCube("First", Vector3.zero, out firstObject);
            second = CreateAttachableCube("Second", Vector3.right * 2f, out secondObject);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerObject);
            Object.Destroy(firstObject);
            Object.Destroy(secondObject);
            Object.Destroy(projectionObject);
            Object.Destroy(serviceObject);
            Object.Destroy(settings);
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
            Rigidbody body = first.Body;
            Collider collider = firstObject.GetComponent<Collider>();
            Vector3 position = firstObject.transform.position;
            Quaternion rotation = firstObject.transform.rotation;
            float mass = body.mass;
            bool gravity = body.useGravity;
            PhysicsMaterial material = collider.sharedMaterial;
            EnterSelecting(first, first, second);

            controller.TickVisual(1f);

            Assert.That(firstObject.transform.position, Is.EqualTo(position));
            Assert.That(firstObject.transform.rotation, Is.EqualTo(rotation));
            Assert.That(body.mass, Is.EqualTo(mass));
            Assert.That(body.useGravity, Is.EqualTo(gravity));
            Assert.That(collider.sharedMaterial, Is.SameAs(material));
            Assert.That(ability.State, Is.EqualTo(AttachAbilityController.AbilityState.Selecting));
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
        public void SkinnedRenderer_IsHighlightedButNeverAddedToProjectionMeshes()
        {
            Mesh mesh = firstObject.GetComponent<MeshFilter>().sharedMesh;
            GameObject skinnedObject = new("Skinned");
            skinnedObject.transform.SetParent(firstObject.transform, false);
            SkinnedMeshRenderer skinned = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            EnterSelecting(first, first);
            Assert.That(ability.TryBeginHolding(), Is.True);

            controller.TickVisual(1f);

            AssertRole(skinned, AttachVisualLayers.Held);
            Assert.That(GetProjectionMeshFilters(), Has.None.EqualTo(skinned));
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

        private static void AssertRole(Renderer renderer, uint expectedRole)
        {
            Assert.That(renderer.renderingLayerMask & AttachVisualLayers.Owned, Is.EqualTo(expectedRole));
        }
    }
}
