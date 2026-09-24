using System;
using System.IO;
using System.Linq;
using TrollStrategy.Presentation.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Editor.Setup
{
    // Locally authored procedural material textures; never touches sprite materials or imports.
    public static class DioramaSurfaceSetup
    {
        public static void Apply(UniversalRendererData renderer)
        {
            const string folder = "Assets/Game/Art/DioramaTextures";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Game/Art", "DioramaTextures");
            var meadow = Texture(folder, "Meadow", 256, Meadow);
            var wood = Texture(folder, "WoodGrain", 64, (x, y) =>
            {
                float grain = Mathf.PerlinNoise(x * .19f, y * .028f);
                float v = .82f + .18f * grain + .035f * Mathf.Sin(x * .7f + grain * 4);
                return new Color(v, v, v, 1);
            });
            var stone = Texture(folder, "StoneGrain", 64, (x, y) =>
            {
                float v = .87f + .13f * Mathf.PerlinNoise(x * .14f, y * .14f);
                return new Color(v, v, v, 1);
            });
            var dirt = Texture(folder, "PathGrain", 64, (x, y) =>
            {
                float v = .89f + .11f * Mathf.PerlinNoise(x * .12f, y * .12f);
                return new Color(v, v, v, 1);
            });
            Assign(PrimitiveEnvironment.Field, meadow, Color.white);
            Assign(PrimitiveEnvironment.Road, dirt);
            Assign(new Color32(196, 162, 116, 255), dirt);
            foreach (string key in new[] { "69442B", "A17549", "B78C5A" }) Assign(key, wood);
            foreach (string key in new[] { "818775", "9B9D85", "A6462F", "BF5B39" }) Assign(key, stone);

            // Faceted rock meshes have no imported UV unwrap. Give them a stable local projection.
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Game/Art/Meshes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name != "DioramaRock" && !name.StartsWith("Diorama_Mine_")) continue;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var vertices = mesh.vertices;
                var uv = new Vector2[vertices.Length];
                for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(vertices[i].x + vertices[i].y * .31f, -vertices[i].z + vertices[i].y * .27f);
                mesh.uv = uv;
                EditorUtility.SetDirty(mesh);
            }
            ConfigureContactShadows(renderer);
        }

        private static Color Meadow(int x, int y)
        {
            float wx = x / 255f * 22 - 4, wy = y / 255f * 22 - 4;
            float noise = Mathf.PerlinNoise(wx * .32f + 11, wy * .32f + 19);
            float detail = Mathf.PerlinNoise(wx * 3.8f + 5, wy * 3.8f + 7);
            float border = Mathf.Min(wx, wy, 14 - wx, 14 - wy);
            float clearing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-1.3f, 1.4f, border + (noise - .5f) * 1.8f));
            var forest = new Color32(67, 91, 58, 255);
            var grass = Color.Lerp(new Color32(124, 141, 89, 255), new Color32(151, 159, 103, 255), noise);
            return Color.Lerp(forest, grass, clearing) * (.96f + detail * .055f);
        }

        private static Texture2D Texture(string folder, string name, int size, Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Bilinear, wrapMode = name == "Meadow" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, anisoLevel = 4 };
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) colors[y * size + x] = pixel(x, y);
            texture.SetPixels(colors);
            texture.Apply();
            string path = $"{folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing == null) { AssetDatabase.CreateAsset(texture, path); return texture; }
            EditorUtility.CopySerialized(texture, existing);
            UnityEngine.Object.DestroyImmediate(texture);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void Assign(Color color, Texture2D texture, Color? tint = null) => Assign(ColorUtility.ToHtmlStringRGB(color), texture, tint);

        private static void Assign(string key, Texture2D texture, Color? tint = null)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Game/Art/Materials/Primitive_{key}.mat");
            if (material == null) throw new InvalidOperationException($"Missing diorama surface {key}");
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", .04f);
            if (tint.HasValue) material.color = tint.Value;
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureContactShadows(UniversalRendererData renderer)
        {
            const string featureName = "Diorama Contact Shadows";
            var feature = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.name == featureName);
            if (feature == null)
            {
                // URP's built-in feature type is internal; configure its serialized settings just as its Inspector does.
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", true);
                feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = featureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            var so = new SerializedObject(feature);
            var settings = so.FindProperty("m_Settings");
            settings.FindPropertyRelative("AfterOpaque").boolValue = false;
            settings.FindPropertyRelative("Downsample").boolValue = true;
            settings.FindPropertyRelative("AOMethod").intValue = 1;
            settings.FindPropertyRelative("Source").intValue = 1;
            settings.FindPropertyRelative("Intensity").floatValue = .8f;
            settings.FindPropertyRelative("DirectLightingStrength").floatValue = .18f;
            settings.FindPropertyRelative("Radius").floatValue = .25f;
            settings.FindPropertyRelative("Falloff").floatValue = 55f;
            so.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            feature.Create();
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }
    }
}
