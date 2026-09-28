using System;
using System.Collections.Generic;
using TrollStrategy.Bootstrap;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.WorldUi;
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
    /// Builds the screen UI prefab and puts it into the colony scene. The prefab is a tree of nested
    /// UIDocuments, one GameObject per screen, band and panel, so the Hierarchy shows the UI's structure;
    /// each panel's layout is its own UXML. Also owns the panel settings, theme and fallback font, the
    /// world-space settings for labels over things in the world, and those labels in the building and
    /// unit prefabs.
    /// </summary>
    public static class UiSetup
    {
        public const string PrefabPath = "Assets/Game/UI/Prefabs/UI.prefab";
        public const string PanelSettingsPath = "Assets/Game/UI/Settings/GamePanelSettings.asset";
        public const string TextSettingsPath = "Assets/Game/UI/Settings/GameTextSettings.asset";
        public const string ThemePath = "Assets/Game/UI/Settings/GameTheme.tss";
        public const string FallbackFontPath = "Assets/Game/UI/Fonts/LiberationSans Fallback.asset";
        public const string WorldPanelSettingsPath = "Assets/Game/UI/Settings/WorldPanelSettings.asset";
        public const string WorldThemePath = "Assets/Game/UI/Settings/WorldTheme.tss";
        private const string BuildingBasePath = "Assets/Game/Prefabs/BuildingBase.prefab";
        private const string UnitBasePath = "Assets/Game/Prefabs/UnitBase.prefab";
        private const string FallbackSourcePath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string UxmlFolder = "Assets/Game/UI/Uxml/";

        /// <summary>One GameObject with a UIDocument; children nest into its root in sorting order.</summary>
        private sealed class Node
        {
            public string Name;
            public string Uxml;
            public int Order;
            public bool Absolute;
            public string[] Classes = Array.Empty<string>();
            public string Field;
            public Node[] Children = Array.Empty<Node>();
        }

        private static Node Part(string folder, string name, int order, string field, params string[] classes) => new()
        {
            Name = name, Uxml = $"{folder}/{name}.uxml", Order = order, Field = field, Classes = classes
        };

        private static Node Layer(string folder, string name, int order, string field) => new()
        {
            Name = name, Uxml = $"{folder}/{name}.uxml", Order = order, Field = field, Absolute = true,
            Classes = new[] { "layer" }
        };

        private static Node Box(string name, int order, string[] classes, params Node[] children) => new()
        {
            Name = name, Order = order, Classes = classes, Children = children
        };

        // Field names are the ColonyHud fields that hold each part's document.
        private static Node ColonyHudTree() => new()
        {
            Name = "ColonyHud",
            Uxml = "ColonyHud.uxml",
            Absolute = true,
            Classes = new[] { "layer" },
            Children = new[]
            {
                Box("Frame", 0, new[] { "hud-root" },
                    Part("Colony", "TopBar", 0, "_topBar", "hud-band"),
                    Box("Middle", 1, new[] { "middle" },
                        Box("LeftColumn", 0, new[] { "left-column" },
                            Part("Colony", "Quest", 0, "_quest"),
                            Part("Colony", "Inspect", 1, "_inspect")),
                        Box("RightColumn", 1, new[] { "right-column" },
                            Part("Colony", "Showcase", 0, "_showcase"),
                            Part("Colony", "Catalog", 1, "_catalog"))),
                    Box("Bottom", 2, new[] { "bottom", "hud-band" },
                        Part("Colony", "Status", 0, "_status"),
                        new Node
                        {
                            Name = "ContextBar", Uxml = "Colony/ContextBar.uxml", Order = 1, Field = "_contextBar",
                            Absolute = true, Classes = new[] { "layer" }
                        })),
                Layer("Colony", "CommandFan", 10, "_commandFan"),
                Layer("Colony", "HaulCargo", 20, "_haulCargo"),
                Layer("Colony", "Reward", 30, "_reward"),
                Layer("Colony", "BattleReward", 31, "_battleReward"),
                Layer("Colony", "Cheat", 40, "_cheat"),
                new Node { Name = "Tooltip", Order = 50, Field = "_tooltip", Absolute = true, Classes = new[] { "layer" } }
            }
        };

        // Field names are the BattleHud fields that hold each part's document. It sorts above the colony
        // HUD and stays hidden (.battle-screen) until a battle opens it.
        private static Node BattleHudTree() => new()
        {
            Name = "BattleHud",
            Uxml = "BattleHud.uxml",
            Order = 10,
            Absolute = true,
            Classes = new[] { "layer", "battle-screen" },
            Children = new[]
            {
                Box("Frame", 0, new[] { "battle-frame" },
                    Part("Battle", "Header", 0, "_header", "hud-band"),
                    // deployment panels, then the replay bar, in the same place at the bottom
                    Box("Bottom", 1, new[] { "hud-band" },
                        new Node
                        {
                            Name = "Deployment", Order = 0, Field = "_deploymentBand", Classes = new[] { "battle-deployment" },
                            Children = new[]
                            {
                                Part("Battle", "Roster", 0, "_roster"),
                                Part("Battle", "Selected", 1, "_selected", "battle-grow"),
                                Part("Battle", "Actions", 2, "_actions")
                            }
                        },
                        Part("Battle", "Replay", 1, "_replay"))),
                Layer("Battle", "Banner", 10, "_banner")
            }
        };

        [MenuItem("TrollStrategy/Setup UI")]
        public static void SetupOpenScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop play mode before setting up the UI.");
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            if (boot == null) throw new InvalidOperationException("Open the colony scene first.");
            Install(boot);
            EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            EditorSceneManager.SaveScene(boot.gameObject.scene);
        }

        /// <summary>Rebuilds the UI prefab and installs it in the bootstrap's scene; the caller saves the scene.</summary>
        public static ColonyHud Install(GameBootstrap boot)
        {
            if (boot == null) throw new ArgumentNullException(nameof(boot));
            var prefab = BuildPrefab();
            var worldPanel = EnsureWorldPanelSettings();
            InstallWorldLabels(worldPanel);

            EnsureEventSystem();

            GameObject instance = null;
            foreach (var root in boot.gameObject.scene.GetRootGameObjects())
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab) instance = root;
            if (instance == null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boot.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create UI");
            }
            var hud = instance.GetComponentInChildren<ColonyHud>(true)
                ?? throw new InvalidOperationException("The UI prefab has no ColonyHud");
            var battleHud = instance.GetComponentInChildren<BattleHud>(true)
                ?? throw new InvalidOperationException("The UI prefab has no BattleHud");

            var serialized = new SerializedObject(boot);
            (serialized.FindProperty("_hud") ?? throw new InvalidOperationException("GameBootstrap has no _hud field"))
                .objectReferenceValue = hud;
            (serialized.FindProperty("_battleHud") ?? throw new InvalidOperationException("GameBootstrap has no _battleHud field"))
                .objectReferenceValue = battleHud;
            (serialized.FindProperty("_worldPanel") ?? throw new InvalidOperationException("GameBootstrap has no _worldPanel field"))
                .objectReferenceValue = worldPanel;
            serialized.ApplyModifiedProperties();
            return hud;
        }

        /// <summary>Writes the UI prefab from the tree above; the GUID and scene instances survive a rebuild.</summary>
        public static GameObject BuildPrefab()
        {
            var settings = EnsurePanelSettings();
            var root = new GameObject("UI");
            try
            {
                var screen = root.AddComponent<UIDocument>();
                screen.panelSettings = settings;
                screen.sortingOrder = 0;
                root.AddComponent<UiDocumentClasses>().SetClasses("ui-screen");

                AddScreen<ColonyHud>(ColonyHudTree(), root.transform);
                AddScreen<BattleHud>(BattleHudTree(), root.transform);

                var folder = System.IO.Path.GetDirectoryName(PrefabPath)?.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Game/UI", "Prefabs");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException($"Could not save {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Adds a screen's document tree and its component, with each part document in its field.</summary>
        private static void AddScreen<T>(Node tree, Transform parent) where T : MonoBehaviour
        {
            var fields = new Dictionary<string, UIDocument>();
            var screen = Add(tree, parent, fields).AddComponent<T>();
            var serialized = new SerializedObject(screen);
            foreach (var pair in fields)
            {
                var property = serialized.FindProperty(pair.Key)
                    ?? throw new InvalidOperationException($"{typeof(T).Name} has no field {pair.Key}");
                property.objectReferenceValue = pair.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // The document is added after the GameObject has its parent: that is when it finds its parent document.
        private static GameObject Add(Node node, Transform parent, Dictionary<string, UIDocument> fields)
        {
            var go = new GameObject(node.Name);
            go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            if (document.parentUI == null)
                throw new InvalidOperationException($"{node.Name} did not nest into its parent document");
            document.sortingOrder = node.Order;
            document.position = node.Absolute ? Position.Absolute : Position.Relative;
            if (node.Uxml != null)
            {
                var path = UxmlFolder + node.Uxml;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path)
                    ?? throw new InvalidOperationException($"UI layout missing: {path}");
            }
            if (node.Classes.Length > 0) go.AddComponent<UiDocumentClasses>().SetClasses(node.Classes);
            if (node.Field != null) fields.Add(node.Field, document);
            foreach (var child in node.Children) Add(child, go.transform, fields);
            return go;
        }

        /// <summary>
        /// World-space settings for WorldPanel labels: 100 panel pixels per world unit and the world theme.
        /// They take no input, so no collider is made for them; one would catch the board's raycasts.
        /// </summary>
        public static PanelSettings EnsureWorldPanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldPanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, WorldPanelSettingsPath);
            }
            settings.renderMode = PanelRenderMode.WorldSpace;
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(WorldThemePath);
            if (settings.themeStyleSheet == null) Debug.LogWarning($"World UI theme missing: {WorldThemePath}");
            settings.textSettings = EnsureTextSettings();
            var serialized = new SerializedObject(settings);
            var colliders = serialized.FindProperty("m_ColliderUpdateMode")
                ?? throw new InvalidOperationException("PanelSettings has no collider mode");
            colliders.enumValueIndex = Array.IndexOf(colliders.enumNames, "Keep");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            return settings;
        }

        /// <summary>
        /// The building name and the hauler's cargo count are world panels in the base prefabs, at the
        /// place of the text they replace; every variant and scene instance inherits them.
        /// </summary>
        public static void InstallWorldLabels(PanelSettings settings)
        {
            ReplaceWithWorldPanel<BuildingView>(BuildingBasePath, "_label", "Label", settings);
            ReplaceWithWorldPanel<UnitView>(UnitBasePath, "_cargoLabel", "CargoLabel", settings);
        }

        private static void ReplaceWithWorldPanel<T>(string prefabPath, string field, string childName,
            PanelSettings settings) where T : Component
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var owner = contents.GetComponentInChildren<T>(true)
                    ?? throw new InvalidOperationException($"{prefabPath} has no {typeof(T).Name}");
                var serialized = new SerializedObject(owner);
                var property = serialized.FindProperty(field)
                    ?? throw new InvalidOperationException($"{typeof(T).Name} has no field {field}");
                if (property.objectReferenceValue is WorldPanel panel)
                {
                    panel.GetComponent<UIDocument>().panelSettings = settings;
                }
                else
                {
                    // the field, retyped to WorldPanel, no longer holds the old text: find it by name
                    Transform old = null;
                    foreach (var child in contents.GetComponentsInChildren<Transform>(true))
                        if (child.name == childName && child.GetComponent<WorldPanel>() == null) old = child;
                    if (old == null) throw new InvalidOperationException($"{prefabPath} has no {childName} to replace");
                    var go = new GameObject(childName);
                    go.transform.SetParent(old.parent, false);
                    go.transform.SetSiblingIndex(old.GetSiblingIndex());
                    go.transform.SetLocalPositionAndRotation(old.localPosition, old.localRotation);
                    go.SetActive(old.gameObject.activeSelf);
                    Object.DestroyImmediate(old.gameObject);
                    var document = go.AddComponent<UIDocument>();
                    document.panelSettings = settings;
                    document.worldSpaceSizeMode = WorldSpaceSizeMode.Dynamic;
                    property.objectReferenceValue = go.AddComponent<WorldPanel>();
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
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
            if (settings.themeStyleSheet == null) Debug.LogWarning($"UI theme missing: {ThemePath}");
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

        /// <summary>Text settings whose fallback covers glyphs the UI font lacks, such as arrows.</summary>
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
                Debug.LogWarning($"UI fallback font source missing: {FallbackSourcePath}");
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
