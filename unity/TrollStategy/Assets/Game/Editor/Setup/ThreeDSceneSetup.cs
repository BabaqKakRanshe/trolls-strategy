using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.IO;
using System;
using Object = UnityEngine.Object;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Visuals;

namespace TrollStrategy.Editor.Setup
{
    public static class ThreeDSceneSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        private const string RendererPath = "Assets/Settings/Colony3DRenderer.asset";

        [MenuItem("TrollStrategy/Refresh Diorama")]
        public static void RefreshDiorama()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop play mode before refreshing the diorama");
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty)
                throw new System.InvalidOperationException("Save the current scene before refreshing the diorama");
            var scene = activeScene.path == ScenePath ? activeScene : EditorSceneManager.OpenScene(ScenePath);
            var grid = Object.FindAnyObjectByType<Grid>();
            var camera = Camera.main;
            if (grid == null || camera == null)
                throw new System.InvalidOperationException("Colony grid or main camera is missing");
            var worldView = grid.GetComponent<TilemapWorldView>();
            if (worldView == null)
                throw new System.InvalidOperationException("Colony map view is missing");
            EnsureRenderer();
            SetupKitMaterial();
            RefreshPrimitiveMaterials();
            var environment = grid.GetComponent<PrimitiveEnvironment>();
            if (environment == null) environment = grid.gameObject.AddComponent<PrimitiveEnvironment>();
            BindKitEnvironment(environment);
            environment.Rebuild();
            ConfigureDiorama(camera, worldView);
            DioramaSurfaceSetup.Apply(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("TrollStrategy/Convert Colony To 3D")]
        public static void ConvertCurrentScene()
        {
            EnsureRenderer();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyToOpenScene(true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Main Camera is missing");
            var target = new RenderTexture(1600, 900, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes("../3d-preview.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(target);
            }
        }

        public static void ApplyToOpenScene(bool preserveLayout = false)
        {
            EnsureRenderer();
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Main Camera is missing");
            var grid = Object.FindAnyObjectByType<Grid>();
            if (grid == null) throw new System.InvalidOperationException("Grid is missing");
            var worldView = grid.GetComponent<TilemapWorldView>();
            if (worldView == null) throw new System.InvalidOperationException("TilemapWorldView is missing");
            if (!preserveLayout)
            {
                grid.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.orthographic = false;
                camera.nearClipPlane = 0.1f;
                var center = worldView.MapToWorld(new Vector3(
                    worldView.GridWidth * worldView.CellSize * 0.5f,
                    worldView.GridHeight * worldView.CellSize * 0.5f, 0f));
                camera.transform.position = center + new Vector3(0f, 14f, -14f);
                camera.transform.LookAt(center, Vector3.forward);
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColonyPalette.Night;
            ConfigureDiorama(camera, worldView);

            var old = grid.transform.Find("EnvironmentWorld");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var environment = grid.GetComponent<PrimitiveEnvironment>();
            if (environment == null) environment = grid.gameObject.AddComponent<PrimitiveEnvironment>();
            BindKitEnvironment(environment);
            if (!preserveLayout) environment.Rebuild();
        }

        private static void ConfigureDiorama(Camera camera, TilemapWorldView worldView)
        {
            camera.orthographic = false;
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            var center = worldView.MapToWorld(new Vector3(
                worldView.GridWidth * worldView.CellSize * 0.5f,
                worldView.GridHeight * worldView.CellSize * 0.5f, 0f));
            var distance = Mathf.Max(worldView.GridWidth, worldView.GridHeight) * worldView.CellSize * 1.16f;
            camera.transform.position = center + new Vector3(1.5f, distance, -distance);
            camera.transform.LookAt(center, Vector3.up);
            camera.backgroundColor = new Color32(47, 57, 48, 255);
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
            var lightObject = GameObject.Find("ColonySun");
            if (lightObject == null) lightObject = new GameObject("ColonySun");
            var light = lightObject.GetComponent<Light>();
            if (light == null) light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            light.color = new Color32(255, 244, 225, 255);
            light.intensity = 1.45f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .48f;
            light.shadowBias = .035f;
            light.shadowNormalBias = .3f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(135, 151, 155, 255);
            RenderSettings.ambientEquatorColor = new Color32(103, 113, 99, 255);
            RenderSettings.ambientGroundColor = new Color32(70, 59, 48, 255);
            RenderSettings.ambientIntensity = 1f;
            EditorUtility.SetDirty(light);
        }

        private static void RefreshPrimitiveMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP Lit shader is missing");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Game/Art/Materials" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).StartsWith("Primitive_")) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                var color = material.color;
                material.shader = shader;
                material.color = color;
                material.SetFloat("_Smoothness", 0.08f);
                material.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(material);
            }
        }

        private static void SetupKitMaterial()
        {
            const string materialPath = "Assets/Vitaria/Materials/Vitaria_Palette.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing");
            if (!AssetDatabase.IsValidFolder("Assets/Vitaria/Materials"))
                AssetDatabase.CreateFolder("Assets/Vitaria", "Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Vitaria_Palette" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Vitaria/Textures/Vitaria_Palette.png");
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Vitaria/Textures/Vitaria_Palette_Emission.png");
            if (palette == null || emission == null) throw new InvalidOperationException("Vitaria palette textures are missing");
            material.SetTexture("_BaseMap", palette);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_EmissionColor", Color.white * 2f);
            material.EnableKeyword("_EMISSION");
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Vitaria/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                var source = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Vitaria_Palette");
                if (importer.GetExternalObjectMap().TryGetValue(source, out var mapped) && mapped == material)
                    continue;
                importer.AddRemap(source, material);
                importer.SaveAndReimport();
            }
        }

        private static void BindKitEnvironment(PrimitiveEnvironment environment)
        {
            var serialized = new SerializedObject(environment);
            Assign(serialized, "_trees", "Nature/Tree_Pine_A", "Nature/Tree_Pine_B", "Nature/Tree_Pine_C",
                "Nature/Tree_Round_A", "Nature/Tree_Round_B");
            Assign(serialized, "_bushes", "Nature/Bush_A", "Nature/Bush_B", "Nature/Bush_Berry");
            Assign(serialized, "_rocks", "Nature/Rock_Small", "Nature/Rock_Medium", "Nature/Rock_Large");
            Assign(serialized, "_groundCover", "Nature/Grass_Tuft_A", "Nature/Grass_Tuft_B", "Nature/Flowers_A");
            Assign(serialized, "_props", "Resources/Res_LogPile", "Props/Prop_Fence",
                "Props/Prop_LanternPost", "Props/Prop_Barrel");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(environment);
        }

        private static void Assign(SerializedObject serialized, string propertyName, params string[] names)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"Environment field missing: {propertyName}");
            property.arraySize = names.Length;
            for (var i = 0; i < names.Length; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Vitaria/Models/{names[i]}.fbx");
                if (model == null) throw new InvalidOperationException($"Vitaria model missing: {names[i]}");
                property.GetArrayElementAtIndex(i).objectReferenceValue = model;
            }
        }

        private static void EnsureRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            if (pipeline == null) throw new System.InvalidOperationException("URP asset is missing");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_ShadowCascadeCount").intValue = 2;
            serialized.FindProperty("m_MSAA").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }
    }
}
