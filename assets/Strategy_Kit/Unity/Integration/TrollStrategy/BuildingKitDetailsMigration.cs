using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Replaces the bare Unity cubes in the building prefabs with Vitaria kit details and gives the barracks and the
    /// warehouse their own models (Strategy_Kit Colony/Buildings exports):
    /// <list type="bullet">
    /// <item>Barracks: KitBarracks Bld_House -> Bld_Barracks (0.49); Warehouse: KitWarehouse Bld_House -> Bld_Warehouse
    /// (0.56). Their fake door cubes (DoorJambLeft/Right, DoorLintel, OpenDoorway, OpenDoorLeaf) and EntranceSill go:
    /// the new models have their own gates and stone steps.</item>
    /// <item>Every other prefab: EntranceSill cube -> Prop_Doorstep, sized to the sill's width.</item>
    /// <item>Every prefab: EntranceApproach cube -> Env_EntrancePath, sized to the approach's width.</item>
    /// <item>Every prefab: SelectionRim bars North/South/East/West -> four FX_Select_Corner at the footprint corners
    /// (SelectionRim keeps four children; BuildingModel.SetHighlighted still toggles it).</item>
    /// </list>
    /// EntranceAnchor is not moved. Running it again changes nothing.
    /// </summary>
    public static class BuildingKitDetailsMigration
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Buildings";
        private const string Models = "Assets/Vitaria/Models/";
        // every Vitaria model inside a building prefab is turned like this: the FBX's Y-up into the prefab's map plane
        // (local -Z up, +Y north), facade to -Y
        private static readonly Quaternion KitRotation = new Quaternion(0f, 0.7071068f, -0.7071068f, 0f);
        private const float DoorstepWidth = 0.52f;     // Prop_Doorstep at scale 1
        private const float PathWidth = 0.70f;         // Env_EntrancePath at scale 1
        private const float CornerLift = 0.02f;        // corners stand 2 cm above the ground (local -Z is up)
        private static readonly string[] DoorCubes =
            { "DoorJambLeft", "DoorJambRight", "DoorLintel", "OpenDoorway", "OpenDoorLeaf", "EntranceSill" };
        private static readonly string[] RimBars = { "North", "South", "East", "West" };

        [MenuItem("TrollStrategy/Buildings/Migrate To Kit Details")]
        public static void MigrateAll()
        {
            var doorstep = Load("Colony/Prop_Doorstep.fbx");
            var path = Load("Colony/Env_EntrancePath.fbx");
            var corner = Load("Colony/FX_Select_Corner.fbx");
            var barracks = Load("Buildings/Bld_Barracks.fbx");
            var warehouse = Load("Buildings/Bld_Warehouse.fbx");
            int changedPrefabs = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                string kind = Path.GetFileNameWithoutExtension(prefabPath);
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var model = root.transform.Find("Model");
                    if (model == null) continue;
                    bool changed = false;
                    if (kind == "Barracks" || kind == "Warehouse")
                    {
                        changed |= kind == "Barracks"
                            ? ReplaceModel(model, "KitBarracks", barracks, 0.49f, new Vector3(-0.023f, 0.17f, 0f))
                            : ReplaceModel(model, "KitWarehouse", warehouse, 0.56f, new Vector3(0f, 0.045f, 0f));
                        foreach (var cube in DoorCubes) changed |= Remove(model, cube);
                    }
                    else
                    {
                        changed |= ReplaceCube(model, "EntranceSill", doorstep, "Doorstep", DoorstepWidth);
                    }
                    changed |= ReplaceCube(model, "EntranceApproach", path, "EntrancePath", PathWidth);
                    changed |= ReplaceRim(model, corner);
                    if (!changed) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    changedPrefabs++;
                    Debug.Log($"[Buildings] {kind}: kit details in place", AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Buildings] Kit details: {changedPrefabs} prefabs changed");
        }

        private static GameObject Load(string relative)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + relative);
            if (asset == null) throw new InvalidOperationException($"Missing kit model {Models + relative}: import the kit first");
            return asset;
        }

        private static GameObject Place(GameObject source, Transform parent, string name, Vector3 localPosition,
            Quaternion localRotation, float scale)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one * scale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            return instance;
        }

        private static bool ReplaceModel(Transform model, string childName, GameObject source, float scale, Vector3 offset)
        {
            var child = model.Find(childName);
            if (child != null && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == source) return false;
            int index = child != null ? child.GetSiblingIndex() : model.childCount;
            if (child != null) Object.DestroyImmediate(child.gameObject);
            var visual = Place(source, model, childName, offset, KitRotation, scale);
            visual.transform.SetSiblingIndex(Mathf.Min(index, model.childCount - 1));
            return true;
        }

        private static bool Remove(Transform model, string childName)
        {
            var child = model.Find(childName);
            if (child == null) return false;
            Object.DestroyImmediate(child.gameObject);
            return true;
        }

        /// <summary>Kit detail on the ground where the cube stood, uniformly scaled to the cube's width.</summary>
        private static bool ReplaceCube(Transform model, string cubeName, GameObject source, string newName, float naturalWidth)
        {
            var cube = model.Find(cubeName);
            if (cube == null) return false;
            var at = cube.localPosition;
            float scale = Mathf.Clamp(cube.localScale.x / naturalWidth, 0.6f, 1.4f);
            int index = cube.GetSiblingIndex();
            Object.DestroyImmediate(cube.gameObject);
            var detail = Place(source, model, newName, new Vector3(at.x, at.y, 0f), KitRotation, scale);
            detail.transform.SetSiblingIndex(Mathf.Min(index, model.childCount - 1));
            return true;
        }

        /// <summary>Four gold corners instead of the four bars; the bars tell where the footprint edges are.</summary>
        private static bool ReplaceRim(Transform model, GameObject corner)
        {
            var rim = model.Find("SelectionRim");
            if (rim == null) return false;
            var east = rim.Find("East");
            var north = rim.Find("North");
            if (east == null || north == null) return false;       // already migrated
            float halfW = Mathf.Abs(east.localPosition.x);
            float halfH = Mathf.Abs(north.localPosition.y);
            foreach (var bar in RimBars)
            {
                var child = rim.Find(bar);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
            // SW corner has its arms along +X and +Y; the others turn about the prefab's local Z (its vertical)
            Place(corner, rim, "CornerSW", new Vector3(-halfW, -halfH, -CornerLift), KitRotation, 1f);
            Place(corner, rim, "CornerSE", new Vector3(halfW, -halfH, -CornerLift),
                Quaternion.AngleAxis(90f, Vector3.forward) * KitRotation, 1f);
            Place(corner, rim, "CornerNE", new Vector3(halfW, halfH, -CornerLift),
                Quaternion.AngleAxis(180f, Vector3.forward) * KitRotation, 1f);
            Place(corner, rim, "CornerNW", new Vector3(-halfW, halfH, -CornerLift),
                Quaternion.AngleAxis(270f, Vector3.forward) * KitRotation, 1f);
            return true;
        }
    }
}
