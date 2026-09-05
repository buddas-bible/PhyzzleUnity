using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Tests
{
    public sealed class AttachVisualAssetBuilderTests
    {
        private const string TempRoot =
            "Assets/Phyzzle/Tests/Temp/AttachVisualAssetBuilderTests";
        private const string MaterialsFolder = TempRoot + "/Materials";
        private const string RewindMaterialsFolder = TempRoot + "/RewindMaterials";
        private const string RendererPath = TempRoot + "/TestRenderer.asset";
        private const string SandboxScenePath = "Assets/Phyzzle/Scenes/PlayerMigrationSandbox.unity";
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string V2FixturePath =
            "Assets/Phyzzle/Tests/EditMode/Fixtures/PC_RendererV2.txt";
        private const string MigratedRendererPath = TempRoot + "/MigratedRenderer.asset";

        private UniversalRendererData rendererData;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            EnsureFolder("Assets/Phyzzle/Tests", "Temp");
            EnsureFolder("Assets/Phyzzle/Tests/Temp", "AttachVisualAssetBuilderTests");
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);
            AddFeature<SentinelRenderFeature>("Before Rewind");
            RewindVisualAssets rewindAssets =
                RewindVisualAssetBuilder.EnsureMaterials(RewindMaterialsFolder);
            RewindVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                rewindAssets.Mask,
                rewindAssets.Composite);
            AddFeature<SentinelRenderFeature>("After Rewind");
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            if (AssetDatabase.IsValidFolder("Assets/Phyzzle/Tests/Temp") &&
                !System.IO.Directory.EnumerateFileSystemEntries(
                    "Assets/Phyzzle/Tests/Temp").Any())
            {
                AssetDatabase.DeleteAsset("Assets/Phyzzle/Tests/Temp");
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void EnsureMaterials_CreatesThreeStableAssetsWithRequiredShaders()
        {
            AttachVisualAssets first = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);
            string[] firstGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder });

            AttachVisualAssets second = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);

            Assert.That(firstGuids, Has.Length.EqualTo(3));
            Assert.That(second.Mask, Is.SameAs(first.Mask));
            Assert.That(second.Composite, Is.SameAs(first.Composite));
            Assert.That(second.Projection, Is.SameAs(first.Projection));
            Assert.That(first.Mask.shader.name, Is.EqualTo("Hidden/Phyzzle/AttachMask"));
            Assert.That(first.Composite.shader.name, Is.EqualTo("Hidden/Phyzzle/AttachComposite"));
            Assert.That(first.Projection.shader.name, Is.EqualTo("Phyzzle/AttachProjection"));
            Assert.That(AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }),
                Is.EquivalentTo(firstGuids));
        }

        [Test]
        public void EnsureRendererFeature_ReusesOneAttachAfterLastRewindAndPreservesNeighbors()
        {
            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);

            AttachRenderFeature first = AttachVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                assets.Mask,
                assets.Composite);
            AttachRenderFeature second = AttachVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                assets.Mask,
                assets.Composite);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RendererPath, ImportAssetOptions.ForceUpdate);
            UniversalRendererData reloaded =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);

            Assert.That(second, Is.SameAs(first));
            Assert.That(reloaded.rendererFeatures.OfType<AttachRenderFeature>().ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(reloaded.rendererFeatures.Select(feature => feature.name), Is.EqualTo(new[]
            {
                "Before Rewind",
                "Recall Selection Visuals",
                "Attach Selection Visuals",
                "After Rewind"
            }));
            AttachRenderFeature reloadedAttach =
                reloaded.rendererFeatures.OfType<AttachRenderFeature>().Single();
            SerializedObject serializedAttach = new(reloadedAttach);
            Assert.That(serializedAttach.FindProperty("maskMaterial").objectReferenceValue,
                Is.SameAs(assets.Mask));
            Assert.That(serializedAttach.FindProperty("compositeMaterial").objectReferenceValue,
                Is.SameAs(assets.Composite));
            AssertRendererFeatureMapMatchesPersistentIds(reloaded);
        }

        [Test]
        public void EnsureRendererFeature_ReconfiguresTheExistingAttachFeature()
        {
            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);
            AttachRenderFeature existing = AddFeature<AttachRenderFeature>("Existing Attach");

            AttachRenderFeature result = AttachVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                assets.Mask,
                assets.Composite);

            Assert.That(result, Is.SameAs(existing));
            SerializedObject serializedAttach = new(existing);
            Assert.That(serializedAttach.FindProperty("maskMaterial").objectReferenceValue,
                Is.SameAs(assets.Mask));
            Assert.That(serializedAttach.FindProperty("compositeMaterial").objectReferenceValue,
                Is.SameAs(assets.Composite));
            Assert.That(rendererData.rendererFeatures.IndexOf(existing), Is.EqualTo(2));
            AssertRendererFeatureMapMatchesPersistentIds(rendererData);
        }

        [Test]
        public void EnsureRendererFeature_RejectsNullRendererData()
        {
            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                AttachVisualAssetBuilder.EnsureRendererFeature(null, assets.Mask, assets.Composite));

            Assert.That(exception.ParamName, Is.EqualTo("rendererData"));
        }

        [Test]
        public void EnsureRendererFeature_RejectsDuplicateAttachFeaturesWithoutDeletingThem()
        {
            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);
            AddFeature<AttachRenderFeature>("Duplicate Attach One");
            AddFeature<AttachRenderFeature>("Duplicate Attach Two");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                AttachVisualAssetBuilder.EnsureRendererFeature(
                    rendererData,
                    assets.Mask,
                    assets.Composite));

            Assert.That(exception.Message, Does.Contain("multiple Attach render features"));
            Assert.That(rendererData.rendererFeatures.OfType<AttachRenderFeature>().ToArray(),
                Has.Length.EqualTo(2));
        }

        [Test]
        public void EnsureRendererFeature_RejectsNonPersistentExistingFeatureBeforeMutation()
        {
            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);
            SentinelRenderFeature nonPersistent = ScriptableObject.CreateInstance<SentinelRenderFeature>();
            rendererData.rendererFeatures.Add(nonPersistent);
            int featureCountBefore = rendererData.rendererFeatures.Count;
            long[] mapBefore = GetRendererFeatureMap(rendererData);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                AttachVisualAssetBuilder.EnsureRendererFeature(
                    rendererData,
                    assets.Mask,
                    assets.Composite));

            Assert.That(exception.Message, Does.Contain("persistent local file ID"));
            Assert.That(rendererData.rendererFeatures, Has.Count.EqualTo(featureCountBefore));
            Assert.That(rendererData.rendererFeatures, Does.Contain(nonPersistent));
            Assert.That(rendererData.rendererFeatures.OfType<AttachRenderFeature>().ToArray(),
                Has.Length.Zero);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(RendererPath).OfType<AttachRenderFeature>(),
                Is.Empty);
            Assert.That(GetRendererFeatureMap(rendererData), Is.EqualTo(mapBefore));

            rendererData.rendererFeatures.Remove(nonPersistent);
            UnityEngine.Object.DestroyImmediate(nonPersistent);
            AttachRenderFeature retry = AttachVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                assets.Mask,
                assets.Composite);

            Assert.That(retry, Is.Not.Null);
            Assert.That(rendererData.rendererFeatures.OfType<AttachRenderFeature>().ToArray(),
                Has.Length.EqualTo(1));
            AssertRendererFeatureMapMatchesPersistentIds(rendererData);
        }

        [Test]
        public void EnsureRendererFeature_MigratesV2FixtureWithoutChangingSupportedSettings()
        {
            string fixturePath = AssetDatabase.GetAssetPath(
                AssetDatabase.LoadAssetAtPath<TextAsset>(V2FixturePath));
            string rawFixture = File.ReadAllText(fixturePath);
            Assert.That(rawFixture, Does.Contain("m_AssetVersion: 2"));
            Assert.That(rawFixture, Does.Contain("m_UseNativeRenderPass: 1"));
            Assert.That(rawFixture, Does.Contain("m_BlueNoise256Textures:"));
            Assert.That(rawFixture, Does.Contain("m_Shader: {fileID: 4800000"));

            File.Copy(fixturePath, MigratedRendererPath, true);
            AssetDatabase.ImportAsset(MigratedRendererPath, ImportAssetOptions.ForceUpdate);
            UniversalRendererData migrated =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MigratedRendererPath);
            SerializedObject serializedMigrated = new(migrated);
            Assert.That(serializedMigrated.FindProperty("m_AssetVersion").intValue, Is.EqualTo(3));
            Assert.That(GetLayerMaskBits(serializedMigrated, "m_PrepassLayerMask"), Is.EqualTo(
                GetLayerMaskBits(serializedMigrated, "m_OpaqueLayerMask")));
            Assert.That(serializedMigrated.FindProperty("m_UseNativeRenderPass"), Is.Null);

            RendererSettingSnapshot[] settingsBefore = SnapshotRendererSettings(migrated);
            FeatureSnapshot[] featuresBefore = migrated.rendererFeatures.Select(Snapshot).ToArray();
            ScreenSpaceAmbientOcclusion ssao =
                migrated.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Single();
            Assert.DoesNotThrow(ssao.Create);

            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials(MaterialsFolder);
            AttachVisualAssetBuilder.EnsureRendererFeature(migrated, assets.Mask, assets.Composite);
            AssetDatabase.SaveAssetIfDirty(migrated);
            AssetDatabase.ImportAsset(MigratedRendererPath, ImportAssetOptions.ForceUpdate);
            UniversalRendererData reloaded =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MigratedRendererPath);

            AssertRendererSettingsEqual(settingsBefore, SnapshotRendererSettings(reloaded));
            AssertFeatureSnapshotsEqual(featuresBefore, reloaded.rendererFeatures
                .Where(feature => feature is not AttachRenderFeature)
                .Select(Snapshot)
                .ToArray());
            Assert.That(reloaded.rendererFeatures.OfType<AttachRenderFeature>().ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(reloaded.rendererFeatures.IndexOf(
                reloaded.rendererFeatures.OfType<AttachRenderFeature>().Single()), Is.EqualTo(
                reloaded.rendererFeatures.FindLastIndex(feature => feature is RewindRenderFeature) + 1));
            AssertRendererFeatureMapMatchesPersistentIds(reloaded);
        }

        [Test]
        public void CreateSandbox_PersistsAttachVisualReferencesAcrossReload()
        {
            PlayerSandboxBuilder.CreateSandbox();
            EditorSceneManager.OpenScene(SandboxScenePath);
            GameObject player = GameObject.Find("PhyzzlePlayer");
            AttachVisualController visuals = player.GetComponent<AttachVisualController>();
            AttachProjectionRenderer projection = player.GetComponent<AttachProjectionRenderer>();

            Assert.That(player.GetComponents<AttachVisualController>(), Has.Length.EqualTo(1));
            Assert.That(player.GetComponents<AttachProjectionRenderer>(), Has.Length.EqualTo(1));
            AssertSerializedReferencesArePresent(new SerializedObject(visuals),
                "ability", "targeting", "holdController", "attachmentService", "projectionRenderer", "settings",
                "supportedPipeline");
            Assert.That(new SerializedObject(visuals).FindProperty("supportedPipeline").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset")));
            AssertSerializedReferencesArePresent(new SerializedObject(projection),
                "camera", "settings", "projectionMaterial");
        }

        [Test]
        public void PcRenderer_PreservesFeatureOrderAndPersistentFeatureMap()
        {
            UniversalRendererData pcRenderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials();
            AttachRenderFeature attach =
                AttachVisualAssetBuilder.EnsurePcRendererFeature(assets);

            Assert.That(pcRenderer.rendererFeatures.Select(feature => feature.name), Is.EqualTo(new[]
            {
                "ScreenSpaceAmbientOcclusion",
                "Recall Selection Visuals",
                "Attach Selection Visuals"
            }));
            Assert.That(pcRenderer.rendererFeatures.OfType<AttachRenderFeature>().ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(pcRenderer.rendererFeatures.IndexOf(attach), Is.EqualTo(
                pcRenderer.rendererFeatures.FindLastIndex(feature => feature is RewindRenderFeature) + 1));
            AssertRendererFeatureMapMatchesPersistentIds(pcRenderer);
        }

        [Test]
        public void EnsurePcRendererFeature_PreservesExistingFeatureSemanticsAcrossSaveReload()
        {
            UniversalRendererData pcRenderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            FeatureSnapshot[] before = pcRenderer.rendererFeatures
                .Where(feature => feature is not AttachRenderFeature)
                .Select(Snapshot)
                .ToArray();
            ScreenSpaceAmbientOcclusion ssao =
                pcRenderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Single();
            Assert.DoesNotThrow(ssao.Create);

            AttachVisualAssets assets = AttachVisualAssetBuilder.EnsureMaterials();
            AttachVisualAssetBuilder.EnsurePcRendererFeature(assets);
            AssetDatabase.SaveAssetIfDirty(pcRenderer);
            AssetDatabase.ImportAsset(PcRendererPath, ImportAssetOptions.ForceUpdate);
            UniversalRendererData reloaded =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            FeatureSnapshot[] after = reloaded.rendererFeatures
                .Where(feature => feature is not AttachRenderFeature)
                .Select(Snapshot)
                .ToArray();

            Assert.That(after, Has.Length.EqualTo(before.Length));
            for (int index = 0; index < before.Length; index++)
            {
                Assert.That(after[index].TypeName, Is.EqualTo(before[index].TypeName));
                Assert.That(after[index].Name, Is.EqualTo(before[index].Name));
                Assert.That(after[index].Active, Is.EqualTo(before[index].Active));
                Assert.That(after[index].SerializedJson, Is.EqualTo(before[index].SerializedJson));
            }

            AssertRendererFeatureMapMatchesPersistentIds(reloaded);
        }

        private TFeature AddFeature<TFeature>(string name) where TFeature : ScriptableRendererFeature
        {
            TFeature feature = ScriptableObject.CreateInstance<TFeature>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(rendererData);
            return feature;
        }

        private static void AssertRendererFeatureMapMatchesPersistentIds(UniversalRendererData data)
        {
            SerializedObject serialized = new(data);
            SerializedProperty featureMap = serialized.FindProperty("m_RendererFeatureMap");
            Assert.That(featureMap.arraySize, Is.EqualTo(data.rendererFeatures.Count));
            for (int index = 0; index < data.rendererFeatures.Count; index++)
            {
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    data.rendererFeatures[index], out _, out long localId), Is.True);
                Assert.That(featureMap.GetArrayElementAtIndex(index).longValue,
                    Is.EqualTo(localId));
            }
        }

        private static long[] GetRendererFeatureMap(UniversalRendererData data)
        {
            SerializedProperty map = new SerializedObject(data).FindProperty("m_RendererFeatureMap");
            long[] values = new long[map.arraySize];
            for (int index = 0; index < map.arraySize; index++)
            {
                values[index] = map.GetArrayElementAtIndex(index).longValue;
            }

            return values;
        }

        private static int GetLayerMaskBits(SerializedObject serialized, string propertyName)
        {
            return serialized.FindProperty(propertyName).FindPropertyRelative("m_Bits").intValue;
        }

        private static RendererSettingSnapshot[] SnapshotRendererSettings(UniversalRendererData data)
        {
            SerializedObject serialized = new(data);
            SerializedProperty property = serialized.GetIterator();
            List<RendererSettingSnapshot> snapshots = new();
            while (property.NextVisible(true))
            {
                if (IsIgnoredRendererProperty(property.propertyPath) || property.hasVisibleChildren)
                {
                    continue;
                }

                snapshots.Add(new RendererSettingSnapshot(
                    property.propertyPath,
                    property.propertyType,
                    GetPropertyValue(property)));
            }

            return snapshots.ToArray();
        }

        private static bool IsIgnoredRendererProperty(string path)
        {
            return path == "m_Script" || path == "m_Name" ||
                   path == "m_EditorClassIdentifier" || path == "m_ObjectHideFlags" ||
                   path == "m_CorrespondingSourceObject" || path == "m_PrefabInstance" ||
                   path == "m_PrefabAsset" || path == "m_GameObject" ||
                   path == "m_AssetVersion" || path.StartsWith("m_RendererFeatures") ||
                   path.StartsWith("m_RendererFeatureMap");
        }

        private static string GetPropertyValue(SerializedProperty property)
        {
            return property.propertyType switch
            {
                SerializedPropertyType.Integer => property.longValue.ToString(),
                SerializedPropertyType.Boolean => property.boolValue.ToString(),
                SerializedPropertyType.Float => property.doubleValue.ToString("R"),
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Color => property.colorValue.ToString(),
                SerializedPropertyType.ObjectReference => GetObjectIdentity(property.objectReferenceValue),
                SerializedPropertyType.LayerMask => property.intValue.ToString(),
                SerializedPropertyType.Enum => property.enumValueIndex.ToString(),
                SerializedPropertyType.Vector2 => property.vector2Value.ToString(),
                SerializedPropertyType.Vector3 => property.vector3Value.ToString(),
                SerializedPropertyType.Vector4 => property.vector4Value.ToString(),
                SerializedPropertyType.Rect => property.rectValue.ToString(),
                SerializedPropertyType.Bounds => property.boundsValue.ToString(),
                SerializedPropertyType.Quaternion => property.quaternionValue.ToString(),
                SerializedPropertyType.Hash128 => property.hash128Value.ToString(),
                _ => property.boxedValue?.ToString() ?? "null"
            };
        }

        private static string GetObjectIdentity(UnityEngine.Object value)
        {
            if (value == null)
            {
                return "null";
            }

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId)
                ? guid + ":" + localId
                : value.GetType().FullName + ":" + value.name;
        }

        private static void AssertRendererSettingsEqual(
            RendererSettingSnapshot[] expected,
            RendererSettingSnapshot[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].Path, Is.EqualTo(expected[index].Path));
                Assert.That(actual[index].Type, Is.EqualTo(expected[index].Type));
                Assert.That(actual[index].Value, Is.EqualTo(expected[index].Value));
            }
        }

        private static void AssertFeatureSnapshotsEqual(
            FeatureSnapshot[] expected,
            FeatureSnapshot[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].TypeName, Is.EqualTo(expected[index].TypeName));
                Assert.That(actual[index].Name, Is.EqualTo(expected[index].Name));
                Assert.That(actual[index].Active, Is.EqualTo(expected[index].Active));
                Assert.That(actual[index].SerializedJson, Is.EqualTo(expected[index].SerializedJson));
            }
        }

        private static void AssertSerializedReferencesArePresent(
            SerializedObject serialized,
            params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, $"{propertyName} must be serialized.");
                Assert.That(property.objectReferenceValue, Is.Not.Null,
                    $"{propertyName} must survive the sandbox scene reload.");
            }
        }

        private static FeatureSnapshot Snapshot(ScriptableRendererFeature feature)
        {
            return new FeatureSnapshot(
                feature.GetType().FullName,
                feature.name,
                feature.isActive,
                EditorJsonUtility.ToJson(feature));
        }

        private readonly struct FeatureSnapshot
        {
            internal FeatureSnapshot(string typeName, string name, bool active, string serializedJson)
            {
                TypeName = typeName;
                Name = name;
                Active = active;
                SerializedJson = serializedJson;
            }

            internal string TypeName { get; }
            internal string Name { get; }
            internal bool Active { get; }
            internal string SerializedJson { get; }
        }

        private readonly struct RendererSettingSnapshot
        {
            internal RendererSettingSnapshot(
                string path,
                SerializedPropertyType type,
                string value)
            {
                Path = path;
                Type = type;
                Value = value;
            }

            internal string Path { get; }
            internal SerializedPropertyType Type { get; }
            internal string Value { get; }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
