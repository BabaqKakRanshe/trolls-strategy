using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.IO;
using Object = UnityEngine.Object;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Editor.Setup
{
    public static class ThreeDSceneSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        private const string RendererPath = "Assets/Settings/Colony3DRenderer.asset";

        // The colony map is the Vitaria kit's Colony_Meadow now: ColonyEnvironmentBuilder builds and installs it
        // and lights the colony like the arena. This menu stays as the old entry point.
        [MenuItem("TrollStrategy/Refresh Diorama")]
        public static void RefreshDiorama()
        {
            EnsureRenderer();
            ColonyEnvironmentBuilder.RebuildColonyMap();
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
            ConfigureDiorama(camera, worldView);
            ColonyEnvironmentBuilder.InstallIntoOpenScene();
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
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
            // sun, trilight ambient and background: the arena profile from the colony layout
            ColonyEnvironmentBuilder.ApplyLighting(camera);
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
