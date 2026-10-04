using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Assembles battle arena prefabs from the Vitaria kit export: the models in Assets/Vitaria/Models/Arena
    /// placed by Assets/Vitaria/Layout/arena_*_layout.json (both written by the kit's build_arena.py).
    /// The prefab root carries <see cref="BattleArenaSet"/> (cell tiles, obstacles, effect meshes by role, camera
    /// framing, lighting, the island look with its global volume) and <see cref="BattleArenaAmbience"/> (gusting windmill sails, swaying banner cloths,
    /// flowing water, flames, smoke/ember/mist sockets); battle missions of the same board size without an
    /// environment get it assigned. Rebuilt when the layout changes (its hash is kept in
    /// the prefab's .meta), checked on load, after layout imports and when Play Mode ends.
    /// </summary>
    [InitializeOnLoad]
    public static class ArenaPrefabBuilder
    {
        private const string LayoutFolder = "Assets/Vitaria/Layout";
        private const string ModelsRoot = "Assets/Vitaria/Models";
        private const string ArenaModelsRoot = ModelsRoot + "/Arena";
        private const string MaterialsRoot = "Assets/Vitaria/Materials";
        private const string PrefabFolder = "Assets/Game/Prefabs/Arenas";
        private const string BuilderVersion = "5";       // bump to rebuild arenas after changing this script
        private const string SkyGroup = "Sky";

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
            public float[] sway;        // degrees, Hz, phase 0..1 about the local X axis (crossbar)
        }

        [Serializable]
        private sealed class LayoutFx
        {
            public string[] dust;
            public string[] smoke;
            public string[] debrisStone;
            public string[] debrisEarth;
            public string[] debrisWood;
            public string[] rock;
            public string[] spark;
        }

        [Serializable]
        private sealed class LayoutSocket
        {
            public string kind;
            public float[] p;           // Unity coordinates from the board middle, like the objects
            public float size;
        }

        [Serializable]
        private sealed class LayoutFraming
        {
            public float pitch;
            public float fov;
            public float distance;
            public float focusOffset;
            public float boardWidthShare;
        }

        [Serializable]
        private sealed class LayoutTiles
        {
            public string[] neutral;
            public string player;
            public string enemy;
            public string[] obstacles;
        }

        [Serializable]
        private sealed class LayoutBoard
        {
            public int width;
            public int height;
        }

        [Serializable]
        private sealed class LayoutAmbient
        {
            public float[] color;       // the island look's flat ambient
            public float[] sky;
            public float[] equator;
            public float[] ground;
        }

        [Serializable]
        private sealed class Layout
        {
            public string name;
            public LayoutBoard board;
            public LayoutItem[] objects;
            public LayoutFraming framing;
            public LayoutTiles tiles;
            public IsleSun sun;
            public LayoutAmbient ambient;
            public float[] background;
            public IsleFog fog;         // fog, post and haze: the colony's island look (IslandLook); without fog
            public IslePost post;       // the arena keeps the battle's own light
            public IsleHaze haze;
            public LayoutFx fx;
            public LayoutSocket[] sockets;
        }
#pragma warning restore 0649

        static ArenaPrefabBuilder()
        {
            // The layout may be imported before this script existed, or during Play Mode.
            EditorApplication.delayCall += BuildPending;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += BuildPending;
            };
        }

        [MenuItem("TrollStrategy/Dev/Arena/Build Arena Prefabs")]
        public static void BuildAll()
        {
            foreach (var path in FindLayouts())
                Build(path);
        }

        /// <summary>Builds arenas whose prefab is missing or older than their layout.</summary>
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

        internal static bool IsLayoutPath(string path)
        {
            if (!path.StartsWith(LayoutFolder + "/", StringComparison.Ordinal)) return false;
            var file = Path.GetFileName(path);
            return file.StartsWith("arena_", StringComparison.Ordinal) &&
                   file.EndsWith("_layout.json", StringComparison.Ordinal);
        }

        internal static bool IsModelPath(string path) =>
            path.StartsWith(ModelsRoot + "/", StringComparison.Ordinal) &&
            path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

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
            // a CRLF checkout of the same layout must not look like a new export
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

        public static GameObject Build(string layoutPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Arena] Leave Play Mode, then run TrollStrategy > Arena > Build Arena Prefabs.");
                return null;
            }
            var layout = Load(layoutPath);
            if (layout == null)
            {
                Debug.LogError($"[Arena] Cannot read arena layout {layoutPath}");
                return null;
            }

            var models = IndexModels();
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
                    placed++;
                    // clouds and the far islets, as the colony's sky: their shadows would only cost casters
                    if (string.Equals(item.group, SkyGroup, StringComparison.Ordinal))
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                            renderer.shadowCastingMode = ShadowCastingMode.Off;

                    // sails turn about their hub axis, Blender's -Y = the model's local +Z in Unity; the export
                    // flips handedness, so a positive Blender spin about -Y is a positive Unity turn about -Z
                    if (item.spin != 0f) ambience.AddSpinner(instance.transform, Vector3.back, item.spin, item.gust);
                    if (item.flicker != 0f) ambience.AddFlame(instance.transform, .12f * item.flicker);
                    // cloth pivots sit on the crossbar, so a turn about local X swings the banner
                    if (item.sway != null && item.sway.Length >= 2)
                        ambience.AddSway(instance.transform, item.sway[0], item.sway[1],
                            item.sway.Length >= 3 ? item.sway[2] : 0f);
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

                var framing = layout.framing ?? new LayoutFraming();
                var tiles = layout.tiles ?? new LayoutTiles();
                var set = root.AddComponent<BattleArenaSet>();
                set.Configure(
                    Models(models, tiles.neutral, missing), Model(models, tiles.player, missing),
                    Model(models, tiles.enemy, missing), Models(models, tiles.obstacles, missing),
                    framing.pitch > 0f ? framing.pitch : 50.2f,
                    framing.fov > 0f ? framing.fov : 32.2f,
                    framing.distance > 0f ? framing.distance : 27f,
                    framing.distance > 0f ? framing.focusOffset : -1.6f,
                    framing.boardWidthShare > 0f ? framing.boardWidthShare : .69f,
                    Rgb(layout.background, new Color(.33f, .45f, .6f, 1f)));
                // the battle scene is not the active one: the arena brings the light its art was made under
                var sun = layout.sun ?? new IsleSun();
                var ambient = layout.ambient ?? new LayoutAmbient();
                set.ConfigureLighting(Vector(sun.forward, set.SunDirection), Rgb(sun.color, set.SunColor),
                    sun.intensity > 0f ? sun.intensity : set.SunIntensity,
                    Rgb(ambient.sky, set.AmbientSky), Rgb(ambient.equator, set.AmbientEquator),
                    Rgb(ambient.ground, set.AmbientGround));
                ConfigureLook(root, set, layout);
                var fx = layout.fx ?? new LayoutFx();
                set.ConfigureEffects(Models(models, fx.dust, missing), Models(models, fx.smoke, missing),
                    Models(models, fx.debrisStone, missing), Models(models, fx.debrisEarth, missing),
                    Models(models, fx.debrisWood, missing), Models(models, fx.rock, missing),
                    Models(models, fx.spark, missing));
                int sockets = AddSockets(ambience, layout.sockets);

                EnsureFolder(PrefabFolder);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(layout));
                if (prefab == null)
                {
                    Debug.LogError($"[Arena] Could not save {PrefabPath(layout)}");
                    return null;
                }
                // an incomplete arena is never marked up to date, not even for an older layout, so any later
                // check (a model import included) rebuilds it
                var importer = AssetImporter.GetAtPath(PrefabPath(layout));
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(layoutPath);
                if (importer != null && text != null)
                {
                    importer.userData = missing.Count == 0 ? SourceHash(text.text) : string.Empty;
                    EditorUtility.SetDirty(importer);
                    AssetDatabase.WriteImportSettingsIfDirty(PrefabPath(layout));
                }
                // which level stands in which surroundings is ArenaContentSetup's alone (its Ladder table)
                var summary = $"[Arena] {layout.name}: {placed} objects, {sockets} sockets -> {PrefabPath(layout)}";
                if (missing.Count > 0)
                    Debug.LogWarning($"{summary}; missing models: {string.Join(", ", missing)}", prefab);
                else
                    Debug.Log(summary, prefab);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The colony's island look when the layout has one (fog, post and haze, written by the kit's game_look like
        /// the island's): flat ambient, the sun's shadow strength, fog scaled by the battle camera, and a global
        /// volume on the root with the arena's own profile (Arena_*_Volume.asset). The prefab lives only in the
        /// battle scene, so the look acts only there; ColonyVolume is off while the colony camera is.
        /// A layout without fog leaves the battle lit as before.
        /// </summary>
        private static void ConfigureLook(GameObject root, BattleArenaSet set, Layout layout)
        {
            if (layout.fog == null) return;
            var sun = layout.sun ?? new IsleSun();
            var ambient = layout.ambient ?? new LayoutAmbient();
            var post = layout.post ?? new IslePost();
            var fogColor = IslandLook.Rgb(layout.fog.color, set.Background);
            EnsureFolder(PrefabFolder);
            // BattleBoardView stands the arena root on the board, at y = 0: the haze depths count down from 0
            var profile = IslandLook.EnsureVolumeProfile(PrefabFolder + "/" + layout.name + "_Volume.asset", post,
                layout.haze, 0f, fogColor);
            DioramaSurfaceSetup.ConfigureIslandHaze();      // the haze pass on the renderer both cameras use
            DioramaSurfaceSetup.ConfigureWorldUi();
            // ColonyVolume's layer (Default): the battle camera copies the colony camera's volume mask
            root.layer = 0;
            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;       // over ColonyVolume in the frame before it switches itself off
            volume.weight = 1f;
            volume.sharedProfile = profile;
            set.ConfigureLook(volume, IslandLook.Rgb(ambient.color, set.AmbientSky),
                sun.shadowStrength > 0f ? Mathf.Clamp01(sun.shadowStrength) : .8f, fogColor,
                IslandLook.Positive(layout.fog.startPerDistance, IslandLook.FogStartPerDistance),
                IslandLook.Positive(layout.fog.endPerDistance, IslandLook.FogEndPerDistance),
                IslandLook.Positive(post.dofStartPerDistance, IslandLook.DofStartPerDistance),
                IslandLook.Positive(post.dofEndPerDistance, IslandLook.DofEndPerDistance));
        }

        /// <summary>Emitters of smoke, embers and mist; a kind this builder does not know is reported and skipped.</summary>
        private static int AddSockets(BattleArenaAmbience ambience, LayoutSocket[] sockets)
        {
            if (sockets == null) return 0;
            int added = 0;
            foreach (var socket in sockets)
            {
                if (socket == null || socket.p == null || socket.p.Length < 3) continue;
                if (!Enum.TryParse<BattleArenaAmbience.SocketKind>(socket.kind, true, out var kind))
                {
                    Debug.LogWarning($"[Arena] Unknown socket kind '{socket.kind}' skipped");
                    continue;
                }
                ambience.AddSocket(kind, Vector(socket.p, Vector3.zero), socket.size);
                added++;
            }
            return added;
        }

        private static Dictionary<string, GameObject> IndexModels()
        {
            var models = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(ModelsRoot)) return models;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var key = Path.GetFileNameWithoutExtension(path);
                // arena exports win over same-named kit models
                if (!models.ContainsKey(key) || path.StartsWith(ArenaModelsRoot + "/", StringComparison.Ordinal))
                    models[key] = model;
            }
            return models;
        }

        private static GameObject Model(Dictionary<string, GameObject> models, string name, List<string> missing)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (models.TryGetValue(name, out var model)) return model;
            missing.Add(name);
            return null;
        }

        private static GameObject[] Models(Dictionary<string, GameObject> models, string[] names, List<string> missing)
        {
            var result = new List<GameObject>();
            if (names != null)
                foreach (var name in names)
                {
                    var model = Model(models, name, missing);
                    if (model != null) result.Add(model);
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

    /// <summary>
    /// Rebuilds an arena prefab when its layout is imported with new content, or when a model arrives
    /// that an incomplete arena was waiting for.
    /// </summary>
    internal sealed class ArenaLayoutWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (!ArenaPrefabBuilder.IsLayoutPath(path) && !ArenaPrefabBuilder.IsModelPath(path)) continue;
                // one pending check per import batch; unchanged layouts are skipped by their hash
                EditorApplication.delayCall -= ArenaPrefabBuilder.BuildPending;
                EditorApplication.delayCall += ArenaPrefabBuilder.BuildPending;
                break;
            }
        }
    }
}
