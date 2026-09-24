using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    public static class AssetSlicer
    {
        [MenuItem("TrollStrategy/Repair Environment Sprites")]
        public static void RepairEnvironmentSprites()
        {
            SliceProps();
            SliceTileset();
            AssetDatabase.SaveAssets();
        }

        private static void SaveSlices(TextureImporter importer, List<SpriteRect> slices)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
                throw new InvalidOperationException($"No sprite data provider for {importer.assetPath}");
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(sprite => sprite.name);
            foreach (var slice in slices)
                slice.spriteID = existing.TryGetValue(slice.name, out var previous)
                    ? previous.spriteID : GUID.Generate();

            provider.SetSpriteRects(slices.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                slices.Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID)));
            provider.Apply();
            DisableSliceOnImport(importer);
            importer.SaveAndReimport();
        }

        private static void DisableSliceOnImport(TextureImporter importer)
        {
            // Unity 6 keeps this setting behind an internal interface. Use its serialized
            // importer property; keep the supported data provider for all sprite identities.
            var serialized = new SerializedObject(importer);
            var entries = serialized.FindProperty("m_SpriteSheet.m_SpriteCustomMetadata.m_Entries");
            if (entries == null)
                throw new InvalidOperationException("Unsupported Sprite Editor metadata layout.");
            const string key = "SpriteEditor.SliceOnImport";
            int index = 0;
            while (index < entries.arraySize &&
                   entries.GetArrayElementAtIndex(index).FindPropertyRelative("m_Key").stringValue != key)
                index++;
            if (index == entries.arraySize) entries.InsertArrayElementAtIndex(index);
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("m_Key").stringValue = key;
            entry.FindPropertyRelative("m_Value").stringValue = "False";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("TrollStrategy/Slice All Assets")]
        public static void SliceAll()
        {
            SliceUnits();
            SliceProps();
            SliceTileset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AssetSlicer] All assets sliced successfully!");
        }

        private static void SliceUnits()
        {
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/goblin-idle.png", 512, 128, 32, 32, 16, "goblin_idle");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/goblin-walk.png", 128, 128, 32, 32, 4, "goblin_walk");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/troll-idle.png", 512, 128, 32, 32, 16, "troll_idle");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/troll-walk.png", 192, 128, 32, 32, 6, "troll_walk");
        }

        private static void SliceSpritesheet(string path, int texW, int texH, int frameW, int frameH, int count, string prefix)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var metas = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                var meta = new SpriteRect
                {
                    name = $"{prefix}_{i}",
                    rect = new Rect(i * frameW, texH - frameH, frameW, frameH),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = SpriteAlignment.Center
                };
                metas.Add(meta);
            }

            SaveSlices(importer, metas);
        }

        private static void SliceProps()
        {
            string path = "Assets/Game/Art/Sprites/Environment/forgotten-memories-props.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Texture is 1024 x 1024
            int H = 1024;
            var frames = new (string name, int x, int y, int w, int h)[]
            {
                ("tree.autumn", 0, 0, 92, 142),
                ("tree.blue", 94, 0, 112, 145),
                ("tree.gold", 220, 0, 72, 142),
                ("tree.pine", 291, 0, 58, 142),
                ("tree.willow", 350, 12, 195, 215),
                ("prop.rock.gold", 0, 220, 102, 72),
                ("prop.rock.moss", 108, 220, 94, 70),
                ("prop.stump", 208, 220, 70, 68)
            };

            var metas = new List<SpriteRect>();
            foreach (var f in frames)
            {
                var meta = new SpriteRect
                {
                    name = f.name,
                    rect = new Rect(f.x, H - f.y - f.h, f.w, f.h),
                    pivot = new Vector2(0.5f, 0f), // Anchor at tree trunk base
                    alignment = SpriteAlignment.BottomCenter
                };
                metas.Add(meta);
            }

            SaveSlices(importer, metas);
        }

        private static void SliceTileset()
        {
            string path = "Assets/Game/Art/Sprites/Environment/forgotten-memories-tiles.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Slicing required frame indices (from palette and locked terrain)
            int texH = 2048;
            int tileSize = 32;
            int cols = 64; // 2048 / 32

            int[] neededFrames = {
                1, 2, 3, 128, 130, 132, 257, 258, 259, // palette: l, t, r, m, c, n, b, u, d
                46, 47, 110, 111, 174, 175, 238, 239, 302, 303 // detail frames: f, g, h, i ...
            };

            var metas = new List<SpriteRect>();
            foreach (int f in neededFrames)
            {
                int col = f % cols;
                int row = f / cols; // from top
                int unityY = texH - (row + 1) * tileSize;

                var meta = new SpriteRect
                {
                    name = $"tile_{f}",
                    rect = new Rect(col * tileSize, unityY, tileSize, tileSize),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = SpriteAlignment.Center
                };
                metas.Add(meta);
            }

            SaveSlices(importer, metas);
        }
    }
}
