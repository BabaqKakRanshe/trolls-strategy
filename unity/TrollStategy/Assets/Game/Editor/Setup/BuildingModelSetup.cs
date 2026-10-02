using System;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Puts the kit model of a building added after the first kit (Tavern, HaulersGuild) into its BuildingBase
    /// variant: the stand-in's Kit* model instance is replaced by Assets/Vitaria/Models/Buildings/Bld_&lt;Kind&gt;.fbx
    /// at the same place, the catalog icon and the product shown over it are set from the icon atlases. Run after
    /// TrollStrategy/Dev/Import Icon Atlases. Idempotent.
    /// </summary>
    public static class BuildingModelSetup
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Buildings/";
        private const string ModelFolder = "Assets/Vitaria/Models/Buildings/";
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private const string ResourceAtlas = "Assets/Game/Art/Sprites/Atlases/resources-icons-0.png";
        private const string BuildingAtlas = "Assets/Game/Art/Sprites/Atlases/buildings-icons-0.png";

        private static readonly (BuildingKind Kind, string Product)[] Buildings =
        {
            (BuildingKind.Tavern, "feast"),
            (BuildingKind.HaulersGuild, null)
        };

        [MenuItem("TrollStrategy/Dev/Setup New Building Models")]
        public static void Apply()
        {
            var products = AssetDatabase.LoadAllAssetsAtPath(ResourceAtlas).OfType<Sprite>().ToDictionary(s => s.name);
            var icons = AssetDatabase.LoadAllAssetsAtPath(BuildingAtlas).OfType<Sprite>().ToDictionary(s => s.name);
            foreach (var (kind, product) in Buildings)
            {
                string prefabPath = PrefabFolder + kind + ".prefab";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "Bld_" + kind + ".fbx")
                    ?? throw new InvalidOperationException("Missing model " + ModelFolder + "Bld_" + kind + ".fbx");
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var model = root.GetComponentInChildren<BuildingModel>(true)
                        ?? throw new InvalidOperationException(prefabPath + " has no BuildingModel");
                    var old = model.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t != model.transform && t.name.StartsWith("Kit", StringComparison.Ordinal))
                        ?? throw new InvalidOperationException(prefabPath + " has no Kit* model to replace");
                    if (PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) != fbx)
                    {
                        var parent = old.parent;
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
                        instance.transform.SetLocalPositionAndRotation(old.localPosition, old.localRotation);
                        instance.transform.localScale = old.localScale;
                        instance.transform.SetSiblingIndex(old.GetSiblingIndex());
                        instance.name = "Kit" + kind;
                        UnityEngine.Object.DestroyImmediate(old.gameObject);
                    }
                    // the production bar floats above the roof, as the kit contract asks (BuildingPresentationTests)
                    var bar = root.transform.Find("ProductionProgress");
                    if (bar != null)
                    {
                        float top = 0f;
                        var kit = model.transform.GetComponentsInChildren<Transform>(true)
                            .First(t => t.name == "Kit" + kind);
                        foreach (var filter in kit.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (filter.sharedMesh == null) continue;
                            var bounds = filter.sharedMesh.bounds;
                            var toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                            for (int c = 0; c < 8; c++)
                                top = Mathf.Max(top, -toRoot.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                                    new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))).z);
                        }
                        var position = bar.localPosition;
                        position.z = -Mathf.Max(-position.z, Mathf.Max(2.1f, top + .3f));
                        bar.localPosition = position;
                    }
                    var serialized = new SerializedObject(model);
                    serialized.FindProperty("_outgoingProductSprite").objectReferenceValue =
                        product != null && products.TryGetValue(product, out var sprite) ? sprite : null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(DefinitionFolder + "Building_" + kind + ".asset");
                if (definition != null && icons.TryGetValue("BuildingIcon_" + kind, out var icon))
                {
                    var serializedDefinition = new SerializedObject(definition);
                    serializedDefinition.FindProperty("_icon").objectReferenceValue = icon;
                    serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(definition);
                }
                Debug.Log($"[BuildingModelSetup] {kind}: model {fbx.name}, icon {(definition != null ? definition.Icon?.name : "-")}");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
