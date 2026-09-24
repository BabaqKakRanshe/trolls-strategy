using System;
using TrollStrategy.Content;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Visuals;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    // Preserve the existing gameplay prefab identities while replacing their visual children.
    public static class DioramaModelBuilder
    {
        private const string Models = "Assets/Vitaria/Models";
        private const string Prefabs = "Assets/Game/Prefabs/Buildings";
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        [MenuItem("TrollStrategy/Rebuild Diorama Buildings")]
        public static void RebuildAll()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before rebuilding building prefabs.");

            Rebuild(BuildingKind.Mine);
            Rebuild(BuildingKind.Warehouse);
            Rebuild(BuildingKind.Market);
            Rebuild(BuildingKind.Barracks);
            AssetDatabase.SaveAssets();
        }

        public static void Rebuild(BuildingKind kind)
        {
            var path = $"{Prefabs}/{kind}Model.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var component = root.GetComponent<PrimitiveBuilding>();
                if (component == null || component.Kind != kind)
                    throw new InvalidOperationException($"Invalid building prefab: {path}");

                var rim = root.transform.Find("SelectionRim");
                for (var i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child != rim) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                // BuildingView is in map-local XY. Rotate the imported Unity Y-up model
                // into that plane, then BuildingView's Grid rotation places it upright.
                root.transform.localRotation = Quaternion.identity;
                switch (kind)
                {
                    case BuildingKind.Mine:
                        Add(root.transform, "Buildings/Bld_Mine", "KitMine", Vector2.zero, .72f);
                        Add(root.transform, "Props/Prop_Minecart_Ore", "OreCart", new Vector2(.88f, -1.03f), .62f);
                        Add(root.transform, "Resources/Res_OrePile_Iron", "OrePile", new Vector2(-.96f, -.96f), .42f);
                        break;
                    case BuildingKind.Warehouse:
                        Add(root.transform, "Buildings/Bld_House", "KitWarehouse", Vector2.zero, .74f);
                        Add(root.transform, "Props/Prop_Crate", "WarehouseCrate", new Vector2(.99f, -1.06f), .68f);
                        Add(root.transform, "Props/Prop_Barrel", "WarehouseBarrel", new Vector2(-1.05f, -1.02f), .67f);
                        break;
                    case BuildingKind.Market:
                        Add(root.transform, "Buildings/Bld_Market", "KitMarket", Vector2.zero, .78f);
                        Add(root.transform, "Resources/Res_CoinPile", "MarketCoins", new Vector2(1.08f, -.66f), .48f);
                        break;
                    case BuildingKind.Barracks:
                        Add(root.transform, "Buildings/Bld_House", "KitBarracks", new Vector2(0f, .16f), .69f);
                        Add(root.transform, "Props/Prop_Fence", "TrainingFenceWest", new Vector2(-1.05f, -1.22f), .46f);
                        Add(root.transform, "Props/Prop_Fence", "TrainingFenceEast", new Vector2(1.05f, -1.22f), .46f);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind));
                }

                if (rim == null) rim = CreateRim(root.transform, kind);
                rim.gameObject.SetActive(false);
                var serialized = new SerializedObject(component);
                serialized.FindProperty("_selectionRim").objectReferenceValue = rim;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Add(Transform parent, string modelPath, string name, Vector2 position, float scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{modelPath}.fbx");
            if (source == null) throw new InvalidOperationException($"Vitaria model missing: {modelPath}");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            instance.name = name;
            instance.transform.localPosition = new Vector3(position.x, position.y, 0f);
            // The kit's façades face +Z; the colony camera approaches from -Z.
            instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = Vector3.one * scale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private static Transform CreateRim(Transform parent, BuildingKind kind)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Building catalog missing");
            var definition = catalog.GetBuilding(kind);
            var rim = new GameObject("SelectionRim").transform;
            rim.SetParent(parent, false);
            var x = definition.Width * .5f;
            var y = definition.Height * .5f;
            PrimitiveArt.Box(rim, "South", new Vector3(0f, -y, -.13f), new Vector3(definition.Width + .1f, .07f, .055f), ColonyPalette.Gold);
            PrimitiveArt.Box(rim, "North", new Vector3(0f, y, -.13f), new Vector3(definition.Width + .1f, .07f, .055f), ColonyPalette.Gold);
            PrimitiveArt.Box(rim, "West", new Vector3(-x, 0f, -.13f), new Vector3(.07f, definition.Height + .1f, .055f), ColonyPalette.Gold);
            PrimitiveArt.Box(rim, "East", new Vector3(x, 0f, -.13f), new Vector3(.07f, definition.Height + .1f, .055f), ColonyPalette.Gold);
            return rim;
        }
    }
}
