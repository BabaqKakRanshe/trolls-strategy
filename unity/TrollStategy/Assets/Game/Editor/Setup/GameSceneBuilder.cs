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

        private static GameContentCatalog CreateOrUpdateContentCatalog()
        {
            string ecoPath = "Assets/Game/Content/Definitions/EconomyConfig.asset";
            var eco = AssetDatabase.LoadAssetAtPath<EconomyConfig>(ecoPath);
            if (eco == null)
            {
                eco = ScriptableObject.CreateInstance<EconomyConfig>();
                AssetDatabase.CreateAsset(eco, ecoPath);
            }

            var marketSprite = LoadSingleSprite("Assets/Game/Art/Sprites/Buildings/Market_01.png");
            var mineSprite = LoadSingleSprite("Assets/Game/Art/Sprites/Buildings/Mine_01.png");
            var warehouseSprite = LoadSingleSprite("Assets/Game/Art/Sprites/Buildings/Warehouse_01.png");

            var mineDef = GetOrCreateBuildingDef("MineDef", BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, mineSprite);
            var warehouseDef = GetOrCreateBuildingDef("WarehouseDef", BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, warehouseSprite);
            var marketDef = GetOrCreateBuildingDef("MarketDef", BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, marketSprite);

            var goblinIdleFrames = LoadSprites("Assets/Game/Art/Sprites/Units/goblin-idle.png", "goblin_idle", 16);
            var goblinWalkFrames = LoadSprites("Assets/Game/Art/Sprites/Units/goblin-walk.png", "goblin_walk", 4);
            var trollIdleFrames = LoadSprites("Assets/Game/Art/Sprites/Units/troll-idle.png", "troll_idle", 16);
            var trollWalkFrames = LoadSprites("Assets/Game/Art/Sprites/Units/troll-walk.png", "troll_walk", 6);

            var goblinPortrait = goblinIdleFrames.Length > 0 ? goblinIdleFrames[0] : null;
            var trollPortrait = trollIdleFrames.Length > 0 ? trollIdleFrames[0] : null;

            var goblinDef = GetOrCreateUnitDef("GoblinDef", UnitKind.Goblin, "Гоблин", 40, 3, 5f, 10, "Быстрый рабочий и носильщик", goblinPortrait, goblinIdleFrames, goblinWalkFrames);
            var trollDef = GetOrCreateUnitDef("TrollDef", UnitKind.Troll, "Тролль", 170, 9, 2f, 30, "Медленный, но очень сильный", trollPortrait, trollIdleFrames, trollWalkFrames);

            string catPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(catPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
                AssetDatabase.CreateAsset(catalog, catPath);
            }
            catalog.SetContent(eco, new List<BuildingDefinition> { mineDef, warehouseDef, marketDef }, new List<UnitDefinition> { goblinDef, trollDef });
            EditorUtility.SetDirty(catalog);

            // Also copy to Resources for robust runtime loading
            string resCatPath = "Assets/Game/Resources/GameContentCatalog.asset";
            if (!File.Exists(resCatPath))
            {
                AssetDatabase.CopyAsset(catPath, resCatPath);
            }

            return catalog;
        }

                private static Sprite LoadSingleSprite(string texturePath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            foreach (var a in assets)
            {
                if (a is Sprite s) return s;
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

private static Sprite[] LoadSprites(string texturePath, string prefix, int count)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            var map = new Dictionary<string, Sprite>();
            foreach (var a in assets)
            {
                if (a is Sprite s) map[s.name] = s;
            }

            var list = new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                string key = $"{prefix}_{i}";
                if (map.TryGetValue(key, out var s))
                    list.Add(s);
            }
            return list.ToArray();
        }

        private static BuildingDefinition GetOrCreateBuildingDef(string assetName, BuildingKind kind, string name, int price, int w, int h, int maxOre, int maxWorkers, Sprite sprite)
        {
            string path = $"Assets/Game/Content/Definitions/{assetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BuildingDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.Init(kind, name, price, w, h, maxOre, maxWorkers, sprite);
            EditorUtility.SetDirty(def);
            return def;
        }

        private static UnitDefinition GetOrCreateUnitDef(string assetName, UnitKind kind, string name, int price, int str, float spd, int cargo, string desc, Sprite portrait, Sprite[] idle, Sprite[] walk)
        {
            string path = $"Assets/Game/Content/Definitions/{assetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.Init(kind, name, price, str, spd, cargo, desc, portrait, idle, walk);
            EditorUtility.SetDirty(def);
            return def;
        }

        private static (BuildingView buildingPrefab, UnitView unitPrefab) CreatePrefabs(GameContentCatalog catalog)
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
            hlSr.sortingOrder = 9;

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
            scGo.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            var clGo = new GameObject("CargoLabel");
            clGo.transform.SetParent(uGo.transform, false);
            clGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var clTmp = clGo.AddComponent<TextMeshPro>();
            if (s_fontAsset != null) clTmp.font = s_fontAsset;
            clTmp.fontSize = 2f;
            clTmp.color = Color.yellow;
            clTmp.alignment = TextAlignmentOptions.Center;
            clTmp.sortingOrder = 25;

            var soU = new SerializedObject(uView);
            soU.FindProperty("_spriteRenderer").objectReferenceValue = uSr;
            soU.FindProperty("_selectionCircle").objectReferenceValue = scSr;
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
            camGo.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();

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
            var (hudPresenter, rosterView, shopDock, cmdDock) = CreateUIHierarchy(cam, catalog);

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
            soBoot.FindProperty("_rosterView").objectReferenceValue = rosterView;
            soBoot.FindProperty("_shopDockView").objectReferenceValue = shopDock;
            soBoot.FindProperty("_commandDockView").objectReferenceValue = cmdDock;
            boot.InjectDependencies(
                catalog,
                worldView,
                cam,
                bManager,
                uManager,
                placementPreview,
                selectionBox,
                routeVisualizer,
                inputHandler,
                buildingPrefab,
                unitPrefab,
                hudPresenter,
                rosterView,
                shopDock,
                cmdDock
            );
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

        private static (HudPresenter, UnitRosterView, ShopDockView, CommandDockView) CreateUIHierarchy(Camera cam, GameContentCatalog catalog)
        {
            var canvasGo = CreateUIGameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var hudPresenter = canvasGo.AddComponent<HudPresenter>();

            // --- TOP WORLD HEADER ---
            var topHeader = CreatePanel(canvasGo.transform, "WorldHeader", new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(0f, 36f), new Color(0.06f, 0.1f, 0.1f, 0.88f));
            var headerTxt = CreateText(topHeader.transform, "HeaderText", "<b>Участок 01</b>  |  ЛКМ — выбор  ·  Shift — группа  ·  ПКМ — приказы  ·  B — шахта  ·  W — работа  ·  H — маршрут", 14, new Color(0.7f, 0.88f, 0.8f));
            headerTxt.alignment = TextAlignmentOptions.Center;
            var hRt = headerTxt.rectTransform;
            hRt.anchorMin = Vector2.zero;
            hRt.anchorMax = Vector2.one;
            hRt.sizeDelta = Vector2.zero;

            // --- LEFT PANEL: ACCESSIBILITY & UNIT ROSTER ---
            var leftPanel = CreatePanel(canvasGo.transform, "LeftRosterPanel", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(115f, 0f), new Vector2(210f, 720f), new Color(0.06f, 0.09f, 0.1f, 0.92f));
            var leftVlg = leftPanel.AddComponent<VerticalLayoutGroup>();
            leftVlg.childAlignment = TextAnchor.UpperCenter;
            leftVlg.spacing = 8f;
            leftVlg.padding = new RectOffset(8, 8, 12, 12);

            CreateText(leftPanel.transform, "RosterTitle", "СУЩЕСТВА", 14, new Color(0.5f, 0.75f, 0.7f));
            var selCountTxt = CreateText(leftPanel.transform, "SelCount", "0 выбрано", 13, Color.yellow);

            var quickBtnsRow = CreateUIGameObject("QuickSelectRow", leftPanel.transform);
            var qbHlg = quickBtnsRow.AddComponent<HorizontalLayoutGroup>();
            qbHlg.spacing = 6f;
            qbHlg.childAlignment = TextAnchor.MiddleCenter;
            var qbRt = quickBtnsRow.GetComponent<RectTransform>();
            qbRt.sizeDelta = new Vector2(190f, 32f);

            var sel3Btn = CreateButton(quickBtnsRow.transform, "Select3Btn", "Первые 3", new Vector2(92f, 30f));
            var selNextBtn = CreateButton(quickBtnsRow.transform, "SelectNextBtn", "Следующий", new Vector2(92f, 30f));

            var scrollContainer = CreateUIGameObject("ScrollContainer", leftPanel.transform);
            var scRt = scrollContainer.GetComponent<RectTransform>();
            scRt.sizeDelta = new Vector2(194f, 580f);
            var scVlg = scrollContainer.AddComponent<VerticalLayoutGroup>();
            scVlg.childAlignment = TextAnchor.UpperCenter;
            scVlg.spacing = 4f;

            var rosterView = leftPanel.AddComponent<UnitRosterView>();
            rosterView.Setup(null, catalog, sel3Btn, selNextBtn, selCountTxt, scrollContainer.transform);

            // --- RIGHT PANEL: GUILD REGISTRY ---
            var rightPanel = CreatePanel(canvasGo.transform, "RightShopPanel", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(280f, 760f), new Color(0.06f, 0.09f, 0.1f, 0.94f));
            var rightVlg = rightPanel.AddComponent<VerticalLayoutGroup>();
            rightVlg.childAlignment = TextAnchor.UpperCenter;
            rightVlg.spacing = 10f;
            rightVlg.padding = new RectOffset(12, 12, 14, 14);
            rightVlg.childControlWidth = true;
            rightVlg.childControlHeight = false;
            rightVlg.childForceExpandWidth = true;
            rightVlg.childForceExpandHeight = false;

            var shopCap = CreateText(rightPanel.transform, "ShopCap", "ГИЛЬДЕЙСКИЙ РЕЕСТР", 15, new Color(0.85f, 0.65f, 0.35f));
            var scLe = shopCap.gameObject.AddComponent<LayoutElement>();
            scLe.preferredWidth = 256f;
            scLe.preferredHeight = 24f;

            // Resource Grid (Gold, Ore, Sold, Pop)
            var resGrid = CreateUIGameObject("ResourceGrid", rightPanel.transform);
            var rgRt = resGrid.GetComponent<RectTransform>();
            rgRt.sizeDelta = new Vector2(256f, 104f);
            var glg = resGrid.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(122f, 44f);
            glg.spacing = new Vector2(10f, 8f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            var rgLe = resGrid.AddComponent<LayoutElement>();
            rgLe.preferredWidth = 256f;
            rgLe.preferredHeight = 104f;
            rgLe.minHeight = 104f;

            var (goldBox, goldVal) = CreateResBox(resGrid.transform, "Золото", "0", Color.yellow);
            var (oreBox, oreVal) = CreateResBox(resGrid.transform, "Руда", "0", new Color(0.95f, 0.6f, 0.2f));
            var (soldBox, soldVal) = CreateResBox(resGrid.transform, "Продано", "0", Color.white);
            var (popBox, popVal) = CreateResBox(resGrid.transform, "Население", "0", Color.cyan);

            // Buildings section
            var bldTitle = CreateText(rightPanel.transform, "BldSecTitle", "ПОСТРОЙКИ", 13, new Color(0.5f, 0.75f, 0.7f));
            var btLe = bldTitle.gameObject.AddComponent<LayoutElement>();
            btLe.preferredWidth = 256f;
            btLe.preferredHeight = 22f;

            var mineCard = CreateCard(rightPanel.transform, "MineCard", "Шахта", "3x3 - до 5 рабочих", "200з");
            var buildMineBtn = mineCard.GetComponentInChildren<Button>();
            var autoPlaceBtn = CreateButton(rightPanel.transform, "AutoPlaceBtn", "Поставить автоматически", new Vector2(250f, 32f));
            autoPlaceBtn.gameObject.SetActive(false);

            // Hire section
            var hireSecHeader = CreateUIGameObject("HireSecHeader", rightPanel.transform);
            var hshRt = hireSecHeader.GetComponent<RectTransform>();
            hshRt.sizeDelta = new Vector2(256f, 32f);
            var hshHlg = hireSecHeader.AddComponent<HorizontalLayoutGroup>();
            hshHlg.childAlignment = TextAnchor.MiddleCenter;
            hshHlg.spacing = 10f;
            hshHlg.childControlWidth = false;
            hshHlg.childControlHeight = false;
            var hshLe = hireSecHeader.AddComponent<LayoutElement>();
            hshLe.preferredWidth = 256f;
            hshLe.preferredHeight = 32f;

            var hireTitle = CreateText(hireSecHeader.transform, "HireTitle", "НАЙМ", 13, new Color(0.5f, 0.75f, 0.7f));
            hireTitle.rectTransform.sizeDelta = new Vector2(60f, 28f);
            var minusBtn = CreateButton(hireSecHeader.transform, "MinusBtn", "-", new Vector2(34f, 28f));
            var amtTxt = CreateText(hireSecHeader.transform, "AmtTxt", "1", 14, Color.white);
            amtTxt.alignment = TextAlignmentOptions.Center;
            amtTxt.rectTransform.sizeDelta = new Vector2(36f, 28f);
            var plusBtn = CreateButton(hireSecHeader.transform, "PlusBtn", "+", new Vector2(34f, 28f));

            var goblinCard = CreateCard(rightPanel.transform, "GoblinCard", "Гоблин", "Скорость 5, груз 10, сила 3", "40з");
            var buyGoblinBtn = goblinCard.GetComponentInChildren<Button>();
            var goblinCostTxt = buyGoblinBtn.GetComponentInChildren<TextMeshProUGUI>();

            var trollCard = CreateCard(rightPanel.transform, "TrollCard", "Тролль", "Сила 9, груз 30, скорость 2", "170з");
            var buyTrollBtn = trollCard.GetComponentInChildren<Button>();
            var trollCostTxt = buyTrollBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Placement Box
            var placeBox = CreatePanel(rightPanel.transform, "UnitPlacementBox", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(256f, 60f), new Color(0.1f, 0.2f, 0.25f, 0.9f));
            var pbLe = placeBox.AddComponent<LayoutElement>();
            pbLe.preferredWidth = 256f;
            pbLe.preferredHeight = 60f;
            var pbVlg = placeBox.AddComponent<VerticalLayoutGroup>();
            pbVlg.childAlignment = TextAnchor.MiddleCenter;
            var pbTitle = CreateText(placeBox.transform, "PlacementTitle", "Кликните по свободной клетке", 12, Color.yellow);
            pbTitle.alignment = TextAlignmentOptions.Center;
            var cancelPlacementBtn = CreateButton(placeBox.transform, "CancelPlacementBtn", "Отмена", new Vector2(100f, 24f));
            placeBox.SetActive(false);

            // Status message
            var statusMsg = CreateText(rightPanel.transform, "StatusMessage", "Постройте шахту и наймите рабочих.", 12, Color.white);
            statusMsg.alignment = TextAlignmentOptions.Center;
            var smLe = statusMsg.gameObject.AddComponent<LayoutElement>();
            smLe.preferredWidth = 256f;
            smLe.preferredHeight = 36f;

            var shopDock = rightPanel.AddComponent<ShopDockView>();
            shopDock.Setup(
                null, null,
                goldVal, oreVal, soldVal, popVal,
                buildMineBtn, autoPlaceBtn,
                buyGoblinBtn, buyTrollBtn,
                plusBtn, minusBtn, amtTxt,
                goblinCostTxt, trollCostTxt,
                placeBox, pbTitle, cancelPlacementBtn,
                statusMsg
            );

            // --- BOTTOM COMMAND DOCK ---
            var cmdPanel = CreatePanel(canvasGo.transform, "CommandDock", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(620f, 75f), new Color(0.06f, 0.09f, 0.1f, 0.94f));
            var cmdVlg = cmdPanel.AddComponent<VerticalLayoutGroup>();
            cmdVlg.childAlignment = TextAnchor.MiddleCenter;
            cmdVlg.spacing = 6f;
            cmdVlg.padding = new RectOffset(10, 10, 6, 6);

            var actionsRow = CreateUIGameObject("ActionsRow", cmdPanel.transform);
            var aHlg = actionsRow.AddComponent<HorizontalLayoutGroup>();
            aHlg.childAlignment = TextAnchor.MiddleCenter;
            aHlg.spacing = 12f;
            var aRt = actionsRow.GetComponent<RectTransform>();
            aRt.sizeDelta = new Vector2(600f, 38f);

            CreateText(actionsRow.transform, "CmdTitle", "Приказы:", 14, new Color(0.85f, 0.65f, 0.35f));
            var workBtn = CreateButton(actionsRow.transform, "WorkBtn", "Работать", new Vector2(110f, 36f));
            var haulBtn = CreateButton(actionsRow.transform, "HaulBtn", "Переносить", new Vector2(110f, 36f));
            var relBtn = CreateButton(actionsRow.transform, "ReleaseBtn", "Освободить", new Vector2(110f, 36f));
            var cancelBtn = CreateButton(actionsRow.transform, "CancelBtn", "Отмена", new Vector2(90f, 36f));

            var targetsRow = CreateUIGameObject("TargetsRow", cmdPanel.transform);
            var tHlg = targetsRow.AddComponent<HorizontalLayoutGroup>();
            tHlg.childAlignment = TextAnchor.MiddleCenter;
            tHlg.spacing = 10f;
            var tRt = targetsRow.GetComponent<RectTransform>();
            tRt.sizeDelta = new Vector2(600f, 34f);

            var promptTxt = CreateText(targetsRow.transform, "TargetPrompt", "Цель:", 13, Color.yellow);
            targetsRow.SetActive(false);

            var cmdDock = cmdPanel.AddComponent<CommandDockView>();
            cmdDock.Setup(null, workBtn, haulBtn, relBtn, cancelBtn, targetsRow.transform, promptTxt);

            return (hudPresenter, rosterView, shopDock, cmdDock);
        }

        private static (GameObject box, TextMeshProUGUI val) CreateResBox(Transform parent, string label, string initVal, Color valColor)
        {
            var go = CreatePanel(parent, "Res_" + label, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(122f, 44f), new Color(0.1f, 0.14f, 0.18f, 0.92f));

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(4, 4, 2, 2);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var lbl = CreateText(go.transform, "Lbl", label, 11, new Color(0.6f, 0.72f, 0.78f));
            lbl.alignment = TextAlignmentOptions.Center;
            var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
            lblLe.preferredHeight = 16f;

            var val = CreateText(go.transform, "Val", initVal, 15, valColor);
            val.alignment = TextAlignmentOptions.Center;
            val.fontStyle = FontStyles.Bold;
            var valLe = val.gameObject.AddComponent<LayoutElement>();
            valLe.preferredHeight = 20f;

            return (go, val);
        }

        private static GameObject CreateCard(Transform parent, string name, string title, string subtitle, string cost)
        {
            var card = CreatePanel(parent, name, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(256f, 54f), new Color(0.1f, 0.14f, 0.18f, 0.85f));
            var cLe = card.AddComponent<LayoutElement>();
            cLe.preferredWidth = 256f;
            cLe.preferredHeight = 54f;
            cLe.minHeight = 54f;

            var hlg = card.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(10, 10, 6, 6);

            var infoGo = CreateUIGameObject("Info", card.transform);
            var vlg = infoGo.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            var iRt = infoGo.GetComponent<RectTransform>();
            iRt.sizeDelta = new Vector2(140f, 42f);

            CreateText(infoGo.transform, "Title", title, 13, Color.white);
            CreateText(infoGo.transform, "Subtitle", subtitle, 9, new Color(0.6f, 0.75f, 0.7f));

            CreateButton(card.transform, "ActionBtn", cost, new Vector2(80f, 34f));
            return card;
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
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            var go = CreateUIGameObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.32f, 0.42f, 1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.32f, 0.42f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.44f, 0.58f, 1f);
            colors.pressedColor = new Color(0.14f, 0.22f, 0.3f, 1f);
            colors.disabledColor = new Color(0.12f, 0.16f, 0.2f, 0.5f);
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
            tmp.color = Color.white;

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
            return tmp;
        }
    }
}