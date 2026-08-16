using System;
using System.Linq;
using Phyzzle.Abilities.Rewind;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Editor
{
    public readonly struct RewindVisualAssets
    {
        internal RewindVisualAssets(Material mask, Material composite, Material preview)
        {
            Mask = mask;
            Composite = composite;
            Preview = preview;
        }

        public Material Mask { get; }
        public Material Composite { get; }
        public Material Preview { get; }
    }

    public static class RewindVisualAssetBuilder
    {
        private const string DefaultMaterialsFolder = "Assets/Phyzzle/Settings";
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        public static RewindVisualAssets EnsureMaterials(
            string materialsFolder = DefaultMaterialsFolder)
        {
            EnsureFolder(materialsFolder);
            Material mask = EnsureMaterial(
                materialsFolder + "/RewindMask.mat",
                "Hidden/Phyzzle/RewindMask");
            Material composite = EnsureMaterial(
                materialsFolder + "/RewindComposite.mat",
                "Hidden/Phyzzle/RewindComposite");
            Material preview = EnsureMaterial(
                materialsFolder + "/RewindPreview.mat",
                "Phyzzle/RewindPreview");
            return new RewindVisualAssets(mask, composite, preview);
        }

        public static RewindRenderFeature EnsurePcRendererFeature(RewindVisualAssets assets)
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (rendererData == null)
            {
                throw new InvalidOperationException(
                    $"PC renderer data was not found at {PcRendererPath}.");
            }

            return EnsureRendererFeature(rendererData, assets.Mask, assets.Composite);
        }

        public static RewindRenderFeature EnsureRendererFeature(
            UniversalRendererData rendererData,
            Material maskMaterial,
            Material compositeMaterial)
        {
            if (rendererData == null)
            {
                throw new ArgumentNullException(nameof(rendererData));
            }

            RewindRenderFeature[] existing =
                rendererData.rendererFeatures.OfType<RewindRenderFeature>().ToArray();
            if (existing.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Renderer data '{rendererData.name}' has multiple Recall render features.");
            }

            RewindRenderFeature feature;
            bool created = existing.Length == 0;
            if (existing.Length == 1)
            {
                feature = existing[0];
            }
            else
            {
                feature = ScriptableObject.CreateInstance<RewindRenderFeature>();
                feature.name = "Recall Selection Visuals";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            SerializedObject serializedFeature = new(feature);
            bool materialsChanged =
                serializedFeature.FindProperty("maskMaterial").objectReferenceValue != maskMaterial ||
                serializedFeature.FindProperty("compositeMaterial").objectReferenceValue !=
                compositeMaterial;
            if (created || materialsChanged)
            {
                feature.Configure(maskMaterial, compositeMaterial);
                feature.Create();
                EditorUtility.SetDirty(feature);
            }

            bool mapChanged = SynchronizeRendererFeatureMap(rendererData);
            if (created || mapChanged)
            {
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssetIfDirty(rendererData);
            }
            else if (materialsChanged)
            {
                AssetDatabase.SaveAssetIfDirty(feature);
            }

            return feature;
        }

        private static bool SynchronizeRendererFeatureMap(
            UniversalRendererData rendererData)
        {
            SerializedObject serialized = new(rendererData);
            serialized.Update();
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            bool changed = map.arraySize != rendererData.rendererFeatures.Count;
            map.arraySize = rendererData.rendererFeatures.Count;
            for (int index = 0; index < rendererData.rendererFeatures.Count; index++)
            {
                ScriptableRendererFeature feature = rendererData.rendererFeatures[index];
                if (feature == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        feature,
                        out _,
                        out long localId))
                {
                    throw new InvalidOperationException(
                        $"Renderer feature {index} has no persistent local file ID.");
                }

                SerializedProperty entry = map.GetArrayElementAtIndex(index);
                if (entry.longValue != localId)
                {
                    entry.longValue = localId;
                    changed = true;
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererData);
            }

            return changed;
        }

        private static Material EnsureMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"Required shader '{shaderName}' was not found.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path)
                };
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssetIfDirty(material);
            }

            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
