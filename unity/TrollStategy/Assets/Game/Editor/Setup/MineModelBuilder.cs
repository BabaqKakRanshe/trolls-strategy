using System.IO;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TrollStrategy.Editor.Setup
{
    public static class MineModelBuilder
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        public static void RebuildAndCaptureKitPreview()
        {
            ThreeDSceneSetup.RefreshDiorama();
            CaptureKitPreview();
        }

        public static void CaptureKitPreview()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/MainColonyScene.unity");
            var camera = Camera.main;
            var world = Object.FindAnyObjectByType<TilemapWorldView>();
            if (camera == null || world == null)
                throw new System.InvalidOperationException("Kit preview needs the colony camera and map.");

            var buildings = new[]
            {
                PreviewBuilding(world, BuildingKind.Mine, new Cell(3, 7)),
                PreviewBuilding(world, BuildingKind.Barracks, new Cell(3, 2)),
                PreviewBuilding(world, BuildingKind.Warehouse, new Cell(10, 8)),
                PreviewBuilding(world, BuildingKind.Market, new Cell(10, 2), 2)
            };
            var target = new RenderTexture(1600, 900, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                // Batch render can expose shader/texture initialization in its first frames.
                for (var frame = 0; frame < 4; frame++)
                    camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes("../vitaria-preview.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);

                var canvas = GameObject.Find("HUDCanvas")?.GetComponent<Canvas>();
                var drawer = GameObject.Find("CatalogDrawer")?.GetComponent<RectTransform>();
                if (canvas != null && drawer != null)
                {
                    var previousMode = canvas.renderMode;
                    var previousCamera = canvas.worldCamera;
                    var previousPosition = drawer.anchoredPosition;
                    try
                    {
                        drawer.anchoredPosition = new Vector2(-22f, previousPosition.y);
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = 1f;
                        Canvas.ForceUpdateCanvases();
                        for (var frame = 0; frame < 4; frame++) camera.Render();
                        RenderTexture.active = target;
                        var hudImage = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                        hudImage.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        hudImage.Apply();
                        File.WriteAllBytes("../vitaria-hud-preview.png", hudImage.EncodeToPNG());
                        Object.DestroyImmediate(hudImage);
                    }
                    finally
                    {
                        canvas.renderMode = previousMode;
                        canvas.worldCamera = previousCamera;
                        drawer.anchoredPosition = previousPosition;
                    }
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(target);
                foreach (var building in buildings) Object.DestroyImmediate(building);
            }
        }

        private static GameObject BuildingPrefab(BuildingKind kind)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            var source = catalog != null ? catalog.GetBuilding(kind).Prefab : null;
            if (source == null) throw new System.InvalidOperationException($"Missing {kind} building prefab");
            return source;
        }

        private static GameObject PreviewBuilding(TilemapWorldView world, BuildingKind kind, Cell cell, int height = 3)
        {
            var source = BuildingPrefab(kind);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.position = world.BuildingCenterWorld(cell, 3, height);
            instance.transform.rotation = world.GroundRotation;
            return instance;
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/MainColonyScene.unity");
            var camera = Camera.main;
            var world = Object.FindAnyObjectByType<TilemapWorldView>();
            var prefab = BuildingPrefab(BuildingKind.Mine);
            if (camera == null || world == null)
                throw new System.InvalidOperationException("Mine preview needs the colony camera and map.");

            var mine = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            mine.transform.position = world.BuildingCenterWorld(new Cell(5, 5), 3, 3);
            mine.transform.rotation = world.GroundRotation;
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
                File.WriteAllBytes("../mine-preview.png", image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(mine);
            }
        }
    }
}
