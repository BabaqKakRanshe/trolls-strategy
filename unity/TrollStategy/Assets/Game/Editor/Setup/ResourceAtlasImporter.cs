using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>Imports the two TexturePacker sheets and migrates references from the previous sprites.</summary>
    public static class ResourceAtlasImporter
    {
        private const string Folder = "Assets/Game/Art/Sprites/Atlases";
        private const string OldResourcesFolder = "Assets/Game/Art/Sprites/Resources/";
        private const string OldBuildingIconsFolder = "Assets/Game/Art/Sprites/BuildingIcons/";
        private const string PrefabFolder = "Assets/Game/Prefabs/Buildings/";
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private static readonly string[] RequiredProducts =
        {
            "iron-ore", "iron-ingot", "wheat", "animal-hide", "leather",
            "logs", "planks", "wooden-shield", "coins"
        };

        [Serializable] private sealed class AtlasData { public AtlasTexture[] textures; }
        [Serializable] private sealed class AtlasTexture
        {
            public string image;
            public AtlasSize size;
            public AtlasFrame[] frames;
        }
        [Serializable] private sealed class AtlasSize { public int w; public int h; }
        [Serializable] private sealed class AtlasFrame
        {
            public string filename;
            public bool rotated;
            public AtlasSize sourceSize;
            public AtlasRect spriteSourceSize;
            public AtlasRect frame;
        }
        [Serializable] private sealed class AtlasRect { public int x; public int y; public int w; public int h; }

        public static void Import()
        {
            SyncSharedSource();
            var products = ImportAtlas("resources-icons", 362f, RequiredProducts);
            var icons = ImportAtlas("buildings-icons", 32f,
                Enum.GetNames(typeof(BuildingKind)).Select(name => "BuildingIcon_" + name));

            SetProduct("Mine", products["iron-ore"]);
            SetProduct("Warehouse", products["iron-ore"]);
            SetProduct("Smeltery", products["iron-ingot"]);
            SetProduct("Field", products["wheat"]);
            SetProduct("Farm", products["animal-hide"]);
            SetProduct("Tannery", products["leather"]);
            SetProduct("LumberCamp", products["logs"]);
            SetProduct("LumberMill", products["planks"]);
            SetProduct("ShieldWorkshop", products["wooden-shield"]);
            SetSaleIncome("Market", products["coins"]);
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
                SetBuildingIcon(kind, icons["BuildingIcon_" + kind]);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ResourceAtlasImporter] Imported {products.Count} resources and {icons.Count} building icons.");
        }

        private static Dictionary<string, Sprite> ImportAtlas(string atlasName, float pixelsPerUnit,
            IEnumerable<string> requiredNames)
        {
            string texturePath = Folder + "/" + atlasName + "-0.png";
            string jsonPath = Folder + "/" + atlasName + ".json";
            var data = JsonUtility.FromJson<AtlasData>(File.ReadAllText(jsonPath));
            if (data?.textures == null || data.textures.Length != 1 ||
                data.textures[0].image != atlasName + "-0.png")
                throw new InvalidOperationException($"Expected one {atlasName}-0.png TexturePacker page.");

            var page = data.textures[0];
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (importer == null || texture == null || page.size == null ||
                texture.width != page.size.w || texture.height != page.size.h)
                throw new InvalidOperationException($"{atlasName} texture or dimensions do not match JSON.");

            var names = new HashSet<string>();
            var slices = new List<SpriteRect>();
            foreach (var item in page.frames ?? Array.Empty<AtlasFrame>())
            {
                if (item == null || item.rotated || item.frame == null || item.sourceSize == null ||
                    item.spriteSourceSize == null)
                    throw new InvalidOperationException($"Missing or rotated {atlasName} frame.");
                string name = Path.GetFileNameWithoutExtension(item.filename);
                var f = item.frame;
                var source = item.sourceSize;
                var trimmed = item.spriteSourceSize;
                if (!names.Add(name) || f.w <= 0 || f.h <= 0 || f.x < 0 || f.y < 0 ||
                    f.x + f.w > texture.width || f.y + f.h > texture.height ||
                    trimmed.w != f.w || trimmed.h != f.h || source.w <= 0 || source.h <= 0)
                    throw new InvalidOperationException($"Invalid {atlasName} frame: {item.filename}");

                int bottomTrim = source.h - trimmed.y - f.h;
                slices.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(f.x, texture.height - f.y - f.h, f.w, f.h),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(
                        (source.w * 0.5f - trimmed.x) / f.w,
                        (source.h * 0.5f - bottomTrim) / f.h)
                });
            }
            if (slices.Count == 0 || requiredNames.Any(name => !names.Contains(name)))
                throw new InvalidOperationException($"{atlasName} is missing required sprites.");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            AssetSlicer.SaveSlices(importer, slices);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath)
                .OfType<Sprite>().ToDictionary(sprite => sprite.name);
            if (!names.SetEquals(sprites.Keys))
                throw new InvalidOperationException("Imported Unity sprites do not match TexturePacker names.");
            return sprites;
        }

        private static void SyncSharedSource()
        {
            string shared = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../../assets/sprites/Atlases"));
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Game/Art/Sprites", "Atlases");
            foreach (string file in new[]
                     {
                         "resources-icons-0.png", "resources-icons.json",
                         "buildings-icons-0.png", "buildings-icons.json"
                     })
            {
                string source = Path.Combine(shared, file);
                string destination = Path.Combine(UnityEngine.Application.dataPath, "Game/Art/Sprites/Atlases", file);
                if (!File.Exists(source))
                    throw new FileNotFoundException("Missing shared resource atlas source", source);
                if (File.Exists(destination) && File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(destination)))
                    continue;
                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(Folder + "/" + file, ImportAssetOptions.ForceUpdate);
            }
        }

        // Migrate old atlas sprites while preserving a product chosen from another source.
        private static void SetProduct(string prefabName, Sprite sprite) =>
            SetModelSprite(prefabName, "_outgoingProductSprite", sprite);

        private static void SetSaleIncome(string prefabName, Sprite sprite) =>
            SetModelSprite(prefabName, "_saleIncomeSprite", sprite);

        private static void SetModelSprite(string prefabName, string fieldName, Sprite sprite)
        {
            string path = PrefabFolder + prefabName + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model = root.GetComponentInChildren<BuildingModel>(true);
                if (model == null)
                    throw new InvalidOperationException($"Missing BuildingModel in {path}");
                var serialized = new SerializedObject(model);
                var property = serialized.FindProperty(fieldName);
                if (property == null)
                    throw new InvalidOperationException($"Missing {fieldName} field in {path}");
                if (property.objectReferenceValue is Sprite current &&
                    !AssetDatabase.GetAssetPath(current).StartsWith(OldResourcesFolder, StringComparison.Ordinal))
                    return;
                property.objectReferenceValue = sprite;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetBuildingIcon(BuildingKind kind, Sprite sprite)
        {
            string path = DefinitionFolder + "Building_" + kind + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (definition == null)
                throw new InvalidOperationException($"Missing building definition: {path}");
            var serialized = new SerializedObject(definition);
            var property = serialized.FindProperty("_icon");
            if (property == null)
                throw new InvalidOperationException($"Missing icon field in {path}");
            if (property.objectReferenceValue is Sprite current &&
                !AssetDatabase.GetAssetPath(current).StartsWith(OldBuildingIconsFolder, StringComparison.Ordinal))
                return;
            property.objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
    }
}
