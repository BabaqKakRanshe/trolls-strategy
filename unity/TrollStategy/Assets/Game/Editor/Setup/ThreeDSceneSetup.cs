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
        private const float ShadowDistance = 60f;

        // The colony map is the Vitaria kit's Colony_Meadow now: ColonyEnvironmentBuilder builds and installs it
        // and lights the colony like the arena. This menu stays as the old entry point.
        [MenuItem("TrollStrategy/Refresh Diorama")]
        public static void RefreshDiorama() => ColonyEnvironmentBuilder.RebuildColonyMap();

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

        // the three frames the Vitaria kit previews are rendered in (Strategy_Kit/Previews/colony_meadow_*.png)
        private static readonly string[] CaptureNames = { "hero", "wide", "ipad" };
        private static readonly Vector2Int[] CaptureSizes =
            { new Vector2Int(1920, 1080), new Vector2Int(2560, 1080), new Vector2Int(1440, 1080) };

        /// <summary>
        /// Game-camera captures of the colony next to the project (../colony_capture_hero.png and so on), in the
        /// same three frames as the kit's Blender previews: after every kit import, compare them side by side.
        /// The HUD is UI Toolkit and is not in the capture; its bands cover the top 8.5% and the bottom 7%.
        /// </summary>
        [MenuItem("TrollStrategy/Colony/Capture Previews (16:9, 21:9, 4:3)")]
        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Main Camera is missing");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousAspect = camera.aspect;
            try
            {
                for (int i = 0; i < CaptureNames.Length; i++)
                {
                    string name = CaptureNames[i];
                    int width = CaptureSizes[i].x, height = CaptureSizes[i].y;
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
                        var path = Path.GetFullPath($"../colony_capture_{name}.png");
                        File.WriteAllBytes(path, image.EncodeToPNG());
                        Object.DestroyImmediate(image);
                        Debug.Log($"[Colony] capture {width}x{height} -> {path}");
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
                camera.aspect = previousAspect;
                camera.ResetAspect();
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
            ConfigureRendering(camera);
            // sun, trilight ambient and background: the arena profile from the colony layout
            ColonyEnvironmentBuilder.ApplyLighting(camera);
        }

        /// <summary>URP shadows and MSAA for the colony, and no post AA on its camera; does not move the camera.</summary>
        public static void ConfigureRendering(Camera camera)
        {
            EnsureRenderer();
            if (camera == null) return;
            // MSAA 4x (URP asset) only: SMAA on top of it cost a full-screen pass and blurred the thin wheat and fences
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.None;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
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
            // the colony camera is ~23 m from the field and the far edge of the frame (the ridge and the massif
            // over the waterfall) is ~55 m away: at URP's default 50 m their shadows cut off along a visible line
            serialized.FindProperty("m_ShadowDistance").floatValue = ShadowDistance;
            serialized.FindProperty("m_Cascade2Split").floatValue = 0.4f;
            serialized.FindProperty("m_MSAA").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }
    }
}
