using System.Linq;
using TrollStrategy.Presentation.Units;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Gives the unit prefabs their battle poses (attack, hurt, death) from the Minifantasy Creatures sheets in
    /// Assets/Game/Art/Sprites/Units (sources: assets/sprites/sources/units). Idempotent: re-slices the sheets,
    /// keeping sprite identities, and rewrites the three frame lists on each prefab.
    /// </summary>
    public static class UnitCombatAnimations
    {
        private const string SpritesFolder = "Assets/Game/Art/Sprites/Units";

        private static readonly (string Prefab, string Sheet)[] Units =
        {
            ("Assets/Game/Prefabs/Units/Troll.prefab", "troll"),
            ("Assets/Game/Prefabs/Units/Goblin.prefab", "goblin"),
        };

        [MenuItem("TrollStrategy/Units/Setup Combat Animations")]
        public static void Setup()
        {
            AssetSlicer.SliceUnits();
            foreach (var (prefabPath, sheet) in Units)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var view = root.GetComponentInChildren<UnitView>(true);
                    if (view == null)
                    {
                        Debug.LogError($"[Units] {prefabPath} has no UnitView");
                        continue;
                    }
                    var serialized = new SerializedObject(view);
                    int attack = Assign(serialized, "_attackFrames", sheet, "attack");
                    int hurt = Assign(serialized, "_hurtFrames", sheet, "hurt");
                    int death = Assign(serialized, "_deathFrames", sheet, "die");
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[Units] {sheet}: attack {attack}, hurt {hurt}, death {death} frames -> {prefabPath}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static int Assign(SerializedObject serialized, string field, string sheet, string pose)
        {
            string prefix = $"{sheet}_{pose}_";
            var frames = AssetDatabase.LoadAllAssetsAtPath($"{SpritesFolder}/{sheet}-{pose}.png")
                .OfType<Sprite>()
                .Where(sprite => sprite.name.StartsWith(prefix))
                .OrderBy(sprite => int.TryParse(sprite.name.Substring(prefix.Length), out int index) ? index : 0)
                .ToArray();
            var list = serialized.FindProperty(field);
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            if (frames.Length == 0) Debug.LogWarning($"[Units] no {prefix}* sprites in {SpritesFolder}/{sheet}-{pose}.png");
            return frames.Length;
        }
    }
}
