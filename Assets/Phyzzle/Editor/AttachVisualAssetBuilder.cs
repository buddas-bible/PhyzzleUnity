using System;
using System.Linq;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Phyzzle.Editor
{
    /// <summary>
    /// 부착 시각 효과에 필요한 마스크, 합성, 투영, 테더와 접촉 프리뷰 재질을 묶어 전달한다.
    /// </summary>
    public readonly struct AttachVisualAssets
    {
        /// <summary>
        /// 부착 시각 효과 재질 묶음을 생성한다.
        /// </summary>
        internal AttachVisualAssets(Material mask, Material composite, Material projection, Material tether, Material contactPreview)
        {
            Mask = mask;
            Composite = composite;
            Projection = projection;
            Tether = tether;
            ContactPreview = contactPreview;
        }

        public Material Mask { get; }
        public Material Composite { get; }
        public Material Projection { get; }
        public Material Tether { get; }
        public Material ContactPreview { get; }
    }

    /// <summary>
    /// 부착 VFX 재질 자산과 URP Renderer Feature를 생성·검증하고 올바른 순서로 구성한다.
    /// </summary>
    public static class AttachVisualAssetBuilder
    {
        private const string DefaultMaterialsFolder = "Assets/Phyzzle/Settings";
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        /// <summary>
        /// 부착 VFX에 필요한 모든 재질을 지정 폴더에 생성하거나 기존 자산을 검증한다.
        /// </summary>
        public static AttachVisualAssets EnsureMaterials(
            string materialsFolder = DefaultMaterialsFolder)
        {
            EnsureFolder(materialsFolder);
            Material mask = EnsureMaterial(
                materialsFolder + "/AttachMask.mat",
                "Hidden/Phyzzle/AttachMask");
            Material composite = EnsureMaterial(
                materialsFolder + "/AttachComposite.mat",
                "Hidden/Phyzzle/AttachComposite");
            Material projection = EnsureMaterial(
                materialsFolder + "/AttachProjection.mat",
                "Phyzzle/AttachProjection");
            Material tether = EnsureMaterial(
                materialsFolder + "/AttachTether.mat",
                "Phyzzle/AttachTether");
            Material contactPreview = EnsureMaterial(
                materialsFolder + "/AttachContactPreview.mat",
                "Phyzzle/AttachContactPreview");
            return new AttachVisualAssets(mask, composite, projection, tether, contactPreview);
        }

        /// <summary>
        /// PC RendererData에 부착 Renderer Feature가 존재하도록 구성한다.
        /// </summary>
        public static AttachRenderFeature EnsurePcRendererFeature(AttachVisualAssets assets)
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
        /// 지정 RendererData의 부착 피처를 하나로 유지하고 재질·순서·Feature Map을 동기화한다.
        /// </summary>
        public static AttachRenderFeature EnsureRendererFeature(
            UniversalRendererData rendererData,
            Material maskMaterial,
            Material compositeMaterial)
        {
            if (rendererData == null)
            {
                throw new ArgumentNullException(nameof(rendererData));
            }

            ValidatePersistentRendererFeatures(rendererData);

            AttachRenderFeature[] existing =
                rendererData.rendererFeatures.OfType<AttachRenderFeature>().ToArray();
            if (existing.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Renderer data '{rendererData.name}' has multiple Attach render features.");
            }

            AttachRenderFeature feature;
            bool created = existing.Length == 0;
            if (created)
            {
                feature = ScriptableObject.CreateInstance<AttachRenderFeature>();
                feature.name = "Attach Selection Visuals";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }
            else
            {
                feature = existing[0];
            }

            SerializedObject serializedFeature = new(feature);
            bool materialsChanged =
                serializedFeature.FindProperty("maskMaterial").objectReferenceValue != maskMaterial ||
                serializedFeature.FindProperty("compositeMaterial").objectReferenceValue !=
                compositeMaterial;
            feature.Configure(maskMaterial, compositeMaterial);
            if (created || materialsChanged)
            {
                feature.Create();
                EditorUtility.SetDirty(feature);
            }

            bool orderChanged = MoveAfterLastRewindFeature(rendererData, feature);
            bool mapChanged = SynchronizeRendererFeatureMap(rendererData);
            if (created || orderChanged || mapChanged)
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
        /// 기존 Renderer Feature가 모두 RendererData의 영속 서브자산인지 검증한다.
        /// </summary>
        private static void ValidatePersistentRendererFeatures(
            UniversalRendererData rendererData)
        {
            for (int index = 0; index < rendererData.rendererFeatures.Count; index++)
            {
                ScriptableRendererFeature feature = rendererData.rendererFeatures[index];
                if (feature == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        feature,
                        out _,
                        out long _))
                {
                    throw new InvalidOperationException(
                        $"Renderer feature {index} has no persistent local file ID.");
                }
            }
        }

        /// <summary>
        /// 부착 피처가 모든 되감기 피처 다음 위치에 오도록 목록 순서를 조정한다.
        /// </summary>
        private static bool MoveAfterLastRewindFeature(
            UniversalRendererData rendererData,
            AttachRenderFeature feature)
        {
            int attachIndex = rendererData.rendererFeatures.IndexOf(feature);
            int lastRewindIndex = rendererData.rendererFeatures.FindLastIndex(
                candidate => candidate is RewindRenderFeature);
            int desiredIndex = lastRewindIndex + 1;
            if (attachIndex == desiredIndex)
            {
                return false;
            }

            rendererData.rendererFeatures.RemoveAt(attachIndex);
            if (attachIndex < desiredIndex)
            {
                desiredIndex--;
            }

            rendererData.rendererFeatures.Insert(desiredIndex, feature);
            return true;
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
