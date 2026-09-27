using System;
using System.Collections.Generic;
using TrollStrategy.Bootstrap;
using TrollStrategy.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Puts the UI Toolkit colony HUD into the colony scene: panel settings with the game theme and a
    /// fallback font, a UIDocument with the HUD layout, and the bootstrap's reference to it. Removes the
    /// retired uGUI HUD canvas when it is still there.
    /// </summary>
    public static class ColonyHudSetup
    {
        public const string PanelSettingsPath = "Assets/Game/UI/Settings/GamePanelSettings.asset";
        public const string TextSettingsPath = "Assets/Game/UI/Settings/GameTextSettings.asset";
        public const string ThemePath = "Assets/Game/UI/Settings/GameTheme.tss";
        public const string ColonyHudPath = "Assets/Game/UI/Uxml/ColonyHud.uxml";
        public const string FallbackFontPath = "Assets/Game/UI/Fonts/LiberationSans Fallback.asset";
        private const string FallbackSourcePath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string HudObjectName = "ColonyHud";
        private const string LegacyCanvasName = "HUDCanvas";

        [MenuItem("TrollStrategy/Setup Colony HUD")]
        public static void SetupOpenScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop play mode before setting up the HUD.");
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            if (boot == null) throw new InvalidOperationException("Open the colony scene first.");
            Install(boot);
            EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            EditorSceneManager.SaveScene(boot.gameObject.scene);
        }

        /// <summary>Installs or refreshes the HUD in the bootstrap's scene; the caller saves the scene.</summary>
        public static ColonyHud Install(GameBootstrap boot)
        {
            if (boot == null) throw new ArgumentNullException(nameof(boot));
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ColonyHudPath);
            if (layout == null) throw new InvalidOperationException($"HUD layout missing: {ColonyHudPath}");

            var legacy = GameObject.Find(LegacyCanvasName);
            if (legacy != null) Undo.DestroyObjectImmediate(legacy);
            EnsureEventSystem();

            var hud = Object.FindAnyObjectByType<ColonyHud>(FindObjectsInactive.Include);
            if (hud == null)
            {
                var hudObject = new GameObject(HudObjectName, typeof(UIDocument), typeof(ColonyHud));
                Undo.RegisterCreatedObjectUndo(hudObject, "Create colony HUD");
                hud = hudObject.GetComponent<ColonyHud>();
            }
            var document = hud.GetComponent<UIDocument>();
            Undo.RecordObject(document, "Configure colony HUD");
            document.panelSettings = EnsurePanelSettings();
            document.visualTreeAsset = layout;
            document.sortingOrder = 0;

            var serialized = new SerializedObject(boot);
            var reference = serialized.FindProperty("_hud")
                ?? throw new InvalidOperationException("GameBootstrap has no _hud field");
            reference.objectReferenceValue = hud;
            serialized.ApplyModifiedProperties();
            return hud;
        }

        /// <summary>1920×1080 reference, scaled by width and height alike, with the game theme.</summary>
        public static PanelSettings EnsurePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (settings.themeStyleSheet == null) Debug.LogWarning($"HUD theme missing: {ThemePath}");
            settings.textSettings = EnsureTextSettings();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = .5f;
            settings.sortingOrder = 0;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            return settings;
        }

        /// <summary>Text settings whose fallback covers glyphs the HUD font lacks, such as arrows.</summary>
        public static PanelTextSettings EnsureTextSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(settings, TextSettingsPath);
            }
            var fallback = EnsureFallbackFont();
            if (fallback != null && (settings.fallbackFontAssets == null || !settings.fallbackFontAssets.Contains(fallback)))
            {
                settings.fallbackFontAssets = new List<FontAsset> { fallback };
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
            return settings;
        }

        private static FontAsset EnsureFallbackFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FontAsset>(FallbackFontPath);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FallbackSourcePath);
            if (font == null)
            {
                Debug.LogWarning($"HUD fallback font source missing: {FallbackSourcePath}");
                return null;
            }
            var asset = FontAsset.CreateFontAsset(font);
            if (asset == null) return null;
            asset.name = "LiberationSans Fallback";
            AssetDatabase.CreateAsset(asset, FallbackFontPath);
            // a dynamic font asset keeps its atlas and material inside itself
            if (asset.material != null) AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var texture in asset.atlasTextures)
                if (texture != null) AssetDatabase.AddObjectToAsset(texture, asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(events, "Create EventSystem");
        }
    }
}
