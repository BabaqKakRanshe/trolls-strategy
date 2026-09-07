using UnityEditor;
using UnityEngine;
using TrollStrategy.Presentation.Buildings;

namespace TrollStrategy.Editor.Setup
{
    public static class CheckSceneObjects
    {
        [MenuItem("TrollStrategy/Debug Check Scene Objects")]
        public static void Check()
        {
            var gs = GameObject.Find("GameSystems");
            Debug.Log($"[CheckScene] GameSystems found: {gs != null}");
            if (gs != null)
            {
                Debug.Log($"[CheckScene] GameSystems childCount: {gs.transform.childCount}");
                for (int i = 0; i < gs.transform.childCount; i++)
                {
                    var child = gs.transform.GetChild(i);
                    Debug.Log($"[CheckScene]   Child {i}: {child.name}, children: {child.childCount}");
                    for (int j = 0; j < child.childCount; j++)
                    {
                        var gc = child.GetChild(j);
                        Debug.Log($"[CheckScene]     Grandchild {j}: {gc.name}, pos: {gc.position}, sprite: {gc.GetComponent<SpriteRenderer>()?.sprite != null}");
                    }
                }
            }

            var bViews = Object.FindObjectsByType<BuildingView>(FindObjectsSortMode.None);
            Debug.Log($"[CheckScene] Total BuildingViews found: {bViews.Length}");
            foreach (var b in bViews)
            {
                var sr = b.GetComponent<SpriteRenderer>();
                Debug.Log($"[CheckScene]   Building: {b.name}, id: {b.BuildingId}, pos: {b.transform.position}, sprite: {sr?.sprite?.name ?? "null"}, order: {sr?.sortingOrder}");
            }
        }
    }
}
