using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Colony environment from the Vitaria kit export (Strategy_Kit/Blender/build_colony.py): the models in
    /// Assets/Vitaria/Models placed by Assets/Vitaria/Layout/colony_*_layout.json.
    /// <list type="bullet">
    /// <item>Builds Assets/Game/Prefabs/Environments/{layout name}.prefab (the layout hash is kept in the prefab's
    /// .meta, like the arena prefabs) with <see cref="BattleArenaAmbience"/> on the root: mill wheel, flowing water,
    /// smoke and mist sockets.</item>
    /// <item>Installs it into MainColonyScene: a world-space root "ColonyEnvironment" at the middle of the plot
    /// (layout origin = MapToWorld(GridWidth / 2, GridHeight / 2)), no rotation; removes the old procedural ground
    /// (PrimitiveEnvironment, ProceduralWorld, EnvironmentWorld); lights the colony with the layout's sun, ambient
    /// and background — the same profile as the arena.</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    public static class ColonyEnvironmentBuilder
    {
        private const string LayoutFolder = "Assets/Vitaria/Layout";
        private const string ModelsRoot = "Assets/Vitaria/Models";
        private const string MaterialsRoot = "Assets/Vitaria/Materials";
        private const string PrefabFolder = "Assets/Game/Prefabs/Environments";
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        public const string SceneRootName = "ColonyEnvironment";
        private const string BuilderVersion = "2";       // bump to rebuild colony prefabs after changing this script
        // wind from the west: trees and bushes lean east-west, about the world north-south axis (Unity +Z).
        // BattleArenaAmbience sways about the target's local X, so the wind pivot turns local X onto world +Z.
        private static readonly Quaternion WindAxis = Quaternion.Euler(0f, -90f, 0f);

#pragma warning disable 0649   // filled by JsonUtility
        [Serializable]
        private sealed class LayoutItem
        {
            public string name;
            public string asset;
            public string group;
            public float[] p;
            public float[] r;
            public float[] s;
            public float spin;
            public float gust;
            public float scroll;
            public float flicker;
            public float[] sway;
        }

        [Serializable]
        private sealed class LayoutPlot
        {
            public int width;
            public int height;
            public float cellSize;
        }

        [Serializable]
        private sealed class LayoutSun
        {
            public float[] forward;
            public float[] color;
            public float intensity;
        }

        [Serializable]
        private sealed class LayoutAmbient
        {
            public float[] sky;
            public float[] equator;
            public float[] ground;
        }

        [Serializable]
        private sealed class LayoutFx
        {
            public string[] smoke;
            public string[] spark;
        }

        [Serializable]
        private sealed class LayoutSocket
        {
            public string kind;
            public float[] p;
            public float size;
        }

        [Serializable]
        private sealed class Layout
        {
            public string name;
            public LayoutPlot plot;
            public LayoutItem[] objects;
            public LayoutSun sun;
            public LayoutAmbient ambient;
            public float[] background;
            public LayoutFx fx;
            public LayoutSocket[] sockets;
        }
#pragma warning restore 0649

        static ColonyEnvironmentBuilder()
        {
            // the layout may be imported before this script existed, or during Play Mode
            EditorApplication.delayCall += BuildPending;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += BuildPending;
            };
        }

        // ------------------------------------------------------------------------------------------ menus

        /// <summary>Prefab from the layout, installed into MainColonyScene with the layout's light; saves the scene.</summary>
        [MenuItem("TrollStrategy/Dev/Colony/Rebuild Colony Map")]
        public static void RebuildColonyMap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding the colony map");
            var active = SceneManager.GetActiveScene();
            if (active.isDirty)
                throw new InvalidOperationException("Save the current scene before rebuilding the colony map");
            var layoutPath = FindLayout() ?? throw new InvalidOperationException(
                $"No colony layout in {LayoutFolder} (colony_*_layout.json)");
            if (Build(layoutPath) == null)
                throw new InvalidOperationException($"Colony prefab was not built from {layoutPath}");
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            InstallIntoOpenScene();
            // shadows out to the far ridge and MSAA only; the camera keeps its framing
            ThreeDSceneSetup.ConfigureRendering(Camera.main);
            DioramaSurfaceSetup.ConfigureContactShadows();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Colony] {ScenePath}: environment installed, light from {Path.GetFileName(layoutPath)}");
        }

        [MenuItem("TrollStrategy/Dev/Colony/Build Environment Prefab")]
        public static void BuildPrefabMenu()
        {
            var layoutPath = FindLayout();
            if (layoutPath == null) Debug.LogError($"[Colony] No colony layout in {LayoutFolder}");
            else Build(layoutPath);
        }

        // ------------------------------------------------------------------------------------------ scene

        /// <summary>
        /// Puts the colony environment into the open colony scene: removes the old procedural ground and a previous
        /// install, adds the prefab at the middle of the plot, applies the layout's light. Does not save.
        /// </summary>
        public static void InstallIntoOpenScene()
        {
            var layoutPath = FindLayout() ?? throw new InvalidOperationException($"No colony layout in {LayoutFolder}");
            var layout = Load(layoutPath) ?? throw new InvalidOperationException($"Cannot read {layoutPath}");
            // explicit == null checks: Unity objects can be "fake null", which ?? does not see
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(layout));
            if (prefab == null) prefab = Build(layoutPath);
            if (prefab == null) throw new InvalidOperationException($"Colony prefab {PrefabPath(layout)} is missing");
            var grid = Object.FindAnyObjectByType<Grid>();
            if (grid == null) throw new InvalidOperationException("Colony Grid is missing");
            var worldView = grid.GetComponent<TilemapWorldView>();
            if (worldView == null) throw new InvalidOperationException("TilemapWorldView is missing on the Grid");
            if (layout.plot != null && layout.plot.width > 0 &&
                (layout.plot.width != worldView.GridWidth || layout.plot.height != worldView.GridHeight))
                Debug.LogWarning($"[Colony] Layout plot {layout.plot.width}x{layout.plot.height} differs from the grid " +
                                 $"{worldView.GridWidth}x{worldView.GridHeight}: re-export the kit layout");

            // old procedural ground: the component (or its missing script once the file is deleted) and its worlds
            foreach (var behaviour in grid.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name == "PrimitiveEnvironment")
                    Object.DestroyImmediate(behaviour);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(grid.gameObject);
            foreach (var oldName in new[] { "ProceduralWorld", "EnvironmentWorld" })
            {
                var old = grid.transform.Find(oldName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            foreach (var root in grid.gameObject.scene.GetRootGameObjects())
                if (root.name == SceneRootName) Object.DestroyImmediate(root);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grid.gameObject.scene);
            instance.name = SceneRootName;
            var middle = worldView.MapToWorld(new Vector3(worldView.GridWidth * worldView.CellSize * .5f,
                worldView.GridHeight * worldView.CellSize * .5f, 0f));
            instance.transform.SetPositionAndRotation(middle, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            ApplyLighting(layout, Camera.main);
        }

        /// <summary>Colony light from the layout (the arena profile): sun, trilight ambient, camera background.</summary>
        public static void ApplyLighting(Camera camera)
        {
            var layoutPath = FindLayout();
            var layout = layoutPath != null ? Load(layoutPath) : null;
            if (layout == null)
            {
                Debug.LogWarning($"[Colony] No colony layout in {LayoutFolder}: light left as is");
                return;
            }
            ApplyLighting(layout, camera);
        }

        private static void ApplyLighting(Layout layout, Camera camera)
        {
            var sunObject = GameObject.Find("ColonySun");
            if (sunObject == null) sunObject = new GameObject("ColonySun");
            var sun = sunObject.GetComponent<Light>();
            if (sun == null) sun = sunObject.AddComponent<Light>();
            var sunData = layout.sun ?? new LayoutSun();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(Vector(sunData.forward, new Vector3(-.369f, -.766f, .527f)));
            sun.color = Rgb(sunData.color, new Color(1f, .95f, .86f));
            sun.intensity = sunData.intensity > 0f ? sunData.intensity : 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;          // as BattleArenaLighting
            RenderSettings.sun = sun;

            var ambient = layout.ambient ?? new LayoutAmbient();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Rgb(ambient.sky, new Color(.64f, .72f, .82f));
            RenderSettings.ambientEquatorColor = Rgb(ambient.equator, new Color(.52f, .58f, .5f));
            RenderSettings.ambientGroundColor = Rgb(ambient.ground, new Color(.28f, .26f, .24f));
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Rgb(layout.background, new Color(.33f, .45f, .6f));
                EditorUtility.SetDirty(camera);
            }
            EditorUtility.SetDirty(sun);
        }

        // ------------------------------------------------------------------------------------------ prefab

        /// <summary>Builds colony prefabs whose prefab is missing or older than their layout (does not touch scenes).</summary>
        internal static void BuildPending()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            foreach (var path in FindLayouts())
            {
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var layout = Load(path);
                if (text == null || layout == null) continue;
                var importer = AssetImporter.GetAtPath(PrefabPath(layout));
                if (importer == null || importer.userData != SourceHash(text.text)) Build(path);
            }
        }

        public static GameObject Build(string layoutPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Colony] Leave Play Mode, then run TrollStrategy > Colony > Build Environment Prefab.");
                return null;
            }
            var layout = Load(layoutPath);
            if (layout == null)
            {
                Debug.LogError($"[Colony] Cannot read colony layout {layoutPath}");
                return null;
            }

            var duplicates = new List<string>();
            var models = IndexModels(duplicates);
            var root = new GameObject(layout.name);
            try
            {
                var groups = new Dictionary<string, Transform>(StringComparer.Ordinal);
                var ambience = root.AddComponent<BattleArenaAmbience>();
                var water = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Vitaria_Water.mat");
                var waterfall = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Vitaria_Waterfall.mat");
                int placed = 0;
                var missing = new List<string>();
                var names = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var item in layout.objects)
                {
                    if (item == null || string.IsNullOrEmpty(item.asset)) continue;
                    if (!models.TryGetValue(item.asset, out var model))
                    {
                        missing.Add(item.asset);
                        continue;
                    }
                    var instance = PrefabUtility.InstantiatePrefab(model, Group(root.transform, groups, item.group)) as GameObject;
                    if (instance == null)
                    {
                        missing.Add(item.asset);
                        continue;
                    }
                    instance.name = UniqueName(CleanName(item.name, item.asset), names);
                    instance.transform.localPosition = Vector(item.p, Vector3.zero);
                    instance.transform.localRotation = Rotation(item.r);
                    instance.transform.localScale = Vector(item.s, Vector3.one);
                    foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(collider);
                    placed++;

                    // wheels and sails turn about their hub: Blender's -Y is the model's local +Z (see ArenaPrefabBuilder)
                    if (item.spin != 0f) ambience.AddSpinner(instance.transform, Vector3.back, item.spin, item.gust);
                    if (item.flicker != 0f) ambience.AddFlame(instance.transform, .12f * item.flicker);
                    if (item.sway != null && item.sway.Length >= 2)
                    {
                        var target = instance.transform;
                        if (string.Equals(item.group, "Nature", StringComparison.Ordinal))
                        {
                            // one wind for every tree: a pivot at the trunk base, the model keeps its own yaw under it
                            var pivot = new GameObject(instance.name + "_Wind").transform;
                            pivot.SetParent(instance.transform.parent, false);
                            pivot.localPosition = instance.transform.localPosition;
                            pivot.localRotation = WindAxis;
                            instance.transform.SetParent(pivot, true);
                            target = pivot;
                        }
                        ambience.AddSway(target, item.sway[0], item.sway[1], item.sway.Length >= 3 ? item.sway[2] : 0f);
                    }
                    if (item.scroll != 0f)
                    {
                        var renderer = instance.GetComponentInChildren<Renderer>();
                        if (renderer == null) continue;
                        // the import remap may have run before the material existed: set it explicitly
                        var material = item.asset.EndsWith("Falls", StringComparison.Ordinal) ? waterfall : water;
                        if (material != null) renderer.sharedMaterial = material;
                        ambience.AddFlow(renderer, item.scroll);
                    }
                }

                var fx = layout.fx ?? new LayoutFx();
                ambience.ConfigureEffects(Models(models, fx.smoke, missing), Models(models, fx.spark, missing));
                int sockets = AddSockets(ambience, layout.sockets);

                EnsureFolder(PrefabFolder);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(layout));
                if (prefab == null)
                {
                    Debug.LogError($"[Colony] Could not save {PrefabPath(layout)}");
                    return null;
                }
                // an incomplete environment is never marked up to date, so a later model import rebuilds it
                bool complete = missing.Count == 0 && duplicates.Count == 0;
                var importer = AssetImporter.GetAtPath(PrefabPath(layout));
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(layoutPath);
                if (importer != null && text != null)
                {
                    importer.userData = complete ? SourceHash(text.text) : string.Empty;
                    EditorUtility.SetDirty(importer);
                    AssetDatabase.WriteImportSettingsIfDirty(PrefabPath(layout));
                }
                var summary = $"[Colony] {layout.name}: {placed} objects, {sockets} sockets -> {PrefabPath(layout)}";
                if (duplicates.Count > 0)
                    Debug.LogError($"{summary}; model names found twice in {ModelsRoot} (delete the stale file): " +
                                   string.Join("; ", duplicates), prefab);
                if (missing.Count > 0)
                    Debug.LogWarning($"{summary}; missing models: {string.Join(", ", missing)}", prefab);
                else if (duplicates.Count == 0)
                    Debug.Log(summary, prefab);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------------------------------ helpers

        internal static bool IsLayoutPath(string path)
        {
            if (!path.StartsWith(LayoutFolder + "/", StringComparison.Ordinal)) return false;
            var file = Path.GetFileName(path);
            return file.StartsWith("colony_", StringComparison.Ordinal) &&
                   file.EndsWith("_layout.json", StringComparison.Ordinal);
        }

        internal static bool IsModelPath(string path) =>
            path.StartsWith(ModelsRoot + "/", StringComparison.Ordinal) &&
            path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        private static string FindLayout()
        {
            string first = null;
            foreach (var path in FindLayouts())
                if (first == null || string.CompareOrdinal(path, first) < 0) first = path;
            return first;
        }

        private static IEnumerable<string> FindLayouts()
        {
            if (!AssetDatabase.IsValidFolder(LayoutFolder)) yield break;
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { LayoutFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsLayoutPath(path)) yield return path;
            }
        }

        private static string PrefabPath(Layout layout) => PrefabFolder + "/" + layout.name + ".prefab";

        private static string SourceHash(string json)
        {
            json = json.Replace("\r\n", "\n");
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(BuilderVersion + "|" + json)))
                .Replace("-", "");
        }

        private static Layout Load(string layoutPath)
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(layoutPath);
            if (text == null) return null;
            var layout = JsonUtility.FromJson<Layout>(text.text);
            return layout != null && !string.IsNullOrEmpty(layout.name) && layout.objects != null ? layout : null;
        }

        private static int AddSockets(BattleArenaAmbience ambience, LayoutSocket[] sockets)
        {
            if (sockets == null) return 0;
            int added = 0;
            foreach (var socket in sockets)
            {
                if (socket == null || socket.p == null || socket.p.Length < 3) continue;
                if (!Enum.TryParse<BattleArenaAmbience.SocketKind>(socket.kind, true, out var kind))
                {
                    Debug.LogWarning($"[Colony] Unknown socket kind '{socket.kind}' skipped");
                    continue;
                }
                ambience.AddSocket(kind, Vector(socket.p, Vector3.zero), socket.size);
                added++;
            }
            return added;
        }

        /// <summary>Kit models by file name. Names are unique across Assets/Vitaria/Models (the kit checks it on
        /// export); a name found twice is reported in <paramref name="duplicates"/> and not guessed.</summary>
        private static Dictionary<string, GameObject> IndexModels(List<string> duplicates = null)
        {
            var models = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(ModelsRoot)) return models;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var key = Path.GetFileNameWithoutExtension(path);
                if (paths.TryGetValue(key, out var other) && other != path)
                {
                    duplicates?.Add($"{key} ({other}, {path})");
                    continue;
                }
                paths[key] = path;
                models[key] = model;
            }
            return models;
        }

        private static GameObject[] Models(Dictionary<string, GameObject> models, string[] names, List<string> missing)
        {
            var result = new List<GameObject>();
            if (names != null)
                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (models.TryGetValue(name, out var model)) result.Add(model);
                    else missing.Add(name);
                }
            return result.ToArray();
        }

        private static Transform Group(Transform root, Dictionary<string, Transform> groups, string name)
        {
            if (string.IsNullOrEmpty(name)) return root;
            if (groups.TryGetValue(name, out var group)) return group;
            group = new GameObject(name).transform;
            group.SetParent(root, false);
            groups[name] = group;
            return group;
        }

        private static string CleanName(string name, string asset)
        {
            if (string.IsNullOrEmpty(name)) return asset;
            int dot = name.LastIndexOf('.');
            return dot > 0 && int.TryParse(name.Substring(dot + 1), out _) ? name.Substring(0, dot) : name;
        }

        private static string UniqueName(string name, Dictionary<string, int> used)
        {
            used.TryGetValue(name, out int count);
            used[name] = count + 1;
            return count == 0 ? name : $"{name} ({count})";
        }

        private static Vector3 Vector(float[] v, Vector3 fallback) =>
            v != null && v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : fallback;

        private static Quaternion Rotation(float[] q) =>
            q != null && q.Length >= 4 ? new Quaternion(q[0], q[1], q[2], q[3]) : Quaternion.identity;

        private static Color Rgb(float[] c, Color fallback) =>
            c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2], 1f) : fallback;

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    /// <summary>Rebuilds the colony prefab when its layout, or a model it was waiting for, is imported.</summary>
    internal sealed class ColonyLayoutWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (!ColonyEnvironmentBuilder.IsLayoutPath(path) && !ColonyEnvironmentBuilder.IsModelPath(path)) continue;
                EditorApplication.delayCall -= ColonyEnvironmentBuilder.BuildPending;
                EditorApplication.delayCall += ColonyEnvironmentBuilder.BuildPending;
                break;
            }
        }
    }
}
