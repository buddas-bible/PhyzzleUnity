using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Phyzzle.Tests
{
    public sealed class PlayerSandboxBuilderTests
    {
        private GameObject player;
        private RewindSettings settings;
        private Material previewMaterial;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Player");
            settings = ScriptableObject.CreateInstance<RewindSettings>();
            Shader shader = Shader.Find("Phyzzle/RewindPreview");
            Assert.That(shader, Is.Not.Null);
            previewMaterial = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(previewMaterial);
        }

        [Test]
        public void EnsureRewindVisualController_ConfiguresExactlyOneWithoutGameplayComponents()
        {
            RewindTargeting targeting = player.AddComponent<RewindTargeting>();
            RewindAbilityController ability = player.AddComponent<RewindAbilityController>();
            int rigidbodyCount = player.GetComponentsInChildren<Rigidbody>(true).Length;
            int colliderCount = player.GetComponentsInChildren<Collider>(true).Length;
            int cameraCount = player.GetComponentsInChildren<Camera>(true).Length;
            int listenerCount = player.GetComponentsInChildren<AudioListener>(true).Length;
            int eventSystemCount = player.GetComponentsInChildren<EventSystem>(true).Length;

            RewindVisualController first =
                PlayerSandboxBuilder.EnsureRewindVisualController(
                    player,
                    ability,
                    targeting,
                    settings,
                    previewMaterial);
            RewindVisualController second =
                PlayerSandboxBuilder.EnsureRewindVisualController(
                    player,
                    ability,
                    targeting,
                    settings,
                    previewMaterial);

            Assert.That(second, Is.SameAs(first));
            Assert.That(player.GetComponents<RewindVisualController>(), Has.Length.EqualTo(1));
            SerializedObject serialized = new(first);
            Assert.That(serialized.FindProperty("ability").objectReferenceValue, Is.SameAs(ability));
            Assert.That(serialized.FindProperty("targeting").objectReferenceValue, Is.SameAs(targeting));
            Assert.That(serialized.FindProperty("playerRoot").objectReferenceValue,
                Is.SameAs(player.transform));
            Assert.That(serialized.FindProperty("settings").objectReferenceValue, Is.SameAs(settings));
            Assert.That(serialized.FindProperty("previewMaterial").objectReferenceValue,
                Is.SameAs(previewMaterial));
            Assert.That(serialized.FindProperty("supportedPipeline").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(
                    "Assets/Settings/PC_RPAsset.asset")));
            Assert.That(player.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(rigidbodyCount));
            Assert.That(player.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(colliderCount));
            Assert.That(player.GetComponentsInChildren<Camera>(true), Has.Length.EqualTo(cameraCount));
            Assert.That(player.GetComponentsInChildren<AudioListener>(true),
                Has.Length.EqualTo(listenerCount));
            Assert.That(player.GetComponentsInChildren<EventSystem>(true),
                Has.Length.EqualTo(eventSystemCount));
        }
    }
}
