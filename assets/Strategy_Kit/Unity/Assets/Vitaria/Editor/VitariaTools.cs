// Vitaria low-poly kit — Unity editor tooling.
// Put this file under Assets/Vitaria/Editor/. Works with URP and the Built-in pipeline (Unity 2021.3+).
//
// Menu: Tools > Vitaria
//   1. Setup Material        — creates Assets/Vitaria/Materials/Vitaria_Palette.mat and remaps every model to it
//   2. Create Prefabs        — prefab variant per model in Assets/Vitaria/Prefabs (+ BoxCollider / MeshCollider)
//   3. Build Scene From Layout — instantiates every object of the Blender scene at its place, grouped by category
//   4. Setup Lighting        — directional sun with soft shadows, gradient ambient, URP shadow distance/resolution
//   5. Align Main Camera     — camera position / FOV / background identical to the Blender preview
//
// Battle arenas (Models/Arena, Layout/arena_*_layout.json) are assembled into a prefab by the game
// (TrollStrategy > Arena > Build Arena Prefabs); here they only get their import profile, the two water
// materials (Vitaria_Water, Vitaria_Waterfall), whose textures scroll at runtime, and Vitaria_FX for the
// faces that glow (arena flames, coals, sparks).

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Vitaria.EditorTools
{
    public static class VitariaTools
    {
        public const string Root = "Assets/Vitaria";
        public const string ModelsRoot = Root + "/Models";
        public const string PrefabsRoot = Root + "/Prefabs";
        public const string MaterialPath = Root + "/Materials/Vitaria_Palette.mat";
        public const string SourceMaterialName = "Vitaria_Palette";
        public const string ArenaModelsRoot = ModelsRoot + "/Arena";
        // Water keeps its own materials: a small tiling texture scrolled along V by the arena script.
        public static readonly string[] WaterMaterialNames = { "Vitaria_Water", "Vitaria_Waterfall" };
        // Every material besides the palette an FBX may use: water, and Vitaria_FX — the palette with emission
        // on, for the glowing faces of arena flames, coals and sparks (the colony's palette keeps emission off).
        public static readonly string[] ExtraMaterialNames = { "Vitaria_Water", "Vitaria_Waterfall", "Vitaria_FX" };
        public static string WaterMaterialPath(string name) { return Root + "/Materials/" + name + ".mat"; }

        /// <summary>Remaps every Vitaria material the FBX files use (palette, water, FX) to the project assets.</summary>
        public static void AddMaterialRemaps(ModelImporter mi)
        {
            var palette = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (palette != null)
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), SourceMaterialName), palette);
            foreach (var name in ExtraMaterialNames)
            {
                var extra = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath(name));
                if (extra != null)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), extra);
            }
        }
        const string AlbedoPath = Root + "/Textures/Vitaria_Palette.png";
        const string EmissionPath = Root + "/Textures/Vitaria_Palette_Emission.png";
        const string LayoutPath = Root + "/Layout/vitaria_layout.json";

        // ------------------------------------------------------------------ layout json
        [Serializable] public class LItem { public string name; public string asset; public string group; public float[] p; public float[] r; public float[] s; }
        [Serializable] public class LCam { public float[] position; public float[] forward; public float[] up; public float fov; }
        [Serializable] public class LSun { public float[] forward; public float[] color; public float intensity; }
        [Serializable] public class LAmb { public float[] sky; public float[] equator; public float[] ground; }
        [Serializable] public class VLayout { public LItem[] objects; public LCam camera; public LSun sun; public LAmb ambient; public float[] background; }

        static Vector3 V(float[] a) { return new Vector3(a[0], a[1], a[2]); }
        static Color C(float[] a) { return new Color(a[0], a[1], a[2], 1f); }

        static VLayout LoadLayout()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (ta == null)
            {
                Debug.LogError("[Vitaria] Layout not found: " + LayoutPath);
                return null;
            }
            return JsonUtility.FromJson<VLayout>(ta.text);
        }

        // ------------------------------------------------------------------ 1. material
        [MenuItem("Tools/Vitaria/1. Setup Material", false, 1)]
        public static void SetupMaterial()
        {
            var mat = GetOrCreateMaterial();
            if (mat == null) return;
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (mi == null) continue;
                    AddMaterialRemaps(mi);
                    mi.SaveAndReimport();
                    n++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log("[Vitaria] Material ready (" + mat.shader.name + "), remapped " + n + " models.");
        }

        public static Material GetOrCreateMaterial()
        {
            var shader = FindLitShader();
            if (shader == null)
            {
                Debug.LogError("[Vitaria] No lit shader found.");
                return null;
            }
            EnsureFolder(Root, "Materials");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = SourceMaterialName };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
            SetTex(mat, albedo, "_BaseMap", "_MainTex");
            SetColor(mat, Color.white, "_BaseColor", "_Color");
            SetFloat(mat, 0.12f, "_Smoothness", "_Glossiness");
            SetFloat(mat, 0f, "_Metallic");
            // The palette itself never glows: glowing faces (fire, coals, lanterns, runes) are exported into a
            // second slot, Vitaria_FX — the same palette with emission. One rule for the arena, the colony
            // and the buildings; before, this menu switched the palette's emission on and made every glowing
            // swatch glow twice where Vitaria_FX was also used.
            mat.DisableKeyword("_EMISSION");
            SetColor(mat, Color.black, "_EmissionColor");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            GetOrCreateFxMaterial(mat, emission);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Vitaria_FX: a copy of the palette with emission from Vitaria_Palette_Emission (only the glowing
        /// swatches are bright there). FBX slots named Vitaria_FX are remapped onto it on import.</summary>
        public static Material GetOrCreateFxMaterial(Material palette, Texture2D emission)
        {
            var path = WaterMaterialPath("Vitaria_FX");
            var fx = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (fx == null)
            {
                fx = new Material(palette) { name = "Vitaria_FX" };
                AssetDatabase.CreateAsset(fx, path);
            }
            else if (fx.shader != palette.shader)
            {
                fx.shader = palette.shader;
            }
            Texture baseMap = palette.HasProperty("_BaseMap") ? palette.GetTexture("_BaseMap") : null;
            if (baseMap == null) baseMap = palette.mainTexture;
            SetTex(fx, baseMap, "_BaseMap", "_MainTex");
            SetColor(fx, Color.white, "_BaseColor", "_Color");
            if (emission != null) SetTex(fx, emission, "_EmissionMap");
            SetColor(fx, Color.white * 1.6f, "_EmissionColor");
            fx.EnableKeyword("_EMISSION");
            fx.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            fx.enableInstancing = true;
            EditorUtility.SetDirty(fx);
            return fx;
        }

        static Shader FindLitShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null)
                s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static void SetTex(Material m, Texture t, params string[] props)
        {
            if (t == null) return;
            foreach (var p in props) if (m.HasProperty(p)) m.SetTexture(p, t);
        }
        static void SetColor(Material m, Color c, params string[] props)
        {
            foreach (var p in props) if (m.HasProperty(p)) m.SetColor(p, c);
        }
        static void SetFloat(Material m, float v, params string[] props)
        {
            foreach (var p in props) if (m.HasProperty(p)) m.SetFloat(p, v);
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }

        // ------------------------------------------------------------------ 2. prefabs
        [MenuItem("Tools/Vitaria/2. Create Prefabs", false, 2)]
        public static void CreatePrefabs()
        {
            EnsureFolder(Root, "Prefabs");
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name == "Vitaria_Scene") continue;
                if (path.StartsWith(ArenaModelsRoot + "/")) continue;     // arenas are assembled by the game
                if (path.StartsWith(ModelsRoot + "/Isle/")) continue;     // so is the island (IslandEnvironmentBuilder)
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var category = Path.GetFileName(Path.GetDirectoryName(path));
                EnsureFolder(PrefabsRoot, category);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (name.StartsWith("Env_Island"))
                {
                    inst.AddComponent<MeshCollider>();
                }
                else if (!name.StartsWith("Grass_") && !name.StartsWith("Flowers_") && !name.StartsWith("Env_Paths"))
                {
                    if (inst.GetComponent<Collider>() == null) inst.AddComponent<BoxCollider>();
                }
                PrefabUtility.SaveAsPrefabAsset(inst, PrefabsRoot + "/" + category + "/" + name + ".prefab");
                UnityEngine.Object.DestroyImmediate(inst);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Vitaria] Created/updated " + n + " prefab variants in " + PrefabsRoot);
        }

        // ------------------------------------------------------------------ 3. scene
        [MenuItem("Tools/Vitaria/3. Build Scene From Layout", false, 3)]
        public static void BuildScene()
        {
            var layout = LoadLayout();
            if (layout == null) return;
            var sources = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) sources[Path.GetFileNameWithoutExtension(path)] = go;
            }
            if (AssetDatabase.IsValidFolder(PrefabsRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsRoot }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) sources[Path.GetFileNameWithoutExtension(path)] = go;   // prefabs win over raw models
                }
            }

            var root = new GameObject("Vitaria_Scene");
            Undo.RegisterCreatedObjectUndo(root, "Build Vitaria Scene");
            var groups = new Dictionary<string, Transform>();
            int placed = 0, missing = 0;
            foreach (var it in layout.objects)
            {
                GameObject src;
                if (!sources.TryGetValue(it.asset, out src)) { missing++; continue; }
                Transform parent;
                if (!groups.TryGetValue(it.group, out parent))
                {
                    parent = new GameObject(it.group).transform;
                    parent.SetParent(root.transform, false);
                    groups[it.group] = parent;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
                inst.name = it.name;
                inst.transform.localPosition = V(it.p);
                inst.transform.localRotation = new Quaternion(it.r[0], it.r[1], it.r[2], it.r[3]);
                inst.transform.localScale = V(it.s);
                GameObjectUtility.SetStaticEditorFlags(inst, StaticEditorFlags.BatchingStatic);
                placed++;
            }
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Vitaria] Placed " + placed + " objects" + (missing > 0 ? (", missing models: " + missing) : ""));
        }

        // ------------------------------------------------------------------ 4. lighting
        [MenuItem("Tools/Vitaria/4. Setup Lighting", false, 4)]
        public static void SetupLighting()
        {
            var layout = LoadLayout();
            if (layout == null) return;

            var sun = RenderSettings.sun;
            if (sun == null)
            {
                var go = new GameObject("Sun");
                Undo.RegisterCreatedObjectUndo(go, "Vitaria Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            else
            {
                Undo.RecordObject(sun, "Vitaria Sun");
                Undo.RecordObject(sun.transform, "Vitaria Sun");
            }
            sun.transform.rotation = Quaternion.LookRotation(V(layout.sun.forward));
            sun.color = C(layout.sun.color);
            sun.intensity = layout.sun.intensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.25f;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C(layout.ambient.sky);
            RenderSettings.ambientEquatorColor = C(layout.ambient.equator);
            RenderSettings.ambientGroundColor = C(layout.ambient.ground);
            RenderSettings.fog = false;

            // URP asset: soft shadows, enough distance for the whole island, sharper shadow map.
            // Done through SerializedObject, so this file needs no URP assembly reference.
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null)
            {
                var so = new SerializedObject(rp);
                var p = so.FindProperty("m_ShadowDistance"); if (p != null) p.floatValue = 45f;
                p = so.FindProperty("m_SoftShadowsSupported"); if (p != null) p.boolValue = true;
                p = so.FindProperty("m_MainLightShadowmapResolution"); if (p != null) p.intValue = 2048;
                p = so.FindProperty("m_MainLightShadowsSupported"); if (p != null) p.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(rp);
            }
            else
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 45f;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Vitaria] Lighting set. For contact shadows add Screen Space Ambient Occlusion to the URP renderer.");
        }

        // ------------------------------------------------------------------ 5. camera
        [MenuItem("Tools/Vitaria/5. Align Main Camera To Preview", false, 5)]
        public static void AlignCamera()
        {
            var layout = LoadLayout();
            if (layout == null) return;
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(go, "Vitaria Camera");
                cam = go.AddComponent<Camera>();
            }
            else
            {
                Undo.RecordObject(cam, "Vitaria Camera");
                Undo.RecordObject(cam.transform, "Vitaria Camera");
            }
            cam.transform.position = V(layout.camera.position);
            cam.transform.rotation = Quaternion.LookRotation(V(layout.camera.forward), V(layout.camera.up));
            cam.fieldOfView = layout.camera.fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = C(layout.background);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 200f);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }

    // Keeps import settings right for anything dropped into Assets/Vitaria/Models.
    public class VitariaModelPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(VitariaTools.ModelsRoot)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null) return;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importBlendShapes = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            // lightmap UVs, in case you bake GI; arenas, the colony and the island are spawned at runtime and their
            // merged meshes (tens of thousands of triangles in hundreds of pieces) would only slow the import down
            mi.generateSecondaryUV = !assetPath.StartsWith(VitariaTools.ArenaModelsRoot + "/") &&
                                     !assetPath.StartsWith(VitariaTools.ModelsRoot + "/Colony/") &&
                                     !assetPath.StartsWith(VitariaTools.ModelsRoot + "/Isle/");
            mi.isReadable = false;
            VitariaTools.AddMaterialRemaps(mi);
        }
    }

    // Palette textures: no mipmaps, no compression — swatches and gradients stay exact.
    public class VitariaTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(VitariaTools.Root + "/Textures")) return;
            var ti = assetImporter as TextureImporter;
            if (ti == null) return;
            if (Path.GetFileNameWithoutExtension(assetPath).StartsWith("Vitaria_Water"))
            {
                // water: tiling stripes scrolled along V — repeat, mipmapped against shimmer
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.sRGBTexture = true;
                return;
            }
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.sRGBTexture = true;
        }
    }
}
