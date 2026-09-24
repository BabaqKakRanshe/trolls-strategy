using UnityEditor;
using UnityEngine;

public static class KitBoundsDiagnostic
{
    public static void Dump()
    {
        foreach (var name in new[] { "Bld_House", "Bld_Mine", "Bld_Market" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Vitaria/Models/Buildings/{name}.fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            Debug.Log($"KIT {name}");
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                Debug.Log($"KIT {name} mesh={filter.name} local={filter.transform.localPosition} bounds={filter.sharedMesh.bounds}");
            Object.DestroyImmediate(instance);
        }
    }
}
