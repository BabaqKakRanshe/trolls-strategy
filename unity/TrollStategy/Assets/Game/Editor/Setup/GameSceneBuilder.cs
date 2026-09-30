using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.Visuals;

namespace TrollStrategy.Editor.Setup
{
    public static class GameSceneBuilder
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private const string ColonyScenePath = "Assets/Game/Scenes/MainColonyScene.unity";

        private const string LandCornerPath = "Assets/Vitaria/Models/Colony/FX_Select_Corner.fbx";

        /// <summary>
        /// The land on the colony island (<see cref="LandPresenter"/>) on the scene's GameSystems, with the kit's
        /// corner piece for blocks for sale, wired into the bootstrap. Keeps an existing one.
        /// </summary>
        public static LandPresenter InstallLandPresenter(GameObject systems, GameBootstrap boot)
        {
            var land = systems.GetComponent<LandPresenter>();
            if (land == null) land = systems.AddComponent<LandPresenter>();
            var soLand = new SerializedObject(land);
            soLand.FindProperty("_corner").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(LandCornerPath);
            soLand.ApplyModifiedPropertiesWithoutUndo();
            if (boot != null)
            {
                var soBoot = new SerializedObject(boot);
                soBoot.FindProperty("_land").objectReferenceValue = land;
                soBoot.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(boot);
            }
            EditorUtility.SetDirty(land);
            return land;
        }

        [MenuItem("TrollStrategy/Isle/Install Land Presenter")]
        public static void InstallLandPresenterMenu()
        {
            var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var systems = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
            if (boot == null || systems == null)
                throw new InvalidOperationException("Open MainColonyScene: GameBootstrap or GameSystems is missing");
            InstallLandPresenter(systems.gameObject, boot);
            EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Debug.Log("[Isle] LandPresenter installed on " + systems.name);
        }

        [MenuItem("TrollStrategy/Setup Game Scene")]
        public static void BuildDefaultScene()
        {
            Debug.Log("[GameSceneBuilder] Starting full project & scene setup...");

            EnsureDirectories();
            try { AssetSlicer.SliceAll(); } catch (System.Exception ex) { Debug.LogWarning($"[GameSceneBuilder] SliceAll warning: {ex.Message}"); }
            try { ConfigureBuildingImports(); } catch (System.Exception ex) { Debug.LogWarning($"[GameSceneBuilder] ConfigureBuildingImports warning: {ex.Message}"); }

            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException($"Content catalog missing: {CatalogPath}");

            SetupScene(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameSceneBuilder] Setup successfully completed!");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = {
                "Assets/Game/Content/Definitions",
                "Assets/Game/Prefabs",
                "Assets/Game/Scenes"
            };
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
        }

        private static void ConfigureBuildingImports()
        {
            string[] buildingFiles = {
                "Assets/Game/Art/Sprites/Buildings/Market_01.png",
                "Assets/Game/Art/Sprites/Buildings/Mine_01.png",
                "Assets/Game/Art/Sprites/Buildings/Warehouse_01.png"
            };

            foreach (var p in buildingFiles)
            {
                var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                if (importer != null)
                {
                    if (importer.spriteImportMode != SpriteImportMode.Single || importer.spritePixelsPerUnit != 104f || importer.filterMode != FilterMode.Point)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = 104f;
                        importer.filterMode = FilterMode.Point;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static void SetupScene(GameContentCatalog catalog)
        {
            string scenePath = "Assets/Game/Scenes/MainColonyScene.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            // 1. Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 8.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColonyPalette.Night;
            cam.transform.position = new Vector3(9.7f, 7f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Physics2DRaycaster>();

            // 2. Grid & Environment
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var worldView = gridGo.AddComponent<TilemapWorldView>();
            worldView.Init(grid, null, null, catalog.Economy);
            // the ground, light and background come from the Vitaria kit: ThreeDSceneSetup.ApplyToOpenScene below
            // installs Colony_Meadow through ColonyEnvironmentBuilder.InstallIntoOpenScene

            // 3. Presentation Systems
            var managersGo = new GameObject("GameSystems");
            var bManager = managersGo.AddComponent<BuildingVisualsManager>();
            var uManager = managersGo.AddComponent<UnitVisualsManager>();
            var placementPreview = managersGo.AddComponent<PlacementPreviewRenderer>();
            var selectionBox = managersGo.AddComponent<SelectionBoxRenderer>();
            var routeVisualizer = managersGo.AddComponent<HaulRouteVisualizer>();
            var inputHandler = managersGo.AddComponent<MapInputHandler>();

            var bContainer = new GameObject("BuildingsContainer");
            bContainer.transform.SetParent(managersGo.transform);
            bManager.SetContainer(bContainer.transform);

            var soBm = new SerializedObject(bManager);
            soBm.FindProperty("_container").objectReferenceValue = bContainer.transform;
            soBm.FindProperty("_worldView").objectReferenceValue = worldView;
            soBm.FindProperty("_catalog").objectReferenceValue = catalog;
            soBm.ApplyModifiedPropertiesWithoutUndo();

            var soUm = new SerializedObject(uManager);
            soUm.FindProperty("_catalog").objectReferenceValue = catalog;
            soUm.ApplyModifiedPropertiesWithoutUndo();

            // 4. GameBootstrap with complete serialized wiring
            var bootGo = new GameObject("GameBootstrap");
            var boot = bootGo.AddComponent<GameBootstrap>();
            
            var soBoot = new SerializedObject(boot);
            soBoot.FindProperty("_catalog").objectReferenceValue = catalog;
            soBoot.FindProperty("_worldView").objectReferenceValue = worldView;
            soBoot.FindProperty("_camera").objectReferenceValue = cam;
            soBoot.FindProperty("_buildingManager").objectReferenceValue = bManager;
            soBoot.FindProperty("_unitManager").objectReferenceValue = uManager;
            soBoot.FindProperty("_placementPreview").objectReferenceValue = placementPreview;
            soBoot.FindProperty("_selectionBox").objectReferenceValue = selectionBox;
            soBoot.FindProperty("_routeVisualizer").objectReferenceValue = routeVisualizer;
            soBoot.FindProperty("_inputHandler").objectReferenceValue = inputHandler;
            soBoot.ApplyModifiedPropertiesWithoutUndo();
            InstallLandPresenter(managersGo, boot);

            // 5. UI Toolkit HUD and the event system its input goes through
            UiSetup.Install(boot);

            EditorUtility.SetDirty(boot);
            ThreeDSceneSetup.ApplyToOpenScene();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.OpenScene(scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            Debug.Log($"[GameSceneBuilder] Scene saved to {scenePath} and set as primary build scene.");
        }
    }
}
