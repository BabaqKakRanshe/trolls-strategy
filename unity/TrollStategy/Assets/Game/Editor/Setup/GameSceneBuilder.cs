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

        private static (HudPresenter, ResourceBarView, ShopDockView, CommandDockView, InspectCardView, StatusMessageView) CreateUIHierarchy(Camera cam, GameContentCatalog catalog)
        {
            var canvasGo = CreateUIGameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var hudPresenter = canvasGo.AddComponent<HudPresenter>();

            var mineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Buildings/Mine_01.png");
            var goblinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Units/goblin-idle.png");
            var trollSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/Units/troll-idle.png");

            // =========================================================================
            // 1. TOP-LEFT BANNER (Controls & Status)
            // =========================================================================
            var bannerPanel = CreatePanel(canvasGo.transform, "TopBanner",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -12f), new Vector2(-368f, 38f), new Color(0.08f, 0.15f, 0.12f, 0.92f));
            var bnHlg = bannerPanel.AddComponent<HorizontalLayoutGroup>();
            bnHlg.childAlignment = TextAnchor.MiddleLeft;
            bnHlg.padding = new RectOffset(16, 16, 6, 6);
            bnHlg.childControlWidth = true;
            bnHlg.childForceExpandWidth = true;

            var bannerText = CreateText(bannerPanel.transform, "BannerText",
                "УЧАСТОК 01   ·   ЛКМ — выбор/размещение   ·   ПКМ/Esc — отмена   ·   <color=#ffd66f>G</color> — сетка", 13, new Color(0.94f, 0.92f, 0.84f));
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.textWrappingMode = TextWrappingModes.NoWrap;
            bannerText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Accessibility unit selectors under the banner
            var accessPanel = CreatePanel(canvasGo.transform, "AccessibilityBar",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -56f), new Vector2(360f, 34f), new Color(0.07f, 0.13f, 0.1f, 0.88f));
            var aHlg = accessPanel.AddComponent<HorizontalLayoutGroup>();
            aHlg.childAlignment = TextAnchor.MiddleCenter;
            aHlg.spacing = 8f;
            aHlg.padding = new RectOffset(8, 8, 4, 4);

            var first3Btn = CreateButton(accessPanel.transform, "First3Btn", "Первые 3", new Vector2(85f, 26f));
            var nextBtn = CreateButton(accessPanel.transform, "NextBtn", "Следующий", new Vector2(90f, 26f));
            var gridBtn = CreateButton(accessPanel.transform, "GridToggleBtn", "Сетка (G)", new Vector2(80f, 26f));

            // =========================================================================
            // 2. RIGHT SIDEBAR: GUILD REGISTRY ("ГИЛЬДЕЙСКИЙ РЕЕСТР")
            // =========================================================================
            var shopPanel = CreatePanel(canvasGo.transform, "GuildRegistryPanel",
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(336f, 0f), new Color(0.09f, 0.16f, 0.13f, 0.98f));

            var spVlg = shopPanel.AddComponent<VerticalLayoutGroup>();
            spVlg.childAlignment = TextAnchor.UpperCenter;
            spVlg.spacing = 8f;
            spVlg.padding = new RectOffset(12, 12, 12, 12);
            spVlg.childControlWidth = true;
            spVlg.childControlHeight = false;

            // --- Registry Header ---
            var regCap = CreatePanel(shopPanel.transform, "RegistryCap",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(312f, 44f), new Color(0.15f, 0.24f, 0.2f, 1f));
            regCap.AddComponent<LayoutElement>().preferredHeight = 44f;
            var rchHlg = regCap.AddComponent<HorizontalLayoutGroup>();
            rchHlg.childAlignment = TextAnchor.MiddleCenter;

            var regTitle = CreateText(regCap.transform, "RegTitle", "ГИЛЬДЕЙСКИЙ РЕЕСТР", 16, new Color(0.95f, 0.92f, 0.82f));
            regTitle.fontStyle = FontStyles.Bold;
            regTitle.alignment = TextAlignmentOptions.Center;

            // --- 2x2 Resource Grid ---
            var resGridGo = CreateUIGameObject("ResourceGrid", shopPanel.transform);
            resGridGo.AddComponent<LayoutElement>().preferredHeight = 92f;
            var resGridGlg = resGridGo.AddComponent<GridLayoutGroup>();
            resGridGlg.cellSize = new Vector2(152f, 42f);
            resGridGlg.spacing = new Vector2(8f, 8f);
            resGridGlg.childAlignment = TextAnchor.MiddleCenter;

            var (goldBox, goldVal) = CreateResourceCell(resGridGo.transform, "ЗОЛОТО", "1000", new Color(1f, 0.85f, 0.2f));
            var (oreBox, oreVal) = CreateResourceCell(resGridGo.transform, "РУДА", "0", new Color(0.75f, 0.85f, 0.85f));
            var (soldBox, soldVal) = CreateResourceCell(resGridGo.transform, "ПРОДАНО", "0", new Color(0.55f, 0.85f, 0.45f));
            var (popBox, popVal) = CreateResourceCell(resGridGo.transform, "НАСЕЛЕНИЕ", "0", new Color(0.94f, 0.92f, 0.84f));

            // --- Buildings Section ---
            var bldSecHeader = CreateSectionHeader(shopPanel.transform, "ПОСТРОЙКИ", "1 ДОСТУПНО");
            var (mineCard, mineArt, mTitle, mSub, buildMineBtn, mineCostTxt) =
                CreateShopCard(shopPanel.transform, "Mine", mineSprite, "Шахта", "3×3 · до 5 рабочих", "Купить", 200);

            var autoPlaceBtn = CreateButton(shopPanel.transform, "AutoPlaceBtn", "Поставить автоматически", new Vector2(312f, 32f));
            autoPlaceBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            autoPlaceBtn.gameObject.SetActive(false);

            // --- Hire Section ---
            var hireSecHeader = CreateSectionHeader(shopPanel.transform, "НАЙМ", "");
            var stepperRow = CreateUIGameObject("HireStepperRow", shopPanel.transform);
            stepperRow.AddComponent<LayoutElement>().preferredHeight = 34f;
            var stHlg = stepperRow.AddComponent<HorizontalLayoutGroup>();
            stHlg.childAlignment = TextAnchor.MiddleCenter;
            stHlg.spacing = 8f;

            var decHireBtn = CreateButton(stepperRow.transform, "DecHireBtn", "-", new Vector2(34f, 30f));
            var hireAmtTxt = CreateText(stepperRow.transform, "HireAmountTxt", "1", 15, Color.white);
            hireAmtTxt.alignment = TextAlignmentOptions.Center;
            hireAmtTxt.fontStyle = FontStyles.Bold;
            hireAmtTxt.rectTransform.sizeDelta = new Vector2(40f, 30f);
            var incHireBtn = CreateButton(stepperRow.transform, "IncHireBtn", "+", new Vector2(34f, 30f));

            var (goblinCard, goblinArt, gTitle, gSub, buyGoblinBtn, goblinCostTxt) =
                CreateShopCard(shopPanel.transform, "Goblin", goblinSprite, "Гоблин", "Скорость 5 · груз 10", "Нанять", 40);

            var (trollCard, trollArt, tTitle, tSub, buyTrollBtn, trollCostTxt) =
                CreateShopCard(shopPanel.transform, "Troll", trollSprite, "Тролль", "Сила 9 · груз 30", "Нанять", 170);

            // --- Placement Mode Banner ---
            var placementBanner = CreatePanel(shopPanel.transform, "PlacementBanner",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(312f, 54f), new Color(0.2f, 0.32f, 0.18f, 1f));
            placementBanner.AddComponent<LayoutElement>().preferredHeight = 54f;
            var pbHlg = placementBanner.AddComponent<HorizontalLayoutGroup>();
            pbHlg.childAlignment = TextAnchor.MiddleCenter;
            pbHlg.spacing = 8f;
            pbHlg.padding = new RectOffset(10, 10, 6, 6);

            var pbTextCol = CreateUIGameObject("TextCol", placementBanner.transform);
            var pbVlg = pbTextCol.AddComponent<VerticalLayoutGroup>();
            pbVlg.spacing = 2f;
            pbTextCol.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 44f);
            var pbTitle = CreateText(pbTextCol.transform, "Title", "Гоблин ×1", 13, Color.white);
            pbTitle.fontStyle = FontStyles.Bold;
            var pbPrompt = CreateText(pbTextCol.transform, "Prompt", "Кликните по свободной клетке", 10, new Color(0.85f, 0.95f, 0.85f));

            var cancelPlaceBtn = CreateButton(placementBanner.transform, "CancelPlaceBtn", "Отмена", new Vector2(74f, 32f));
            placementBanner.SetActive(false);

            // --- Game Status Message at bottom of shop panel ---
            var statusBox = CreatePanel(shopPanel.transform, "StatusBox",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(312f, 56f), new Color(0.06f, 0.1f, 0.08f, 0.9f));
            statusBox.AddComponent<LayoutElement>().preferredHeight = 56f;
            var sbHlg = statusBox.AddComponent<HorizontalLayoutGroup>();
            sbHlg.childAlignment = TextAnchor.MiddleLeft;
            sbHlg.padding = new RectOffset(10, 10, 6, 6);

            var gameStatusTxt = CreateText(statusBox.transform, "GameStatusTxt", "Постройте шахту и наймите рабочих.", 12, new Color(0.88f, 0.85f, 0.74f));
            gameStatusTxt.alignment = TextAlignmentOptions.Left;

            var shopDock = shopPanel.AddComponent<ShopDockView>();
            shopDock.Setup(
                null, null,
                goldVal, oreVal, soldVal, popVal,
                mineArt, buildMineBtn, mineCostTxt, autoPlaceBtn,
                decHireBtn, incHireBtn, hireAmtTxt,
                goblinArt, buyGoblinBtn, goblinCostTxt,
                trollArt, buyTrollBtn, trollCostTxt,
                placementBanner, pbTitle, cancelPlaceBtn,
                gameStatusTxt
            );

            // =========================================================================
            // 3. BOTTOM COMMAND DOCK ("✦ ПРИКАЗЫ")
            // =========================================================================
            var cmdPanel = CreatePanel(canvasGo.transform, "CommandDockPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(480f, 96f), new Color(0.07f, 0.13f, 0.11f, 0.96f));

            var cpVlg = cmdPanel.AddComponent<VerticalLayoutGroup>();
            cpVlg.childAlignment = TextAnchor.MiddleCenter;
            cpVlg.spacing = 6f;
            cpVlg.padding = new RectOffset(14, 14, 8, 8);

            var cmdHeaderRow = CreateUIGameObject("HeaderRow", cmdPanel.transform);
            var chHlg = cmdHeaderRow.AddComponent<HorizontalLayoutGroup>();
            chHlg.childAlignment = TextAnchor.MiddleLeft;
            cmdHeaderRow.GetComponent<RectTransform>().sizeDelta = new Vector2(452f, 20f);

            var cmdCountTxt = CreateText(cmdHeaderRow.transform, "SelectedCount", "✦ ПРИКАЗЫ", 13, new Color(0.95f, 0.85f, 0.3f));
            cmdCountTxt.fontStyle = FontStyles.Bold;

            var actionsRow = CreateUIGameObject("ActionsRow", cmdPanel.transform);
            var aRowHlg = actionsRow.AddComponent<HorizontalLayoutGroup>();
            aRowHlg.childAlignment = TextAnchor.MiddleCenter;
            aRowHlg.spacing = 10f;
            actionsRow.GetComponent<RectTransform>().sizeDelta = new Vector2(452f, 38f);

            var workBtn = CreateButton(actionsRow.transform, "WorkBtn", "Работать", new Vector2(100f, 36f));
            var haulBtn = CreateButton(actionsRow.transform, "HaulBtn", "Переносить", new Vector2(100f, 36f));
            var releaseBtn = CreateButton(actionsRow.transform, "ReleaseBtn", "Освободить", new Vector2(100f, 36f));
            var cancelCmdBtn = CreateButton(actionsRow.transform, "CancelBtn", "Отмена", new Vector2(85f, 36f));

            var targetChoicesRow = CreateUIGameObject("TargetChoicesRow", cmdPanel.transform);
            var tcHlg = targetChoicesRow.AddComponent<HorizontalLayoutGroup>();
            tcHlg.childAlignment = TextAnchor.MiddleLeft;
            tcHlg.spacing = 8f;
            targetChoicesRow.GetComponent<RectTransform>().sizeDelta = new Vector2(452f, 32f);

            var targetPromptTxt = CreateText(targetChoicesRow.transform, "TargetPrompt", "Цель:", 12, new Color(0.85f, 0.85f, 0.7f));
            targetPromptTxt.rectTransform.sizeDelta = new Vector2(65f, 28f);

            var targetButtonsContainer = CreateUIGameObject("TargetBtnsContainer", targetChoicesRow.transform);
            var tbcHlg = targetButtonsContainer.AddComponent<HorizontalLayoutGroup>();
            tbcHlg.childAlignment = TextAnchor.MiddleLeft;
            tbcHlg.spacing = 6f;

            targetChoicesRow.SetActive(false);
            cmdPanel.SetActive(false);

            var cmdDock = cmdPanel.AddComponent<CommandDockView>();
            cmdDock.Setup(
                null, null,
                cmdPanel, cmdCountTxt,
                workBtn, haulBtn, releaseBtn, cancelCmdBtn,
                targetChoicesRow, targetPromptTxt, targetButtonsContainer.transform
            );

            // =========================================================================
            // 4. BOTTOM-LEFT INSPECT CARD (Building/Unit details on click)
            // =========================================================================
            var inspectPanel = CreatePanel(canvasGo.transform, "InspectCardPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 130f), new Vector2(280f, 180f), new Color(0.06f, 0.12f, 0.1f, 0.96f));

            var ipVlg = inspectPanel.AddComponent<VerticalLayoutGroup>();
            ipVlg.childAlignment = TextAnchor.UpperLeft;
            ipVlg.spacing = 6f;
            ipVlg.padding = new RectOffset(12, 12, 10, 10);

            var inspectHeaderRow = CreateUIGameObject("HeaderRow", inspectPanel.transform);
            var ihrHlg = inspectHeaderRow.AddComponent<HorizontalLayoutGroup>();
            ihrHlg.childAlignment = TextAnchor.MiddleCenter;
            inspectHeaderRow.GetComponent<RectTransform>().sizeDelta = new Vector2(256f, 26f);

            var insTitle = CreateText(inspectHeaderRow.transform, "Title", "Шахта", 15, new Color(0.95f, 0.8f, 0.3f));
            insTitle.fontStyle = FontStyles.Bold;
            insTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var closeInspectBtn = CreateButton(inspectHeaderRow.transform, "CloseBtn", "X", new Vector2(28f, 24f));
            var insSubtitle = CreateText(inspectPanel.transform, "Subtitle", "Информация", 11, new Color(0.6f, 0.8f, 0.75f));
            insSubtitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

            var insDetails = CreateText(inspectPanel.transform, "Details", "", 12, Color.white);
            insDetails.gameObject.AddComponent<LayoutElement>().preferredHeight = 55f;

            var inspectActionsRow = CreateUIGameObject("ActionsRow", inspectPanel.transform);
            var iarHlg = inspectActionsRow.AddComponent<HorizontalLayoutGroup>();
            iarHlg.childAlignment = TextAnchor.MiddleCenter;
            iarHlg.spacing = 8f;
            inspectActionsRow.GetComponent<RectTransform>().sizeDelta = new Vector2(256f, 34f);

            var insAction1Btn = CreateButton(inspectActionsRow.transform, "Action1Btn", "Назначить", new Vector2(120f, 32f));
            var insAction1Txt = insAction1Btn.GetComponentInChildren<TextMeshProUGUI>();

            var insAction2Btn = CreateButton(inspectActionsRow.transform, "Action2Btn", "Закрыть", new Vector2(120f, 32f));
            var insAction2Txt = insAction2Btn.GetComponentInChildren<TextMeshProUGUI>();

            inspectPanel.SetActive(false);

            var inspectCard = inspectPanel.AddComponent<InspectCardView>();
            inspectCard.Setup(
                null, null,
                inspectPanel, insTitle, insSubtitle, insDetails,
                insAction1Btn, insAction1Txt, insAction2Btn, insAction2Txt,
                closeInspectBtn
            );

            var statusMsg = canvasGo.AddComponent<StatusMessageView>();

            return (hudPresenter, null, shopDock, cmdDock, inspectCard, statusMsg);
        }

        private static (GameObject cell, TextMeshProUGUI val) CreateResourceCell(Transform parent, string label, string initVal, Color valColor)
        {
            var go = CreateUIGameObject($"Res_{label}", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(152f, 42f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.11f, 0.19f, 0.16f, 1f);
            bg.raycastTarget = false;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(2, 2, 3, 3);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            var lbl = CreateText(go.transform, "Lbl", label, 10, new Color(0.68f, 0.78f, 0.72f));
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;

            var val = CreateText(go.transform, "Val", initVal, 16, valColor);
            val.alignment = TextAlignmentOptions.Center;
            val.fontStyle = FontStyles.Bold;
            val.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            return (go, val);
        }

        private static GameObject CreateSectionHeader(Transform parent, string title, string sub)
        {
            var go = CreateUIGameObject($"SectionHeader_{title}", parent);
            go.AddComponent<LayoutElement>().preferredHeight = 26f;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 8f;

            var tTitle = CreateText(go.transform, "Title", title, 12, new Color(0.87f, 0.78f, 0.56f));
            tTitle.fontStyle = FontStyles.Bold;
            tTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            if (!string.IsNullOrEmpty(sub))
            {
                var tSub = CreateText(go.transform, "Sub", sub, 10, new Color(0.6f, 0.7f, 0.65f));
                tSub.alignment = TextAlignmentOptions.Right;
            }
            return go;
        }

        private static (GameObject card, Image artImg, TextMeshProUGUI titleTxt, TextMeshProUGUI subTxt, Button buyBtn, TextMeshProUGUI costTxt)
            CreateShopCard(Transform parent, string name, Sprite sprite, string title, string subtitle, string btnPrefix, int initialCost)
        {
            var card = CreatePanel(parent, $"Card_{name}",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(312f, 62f), new Color(0.1f, 0.18f, 0.15f, 1f));
            card.AddComponent<LayoutElement>().preferredHeight = 62f;

            var hlg = card.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(8, 8, 5, 5);

            // 50x50 Art Box
            var artBox = CreateUIGameObject("ArtBox", card.transform);
            var artRt = artBox.GetComponent<RectTransform>();
            artRt.sizeDelta = new Vector2(50f, 50f);
            var artBg = artBox.AddComponent<Image>();
            artBg.color = new Color(0.06f, 0.13f, 0.1f, 1f);
            artBg.raycastTarget = false;

            var imgGo = CreateUIGameObject("Sprite", artBox.transform);
            var imgRt = imgGo.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.sizeDelta = new Vector2(-4f, -4f);
            var img = imgGo.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Text column
            var textCol = CreateUIGameObject("TextCol", card.transform);
            var tcRt = textCol.GetComponent<RectTransform>();
            tcRt.sizeDelta = new Vector2(144f, 48f);
            var tcVlg = textCol.AddComponent<VerticalLayoutGroup>();
            tcVlg.childAlignment = TextAnchor.MiddleLeft;
            tcVlg.spacing = 2f;

            var tTitle = CreateText(textCol.transform, "Title", title, 14, new Color(0.94f, 0.92f, 0.84f));
            tTitle.fontStyle = FontStyles.Bold;
            var tSub = CreateText(textCol.transform, "Sub", subtitle, 10, new Color(0.68f, 0.78f, 0.72f));

            // Buy Button
            var btn = CreateButton(card.transform, "BuyBtn", $"{btnPrefix}\n{initialCost}", new Vector2(80f, 46f));
            var costTxt = btn.GetComponentInChildren<TextMeshProUGUI>();
            costTxt.fontSize = 11;
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
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            var go = CreateUIGameObject(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.32f, 0.26f, 1f);
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
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.94f, 0.92f, 0.84f);
            tmp.raycastTarget = false;
            if (s_fontAsset != null) tmp.font = s_fontAsset;

            return btn;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = CreateUIGameObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.raycastTarget = false;
            if (s_fontAsset != null) tmp.font = s_fontAsset;
            return tmp;
        }
    }
}
