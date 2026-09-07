using UnityEditor;
using UnityEngine;
using TMPro;

namespace TrollStrategy.Editor.Setup
{
    public static class FontTester
    {
        public static TMP_FontAsset CreateOrGetArial()
        {
            string path = "Assets/Game/Art/Fonts/Arial Dynamic.asset";
            AssetDatabase.DeleteAsset(path);

            var fa = TMP_FontAsset.CreateFontAsset("Arial", "Regular");
            if (fa == null)
            {
                Debug.LogError("[FontTester] Failed to create Arial font asset!");
                return null;
            }

            fa.name = "Arial Dynamic";
            AssetDatabase.CreateAsset(fa, path);

            if (fa.material != null)
            {
                fa.material.name = "Arial Dynamic Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            if (fa.atlasTextures != null)
            {
                for (int i = 0; i < fa.atlasTextures.Length; i++)
                {
                    var tex = fa.atlasTextures[i];
                    if (tex != null)
                    {
                        tex.name = $"Arial Dynamic Atlas {i}";
                        AssetDatabase.AddObjectToAsset(tex, fa);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FontTester] Saved {path} with material and atlas textures successfully!");
            return fa;
        }
    }
}