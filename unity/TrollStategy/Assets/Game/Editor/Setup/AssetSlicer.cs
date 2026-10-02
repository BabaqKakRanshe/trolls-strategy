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
        internal static void SaveSlices(TextureImporter importer, List<SpriteRect> slices)
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
        internal static void SliceUnits()
        {
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/goblin-idle.png", 512, 128, 32, 32, 16, "goblin_idle");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/goblin-walk.png", 128, 128, 32, 32, 4, "goblin_walk");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/troll-idle.png", 512, 128, 32, 32, 16, "troll_idle");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/troll-walk.png", 192, 128, 32, 32, 6, "troll_walk");
            // battle poses (Minifantasy Creatures): the top row faces right, like idle and walk
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/goblin-attack.png", 128, 128, 32, 32, 4, "goblin_attack");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/goblin-hurt.png", 128, 128, 32, 32, 4, "goblin_hurt");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/goblin-die.png", 384, 32, 32, 32, 12, "goblin_die");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/troll-attack.png", 128, 128, 32, 32, 4, "troll_attack");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/troll-hurt.png", 128, 128, 32, 32, 4, "troll_hurt");
            SliceSpritesheet("Assets/Game/Art/Sprites/Units/troll-die.png", 448, 32, 32, 32, 14, "troll_die");
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

    }
}
