using System;
using System.IO;
using System.Collections.Generic;
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

        [MenuItem("TrollStrategy/Setup Game Scene")]
        public static void BuildDefaultScene()
        {
            Debug.Log("[GameSceneBuilder] Starting full project & scene setup...");

            EnsureDirectories();
            s_fontAsset = FontTester.CreateOrGetArial();
            AssetSlicer.SliceAll();
            ConfigureBuildingImports();

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
                "Assets/Game/Scenes",
                "Assets/Game/Resources"
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

            var goblinIdle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Units/goblin-idle.png");
            var goblinWalk = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Units/goblin-walk.png");
            var trollIdle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Units/troll-idle.png");
            var trollWalk = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Units/troll-walk.png");

            var goblinDef = GetOrCreateAsset<UnitDefinition>("Assets/Game/Content/Definitions/Unit_Goblin.asset");
            goblinDef.Init(UnitKind.Goblin, "Гоблин", 25, 1, 3, 12, goblinIdle, new[] { goblinIdle }, new[] { goblinWalk });
            EditorUtility.SetDirty(goblinDef);

            var trollDef = GetOrCreateAsset<UnitDefinition>("Assets/Game/Content/Definitions/Unit_Troll.asset");
            trollDef.Init(UnitKind.Troll, "Тролль", 50, 3, 6, 8, trollIdle, new[] { trollIdle }, new[] { trollWalk });
            EditorUtility.SetDirty(trollDef);

            var mineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Buildings/Mine_01.png");
            var whSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Buildings/Warehouse_01.png");
            var mktSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Buildings/Market_01.png");

            var mineDef = GetOrCreateAsset<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Mine.asset");
            mineDef.Init(BuildingKind.Mine, "Шахта", 100, 2, 2, 3, 50, mineSprite);
            EditorUtility.SetDirty(mineDef);

            var whDef = GetOrCreateAsset<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Warehouse.asset");
            whDef.Init(BuildingKind.Warehouse, "Склад", 100, 3, 3, 500, 200, whSprite);
            EditorUtility.SetDirty(whDef);

            var mktDef = GetOrCreateAsset<BuildingDefinition>("Assets/Game/Content/Definitions/Building_Market.asset");
            mktDef.Init(BuildingKind.Market, "Рынок", 150, 3, 2, 0, 0, mktSprite);
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

            string resCatPath = "Assets/Game/Resources/GameContentCatalog.asset";
            if (File.Exists(resCatPath))
            {
                AssetDatabase.DeleteAsset(resCatPath);
            }
            AssetDatabase.CopyAsset(catPath, resCatPath);
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

            var pbGo = new GameObject("ProgressBar");
            pbGo.transform.SetParent(bGo.transform, false);
            pbGo.transform.localPosition = new Vector3(0f, -1.35f, 0f);
            var pbSr = pbGo.AddComponent<SpriteRenderer>();
            pbSr.color = new Color(1f, 0.65f, 0.1f, 0.9f);
            pbSr.sortingOrder = 12;

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
            soB.ApplyModifiedPropertiesWithoutUndo();

            var bPrefab = PrefabUtility.SaveAsPrefabAsset(bGo, buildingPath).GetComponent<BuildingView>();
            string resBldPath = "Assets/Game/Resources/BuildingPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(bGo, resBldPath);
            UnityEngine.Object.DestroyImmediate(bGo);

            string unitPath = "Assets/Game/Prefabs/UnitPrefab.prefab";
            var uGo = new GameObject("UnitPrefab");
            var uView = uGo.AddComponent<UnitView>();
            var uSr = uGo.AddComponent<SpriteRenderer>();
            uSr.sortingOrder = 20;
            var uCol = uGo.AddComponent<CircleCollider2D>();
            uCol.radius = 0.35f;

            var scGo = new GameObject("SelectionCircle");
            scGo.transform.SetParent(uGo.transform, false);
            var scSr = scGo.AddComponent<SpriteRenderer>();
            scSr.color = new Color(1f, 0.9f, 0.2f, 0.8f);
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
            string resUnitPath = "Assets/Game/Resources/UnitPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(uGo, resUnitPath);
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
            cam.backgroundColor = new Color(0.04f, 0.08f, 0.08f);
            cam.transform.position = new Vector3(7f, 7f, -10f);
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
            var whSnap = new BuildingSnapshot { Id = "warehouse-1", Kind = BuildingKind.Warehouse, Name = whDef.DisplayName, Cell = new Cell(10, 8), Width = 3, Height = 3, Ore = 0, MaxOre = 500 };
            whView.Setup(whSnap, whDef.Sprite, null);

            var mkPos = worldView.BuildingCenterWorld(new Cell(10, 2), 3, 2);
            var mkGo = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab.gameObject, bContainer.transform);
            mkGo.name = "Building_market-1";
            mkGo.transform.position = mkPos;
            var mkView = mkGo.GetComponent<BuildingView>();
            var mkSnap = new BuildingSnapshot { Id = "market-1", Kind = BuildingKind.Market, Name = mkDef.DisplayName, Cell = new Cell(10, 2), Width = 3, Height = 2, Ore = 0, MaxOre = 0 };
            mkView.Setup(mkSnap, mkDef.Sprite, null);

            // 4. UI Canvas & HUD
            var (hudPresenter, resBar, shopDock, cmdDock, inspectCard, statusMsg) = CreateUIHierarchy(cam, catalog);

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
            soBoot.FindProperty("_shopDockView").objectReferenceValue = shopDock;
            soBoot.FindProperty("_commandDockView").objectReferenceValue = cmdDock;
            soBoot.FindProperty("_inspectCardView").objectReferenceValue = inspectCard;
            soBoot.FindProperty("_statusMessageView").objectReferenceValue = statusMsg;
            soBoot.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(boot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.OpenScene(scenePath);
            Debug.Log($"[GameSceneBuilder] Scene saved to {scenePath}");
        }

        private static GameObject CreateUIGameObject(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static (HudPresenter, ResourceBarView, ShopDockView, CommandDockView, InspectCardView, StatusMessageView) CreateUIHierarchy(Camera cam, GameContentCatalog catalog)
        {
            var canvasGo = CreateUIGameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var hudPresenter = canvasGo.AddComponent<HudPresenter>();

            // --- 1. TOP-LEFT STATS PANEL (ResourceBar) ---
            var statsPanel = CreatePanel(canvasGo.transform, "ResourceBarPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(420f, 68f), new Color(0.06f, 0.1f, 0.08f, 0.92f));

            var statsHlg = statsPanel.AddComponent<HorizontalLayoutGroup>();
            statsHlg.childAlignment = TextAnchor.MiddleCenter;
            statsHlg.spacing = 16f;
            statsHlg.padding = new RectOffset(14, 14, 8, 8);

            var (goldBox, goldVal) = CreateStatColumn(statsPanel.transform, "GOLD", "100", new Color(1f, 0.85f, 0.2f));
            var (minionsBox, minionsVal) = CreateStatColumn(statsPanel.transform, "MINIONS", "0", new Color(0.3f, 0.95f, 0.8f));
            var (oreBox, oreVal) = CreateStatColumn(statsPanel.transform, "RESOURCES", "0", new Color(0.9f, 0.65f, 0.35f));

            var resBar = statsPanel.AddComponent<ResourceBarView>();
            resBar.Setup(goldVal, minionsVal, oreVal);

            // --- 2. TOP-CENTER STATUS MESSAGES (StatusMessageView) ---
            var promptPanel = CreatePanel(canvasGo.transform, "ModePromptPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(480f, 44f), new Color(0.12f, 0.2f, 0.15f, 0.95f));

            var ppHlg = promptPanel.AddComponent<HorizontalLayoutGroup>();
            ppHlg.childAlignment = TextAnchor.MiddleCenter;
            ppHlg.spacing = 12f;
            ppHlg.padding = new RectOffset(14, 14, 6, 6);

            var promptTxt = CreateText(promptPanel.transform, "PromptText", "Режим команды", 13, new Color(0.95f, 0.9f, 0.8f));
            var ppLe = promptTxt.gameObject.AddComponent<LayoutElement>();
            ppLe.flexibleWidth = 1f;

            var cancelModeBtn = CreateButton(promptPanel.transform, "CancelModeBtn", "Отмена", new Vector2(80f, 32f));
            promptPanel.SetActive(false);

            var normTxt = CreateText(canvasGo.transform, "NormalStatusText", "Постройте шахту и наймите рабочих.", 13, new Color(0.9f, 0.85f, 0.75f));
            normTxt.alignment = TextAlignmentOptions.Center;
            var ntRt = normTxt.GetComponent<RectTransform>();
            ntRt.anchorMin = new Vector2(0.5f, 1f);
            ntRt.anchorMax = new Vector2(0.5f, 1f);
            ntRt.pivot = new Vector2(0.5f, 1f);
            ntRt.anchoredPosition = new Vector2(0f, -24f);
            ntRt.sizeDelta = new Vector2(600f, 30f);

            var statusMsg = canvasGo.AddComponent<StatusMessageView>();
            statusMsg.Setup(null, promptPanel, promptTxt, cancelModeBtn, normTxt);

            // --- 3. RIGHT SHOP DOCK (ShopDockView) ---
            var shopContainer = CreateUIGameObject("ShopContainer", canvasGo.transform);
            var scRt = shopContainer.GetComponent<RectTransform>();
            scRt.anchorMin = new Vector2(1f, 0.5f);
            scRt.anchorMax = new Vector2(1f, 0.5f);
            scRt.pivot = new Vector2(1f, 0.5f);
            scRt.anchoredPosition = new Vector2(-16f, 0f);
            scRt.sizeDelta = new Vector2(400f, 380f);

            // Tabs panel on the far right
            var tabsPanel = CreatePanel(shopContainer.transform, "CategoryTabs",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 0f), new Vector2(114f, 180f), new Color(0.06f, 0.1f, 0.08f, 0.95f));

            var tabsVlg = tabsPanel.AddComponent<VerticalLayoutGroup>();
            tabsVlg.childAlignment = TextAnchor.MiddleCenter;
            tabsVlg.spacing = 10f;
            tabsVlg.padding = new RectOffset(8, 8, 8, 8);

            var minionsTab = CreateButton(tabsPanel.transform, "Tab_Minions", "Миньоны", new Vector2(98f, 44f));
            var buildingsTab = CreateButton(tabsPanel.transform, "Tab_Buildings", "Постройки", new Vector2(98f, 44f));
            var decorationsTab = CreateButton(tabsPanel.transform, "Tab_Decorations", "Декорации", new Vector2(98f, 44f));

            // Drawer panel to the left of the tabs
            var drawerPanel = CreatePanel(shopContainer.transform, "ShopDrawer",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-120f, 0f), new Vector2(230f, 260f), new Color(0.08f, 0.14f, 0.12f, 0.96f));

            var drawerVlg = drawerPanel.AddComponent<VerticalLayoutGroup>();
            drawerVlg.childAlignment = TextAnchor.UpperCenter;
            drawerVlg.spacing = 10f;
            drawerVlg.padding = new RectOffset(12, 12, 12, 12);

            var drawerTitle = CreateText(drawerPanel.transform, "DrawerTitle", "МИНЬОНЫ", 15, new Color(0.9f, 0.75f, 0.4f));
            drawerTitle.alignment = TextAlignmentOptions.Center;
            drawerTitle.fontStyle = FontStyles.Bold;
            var dtLe = drawerTitle.gameObject.AddComponent<LayoutElement>();
            dtLe.preferredHeight = 24f;

            // Minions content
            var minionsContent = CreateUIGameObject("MinionsContent", drawerPanel.transform);
            var mcVlg = minionsContent.AddComponent<VerticalLayoutGroup>();
            mcVlg.spacing = 10f;
            var mcRt = minionsContent.GetComponent<RectTransform>();
            mcRt.sizeDelta = new Vector2(206f, 180f);

            var buyGoblinBtn = CreateButton(minionsContent.transform, "BuyGoblinBtn", "Гоблин (25G)", new Vector2(206f, 42f));
            var goblinCostTxt = buyGoblinBtn.GetComponentInChildren<TextMeshProUGUI>();

            var buyTrollBtn = CreateButton(minionsContent.transform, "BuyTrollBtn", "Тролль (50G)", new Vector2(206f, 42f));
            var trollCostTxt = buyTrollBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Buildings content
            var buildingsContent = CreateUIGameObject("BuildingsContent", drawerPanel.transform);
            var bcVlg = buildingsContent.AddComponent<VerticalLayoutGroup>();
            bcVlg.spacing = 10f;
            var bcRt = buildingsContent.GetComponent<RectTransform>();
            bcRt.sizeDelta = new Vector2(206f, 180f);

            var buildMineBtn = CreateButton(buildingsContent.transform, "BuildMineBtn", "Шахта (100G)", new Vector2(206f, 42f));
            var mineCostTxt = buildMineBtn.GetComponentInChildren<TextMeshProUGUI>();

            var autoPlaceMineBtn = CreateButton(buildingsContent.transform, "AutoPlaceMineBtn", "Авто-размещение", new Vector2(206f, 38f));

            // Decorations content
            var decorationsContent = CreateUIGameObject("DecorationsContent", drawerPanel.transform);
            var decRt = decorationsContent.GetComponent<RectTransform>();
            decRt.sizeDelta = new Vector2(206f, 180f);
            var decTxt = CreateText(decorationsContent.transform, "EmptyTxt", "Пока пусто", 13, new Color(0.6f, 0.75f, 0.7f));
            decTxt.alignment = TextAlignmentOptions.Center;

            drawerPanel.SetActive(false);

            var shopDock = shopContainer.AddComponent<ShopDockView>();
            shopDock.Setup(
                null, null,
                minionsTab, buildingsTab, decorationsTab,
                drawerPanel, drawerTitle,
                minionsContent, buyGoblinBtn, buyTrollBtn, goblinCostTxt, trollCostTxt,
                buildingsContent, buildMineBtn, autoPlaceMineBtn, mineCostTxt,
                decorationsContent
            );

            // --- 4. BOTTOM SELECTION / COMMAND DOCK (CommandDockView) ---
            var selectionPanel = CreatePanel(canvasGo.transform, "SelectionCommandDock",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(580f, 84f), new Color(0.06f, 0.1f, 0.08f, 0.95f));

            var spVlg = selectionPanel.AddComponent<VerticalLayoutGroup>();
            spVlg.childAlignment = TextAnchor.MiddleCenter;
            spVlg.spacing = 6f;
            spVlg.padding = new RectOffset(14, 14, 8, 8);

            var selCountTxt = CreateText(selectionPanel.transform, "SelectedCount", "Выбрано: 0", 13, Color.yellow);
            selCountTxt.alignment = TextAlignmentOptions.Center;
            var scLe = selCountTxt.gameObject.AddComponent<LayoutElement>();
            scLe.preferredHeight = 18f;

            // Main row (collapsed commands)
            var mainRow = CreateUIGameObject("MainActionsRow", selectionPanel.transform);
            var mrHlg = mainRow.AddComponent<HorizontalLayoutGroup>();
            mrHlg.childAlignment = TextAnchor.MiddleCenter;
            mrHlg.spacing = 14f;
            var mrRt = mainRow.GetComponent<RectTransform>();
            mrRt.sizeDelta = new Vector2(550f, 40f);

            var openCmdsBtn = CreateButton(mainRow.transform, "OpenCommandsBtn", "Команды", new Vector2(130f, 38f));
            var sellSelectedBtn = CreateButton(mainRow.transform, "SellSelectedBtn", "Продать (50%)", new Vector2(130f, 38f));
            var clearSelBtn = CreateButton(mainRow.transform, "ClearSelBtn", "Снять выбор", new Vector2(130f, 38f));

            // Commands row (expanded commands)
            var cmdRow = CreateUIGameObject("CommandsRow", selectionPanel.transform);
            var crHlg = cmdRow.AddComponent<HorizontalLayoutGroup>();
            crHlg.childAlignment = TextAnchor.MiddleCenter;
            crHlg.spacing = 8f;
            var crRt = cmdRow.GetComponent<RectTransform>();
            crRt.sizeDelta = new Vector2(550f, 40f);

            var workBtn = CreateButton(cmdRow.transform, "WorkBtn", "Работать", new Vector2(100f, 36f));
            var haulBtn = CreateButton(cmdRow.transform, "HaulBtn", "Переносить", new Vector2(105f, 36f));
            var barracksBtn = CreateButton(cmdRow.transform, "BarracksBtn", "В бараки", new Vector2(100f, 36f));
            var releaseBtn = CreateButton(cmdRow.transform, "ReleaseBtn", "Освободить", new Vector2(100f, 36f));
            var cancelCmdsBtn = CreateButton(cmdRow.transform, "CancelCmdsBtn", "Отмена", new Vector2(85f, 36f));
            cmdRow.SetActive(false);

            // Stack picker row
            var stackRow = CreateUIGameObject("StackPickerRow", selectionPanel.transform);
            var srHlg = stackRow.AddComponent<HorizontalLayoutGroup>();
            srHlg.childAlignment = TextAnchor.MiddleCenter;
            srHlg.spacing = 12f;
            var srRt = stackRow.GetComponent<RectTransform>();
            srRt.sizeDelta = new Vector2(550f, 26f);

            var stackLbl = CreateText(stackRow.transform, "StackLabel", "Выбрать: 1 из 1", 12, Color.white);
            stackLbl.rectTransform.sizeDelta = new Vector2(140f, 24f);

            var sliderGo = CreateUIGameObject("Slider", stackRow.transform);
            var slider = sliderGo.AddComponent<Slider>();
            slider.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 20f);

            var confirmStackBtn = CreateButton(stackRow.transform, "ConfirmStackBtn", "Выбрать", new Vector2(80f, 24f));
            stackRow.SetActive(false);

            selectionPanel.SetActive(false);

            var cmdDock = selectionPanel.AddComponent<CommandDockView>();
            cmdDock.Setup(
                null, null,
                selectionPanel, selCountTxt,
                mainRow, openCmdsBtn, sellSelectedBtn, clearSelBtn,
                cmdRow, workBtn, haulBtn, barracksBtn, releaseBtn, cancelCmdsBtn,
                stackRow, slider, stackLbl, confirmStackBtn
            );

            // --- 5. BOTTOM-LEFT INSPECT CARD (InspectCardView) ---
            var inspectPanel = CreatePanel(canvasGo.transform, "InspectCardPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(280f, 200f), new Color(0.06f, 0.1f, 0.08f, 0.96f));

            var ipVlg = inspectPanel.AddComponent<VerticalLayoutGroup>();
            ipVlg.childAlignment = TextAnchor.UpperLeft;
            ipVlg.spacing = 6f;
            ipVlg.padding = new RectOffset(12, 12, 10, 10);

            var inspectHeaderRow = CreateUIGameObject("HeaderRow", inspectPanel.transform);
            var ihrHlg = inspectHeaderRow.AddComponent<HorizontalLayoutGroup>();
            ihrHlg.childAlignment = TextAnchor.MiddleCenter;
            var ihrRt = inspectHeaderRow.GetComponent<RectTransform>();
            ihrRt.sizeDelta = new Vector2(256f, 26f);

            var insTitle = CreateText(inspectHeaderRow.transform, "Title", "Шахта #1", 15, new Color(0.95f, 0.8f, 0.3f));
            insTitle.fontStyle = FontStyles.Bold;
            var itLe = insTitle.gameObject.AddComponent<LayoutElement>();
            itLe.flexibleWidth = 1f;

            var closeInspectBtn = CreateButton(inspectHeaderRow.transform, "CloseBtn", "X", new Vector2(28f, 24f));

            var insSubtitle = CreateText(inspectPanel.transform, "Subtitle", "Добыча руды", 11, new Color(0.6f, 0.8f, 0.75f));
            var isLe = insSubtitle.gameObject.AddComponent<LayoutElement>();
            isLe.preferredHeight = 18f;

            var insDetails = CreateText(inspectPanel.transform, "Details", "Руда: 10 / 50\nРабочие: 2 / 3\nДобыча: +1.0/сек", 12, Color.white);
            var idLe = insDetails.gameObject.AddComponent<LayoutElement>();
            idLe.preferredHeight = 60f;

            var inspectActionsRow = CreateUIGameObject("ActionsRow", inspectPanel.transform);
            var iarHlg = inspectActionsRow.AddComponent<HorizontalLayoutGroup>();
            iarHlg.childAlignment = TextAnchor.MiddleCenter;
            iarHlg.spacing = 8f;
            var iarRt = inspectActionsRow.GetComponent<RectTransform>();
            iarRt.sizeDelta = new Vector2(256f, 34f);

            var insAction1Btn = CreateButton(inspectActionsRow.transform, "Action1Btn", "Действие 1", new Vector2(120f, 32f));
            var insAction1Txt = insAction1Btn.GetComponentInChildren<TextMeshProUGUI>();

            var insAction2Btn = CreateButton(inspectActionsRow.transform, "Action2Btn", "Действие 2", new Vector2(120f, 32f));
            var insAction2Txt = insAction2Btn.GetComponentInChildren<TextMeshProUGUI>();

            inspectPanel.SetActive(false);

            var inspectCard = inspectPanel.AddComponent<InspectCardView>();
            inspectCard.Setup(
                null, null,
                inspectPanel, insTitle, insSubtitle, insDetails,
                insAction1Btn, insAction1Txt, insAction2Btn, insAction2Txt,
                closeInspectBtn
            );

            return (hudPresenter, resBar, shopDock, cmdDock, inspectCard, statusMsg);
        }

        private static (GameObject col, TextMeshProUGUI val) CreateStatColumn(Transform parent, string label, string initVal, Color valColor)
        {
            var go = CreateUIGameObject("Stat_" + label, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110f, 48f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 2f;

            var lbl = CreateText(go.transform, "Lbl", label, 11, new Color(0.6f, 0.75f, 0.7f));
            lbl.alignment = TextAlignmentOptions.Center;

            var val = CreateText(go.transform, "Val", initVal, 16, valColor);
            val.alignment = TextAlignmentOptions.Center;
            val.fontStyle = FontStyles.Bold;

            return (go, val);
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
            // CRITICAL: Panels should NEVER block world raycasts!
            img.raycastTarget = false;
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            var go = CreateUIGameObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.32f, 0.26f, 1f);
            // Buttons ARE raycast targets!
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.32f, 0.26f, 1f);
            colors.highlightedColor = new Color(0.26f, 0.44f, 0.36f, 1f);
            colors.pressedColor = new Color(0.12f, 0.22f, 0.18f, 1f);
            colors.disabledColor = new Color(0.1f, 0.14f, 0.12f, 0.5f);
            btn.colors = colors;

            var txtGo = CreateUIGameObject("Text", go.transform);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            if (s_fontAsset != null) tmp.font = s_fontAsset;
            tmp.text = label;
            tmp.fontSize = 12f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.95f, 0.92f, 0.85f, 1f);
            tmp.raycastTarget = false;

            return btn;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Color color)
        {
            var go = CreateUIGameObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (s_fontAsset != null) tmp.font = s_fontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            // Text should NEVER block world raycasts!
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
