using System.Linq;
using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Tests
{
    public sealed class RewindVisualAssetBuilderTests
    {
        private const string TempRoot =
            "Assets/Phyzzle/Tests/Temp/RewindVisualAssetBuilderTests";
        private const string MaterialsFolder = TempRoot + "/Materials";
        private const string RendererPath = TempRoot + "/TestRenderer.asset";

        private UniversalRendererData rendererData;
        private SentinelRenderFeature sentinel;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            EnsureFolder("Assets/Phyzzle/Tests", "Temp");
            EnsureFolder("Assets/Phyzzle/Tests/Temp", "RewindVisualAssetBuilderTests");
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);
            sentinel = ScriptableObject.CreateInstance<SentinelRenderFeature>();
            sentinel.name = "Sentinel Feature";
            AssetDatabase.AddObjectToAsset(sentinel, rendererData);
            rendererData.rendererFeatures.Add(sentinel);
            EditorUtility.SetDirty(rendererData);
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
        public void EnsureMaterials_IsIdempotentAndPreservesAssetGuids()
        {
            RewindVisualAssets first = RewindVisualAssetBuilder.EnsureMaterials(MaterialsFolder);
            string maskGuid = AssetDatabase.AssetPathToGUID(MaterialsFolder + "/RewindMask.mat");
            string compositeGuid =
                AssetDatabase.AssetPathToGUID(MaterialsFolder + "/RewindComposite.mat");
            string previewGuid = AssetDatabase.AssetPathToGUID(MaterialsFolder + "/RewindPreview.mat");

            RewindVisualAssets second = RewindVisualAssetBuilder.EnsureMaterials(MaterialsFolder);

            Assert.That(first.Mask, Is.SameAs(second.Mask));
            Assert.That(first.Composite, Is.SameAs(second.Composite));
            Assert.That(first.Preview, Is.SameAs(second.Preview));
            Assert.That(first.Mask.shader.name, Is.EqualTo("Hidden/Phyzzle/RewindMask"));
            Assert.That(first.Composite.shader.name, Is.EqualTo("Hidden/Phyzzle/RewindComposite"));
            Assert.That(first.Preview.shader.name, Is.EqualTo("Phyzzle/RewindPreview"));
            Assert.That(AssetDatabase.AssetPathToGUID(MaterialsFolder + "/RewindMask.mat"),
                Is.EqualTo(maskGuid).And.Not.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(MaterialsFolder + "/RewindComposite.mat"),
                Is.EqualTo(compositeGuid).And.Not.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(MaterialsFolder + "/RewindPreview.mat"),
                Is.EqualTo(previewGuid).And.Not.Empty);
        }

        [Test]
        public void EnsureRendererFeature_AppendsOnceAndSurvivesSaveReload()
        {
            RewindVisualAssets assets = RewindVisualAssetBuilder.EnsureMaterials(MaterialsFolder);

            RewindRenderFeature first = RewindVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                assets.Mask,
                assets.Composite);
            RewindRenderFeature second = RewindVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                assets.Mask,
                assets.Composite);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RendererPath, ImportAssetOptions.ForceUpdate);
            UniversalRendererData reloaded =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            RewindRenderFeature reloadedFeature =
                reloaded.rendererFeatures.OfType<RewindRenderFeature>().Single();

            Assert.That(first, Is.SameAs(second));
            Assert.That(reloaded.rendererFeatures, Has.Count.EqualTo(2));
            Assert.That(reloaded.rendererFeatures[0].name, Is.EqualTo("Sentinel Feature"));
            Assert.That(reloaded.rendererFeatures[1], Is.SameAs(reloadedFeature));
            Assert.That(reloadedFeature.MaskMaterial, Is.SameAs(assets.Mask));
            Assert.That(reloadedFeature.CompositeMaterial, Is.SameAs(assets.Composite));
            Assert.That(AssetDatabase.GetAssetPath(reloadedFeature), Is.EqualTo(RendererPath));

            SerializedObject serializedRenderer = new(reloaded);
            SerializedProperty featureMap =
                serializedRenderer.FindProperty("m_RendererFeatureMap");
            Assert.That(featureMap.arraySize, Is.EqualTo(reloaded.rendererFeatures.Count));
            for (int index = 0; index < reloaded.rendererFeatures.Count; index++)
            {
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    reloaded.rendererFeatures[index], out _, out long localId), Is.True);
                Assert.That(featureMap.GetArrayElementAtIndex(index).longValue,
                    Is.EqualTo(localId));
            }
        }

        [Test]
        public void EnsureInstallation_DoesNotSaveAnUnrelatedDirtyAsset()
        {
            const string unrelatedPath = TempRoot + "/UnrelatedSettings.asset";
            RewindSettings unrelated = ScriptableObject.CreateInstance<RewindSettings>();
            AssetDatabase.CreateAsset(unrelated, unrelatedPath);
            AssetDatabase.SaveAssetIfDirty(unrelated);
            unrelated.targetRayDistance += 1f;
            EditorUtility.SetDirty(unrelated);
            Assert.That(EditorUtility.IsDirty(unrelated), Is.True);

            RewindVisualAssets assets = RewindVisualAssetBuilder.EnsureMaterials(MaterialsFolder);
            RewindVisualAssetBuilder.EnsureRendererFeature(
                rendererData,
                assets.Mask,
                assets.Composite);

            Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
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
