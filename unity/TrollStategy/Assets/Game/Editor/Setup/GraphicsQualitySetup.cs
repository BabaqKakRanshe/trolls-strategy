using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Gives the quality levels their own URP assets, so the menu's graphics choice changes what is drawn: URP
    /// ignores the old QualitySettings fields (pixel lights, shadows, anti-aliasing) and reads only its asset.
    /// The top levels keep UniversalRP.asset as the island setup writes it; the lower ones are copies of it with
    /// cheaper anti-aliasing and shadows, rewritten from it on every run so a change to the look reaches all levels.
    /// The picture stays at full resolution everywhere: text over the world is drawn by the 3D camera.
    /// </summary>
    public static class GraphicsQualitySetup
    {
        public const string HighPath = "Assets/Settings/UniversalRP.asset";
        public const string MediumPath = "Assets/Settings/UniversalRP_Medium.asset";
        public const string LowPath = "Assets/Settings/UniversalRP_Low.asset";
        private const string QualityPath = "ProjectSettings/QualitySettings.asset";

        // the six levels in QualitySettings order (Very Low .. Ultra); the menu offers 1, 3 and 5
        private static readonly string[] LevelAssets = { LowPath, LowPath, MediumPath, MediumPath, HighPath, HighPath };

        [MenuItem("TrollStrategy/Dev/Setup Graphics Quality")]
        public static void Apply()
        {
            var high = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(HighPath)
                ?? throw new InvalidOperationException("Missing " + HighPath);
            var medium = Derive(high, MediumPath, msaa: 2, shadowmap: 2048, softShadows: true, softQuality: 1,
                cascades: 2, shadowDistance: high.shadowDistance);
            var low = Derive(high, LowPath, msaa: 1, shadowmap: 1024, softShadows: false, softQuality: 1,
                cascades: 1, shadowDistance: Mathf.Min(70f, high.shadowDistance));

            var quality = AssetDatabase.LoadAllAssetsAtPath(QualityPath);
            if (quality == null || quality.Length == 0) throw new InvalidOperationException("Missing " + QualityPath);
            var serialized = new SerializedObject(quality[0]);
            var levels = serialized.FindProperty("m_QualitySettings");
            if (levels.arraySize != LevelAssets.Length)
                throw new InvalidOperationException($"Expected {LevelAssets.Length} quality levels, found {levels.arraySize}");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var asset = LevelAssets[i] == LowPath ? low : LevelAssets[i] == MediumPath ? medium : high;
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = asset;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[GraphicsQualitySetup] Quality levels: low, low, medium, medium, high, high.");
        }

        private static UniversalRenderPipelineAsset Derive(UniversalRenderPipelineAsset source, string path, int msaa,
            int shadowmap, bool softShadows, int softQuality, int cascades, float shadowDistance)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                if (!AssetDatabase.CopyAsset(HighPath, path)) throw new InvalidOperationException("Could not copy to " + path);
                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            }
            else EditorUtility.CopySerialized(source, asset);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_MSAA").intValue = msaa;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue = shadowmap;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = softShadows;
            serialized.FindProperty("m_SoftShadowQuality").intValue = softQuality;
            serialized.FindProperty("m_ShadowCascadeCount").intValue = cascades;
            serialized.FindProperty("m_ShadowDistance").floatValue = shadowDistance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
