using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// The colony island that grows by 5x5 blocks, from the Vitaria kit export (Strategy_Kit/Blender/build_isle.py
    /// --export): the models in Assets/Vitaria/Models/Isle placed by Assets/Vitaria/Layout/isle_layout.json.
    /// <list type="bullet">
    /// <item>Builds Assets/Game/Prefabs/Environments/Colony_Isle.prefab (the layout hash is kept in the prefab's
    /// .meta): <see cref="IslandView"/> on the root and an <see cref="IslandBlockView"/> per block — B_x_y/Land (lawn,
    /// walls, edge ledges, outer corners, wild ground, clover, and the Wild trees and stones) and B_x_y/Cover (cloud
    /// puffs over the empty slot) — plus Satellites and Sky; <see cref="BattleArenaAmbience"/> runs the waterfall and
    /// the wind in the wild trees.</item>
    /// <item>Installs it into MainColonyScene as "ColonyEnvironment" (in place of Colony_Meadow) at the middle of the
    /// grid, no rotation, with the island look: sun, flat ambient, linear fog, background, a global volume
    /// (ColonyVolume, <see cref="IslandAtmosphere"/>) and <see cref="IslandCameraRig"/> on the main camera.</item>
    /// <item>Shows the kit's stages (start, mid, max) in the open scene and captures them next to the project.</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    public static class IslandEnvironmentBuilder
    {
        public const string LayoutPath = "Assets/Vitaria/Layout/isle_layout.json";
        private const string ModelsRoot = "Assets/Vitaria/Models";
        private const string IsleModelsRoot = ModelsRoot + "/Isle";
        private const string MaterialsRoot = "Assets/Vitaria/Materials";
        private const string PrefabFolder = "Assets/Game/Prefabs/Environments";
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        public const string SceneRootName = ColonyEnvironmentBuilder.SceneRootName;
        private const string SunName = "ColonySun";
        private const string VolumeName = "ColonyVolume";
        private const string BuilderVersion = "1";       // bump to rebuild the island prefab after changing this script
        // wind from the west, as in the colony: the pivot turns the sway axis (local X) onto world +Z
        private static readonly Quaternion WindAxis = Quaternion.Euler(0f, -90f, 0f);
        private static readonly string[] SideNames = { "S", "E", "N", "W" };
        private static readonly string[] CornerNames = { "SE", "NE", "NW", "SW" };
        public static readonly string[] StageNames = { "start", "mid", "max" };

#pragma warning disable 0649   // filled by JsonUtility
        [Serializable]
        private sealed class IsleGrid
        {
            public int cells;
            public float cellSize;
            public float blockSize;
            public int blocksPerSide;
            public int[] start;
        }

        [Serializable]
        private sealed class IsleRise
        {
            public float depth;
            public float seconds;
        }

        [Serializable]
        private sealed class IsleBlock
        {
            public int bx;
            public int by;
            public string model;
            public float bottom;
            public string biome;
            public string[] pieces;
        }

        [Serializable]
        private sealed class IsleItem
        {
            public string name;
            public string asset;
            public string group;
            public string block;
            public string role;
            public float[] p;
            public float[] r;
            public float[] s;
            public float scroll;
            public float[] sway;
        }

        [Serializable]
        private sealed class IsleStage
        {
            public string name;
            public int[] owned;
            public int[] wild;
            public int rising = -1;
        }

        [Serializable]
        private sealed class IsleCamera
        {
            public float fov;
            public float offsetX;
            public float distancePerSide;
        }

        [Serializable]
        private sealed class IsleSun
        {
            public float[] forward;
            public float[] color;
            public float intensity;
        }

        [Serializable]
        private sealed class IsleAmbient
        {
            public float[] color;
        }

        [Serializable]
        private sealed class IsleFog
        {
            public float[] color;
            public float startPerDistance;
            public float endPerDistance;
        }

        [Serializable]
        private sealed class IslePost
        {
            public float exposure;
            public float saturation;
            public float bloomThreshold;
            public float bloomIntensity;
            public float bloomScatter;
            public float dofStartPerDistance;
            public float dofEndPerDistance;
            public float dofMaxRadius;
            public float vignette;
            public float vignetteSmoothness;
            public float[] lift;
            public float[] gamma;
            public float[] gain;
        }

        [Serializable]
        private sealed class IsleLayout
        {
            public string name;
            public string kitVersion;
            public IsleGrid grid;
            public float[] pieceRotation;
            public float wallStep;
            public IsleRise rise;
            public IsleBlock[] blocks;
            public IsleItem[] objects;
            public IsleStage[] stages;
            public IsleCamera camera;
            public IsleSun sun;
            public IsleAmbient ambient;
            public float[] background;
            public IsleFog fog;
            public IslePost post;
        }
#pragma warning restore 0649

        static IslandEnvironmentBuilder()
        {
            EditorApplication.delayCall += BuildPending;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += BuildPending;
            };
        }

        public static bool HasLayout => AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath) != null;

        // ------------------------------------------------------------------------------------------ menus

        /// <summary>Prefab from the layout, installed into MainColonyScene with the island look; saves the scene.</summary>
        [MenuItem("TrollStrategy/Isle/Install Island Into Colony Scene")]
        public static void RebuildIsland()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing the island");
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
                throw new InvalidOperationException("Save the current scene before installing the island");
            if (Build() == null) throw new InvalidOperationException($"Island prefab was not built from {LayoutPath}");
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            InstallIntoOpenScene();
            ThreeDSceneSetup.ConfigureRendering(Camera.main);     // URP shadows 60 m, MSAA only, as for the meadow
            DioramaSurfaceSetup.ConfigureContactShadows();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Isle] {ScenePath}: island installed");
        }

        [MenuItem("TrollStrategy/Isle/Build Island Prefab")]
        public static void BuildPrefabMenu() => Build();

        [MenuItem("TrollStrategy/Isle/Preview Stage/Start")]
        public static void PreviewStart() => PreviewStage("start");

        [MenuItem("TrollStrategy/Isle/Preview Stage/Mid (grown, 2 wild blocks, 1 rising)")]
        public static void PreviewMid() => PreviewStage("mid");

        [MenuItem("TrollStrategy/Isle/Preview Stage/Max (40x40)")]
        public static void PreviewMax() => PreviewStage("max");

        /// <summary>
        /// The kit's stages through the game camera, 1920x1080, next to the project (../colony_isle_capture_start.png
        /// and so on): compare them with Strategy_Kit/Previews/colony_isle_start.png, _mid.png, _max.png. Ends on the
        /// start stage. The kit previews also show the stage's buildings; the capture shows the scene's.
        /// </summary>
        [MenuItem("TrollStrategy/Isle/Capture Stages (16:9)")]
        public static void CaptureStages()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Main Camera is missing");
            const int width = 1920, height = 1080;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                foreach (var stage in StageNames)
                {
                    PreviewStage(stage);
                    var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
                    try
                    {
                        camera.targetTexture = target;
                        camera.aspect = width / (float)height;
                        camera.Render();
                        RenderTexture.active = target;
                        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        image.Apply();
                        var path = Path.GetFullPath($"../colony_isle_capture_{stage}.png");
                        File.WriteAllBytes(path, image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                        Debug.Log($"[Isle] capture {stage} -> {path}");
                    }
                    finally
                    {
                        camera.targetTexture = previousTarget;
                        RenderTexture.active = previousActive;
                        Object.DestroyImmediate(target);
                    }
                }
            }
            finally
            {
                camera.ResetAspect();
                PreviewStage("start");
            }
        }

        // ------------------------------------------------------------------------------------------ scene

        /// <summary>
        /// Puts the island into the open colony scene: removes a previous environment install ("ColonyEnvironment",
        /// the meadow or an older island), adds the prefab at the middle of the grid, applies the island look and
        /// frames the camera on the start zone. Does not save.
        /// </summary>
        public static void InstallIntoOpenScene()
        {
            var layout = Load() ?? throw new InvalidOperationException($"Cannot read {LayoutPath}");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(layout));
            if (prefab == null) prefab = Build();
            if (prefab == null) throw new InvalidOperationException($"Island prefab {PrefabPath(layout)} is missing");
            var grid = Object.FindAnyObjectByType<Grid>();
            if (grid == null) throw new InvalidOperationException("Colony Grid is missing");
            var worldView = grid.GetComponent<TilemapWorldView>();
            if (worldView == null) throw new InvalidOperationException("TilemapWorldView is missing on the Grid");
            int cells = layout.grid != null ? layout.grid.cells : 40;
            if (worldView.GridWidth != cells || worldView.GridHeight != cells)
                Debug.Log($"[Isle] The island grid is {cells}x{cells} cells, the game grid {worldView.GridWidth}x" +
                          $"{worldView.GridHeight}: the island is centred on the game grid; the start zone is the middle " +
                          "20x20 cells until the game grid becomes 40x40");

            // old procedural ground (if any is left) and the previous environment
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
            var view = instance.GetComponent<IslandView>();
            view.ShowStart();
            ApplyLook(layout, Camera.main, view);
        }

        /// <summary>
        /// The island look from the layout: sun, flat ambient, linear fog, camera background and post-processing,
        /// the camera rig and a camera framing of the docked land. Needs the island in the scene.
        /// </summary>
        public static void ApplyLook(Camera camera)
        {
            var layout = Load() ?? throw new InvalidOperationException($"Cannot read {LayoutPath}");
            var view = FindIsland() ?? throw new InvalidOperationException("No island in the open scene");
            ApplyLook(layout, camera, view);
        }

        private static void ApplyLook(IsleLayout layout, Camera camera, IslandView view)
        {
            var sunObject = GameObject.Find(SunName);
            if (sunObject == null) sunObject = new GameObject(SunName);
            var sun = sunObject.GetComponent<Light>();
            if (sun == null) sun = sunObject.AddComponent<Light>();
            var sunData = layout.sun ?? new IsleSun();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(Vector(sunData.forward, new Vector3(-.369f, -.766f, .527f)));
            sun.color = Rgb(sunData.color, new Color(1f, .95f, .86f));
            sun.intensity = sunData.intensity > 0f ? sunData.intensity : 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;
            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);

            // one flat ambient: the island hangs in a bright sky, there is no ground colour below it
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Rgb(layout.ambient?.color, new Color(.68f, .74f, .84f));
            RenderSettings.ambientIntensity = 1f;
            var fogData = layout.fog ?? new IsleFog { startPerDistance = .85f, endPerDistance = 3f };
            var background = Rgb(layout.background, new Color(.71f, .82f, .93f));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Rgb(fogData.color, background);

            var post = layout.post ?? new IslePost();
            var profile = EnsureVolumeProfile(layout, post);
            var volumeObject = GameObject.Find(VolumeName);
            if (volumeObject == null) volumeObject = new GameObject(VolumeName);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(volumeObject);
            var volume = volumeObject.GetComponent<Volume>();
            if (volume == null) volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            var atmosphere = volumeObject.GetComponent<IslandAtmosphere>();
            if (atmosphere == null) atmosphere = volumeObject.AddComponent<IslandAtmosphere>();
            atmosphere.Configure(camera, volume, view.transform, Positive(fogData.startPerDistance, .85f),
                Positive(fogData.endPerDistance, 3f), Positive(post.dofStartPerDistance, 1.4f),
                Positive(post.dofEndPerDistance, 2f));
            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(atmosphere);

            if (camera == null)
            {
                Debug.LogWarning("[Isle] No main camera: the island look is set, the camera is not framed");
                return;
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);
            camera.orthographic = false;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 400f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(cameraData);
            var framing = layout.camera ?? new IsleCamera();
            var rig = camera.GetComponent<IslandCameraRig>();
            if (rig == null) rig = camera.gameObject.AddComponent<IslandCameraRig>();
            rig.Configure(camera, view, Positive(framing.fov, 45f), framing.offsetX,
                Positive(framing.distancePerSide, 1.16f));
            EditorUtility.SetDirty(rig);
            FrameCamera(layout, camera, view, atmosphere);
        }

        /// <summary>The camera over the docked land as the rig frames it at the start of a game; fog and depth of
        /// field for that distance.</summary>
        private static void FrameCamera(IsleLayout layout, Camera camera, IslandView view, IslandAtmosphere atmosphere)
        {
            var framing = layout.camera ?? new IsleCamera();
            var bounds = view.OwnedBounds();
            var target = view.transform.TransformPoint(bounds.center);
            IslandCameraRig.Place(camera, target, Mathf.Max(bounds.size.x, bounds.size.z), Positive(framing.fov, 45f),
                framing.offsetX, Positive(framing.distancePerSide, 1.16f));
            EditorUtility.SetDirty(camera.transform);
            EditorUtility.SetDirty(camera);
            if (atmosphere == null) return;
            var profile = atmosphere.Volume != null ? atmosphere.Volume.sharedProfile : null;
            atmosphere.Apply(IslandAtmosphere.ViewDistance(camera, view.transform.position.y), profile);
            if (profile != null) EditorUtility.SetDirty(profile);
        }

        /// <summary>Shows a kit stage on the island in the open scene and frames the camera on its docked land.</summary>
        public static void PreviewStage(string stageName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stage previews are for Edit Mode");
            var layout = Load() ?? throw new InvalidOperationException($"Cannot read {LayoutPath}");
            var view = FindIsland() ?? throw new InvalidOperationException(
                "No island in the open scene: run TrollStrategy > Isle > Install Island Into Colony Scene");
            var stage = Array.Find(layout.stages ?? Array.Empty<IsleStage>(), s => s != null && s.name == stageName) ??
                        throw new InvalidOperationException($"No stage '{stageName}' in {LayoutPath}");
            var states = new LandBlockState[view.Count];
            foreach (int i in stage.owned ?? Array.Empty<int>())
                if (i >= 0 && i < states.Length) states[i] = LandBlockState.Cleared;
            foreach (int i in stage.wild ?? Array.Empty<int>())
                if (i >= 0 && i < states.Length) states[i] = LandBlockState.Wild;
            if (stage.rising >= 0 && stage.rising < states.Length) states[stage.rising] = LandBlockState.Rising;
            view.Apply(states, false);
            RecordInstance(view);
            var camera = Camera.main;
            if (camera != null)
            {
                var volumeObject = GameObject.Find(VolumeName);
                FrameCamera(layout, camera, view, volumeObject != null ? volumeObject.GetComponent<IslandAtmosphere>() : null);
            }
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Debug.Log($"[Isle] stage {stageName}: {stage.owned?.Length ?? 0} blocks bought, {stage.wild?.Length ?? 0} wild" +
                      (stage.rising >= 0 ? ", 1 rising" : ""));
        }

        private static IslandView FindIsland()
        {
            foreach (var view in Object.FindObjectsByType<IslandView>(FindObjectsInactive.Include))
                if (view != null && view.gameObject.scene.IsValid()) return view;
            return null;
        }

        /// <summary>Script changes to a prefab instance are kept (saved, carried into Play Mode) only once recorded
        /// as overrides: the stage's active flags, positions and scales of the island's pieces and the view's states.</summary>
        private static void RecordInstance(IslandView view)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(view)) return;
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            foreach (var transform in view.GetComponentsInChildren<Transform>(true))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform.gameObject);
            }
        }

        private static VolumeProfile EnsureVolumeProfile(IsleLayout layout, IslePost post)
        {
            EnsureFolder(PrefabFolder);
            var path = PrefabFolder + "/" + layout.name + "_Volume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            // the kit renders with AgX; URP has Neutral and ACES, Neutral keeps the palette's hues
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
            var color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(post.exposure);
            color.saturation.Override(post.saturation);
            color.contrast.Override(0f);
            var lgg = Get<LiftGammaGain>(profile);
            lgg.lift.Override(Vec4(post.lift, new Vector4(1f, 1f, 1f, 0f)));
            lgg.gamma.Override(Vec4(post.gamma, new Vector4(1f, 1f, 1f, 0f)));
            lgg.gain.Override(Vec4(post.gain, new Vector4(1f, 1f, 1f, 0f)));
            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(Positive(post.bloomThreshold, .9f));
            bloom.intensity.Override(post.bloomIntensity);
            bloom.scatter.Override(Positive(post.bloomScatter, .6f));
            var dof = Get<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(46f);            // IslandAtmosphere scales both with the camera distance
            dof.gaussianEnd.Override(66f);
            dof.gaussianMaxRadius.Override(Mathf.Clamp(Positive(post.dofMaxRadius, 1f), .5f, 1.5f));
            dof.highQualitySampling.Override(true);
            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(post.vignette);
            vignette.smoothness.Override(Positive(post.vignetteSmoothness, .45f));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;   // as the profile editor does
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------------------------------ prefab

        /// <summary>Builds the island prefab when it is missing or older than the layout (does not touch scenes).</summary>
        internal static void BuildPending()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            var layout = Load();
            if (text == null || layout == null) return;
            var importer = AssetImporter.GetAtPath(PrefabPath(layout));
            if (importer == null || importer.userData != SourceHash(text.text)) Build();
        }

        public static GameObject Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Isle] Leave Play Mode, then run TrollStrategy > Isle > Build Island Prefab.");
                return null;
            }
            var layout = Load();
            if (layout == null)
            {
                Debug.LogError($"[Isle] Cannot read the island layout {LayoutPath}");
                return null;
            }

            var duplicates = new List<string>();
            var models = IndexModels(duplicates);
            var missing = new List<string>();
            var root = new GameObject(layout.name);
            try
            {
                var ambience = root.AddComponent<BattleArenaAmbience>();
                var view = root.AddComponent<IslandView>();
                var satellites = Child(root.transform, "Satellites");
                var sky = Child(root.transform, "Sky");
                var blocksRoot = Child(root.transform, "Blocks");
                var grid = layout.grid ?? new IsleGrid { blocksPerSide = 8, blockSize = 5f };
                int side = Mathf.Max(1, grid.blocksPerSide);
                var pieceRotation = Rotation(layout.pieceRotation);

                // blocks: every piece of Isle_Block_x_y.fbx becomes its own renderer under B_x_y/Land
                var blocks = new Dictionary<string, BlockParts>(StringComparer.Ordinal);
                int pieceCount = 0;
                foreach (var block in layout.blocks ?? Array.Empty<IsleBlock>())
                {
                    if (block == null || string.IsNullOrEmpty(block.model)) continue;
                    if (!models.TryGetValue(block.model, out var model))
                    {
                        missing.Add(block.model);
                        continue;
                    }
                    var parts = new BlockParts { Block = block, Root = new GameObject($"B_{block.bx}_{block.by}") };
                    parts.Root.transform.SetParent(blocksRoot, false);
                    parts.Land = Child(parts.Root.transform, "Land");
                    var filters = model.GetComponentsInChildren<MeshFilter>(true);
                    foreach (var filter in filters)
                    {
                        if (filter.sharedMesh == null) continue;
                        // an FBX with a single object imports as that object under the file's name
                        string pieceName = filters.Length == 1 && block.pieces != null && block.pieces.Length == 1
                            ? block.pieces[0]
                            : filter.gameObject.name;
                        var piece = new GameObject(pieceName);
                        piece.transform.SetParent(parts.Land, false);
                        piece.transform.localRotation = pieceRotation;
                        piece.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                        var renderer = piece.AddComponent<MeshRenderer>();
                        var source = filter.GetComponent<MeshRenderer>();
                        if (source != null) renderer.sharedMaterials = source.sharedMaterials;
                        // the flat clover and the grass tufts would only add shadow casters
                        renderer.shadowCastingMode = pieceName == "Decor" || pieceName == "WildGround"
                            ? ShadowCastingMode.Off
                            : ShadowCastingMode.On;
                        parts.Pieces[pieceName] = piece;
                        pieceCount++;
                    }
                    foreach (var name in block.pieces ?? Array.Empty<string>())
                        if (!parts.Pieces.ContainsKey(name)) missing.Add($"{block.model}/{name}");
                    parts.Wild = Child(parts.Land, "Wild");
                    parts.Cover = Child(parts.Root.transform, "Cover");
                    blocks[$"{block.bx},{block.by}"] = parts;
                }

                // wild trees and stones, cover clouds, satellites and sky
                var waterfall = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Vitaria_Waterfall.mat");
                var names = new Dictionary<string, int>(StringComparer.Ordinal);
                int wild = 0, cover = 0, other = 0;
                foreach (var item in layout.objects ?? Array.Empty<IsleItem>())
                {
                    if (item == null || string.IsNullOrEmpty(item.asset)) continue;
                    Transform parent;
                    bool isCover = string.Equals(item.role, "cover", StringComparison.Ordinal);
                    if (!string.IsNullOrEmpty(item.block))
                    {
                        if (!blocks.TryGetValue(item.block, out var parts))
                        {
                            missing.Add($"block {item.block} for {item.name}");
                            continue;
                        }
                        parent = isCover ? parts.Cover : parts.Wild;
                    }
                    else
                    {
                        parent = string.Equals(item.group, "Sky", StringComparison.Ordinal) ? sky : satellites;
                    }
                    if (!models.TryGetValue(item.asset, out var model))
                    {
                        missing.Add(item.asset);
                        continue;
                    }
                    var instance = PrefabUtility.InstantiatePrefab(model, parent) as GameObject;
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
                    if (isCover || parent == sky)
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (isCover) cover++;
                    else if (!string.IsNullOrEmpty(item.block)) wild++;
                    else other++;

                    if (item.sway != null && item.sway.Length >= 2)
                    {
                        // a pivot at the trunk base: the tree keeps its own yaw under it, the wind is the same for all
                        var pivot = new GameObject(instance.name + "_Wind").transform;
                        pivot.SetParent(instance.transform.parent, false);
                        pivot.localPosition = instance.transform.localPosition;
                        pivot.localRotation = WindAxis;
                        instance.transform.SetParent(pivot, true);
                        ambience.AddSway(pivot, item.sway[0], item.sway[1], item.sway.Length >= 3 ? item.sway[2] : 0f);
                    }
                    if (item.scroll != 0f)
                    {
                        var renderer = instance.GetComponentInChildren<Renderer>();
                        if (renderer == null) continue;
                        if (waterfall != null) renderer.sharedMaterial = waterfall;
                        ambience.AddFlow(renderer, item.scroll);
                    }
                }

                // block views: after the cover puffs are in place (the view keeps their resting positions)
                var views = new IslandBlockView[side * side];
                foreach (var parts in blocks.Values)
                {
                    var b = parts.Block;
                    var blockView = parts.Root.AddComponent<IslandBlockView>();
                    blockView.Setup(b.bx, b.by, b.bottom, b.biome ?? string.Empty, parts.Land,
                        parts.Named("Wall_", SideNames), parts.Named("Ledge_", SideNames),
                        parts.Named("Corner_", CornerNames), parts.Get("WildGround"), parts.Get("Decor"), parts.Wild,
                        parts.Cover);
                    if (b.bx >= 0 && b.by >= 0 && b.bx < side && b.by < side) views[b.by * side + b.bx] = blockView;
                }
                for (int i = 0; i < views.Length; i++)
                    if (views[i] == null) missing.Add($"block {i % side},{i / side}");
                var start = grid.start != null && grid.start.Length >= 4
                    ? new RectInt(grid.start[0], grid.start[1], grid.start[2], grid.start[3])
                    : new RectInt(2, 2, 4, 4);
                var rise = layout.rise ?? new IsleRise { depth = 8f, seconds = 2.2f };
                view.Configure(side, Positive(grid.blockSize, 5f), start, Positive(layout.wallStep, 2f),
                    Positive(rise.depth, 8f), Positive(rise.seconds, 2.2f), views);
                view.ShowStart();

                EnsureFolder(PrefabFolder);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(layout));
                if (prefab == null)
                {
                    Debug.LogError($"[Isle] Could not save {PrefabPath(layout)}");
                    return null;
                }
                // an incomplete island is never marked up to date, so a later model import rebuilds it
                bool complete = missing.Count == 0 && duplicates.Count == 0;
                var importer = AssetImporter.GetAtPath(PrefabPath(layout));
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
                if (importer != null && text != null)
                {
                    importer.userData = complete ? SourceHash(text.text) : string.Empty;
                    EditorUtility.SetDirty(importer);
                    AssetDatabase.WriteImportSettingsIfDirty(PrefabPath(layout));
                }
                var summary = $"[Isle] {layout.name} (kit {layout.kitVersion}): {blocks.Count} blocks, {pieceCount} " +
                              $"pieces, {wild} wild objects, {cover} cover clouds, {other} satellite and sky objects " +
                              $"-> {PrefabPath(layout)}";
                if (duplicates.Count > 0)
                    Debug.LogError($"{summary}; model names found twice in {ModelsRoot} (delete the stale file): " +
                                   string.Join("; ", duplicates), prefab);
                if (missing.Count > 0)
                    Debug.LogWarning($"{summary}; missing: {string.Join(", ", missing)}", prefab);
                else if (duplicates.Count == 0)
                    Debug.Log(summary, prefab);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private sealed class BlockParts
        {
            public IsleBlock Block;
            public GameObject Root;
            public Transform Land;
            public Transform Wild;
            public Transform Cover;
            public readonly Dictionary<string, GameObject> Pieces = new Dictionary<string, GameObject>(StringComparer.Ordinal);

            public GameObject Get(string name) => Pieces.TryGetValue(name, out var piece) ? piece : null;

            public GameObject[] Named(string prefix, string[] suffixes)
            {
                var result = new GameObject[suffixes.Length];
                for (int i = 0; i < suffixes.Length; i++) result[i] = Get(prefix + suffixes[i]);
                return result;
            }
        }

        // ------------------------------------------------------------------------------------------ helpers

        internal static bool IsIslandAsset(string path) =>
            path == LayoutPath ||
            (path.StartsWith(ModelsRoot + "/", StringComparison.Ordinal) &&
             path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));

        private static string PrefabPath(IsleLayout layout) => PrefabFolder + "/" + layout.name + ".prefab";

        private static string SourceHash(string json)
        {
            json = json.Replace("\r\n", "\n");
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(BuilderVersion + "|" + json)))
                .Replace("-", "");
        }

        private static IsleLayout Load()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (text == null) return null;
            var layout = JsonUtility.FromJson<IsleLayout>(text.text);
            return layout != null && !string.IsNullOrEmpty(layout.name) && layout.blocks != null ? layout : null;
        }

        /// <summary>Kit models by file name; names are unique across Assets/Vitaria/Models (the kit checks it on
        /// export), a name found twice goes to <paramref name="duplicates"/>.</summary>
        private static Dictionary<string, GameObject> IndexModels(List<string> duplicates)
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
                    duplicates.Add($"{key} ({other}, {path})");
                    continue;
                }
                paths[key] = path;
                models[key] = model;
            }
            return models;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
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

        private static float Positive(float value, float fallback) => value > 0f ? value : fallback;

        private static Vector3 Vector(float[] v, Vector3 fallback) =>
            v != null && v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : fallback;

        private static Vector4 Vec4(float[] v, Vector4 fallback) =>
            v != null && v.Length >= 4 ? new Vector4(v[0], v[1], v[2], v[3]) : fallback;

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

    /// <summary>Rebuilds the island prefab when its layout or a kit model is imported.</summary>
    internal sealed class IslandLayoutWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (!IslandEnvironmentBuilder.IsIslandAsset(path)) continue;
                EditorApplication.delayCall -= IslandEnvironmentBuilder.BuildPending;
                EditorApplication.delayCall += IslandEnvironmentBuilder.BuildPending;
                break;
            }
        }
    }
}
