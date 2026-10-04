using System;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Puts the kit's level models next to each building's level-1 model. Bld_&lt;Name&gt;_L2 and _L3 (the FBX of the
    /// Kit&lt;Kind&gt; model plus the suffix) go into Model as Kit&lt;Kind&gt;_L2 and _L3, at the same local position,
    /// rotation and scale, hidden; BuildingModel learns the three models and the top of each, so the view shows the
    /// building's level and floats the production bar over that roof. Run after a kit export and after
    /// BuildingModelSetup. Idempotent.
    /// </summary>
    public static class BuildingLevelModelSetup
    {
        public const int Levels = 3;
        private const string PrefabFolder = "Assets/Game/Prefabs/Buildings/";

        [MenuItem("TrollStrategy/Dev/Setup Building Level Models")]
        public static void Apply()
        {
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                string prefabPath = PrefabFolder + kind + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var model = root.GetComponentInChildren<BuildingModel>(true)
                        ?? throw new InvalidOperationException(prefabPath + " has no BuildingModel");
                    var kit = model.transform.Find("Kit" + kind)
                        ?? throw new InvalidOperationException(prefabPath + " has no Kit" + kind + " model");
                    string source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(kit.gameObject));
                    if (!source.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"{prefabPath}: Kit{kind} is no instance of a kit FBX");

                    var models = new GameObject[Levels];
                    models[0] = kit.gameObject;
                    kit.gameObject.SetActive(true);
                    for (int level = 2; level <= Levels; level++)
                    {
                        string path = source.Substring(0, source.Length - ".fbx".Length) + "_L" + level + ".fbx";
                        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                            ?? throw new InvalidOperationException("Missing level model " + path);
                        string name = LevelModelName(kind, level);
                        var instance = kit.parent.Find(name)?.gameObject;
                        if (instance != null && PrefabUtility.GetCorrespondingObjectFromSource(instance) != fbx)
                        {
                            UnityEngine.Object.DestroyImmediate(instance);
                            instance = null;
                        }
                        if (instance == null)
                        {
                            instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, kit.parent);
                            instance.name = name;
                        }
                        instance.transform.SetLocalPositionAndRotation(kit.localPosition, kit.localRotation);
                        instance.transform.localScale = kit.localScale;
                        instance.SetActive(false);
                        models[level - 1] = instance;
                    }

                    var serialized = new SerializedObject(model);
                    var modelList = serialized.FindProperty("_levelModels");
                    var topList = serialized.FindProperty("_levelTops");
                    modelList.arraySize = Levels;
                    topList.arraySize = Levels;
                    var tops = new string[Levels];
                    for (int i = 0; i < Levels; i++)
                    {
                        float top = Top(root.transform, models[i].transform);
                        modelList.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
                        topList.GetArrayElementAtIndex(i).floatValue = top;
                        tops[i] = top.ToString("0.00");
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[BuildingLevelModelSetup] {kind}: levels from {source}, tops {string.Join(" / ", tops)} m");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        public static string LevelModelName(BuildingKind kind, int level) => "Kit" + kind + "_L" + level;

        /// <summary>Whether a child of Model is one of the level models this setup adds.</summary>
        public static bool IsLevelModel(string name)
        {
            for (int level = 2; level <= Levels; level++)
                if (name.EndsWith("_L" + level, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The highest point of a model above the ground, m: -z of the building root points up.</summary>
        public static float Top(Transform root, Transform model)
        {
            float top = 0f;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                var toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                    top = Mathf.Max(top, -toRoot.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))).z);
            }
            return top;
        }
    }
}
