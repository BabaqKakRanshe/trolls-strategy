using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Makes the scripts of the translations readable: every font in UI/Fonts/Noto (Noto Sans subsets cut to the
    /// glyphs the translations use, by tools/localization/build.py) becomes a dynamic font asset in the UI text
    /// settings' fallback list, after the Latin and Cyrillic fallback. Idempotent.
    /// </summary>
    public static class LocalizationSetup
    {
        private const string FontFolder = "Assets/Game/UI/Fonts/Noto";
        private const string LanguageFolder = "Assets/Game/UI/Localization";
        private const string TablePath = LanguageFolder + "/Languages.asset";
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";

        // every translation file in UI/Localization into the table, and the table into the colony scene's bootstrap
        private static void WriteTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<TrollStrategy.Presentation.LanguageTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<TrollStrategy.Presentation.LanguageTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }
            var entries = Directory.GetFiles(LanguageFolder, "*.json").OrderBy(p => p)
                .Select(p => new TrollStrategy.Presentation.LanguageTable.Entry
                {
                    Code = Path.GetFileNameWithoutExtension(p),
                    File = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(p.Replace("\\", "/"))
                }).Where(e => e.File != null).ToList();
            table.Set(entries);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<TrollStrategy.Bootstrap.GameBootstrap>(FindObjectsInactive.Include);
            if (bootstrap != null)
            {
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("_languages").objectReferenceValue = table;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[LocalizationSetup] {entries.Count()} languages in {TablePath}" + (bootstrap != null ? ", wired to the bootstrap" : ", no bootstrap in the scene"));
        }

        [MenuItem("TrollStrategy/Dev/Setup Localization Fonts")]
        public static void Apply()
        {
            var settings = UiSetup.EnsureTextSettings();
            var fallbacks = new List<FontAsset>(settings.fallbackFontAssets ?? new List<FontAsset>());
            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                Debug.LogWarning("[LocalizationSetup] No " + FontFolder);
                return;
            }
            foreach (string path in Directory.GetFiles(FontFolder).Where(p => p.EndsWith(".otf") || p.EndsWith(".ttf")).OrderBy(p => p))
            {
                string fontPath = path.Replace('\\', '/');
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font == null) continue;
                string assetPath = Path.ChangeExtension(fontPath, null) + " Fallback.asset";
                var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath);
                if (asset == null)
                {
                    asset = FontAsset.CreateFontAsset(font);
                    if (asset == null) continue;
                    asset.name = Path.GetFileNameWithoutExtension(fontPath) + " Fallback";
                    AssetDatabase.CreateAsset(asset, assetPath);
                    if (asset.material != null) AssetDatabase.AddObjectToAsset(asset.material, asset);
                    foreach (var texture in asset.atlasTextures)
                        if (texture != null) AssetDatabase.AddObjectToAsset(texture, asset);
                }
                if (!fallbacks.Contains(asset)) fallbacks.Add(asset);
            }
            settings.fallbackFontAssets = fallbacks;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            WriteTable();
            Debug.Log($"[LocalizationSetup] {fallbacks.Count} fallback fonts: {string.Join(", ", fallbacks.Select(f => f.name))}");
        }
    }
}
