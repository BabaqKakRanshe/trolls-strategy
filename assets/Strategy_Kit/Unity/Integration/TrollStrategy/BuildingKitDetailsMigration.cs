using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Puts every building prefab (Assets/Game/Prefabs/Buildings) on the Vitaria kit's building contract:
    /// <list type="bullet">
    /// <item>One scale for every kit model: <see cref="KitScale"/> (2 m of the kit = 1 m of the game), no per-prefab
    /// offset. The kit centres each model on its footprint and fits it inside (Strategy_Kit
    /// Blender/vitaria_buildings/__init__.py: KIT_SCALE, FOOTPRINT), so doors and windows match between buildings.
    /// Before this, each model was squeezed into its footprint with its own scale (0.40–0.71).</item>
    /// <item>Barracks and Warehouse get their own models (Bld_Barracks, Bld_Warehouse) instead of Bld_House; their
    /// fake door cubes (DoorJambLeft/Right, DoorLintel, OpenDoorway, OpenDoorLeaf) go: the models have gates.</item>
    /// <item>Footprint: Env_Footprint_WxH under the model — trodden earth on the whole footprint, the same base for
    /// every building; it also covers the flat clover of the colony field.</item>
    /// <item>EntranceSill cubes go (the kit buildings have their own steps; open workshops need none).
    /// EntranceApproach -> Env_EntrancePath on the front edge of the footprint, created where it was missing.</item>
    /// <item>EntranceAnchor moves onto the path at the front edge of the footprint (12 cm inside it): the models now
    /// fill their footprints, and the old anchors (0.35 cells from the edge) stood inside their front props.
    /// Run TrollStrategy > Bake Building Entrances afterwards: it copies the anchors into the BuildingDefinitions.</item>
    /// <item>SelectionRim bars North/South/East/West -> four FX_Select_Corner at the footprint corners
    /// (SelectionRim keeps four children; BuildingModel.SetHighlighted still toggles it).</item>
    /// <item>ProductionProgress stays at BuildingBase's 2.1 m, or <see cref="BarClearance"/> above the top of a kit
    /// model that is taller than that: at the kit scale some models outgrew the old squeezed ones.</item>
    /// </list>
    /// Running it again changes nothing.
    /// </summary>
    public static class BuildingKitDetailsMigration
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Buildings";
        private const string Models = "Assets/Vitaria/Models/";
        public const float KitScale = 0.5f;            // vitaria_buildings.KIT_SCALE
        // every Vitaria model inside a building prefab is turned like this: the FBX's Y-up into the prefab's map plane
        // (local -Z up, +Y north), facade to -Y
        private static readonly Quaternion KitRotation = new Quaternion(0f, 0.7071068f, -0.7071068f, 0f);
        private const float PathOut = 0.08f;           // Env_EntrancePath centre: this far in front of the footprint
        private const float AnchorIn = 0.12f;          // EntranceAnchor: on the path, 12 cm inside the footprint's front edge
        private const float CornerLift = 0.02f;        // corners stand 2 cm above the ground (local -Z is up)
        public const float BarClearance = 0.3f;        // ProductionProgress: at least this far above the model's highest point
        private const float BarHeight = 2.1f;          // ProductionProgress height in BuildingBase
        private static readonly string[] DoorCubes =
            { "DoorJambLeft", "DoorJambRight", "DoorLintel", "OpenDoorway", "OpenDoorLeaf" };
        private static readonly string[] RimBars = { "North", "South", "East", "West" };

        [MenuItem("TrollStrategy/Buildings/Migrate To Kit Details")]
        public static void MigrateAll()
        {
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
                    if (!Footprint(model, out float halfW, out float halfH))
                    {
                        Debug.LogWarning($"[Buildings] {kind}: no SelectionRim to read the footprint from, skipped");
                        continue;
                    }
                    bool changed = false;
                    if (kind == "Barracks" || kind == "Warehouse")
                    {
                        changed |= ReplaceModel(model, "Kit" + kind, kind == "Barracks" ? barracks : warehouse);
                        foreach (var cube in DoorCubes) changed |= Remove(model, cube);
                    }
                    changed |= FitKitModel(model, kind);
                    changed |= PlacePad(model, halfW, halfH);
                    changed |= Remove(model, "EntranceSill");
                    changed |= Remove(model, "Doorstep");
                    changed |= PlacePath(model, path, halfH);
                    changed |= MoveAnchor(model, halfH);
                    changed |= ReplaceRim(model, corner, halfW, halfH);
                    changed |= PlaceProgressBar(root.transform, model, kind);
                    if (!changed) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    changedPrefabs++;
                    Debug.Log($"[Buildings] {kind}: kit contract applied", AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Buildings] Kit contract: {changedPrefabs} prefabs changed. Now run TrollStrategy > Bake Building Entrances.");
        }

        private static GameObject Load(string relative)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + relative);
            if (asset == null) throw new InvalidOperationException($"Missing kit model {Models + relative}: import the kit first");
            return asset;
        }

        /// <summary>Half sizes of the footprint in model units: from the rim bars, or from the corners once migrated.</summary>
        private static bool Footprint(Transform model, out float halfW, out float halfH)
        {
            halfW = halfH = 0f;
            var rim = model.Find("SelectionRim");
            if (rim == null) return false;
            var east = rim.Find("East");
            var north = rim.Find("North");
            var ne = rim.Find("CornerNE");
            if (east != null && north != null)
            {
                halfW = Mathf.Abs(east.localPosition.x);
                halfH = Mathf.Abs(north.localPosition.y);
            }
            else if (ne != null)
            {
                halfW = Mathf.Abs(ne.localPosition.x);
                halfH = Mathf.Abs(ne.localPosition.y);
            }
            return halfW > 0f && halfH > 0f;
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

        private static bool Same(Transform t, Vector3 position, Quaternion rotation, float scale) =>
            (t.localPosition - position).sqrMagnitude < 1e-8f && Quaternion.Angle(t.localRotation, rotation) < 0.01f &&
            Mathf.Abs(t.localScale.x - scale) < 1e-5f && Mathf.Abs(t.localScale.y - scale) < 1e-5f;

        private static void Set(Transform t, Vector3 position, Quaternion rotation, float scale)
        {
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = Vector3.one * scale;
        }

        /// <summary>Deletes the direct child <paramref name="name"/> of the model; false when there is none.</summary>
        private static bool Remove(Transform model, string name)
        {
            var child = model.Find(name);
            if (child == null) return false;
            Object.DestroyImmediate(child.gameObject);
            return true;
        }

        private static bool ReplaceModel(Transform model, string childName, GameObject source)
        {
            var child = model.Find(childName);
            if (child != null && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == source) return false;
            int index = child != null ? child.GetSiblingIndex() : model.childCount;
            if (child != null) Object.DestroyImmediate(child.gameObject);
            var visual = Place(source, model, childName, Vector3.zero, KitRotation, KitScale);
            visual.transform.SetSiblingIndex(Mathf.Min(index, model.childCount - 1));
            return true;
        }

        /// <summary>Kit model of the building (Model/Kit&lt;Kind&gt;): the one scale, centred on the footprint.</summary>
        private static bool FitKitModel(Transform model, string kind)
        {
            var kit = model.Find("Kit" + kind);
            if (kit == null)
            {
                Debug.LogWarning($"[Buildings] {kind}: no Model/Kit{kind}");
                return false;
            }
            if (Same(kit, Vector3.zero, KitRotation, KitScale)) return false;
            Set(kit, Vector3.zero, KitRotation, KitScale);
            return true;
        }

        /// <summary>Env_Footprint_WxH under the model, at the footprint centre on the ground.</summary>
        private static bool PlacePad(Transform model, float halfW, float halfH)
        {
            int w = Mathf.RoundToInt(halfW * 2f), h = Mathf.RoundToInt(halfH * 2f);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}Colony/Env_Footprint_{w}x{h}.fbx");
            if (source == null)
            {
                Debug.LogWarning($"[Buildings] no Env_Footprint_{w}x{h} in the kit for a {w}x{h} footprint");
                return false;
            }
            var pad = model.Find("Footprint");
            if (pad != null && PrefabUtility.GetCorrespondingObjectFromSource(pad.gameObject) == source)
            {
                if (Same(pad, Vector3.zero, KitRotation, 1f)) return false;
                Set(pad, Vector3.zero, KitRotation, 1f);
                return true;
            }
            if (pad != null) Object.DestroyImmediate(pad.gameObject);
            Place(source, model, "Footprint", Vector3.zero, KitRotation, 1f).transform.SetAsFirstSibling();
            return true;
        }

        /// <summary>Env_EntrancePath straddling the front edge of the footprint (its back half lies on the pad).</summary>
        private static bool PlacePath(Transform model, GameObject source, float halfH)
        {
            var at = new Vector3(0f, -halfH - PathOut, 0f);
            var cube = model.Find("EntranceApproach");
            int index = model.childCount;
            if (cube != null)
            {
                index = cube.GetSiblingIndex();
                Object.DestroyImmediate(cube.gameObject);
            }
            var path = model.Find("EntrancePath");
            if (path != null)
            {
                if (Same(path, at, KitRotation, 1f)) return cube != null;
                Set(path, at, KitRotation, 1f);
                return true;
            }
            Place(source, model, "EntrancePath", at, KitRotation, 1f).transform
                .SetSiblingIndex(Mathf.Min(index, model.childCount - 1));
            return true;
        }

        /// <summary>Workers gather on the entrance path at the front edge of the footprint, not inside its front props.</summary>
        private static bool MoveAnchor(Transform model, float halfH)
        {
            var anchor = model.Find(BuildingEntranceBaker.AnchorName);
            if (anchor == null) return false;
            var at = new Vector3(0f, -halfH + AnchorIn, anchor.localPosition.z);
            if ((anchor.localPosition - at).sqrMagnitude < 1e-8f) return false;
            anchor.localPosition = at;
            return true;
        }

        /// <summary>Four gold corners instead of the four bars (or corners put back where they belong).</summary>
        private static bool ReplaceRim(Transform model, GameObject corner, float halfW, float halfH)
        {
            var rim = model.Find("SelectionRim");
            if (rim == null) return false;
            bool changed = false;
            foreach (var bar in RimBars)
            {
                var child = rim.Find(bar);
                if (child == null) continue;
                Object.DestroyImmediate(child.gameObject);
                changed = true;
            }
            // SW corner has its arms along +X and +Y; the others turn about the prefab's local Z (its vertical)
            changed |= Corner(rim, corner, "CornerSW", new Vector3(-halfW, -halfH, -CornerLift), KitRotation);
            changed |= Corner(rim, corner, "CornerSE", new Vector3(halfW, -halfH, -CornerLift),
                Quaternion.AngleAxis(90f, Vector3.forward) * KitRotation);
            changed |= Corner(rim, corner, "CornerNE", new Vector3(halfW, halfH, -CornerLift),
                Quaternion.AngleAxis(180f, Vector3.forward) * KitRotation);
            changed |= Corner(rim, corner, "CornerNW", new Vector3(-halfW, halfH, -CornerLift),
                Quaternion.AngleAxis(270f, Vector3.forward) * KitRotation);
            return changed;
        }

        /// <summary>The production bar above the kit model's highest point (in the prefab root's frame, -Z is up).</summary>
        private static bool PlaceProgressBar(Transform root, Transform model, string kind)
        {
            var bar = root.Find("ProductionProgress");
            var kit = model.Find("Kit" + kind);
            if (bar == null || kit == null) return false;
            float top = 0f;
            foreach (var filter in kit.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    top = Mathf.Max(top, -toRoot.MultiplyPoint3x4(local).z);
                }
            }
            float z = -Mathf.Max(BarHeight, Mathf.Ceil((top + BarClearance) * 100f) / 100f);
            if (Mathf.Abs(bar.localPosition.z - z) < 1e-4f) return false;
            bar.localPosition = new Vector3(bar.localPosition.x, bar.localPosition.y, z);
            return true;
        }

        private static bool Corner(Transform rim, GameObject source, string name, Vector3 at, Quaternion rotation)
        {
            var existing = rim.Find(name);
            if (existing != null)
            {
                if (Same(existing, at, rotation, 1f)) return false;
                Set(existing, at, rotation, 1f);
                return true;
            }
            Place(source, rim, name, at, rotation, 1f);
            return true;
        }
    }
}
