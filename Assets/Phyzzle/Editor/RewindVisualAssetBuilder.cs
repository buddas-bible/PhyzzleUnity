using System;
using System.Linq;
using Phyzzle.Abilities.Rewind;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Editor
{
    /// <summary>
    /// 되감기 시각 효과에 필요한 마스크, 합성, 프리뷰 재질을 묶어 전달한다.
    /// </summary>
    public readonly struct RewindVisualAssets
    {
        /// <summary>
        /// 되감기 시각 효과 재질 묶음을 생성한다.
        /// </summary>
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

    /// <summary>
    /// 되감기 VFX 재질 자산과 URP Renderer Feature를 생성·검증한다.
    /// </summary>
    public static class RewindVisualAssetBuilder
    {
        private const string DefaultMaterialsFolder = "Assets/Phyzzle/Settings";
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        /// <summary>
        /// 되감기 VFX에 필요한 모든 재질을 지정 폴더에 생성하거나 기존 자산을 검증한다.
        /// </summary>
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

        /// <summary>
        /// PC RendererData에 되감기 Renderer Feature가 존재하도록 구성한다.
        /// </summary>
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

        /// <summary>
        /// 지정 RendererData의 되감기 피처를 하나로 유지하고 재질과 Feature Map을 동기화한다.
        /// </summary>
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

        /// <summary>
        /// Renderer Feature 목록과 직렬화된 local file ID 맵의 길이와 순서를 일치시킨다.
        /// </summary>
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

        /// <summary>
        /// 지정 셰이더를 사용하는 Material 자산을 생성하거나 셰이더 참조를 교정한다.
        /// </summary>
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

        /// <summary>
        /// 중첩 경로를 순회하며 존재하지 않는 AssetDatabase 폴더를 순서대로 생성한다.
        /// </summary>
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
