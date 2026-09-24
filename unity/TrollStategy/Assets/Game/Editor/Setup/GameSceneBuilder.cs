using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.UI;

namespace TrollStrategy.Editor.Setup
{
    public static class GameSceneBuilder
    {
        private static TMP_FontAsset s_fontAsset;

        private static PrimitiveBuilding LoadBuildingModel(BuildingKind kind) =>
            AssetDatabase.LoadAssetAtPath<PrimitiveBuilding>($"Assets/Game/Prefabs/Buildings/{kind}Model.prefab");

        [MenuItem("TrollStrategy/Refresh HUD Only")]
        public static void RefreshHudOnly()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop play mode before refreshing the HUD.");
            var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (boot == null) throw new InvalidOperationException("Open the colony scene first.");
            var serialized = new SerializedObject(boot);
            var catalog = (GameContentCatalog)serialized.FindProperty("_catalog").objectReferenceValue;
            s_fontAsset = FontTester.CreateOrGetArial();
            var old = GameObject.Find("HUDCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var events = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (events != null) Undo.DestroyObjectImmediate(events.gameObject);
            var (hud, resources, roster, shop, commands, inspect, status) = CreateUIHierarchy(Camera.main, catalog);
            Undo.RegisterCreatedObjectUndo(hud.gameObject, "Refresh HUD");
            Undo.RecordObject(boot, "Reconnect HUD");
            serialized.FindProperty("_hudPresenter").objectReferenceValue = hud;
            serialized.FindProperty("_resourceBar").objectReferenceValue = resources;
            serialized.FindProperty("_unitRosterView").objectReferenceValue = roster;
            serialized.FindProperty("_shopDockView").objectReferenceValue = shop;
            serialized.FindProperty("_commandDockView").objectReferenceValue = commands;
            serialized.FindProperty("_inspectCardView").objectReferenceValue = inspect;
            serialized.FindProperty("_statusMessageView").objectReferenceValue = status;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            EditorSceneManager.SaveScene(boot.gameObject.scene);
        }

        [MenuItem("TrollStrategy/Setup Game Scene")]
        public static void BuildDefaultScene()
        {
            Debug.Log("[GameSceneBuilder] Starting full project & scene setup...");

            EnsureDirectories();
            s_fontAsset = FontTester.CreateOrGetArial();
            try { AssetSlicer.SliceAll(); } catch (System.Exception ex) { Debug.LogWarning($"[GameSceneBuilder] SliceAll warning: {ex.Message}"); }
            try { ConfigureBuildingImports(); } catch (System.Exception ex) { Debug.LogWarning($"[GameSceneBuilder] ConfigureBuildingImports warning: {ex.Message}"); }

            var catalog = CreateOrUpdateContentCatalog();
            var (buildingPrefab, unitPrefab) = CreatePrefabs(catalog);

            SetupScene(catalog, buildingPrefab, unitPrefab);

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

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static Sprite[] LoadSpriteFrames(string path)
        {
            var frames = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(sprite => GetFrameIndex(sprite.name))
                .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();

            if (frames.Length > 0)
                return frames;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? new[] { sprite } : Array.Empty<Sprite>();
        }

        private static int GetFrameIndex(string spriteName)
        {
            int separator = spriteName.LastIndexOf('_');
            return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), out int index)
                ? index
                : int.MaxValue;
        }

        private static GameContentCatalog CreateOrUpdateContentCatalog()
        {
            string catPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
            var catalog = GetOrCreateAsset<GameContentCatalog>(catPath);

            var economy = GetOrCreateAsset<EconomyConfig>("Assets/Game/Content/Definitions/EconomyConfig.asset");
            economy.Init(
                gridWidth: 14,
                gridHeight: 14,
                cellSize: 1f,
                startingGold: 1000,
                maxUnitsPerCell: 20,
                tickIntervalSeconds: 0.25f,
                oreSellPrice: 3,
                orePerStrengthSecond: 0.1f,
                transferTimeSeconds: 0.5f
            );
            EditorUtility.SetDirty(economy);

            var goblinIdleFrames = LoadSpriteFrames("Assets/Game/Art/Sprites/Units/goblin-idle.png");
            var goblinWalkFrames = LoadSpriteFrames("Assets/Game/Art/Sprites/Units/goblin-walk.png");
            var trollIdleFrames = LoadSpriteFrames("Assets/Game/Art/Sprites/Units/troll-idle.png");
            var trollWalkFrames = LoadSpriteFrames("Assets/Game/Art/Sprites/Units/troll-walk.png");

            var goblinIdle = goblinIdleFrames.FirstOrDefault();
            var trollIdle = trollIdleFrames.FirstOrDefault();

            var goblinDef = GetOrCreateAsset<UnitDefinition>("Assets/Game/Content/Definitions/Unit_Goblin.asset");
            goblinDef.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 10, "Быстрый рабочий и носильщик", goblinIdle, goblinIdleFrames, goblinWalkFrames);
            EditorUtility.SetDirty(goblinDef);

            var trollDef = GetOrCreateAsset<UnitDefinition>("Assets/Game/Content/Definitions/Unit_Troll.asset");
            trollDef.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 30, "Медленный, но очень сильный", trollIdle, trollIdleFrames, trollWalkFrames);
            EditorUtility.SetDirty(trollDef);

            var mineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Buildings/Mine_01.png");
            var whSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Buildings/Warehouse_01.png");
            var mktSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Buildings/Market_01.png");

            var mineDef = GetOrCreateAsset<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Mine.asset");
            mineDef.Init(BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, mineSprite);
            EditorUtility.SetDirty(mineDef);

            var whDef = GetOrCreateAsset<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Warehouse.asset");
            whDef.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, whSprite);
            EditorUtility.SetDirty(whDef);

            var mktDef = GetOrCreateAsset<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Market.asset");
            mktDef.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, mktSprite);
            EditorUtility.SetDirty(mktDef);

            var bksDef = GetOrCreateAsset<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Barracks.asset");
            bksDef.Init(BuildingKind.Barracks, "Бараки", 0, 3, 3, 0, 0, null);
            EditorUtility.SetDirty(bksDef);

            var soCat = new SerializedObject(catalog);
            soCat.FindProperty("_economy").objectReferenceValue = economy;
            var bProp = soCat.FindProperty("_buildings");
            bProp.ClearArray();
            var bDefs = new[] { mineDef, whDef, mktDef, bksDef };
            for (int i = 0; i < bDefs.Length; i++)
            {
                bProp.InsertArrayElementAtIndex(i);
                bProp.GetArrayElementAtIndex(i).objectReferenceValue = bDefs[i];
            }
            var uProp = soCat.FindProperty("_units");
            uProp.ClearArray();
            var uDefs = new[] { goblinDef, trollDef };
            for (int i = 0; i < uDefs.Length; i++)
            {
                uProp.InsertArrayElementAtIndex(i);
                uProp.GetArrayElementAtIndex(i).objectReferenceValue = uDefs[i];
            }
            soCat.ApplyModifiedProperties();

            catalog.Init(economy, uDefs, bDefs);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            return catalog;
        }

        private static (BuildingView, UnitView) CreatePrefabs(GameContentCatalog catalog)
        {
            string buildingPath = "Assets/Game/Prefabs/BuildingPrefab.prefab";
            var bGo = new GameObject("BuildingPrefab");
            var bView = bGo.AddComponent<BuildingView>();
            var bSr = bGo.AddComponent<SpriteRenderer>();
            bSr.sortingOrder = 10;
            var bCol = bGo.AddComponent<BoxCollider2D>();

            var hlGo = new GameObject("Highlight");
            hlGo.transform.SetParent(bGo.transform, false);
            var hlSr = hlGo.AddComponent<SpriteRenderer>();
            hlSr.sortingOrder = 14;
            hlSr.sprite = BuildingView.GetBoxOutlineSprite();
            hlSr.gameObject.SetActive(false);

            var pbGo = new GameObject("ProductionProgressFill");
            pbGo.transform.SetParent(bGo.transform, false);
            pbGo.transform.localPosition = new Vector3(0f, -1.74f, 0f);
            var pbSr = pbGo.AddComponent<SpriteRenderer>();
            pbSr.color = ColonyPalette.Gold;
            pbSr.sortingOrder = 13;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(bGo.transform, false);
            lblGo.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            var lblTmp = lblGo.AddComponent<TextMeshPro>();
            if (s_fontAsset != null) lblTmp.font = s_fontAsset;
            lblTmp.fontSize = 2.4f;
            lblTmp.alignment = TextAlignmentOptions.Center;
            lblTmp.sortingOrder = 15;

            var soB = new SerializedObject(bView);
            soB.FindProperty("_spriteRenderer").objectReferenceValue = bSr;
            soB.FindProperty("_selectionHighlight").objectReferenceValue = hlSr;
            soB.FindProperty("_progressBar").objectReferenceValue = pbSr;
            soB.FindProperty("_label").objectReferenceValue = lblTmp;
            soB.FindProperty("_collider").objectReferenceValue = bCol;
            soB.FindProperty("_mineModelPrefab").objectReferenceValue = LoadBuildingModel(BuildingKind.Mine);
            soB.FindProperty("_warehouseModelPrefab").objectReferenceValue = LoadBuildingModel(BuildingKind.Warehouse);
            soB.FindProperty("_marketModelPrefab").objectReferenceValue = LoadBuildingModel(BuildingKind.Market);
            soB.FindProperty("_barracksModelPrefab").objectReferenceValue = LoadBuildingModel(BuildingKind.Barracks);
            soB.ApplyModifiedPropertiesWithoutUndo();

            var bPrefab = PrefabUtility.SaveAsPrefabAsset(bGo, buildingPath).GetComponent<BuildingView>();
            UnityEngine.Object.DestroyImmediate(bGo);

            string unitPath = "Assets/Game/Prefabs/UnitPrefab.prefab";
            var uGo = new GameObject("UnitPrefab");
            var uView = uGo.AddComponent<UnitView>();
            var spriteGo = new GameObject("SpriteVisual");
            spriteGo.transform.SetParent(uGo.transform, false);
            var uSr = spriteGo.AddComponent<SpriteRenderer>();
            uSr.sortingOrder = 20;
            var uCol = uGo.AddComponent<CircleCollider2D>();
            uCol.radius = 0.35f;

            var scGo = new GameObject("SelectionCircle");
            scGo.transform.SetParent(uGo.transform, false);
            var scSr = scGo.AddComponent<SpriteRenderer>();
            scSr.color = ColonyPalette.WithAlpha(ColonyPalette.Gold, 0.8f);
            scSr.sortingOrder = 19;

            var cgGo = new GameObject("CargoIcon");
            cgGo.transform.SetParent(uGo.transform, false);
            cgGo.transform.localPosition = new Vector3(0.25f, 0.25f, 0f);
            var cgSr = cgGo.AddComponent<SpriteRenderer>();
            cgSr.sortingOrder = 22;

            var clGo = new GameObject("CargoLabel");
            clGo.transform.SetParent(cgGo.transform, false);
            clGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            var clTmp = clGo.AddComponent<TextMeshPro>();
            if (s_fontAsset != null) clTmp.font = s_fontAsset;
            clTmp.fontSize = 2f;
            clTmp.alignment = TextAlignmentOptions.Center;
            clTmp.sortingOrder = 23;

            var soU = new SerializedObject(uView);
            soU.FindProperty("_spriteRenderer").objectReferenceValue = uSr;
            soU.FindProperty("_selectionCircle").objectReferenceValue = scSr;
            soU.FindProperty("_cargoIcon").objectReferenceValue = cgSr;
            soU.FindProperty("_cargoLabel").objectReferenceValue = clTmp;
            soU.FindProperty("_collider").objectReferenceValue = uCol;
            soU.ApplyModifiedPropertiesWithoutUndo();

            var uPrefab = PrefabUtility.SaveAsPrefabAsset(uGo, unitPath).GetComponent<UnitView>();
            UnityEngine.Object.DestroyImmediate(uGo);

            return (bPrefab, uPrefab);
        }

        private static void SetupScene(GameContentCatalog catalog, BuildingView buildingPrefab, UnitView unitPrefab)
        {
            string scenePath = "Assets/Game/Scenes/MainColonyScene.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/Game/Content/Definitions/GameContentCatalog.asset");
            if (catalog == null || catalog.Buildings == null || catalog.Buildings.Count == 0 || catalog.Buildings[0] == null)
            {
                var cMineDef = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Mine.asset");
                var cWhDef = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Warehouse.asset");
                var cMktDef = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Market.asset");
                var cBksDef = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Barracks.asset");
                var cGoblinDef = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Game/Content/Definitions/Unit_Goblin.asset");
                var cTrollDef = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Game/Content/Definitions/Unit_Troll.asset");
                var cEconomy = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/Game/Content/Definitions/EconomyConfig.asset");

                if (catalog == null)
                {
                    catalog = GetOrCreateAsset<GameContentCatalog>("Assets/Game/Content/Definitions/GameContentCatalog.asset");
                }
                catalog.Init(cEconomy, new[] { cGoblinDef, cTrollDef }, new[] { cMineDef, cWhDef, cMktDef, cBksDef });
                EditorUtility.SetDirty(catalog);
            }

            if (buildingPrefab == null)
                buildingPrefab = AssetDatabase.LoadAssetAtPath<BuildingView>("Assets/Game/Prefabs/BuildingPrefab.prefab");
            if (unitPrefab == null)
                unitPrefab = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Game/Prefabs/UnitPrefab.prefab");

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

            EnvironmentBuilder.BuildEnvironment(gridGo.transform);

            // 3. Presentation Systems
            var managersGo = new GameObject("GameSystems");
            var bManager = managersGo.AddComponent<BuildingVisualsManager>();
            var uManager = managersGo.AddComponent<UnitVisualsManager>();
            var placementPreview = managersGo.AddComponent<PlacementPreviewRenderer>();
            var soPreview = new SerializedObject(placementPreview);
            soPreview.FindProperty("_mineModelPrefab").objectReferenceValue = LoadBuildingModel(BuildingKind.Mine);
            soPreview.ApplyModifiedPropertiesWithoutUndo();
            var selectionBox = managersGo.AddComponent<SelectionBoxRenderer>();
            var routeVisualizer = managersGo.AddComponent<HaulRouteVisualizer>();
            var inputHandler = managersGo.AddComponent<MapInputHandler>();

            var bContainer = new GameObject("BuildingsContainer");
            bContainer.transform.SetParent(managersGo.transform);
            bManager.SetContainer(bContainer.transform);

            var soBm = new SerializedObject(bManager);
            soBm.FindProperty("_buildingPrefab").objectReferenceValue = buildingPrefab;
            soBm.FindProperty("_container").objectReferenceValue = bContainer.transform;
            soBm.FindProperty("_worldView").objectReferenceValue = worldView;
            soBm.FindProperty("_catalog").objectReferenceValue = catalog;
            soBm.ApplyModifiedPropertiesWithoutUndo();

            var soUm = new SerializedObject(uManager);
            soUm.FindProperty("_unitPrefab").objectReferenceValue = unitPrefab;
            soUm.FindProperty("_catalog").objectReferenceValue = catalog;
            soUm.ApplyModifiedPropertiesWithoutUndo();

            var whDef = catalog.GetBuilding(BuildingKind.Warehouse);
            var mkDef = catalog.GetBuilding(BuildingKind.Market);

            var whPos = worldView.BuildingCenterWorld(new Cell(10, 8), 3, 3);
            var whGo = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab.gameObject, bContainer.transform);
            whGo.name = "Building_warehouse-1";
            whGo.transform.position = whPos;
            var whView = whGo.GetComponent<BuildingView>();
            var whSnap = new BuildingSnapshot("warehouse-1", BuildingKind.Warehouse, whDef.DisplayName,
                new Cell(10, 8), 3, 3, 0, 500, 0, 0, 0f);
            whView.Setup(whSnap, whDef.Sprite, null);

            var mkPos = worldView.BuildingCenterWorld(new Cell(10, 2), 3, 2);
            var mkGo = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab.gameObject, bContainer.transform);
            mkGo.name = "Building_market-1";
            mkGo.transform.position = mkPos;
            var mkView = mkGo.GetComponent<BuildingView>();
            var mkSnap = new BuildingSnapshot("market-1", BuildingKind.Market, mkDef.DisplayName,
                new Cell(10, 2), 3, 2, 0, 0, 0, 0, 0f);
            mkView.Setup(mkSnap, mkDef.Sprite, null);

            // 4. UI Canvas & HUD
            var (hudPresenter, resBar, unitRoster, shopDock, cmdDock, inspectCard, statusMsg) = CreateUIHierarchy(cam, catalog);

            // 5. GameBootstrap with complete serialized wiring
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
            soBoot.FindProperty("_buildingPrefab").objectReferenceValue = buildingPrefab;
            soBoot.FindProperty("_unitPrefab").objectReferenceValue = unitPrefab;
            soBoot.FindProperty("_hudPresenter").objectReferenceValue = hudPresenter;
            soBoot.FindProperty("_resourceBar").objectReferenceValue = resBar;
            soBoot.FindProperty("_unitRosterView").objectReferenceValue = unitRoster;
            soBoot.FindProperty("_shopDockView").objectReferenceValue = shopDock;
            soBoot.FindProperty("_commandDockView").objectReferenceValue = cmdDock;
            soBoot.FindProperty("_inspectCardView").objectReferenceValue = inspectCard;
            soBoot.FindProperty("_statusMessageView").objectReferenceValue = statusMsg;
            soBoot.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(boot);
            ThreeDSceneSetup.ApplyToOpenScene();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.OpenScene(scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            Debug.Log($"[GameSceneBuilder] Scene saved to {scenePath} and set as primary build scene.");
        }

        private static GameObject CreateUIGameObject(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static (HudPresenter, ResourceBarView, UnitRosterView, ShopDockView, CommandDockView, InspectCardView, StatusMessageView) CreateUIHierarchy(Camera cam, GameContentCatalog catalog)
        {
            var canvasGo = CreateUIGameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var text = ColonyPalette.Text;
            var panelColor = ColonyPalette.WithAlpha(ColonyPalette.Night, 0.94f);
            var resources = CreatePanel(canvasGo.transform, "ResourceCounters",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -12f), new Vector2(190f, 244f), panelColor);
            resources.GetComponent<Image>().raycastTarget = false;
            var resourceLayout = resources.AddComponent<VerticalLayoutGroup>();
            resourceLayout.childControlWidth = true;
            resourceLayout.childControlHeight = true;
            resourceLayout.childForceExpandWidth = false;
            resourceLayout.childForceExpandHeight = false;
            resourceLayout.padding = new RectOffset(6, 6, 6, 6);
            resourceLayout.spacing = 4f;
            var (_, goldVal) = CreateResourceCell(resources.transform, "ЗОЛОТО", "1000", text);
            var (_, oreVal) = CreateResourceCell(resources.transform, "РУДА", "0", text);
            var (_, popVal) = CreateResourceCell(resources.transform, "НАСЕЛЕНИЕ", "0", text);
            var (_, soldVal) = CreateResourceCell(resources.transform, "ПРОДАНО", "0", text);

            var toggleButton = CreateButton(canvasGo.transform, "CatalogToggleButton", "КАТАЛОГ  ◀", new Vector2(164f, 48f));
            var toggleRect = toggleButton.GetComponent<RectTransform>();
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 1f);
            toggleRect.pivot = new Vector2(1f, 1f);
            toggleRect.anchoredPosition = new Vector2(-12f, -12f);

            var shopPanel = CreatePanel(canvasGo.transform, "CatalogDrawer",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(372f, -68f), new Vector2(360f, 432f), panelColor);
            var shopLayout = shopPanel.AddComponent<VerticalLayoutGroup>();
            shopLayout.childControlWidth = true;
            shopLayout.childControlHeight = true;
            shopLayout.childForceExpandWidth = false;
            shopLayout.childForceExpandHeight = false;
            shopLayout.padding = new RectOffset(10, 10, 10, 10);
            shopLayout.spacing = 8f;

            var tabs = CreateUIGameObject("CatalogCategories", shopPanel.transform);
            tabs.AddComponent<LayoutElement>().preferredHeight = 44f;
            var tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = false;
            tabLayout.spacing = 8f;
            var creaturesButton = CreateButton(tabs.transform, "CreaturesButton", "СУЩЕСТВА", new Vector2(160f, 42f));
            var buildingsButton = CreateButton(tabs.transform, "BuildingsButton", "ЗДАНИЯ", new Vector2(160f, 42f));

            var creaturesPage = CreateCatalogPage(shopPanel.transform, "CreaturesPage");
            var buildingsPage = CreateCatalogPage(shopPanel.transform, "BuildingsPage");
            buildingsPage.SetActive(false);

            var mineDef = catalog.GetBuilding(BuildingKind.Mine);
            var (_, mineArt, _, _, buildMineBtn, mineCostTxt) =
                CreateShopCard(buildingsPage.transform, "Mine", mineDef.Sprite, "ШАХТА",
                    $"Добывает руду\n{mineDef.Width}×{mineDef.Height} · до {mineDef.MaxWorkers} рабочих", "ПОСТРОИТЬ", mineDef.Price);
            var autoPlaceBtn = CreateButton(buildingsPage.transform, "AutoPlaceBtn", "ПОСТАВИТЬ АВТОМАТИЧЕСКИ", new Vector2(330f, 38f));
            autoPlaceBtn.GetComponent<LayoutElement>().preferredHeight = 38f;
            autoPlaceBtn.gameObject.SetActive(false);

            var stepper = CreateUIGameObject("HireStepper", creaturesPage.transform);
            stepper.AddComponent<LayoutElement>().preferredHeight = 42f;
            var stepperLayout = stepper.AddComponent<HorizontalLayoutGroup>();
            stepperLayout.childControlWidth = true;
            stepperLayout.childControlHeight = true;
            stepperLayout.childForceExpandWidth = false;
            stepperLayout.childForceExpandHeight = false;
            stepperLayout.childAlignment = TextAnchor.MiddleRight;
            stepperLayout.spacing = 6f;
            var amountLabel = CreateText(stepper.transform, "AmountLabel", "В группе:", 14, text);
            amountLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var decHireBtn = CreateButton(stepper.transform, "DecHireBtn", "−", new Vector2(38f, 36f));
            var hireAmtTxt = CreateText(stepper.transform, "HireAmountTxt", "1", 16, text);
            hireAmtTxt.alignment = TextAlignmentOptions.Center;
            hireAmtTxt.fontStyle = FontStyles.Bold;
            var amountElement = hireAmtTxt.gameObject.AddComponent<LayoutElement>();
            amountElement.preferredWidth = 42f;
            amountElement.preferredHeight = 36f;
            var incHireBtn = CreateButton(stepper.transform, "IncHireBtn", "+", new Vector2(38f, 36f));

            var goblinDef = catalog.GetUnit(UnitKind.Goblin);
            var trollDef = catalog.GetUnit(UnitKind.Troll);
            var (_, goblinArt, _, _, buyGoblinBtn, goblinCostTxt) =
                CreateShopCard(creaturesPage.transform, "Goblin", goblinDef.PortraitSprite, "ГОБЛИН",
                    $"Сила {goblinDef.Strength} · скорость {goblinDef.Speed}\nГруз {goblinDef.CargoCapacity}", "НАНЯТЬ", goblinDef.Price);
            var (_, trollArt, _, _, buyTrollBtn, trollCostTxt) =
                CreateShopCard(creaturesPage.transform, "Troll", trollDef.PortraitSprite, "ТРОЛЛЬ",
                    $"Сила {trollDef.Strength} · скорость {trollDef.Speed}\nГруз {trollDef.CargoCapacity}", "НАНЯТЬ", trollDef.Price);

            var shopDock = shopPanel.AddComponent<ShopDockView>();
            shopDock.Setup(null, null, goldVal, oreVal, soldVal, popVal,
                mineArt, buildMineBtn, mineCostTxt, autoPlaceBtn,
                decHireBtn, incHireBtn, hireAmtTxt,
                goblinArt, buyGoblinBtn, goblinCostTxt,
                trollArt, buyTrollBtn, trollCostTxt,
                null, null, null, null);
            shopDock.SetupDrawer(toggleButton, creaturesButton, buildingsButton,
                creaturesPage, buildingsPage);

            var presenter = canvasGo.AddComponent<HudPresenter>();
            return (presenter, null, null, shopDock, null, null, null);
        }

        private static GameObject CreateCatalogPage(Transform parent, string name)
        {
            var page = CreateUIGameObject(name, parent);
            page.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var layout = page.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 8f;
            return page;
        }

        private static void MakeScrollable(GameObject content, bool roster)
        {
            var parent = content.transform.parent;
            int sibling = content.transform.GetSiblingIndex();
            var original = content.GetComponent<RectTransform>();
            var viewport = CreateUIGameObject(content.name + "Viewport", parent);
            var rect = viewport.GetComponent<RectTransform>();
            rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax;
            rect.pivot = original.pivot; rect.sizeDelta = original.sizeDelta;
            rect.anchoredPosition = original.anchoredPosition;
            viewport.transform.SetSiblingIndex(sibling);
            if (roster) viewport.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var background = viewport.AddComponent<Image>();
            background.color = ColonyPalette.WithAlpha(ColonyPalette.Night, 0.98f);
            viewport.AddComponent<RectMask2D>();
            content.transform.SetParent(viewport.transform, false);
            original.anchorMin = new Vector2(0, 1); original.anchorMax = Vector2.one;
            original.pivot = new Vector2(0.5f, 1); original.sizeDelta = Vector2.zero;
            original.anchoredPosition = Vector2.zero;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = rect; scroll.content = original;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        private static GameObject CreateObjectiveRow(Transform parent, string index, string title, string subtitle, Color textColor, Color mutedColor)
        {
            var row = CreatePanel(parent, $"Objective_{index}",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 66f), ColonyPalette.Night);
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 66f;
            rowElement.flexibleHeight = 0f;
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(12, 10, 8, 8);
            layout.spacing = 10f;
            var number = CreateText(row.transform, "Index", index, 12, mutedColor);
            number.alignment = TextAlignmentOptions.Center;
            number.rectTransform.sizeDelta = new Vector2(28f, 28f);
            var copy = CreateUIGameObject("Copy", row.transform);
            copy.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var copyLayout = copy.AddComponent<VerticalLayoutGroup>();
            copyLayout.childControlWidth = true;
            copyLayout.childControlHeight = true;
            copyLayout.childForceExpandHeight = false;
            copyLayout.childForceExpandWidth = false;
            copyLayout.childAlignment = TextAnchor.MiddleLeft;
            copyLayout.spacing = 2f;
            var titleText = CreateText(copy.transform, "Title", title, 12, textColor);
            titleText.fontStyle = FontStyles.Bold;
            CreateText(copy.transform, "Subtitle", subtitle, 10, mutedColor);
            return row;
        }

        private static (GameObject cell, TextMeshProUGUI val) CreateResourceCell(Transform parent, string label, string initVal, Color valColor)
        {
            var go = CreateUIGameObject($"Res_{label}", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(190f, 50f);
            var cellElement = go.AddComponent<LayoutElement>();
            cellElement.preferredWidth = 190f;
            cellElement.preferredHeight = 52f;
            cellElement.flexibleWidth = 0f;
            cellElement.flexibleHeight = 0f;

            var bg = go.AddComponent<Image>();
            bg.color = ColonyPalette.Panel;
            bg.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ColonyPalette.Stone;
            outline.effectDistance = new Vector2(1f, -1f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(2, 2, 3, 3);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            var lbl = CreateText(go.transform, "Lbl", label, 10, ColonyPalette.MutedText);
            lbl.alignment = TextAlignmentOptions.Left;
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            var val = CreateText(go.transform, "Val", initVal, 16, valColor);
            val.alignment = TextAlignmentOptions.Left;
            val.fontStyle = FontStyles.Bold;
            val.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            return (go, val);
        }

        private static GameObject CreateSectionHeader(Transform parent, string title, string sub)
        {
            var go = CreateUIGameObject($"SectionHeader_{title}", parent);
            var sectionElement = go.AddComponent<LayoutElement>();
            sectionElement.preferredHeight = 34f;
            sectionElement.flexibleHeight = 0f;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 8f;

            var tTitle = CreateText(go.transform, "Title", title, 12, ColonyPalette.Text);
            tTitle.fontStyle = FontStyles.Bold;
            tTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            if (!string.IsNullOrEmpty(sub))
            {
                var tSub = CreateText(go.transform, "Sub", sub, 10, ColonyPalette.MutedText);
                tSub.alignment = TextAlignmentOptions.Right;
            }
            return go;
        }

        private static (GameObject card, Image artImg, TextMeshProUGUI titleTxt, TextMeshProUGUI subTxt, Button buyBtn, TextMeshProUGUI costTxt)
            CreateShopCard(Transform parent, string name, Sprite sprite, string title, string subtitle, string btnPrefix, int initialCost)
        {
            var card = CreatePanel(parent, $"Card_{name}",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(330f, 106f), ColonyPalette.Night);
            var cardElement = card.AddComponent<LayoutElement>();
            cardElement.preferredHeight = 106f;
            cardElement.flexibleHeight = 0f;

            var hlg = card.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(8, 8, 8, 8);

            // 50x50 Art Box
            var artBox = CreateUIGameObject("ArtBox", card.transform);
            var artRt = artBox.GetComponent<RectTransform>();
            artRt.sizeDelta = new Vector2(48f, 58f);
            var artElement = artBox.AddComponent<LayoutElement>();
            artElement.minWidth = artElement.preferredWidth = 48f;
            artElement.preferredHeight = 58f;
            var artBg = artBox.AddComponent<Image>();
            artBg.color = ColonyPalette.Raised;
            artBg.raycastTarget = false;
            var artOutline = artBox.AddComponent<Outline>();
            artOutline.effectColor = ColonyPalette.Stone;
            artOutline.effectDistance = new Vector2(1f, -1f);

            var imgGo = CreateUIGameObject("Sprite", artBox.transform);
            var imgRt = imgGo.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.sizeDelta = new Vector2(-4f, -4f);
            var img = imgGo.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            else img.enabled = false;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Text column
            var textCol = CreateUIGameObject("TextCol", card.transform);
            var tcRt = textCol.GetComponent<RectTransform>();
            tcRt.sizeDelta = new Vector2(140f, 80f);
            var textElement = textCol.AddComponent<LayoutElement>();
            textElement.minWidth = 0;
            textElement.flexibleWidth = 1;
            var tcVlg = textCol.AddComponent<VerticalLayoutGroup>();
            tcVlg.childControlWidth = true;
            tcVlg.childControlHeight = true;
            tcVlg.childForceExpandHeight = false;
            tcVlg.childForceExpandWidth = false;
            tcVlg.childAlignment = TextAnchor.MiddleLeft;
            tcVlg.spacing = 2f;

            var tTitle = CreateText(textCol.transform, "Title", title, 14, ColonyPalette.Text);
            tTitle.fontStyle = FontStyles.Bold;
            var tSub = CreateText(textCol.transform, "Sub", subtitle, 10, ColonyPalette.MutedText);

            // Buy Button
            var btn = CreateButton(card.transform, "BuyBtn", $"{btnPrefix}\n{initialCost}", new Vector2(104f, 66f));
            var costTxt = btn.GetComponentInChildren<TextMeshProUGUI>();
            costTxt.fontSize = 14;
            costTxt.alignment = TextAlignmentOptions.Center;

            return (card, img, tTitle, tSub, btn, costTxt);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = CreateUIGameObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ColonyPalette.Stone;
            outline.effectDistance = new Vector2(1f, -1f);
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            var go = CreateUIGameObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var buttonSize = go.AddComponent<LayoutElement>();
            buttonSize.minWidth = buttonSize.preferredWidth = size.x;
            buttonSize.minHeight = buttonSize.preferredHeight = size.y;

            var img = go.AddComponent<Image>();
            img.color = ColonyPalette.Raised;
            img.raycastTarget = true;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ColonyPalette.PaleStone;
            outline.effectDistance = new Vector2(1f, -1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = ColonyPalette.PaleStone;
            colors.disabledColor = ColonyPalette.WithAlpha(ColonyPalette.Slate, 0.45f);
            btn.colors = colors;

            var txtGo = CreateUIGameObject("Text", go.transform);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = ColonyPalette.Text;
            tmp.raycastTarget = false;
            if (s_fontAsset != null) tmp.font = s_fontAsset;

            return btn;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = CreateUIGameObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = Mathf.Max(14, fontSize);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.color = color.grayscale < 0.75f ? ColonyPalette.MutedText : color;
            tmp.raycastTarget = false;
            if (s_fontAsset != null) tmp.font = s_fontAsset;
            return tmp;
        }
    }
}
