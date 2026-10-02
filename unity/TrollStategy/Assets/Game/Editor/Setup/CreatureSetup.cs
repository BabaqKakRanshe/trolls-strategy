using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Units;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Brings every Minifantasy Creatures species into the game: copies its sheets from
    /// assets/sprites/Minifantasy_Creatures_Assets into Assets/Game/Art/Sprites/Units, slices the top row (the pose
    /// facing right) of each, makes its UnitBase variant in Prefabs/Units and writes its UnitDefinition with the
    /// numbers below; names come from CreatureNames.json beside this script. Idempotent: re-running keeps sprite
    /// identities and prefab references, and resets the numbers.
    /// </summary>
    public static class CreatureSetup
    {
        private const string SourceRoot = "../../../assets/sprites/Minifantasy_Creatures_Assets";
        private const string SpritesFolder = "Assets/Game/Art/Sprites/Units";
        private const string PrefabFolder = "Assets/Game/Prefabs/Units";
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private const string CatalogPath = DefinitionFolder + "GameContentCatalog.asset";
        private const string NamesPath = "Assets/Game/Editor/Setup/CreatureNames.json";
        private const int Frame = 32;

        private sealed class Spec
        {
            public UnitKind Kind;
            public string Name;
            public string Description;
            public string Folder;
            public bool Hireable;
            public int Price, Strength, Stamina;
            public float Speed;
            public int Health, Damage, Armor, IntervalMs, Range;
            public int FavoredPercent;
            public BuildingKind[] Favored = Array.Empty<BuildingKind>();
        }

        private static Spec Folk(UnitKind kind, string name, string folder, int price, int strength, float speed,
            int stamina, int health, int damage, int armor, int intervalMs, int range, int favoredPercent,
            BuildingKind[] favored, string description) => new()
        {
            Kind = kind, Name = name, Folder = folder, Hireable = true, Price = price, Strength = strength,
            Speed = speed, Stamina = stamina, Health = health, Damage = damage, Armor = armor,
            IntervalMs = intervalMs, Range = range, FavoredPercent = favoredPercent, Favored = favored,
            Description = description
        };

        private static Spec Beast(UnitKind kind, string name, string folder, float speed, int health, int damage,
            int armor, int intervalMs, int range, string description) => new()
        {
            Kind = kind, Name = name, Folder = folder, Hireable = false, Price = 0, Strength = 1, Speed = speed,
            Stamina = 100, Health = health, Damage = damage, Armor = armor, IntervalMs = intervalMs, Range = range,
            Description = description
        };

        private static readonly BuildingKind[] Metal = { BuildingKind.Mine, BuildingKind.Smeltery, BuildingKind.Forge };

        // The folk join the colony after their first defeat on the arena (BattleMissionDefinition.UnlockUnit);
        // the rest are met only there. Numbers sit around the goblin (40 gold, strength 3) and the troll
        // (170, strength 9): a favourite building is worth its premium only when the creature works there.
        private static readonly Spec[] Specs =
        {
            Folk(UnitKind.Dwarf, "Гном", "Base_Humanoids/Dwarf/Base_Dwarf", 150, 6, 3f, 130, 45, 5, 4, 2200, 1, 50, Metal,
                "Упорный мастер: в шахте, плавильне и кузнице работает в полтора раза быстрее. Ходит медленно, в бою стоит стеной."),
            Folk(UnitKind.YellowBeardDwarf, "Рыжебородый гном", "Base_Humanoids/Dwarf/Dwarf_Yellow_Beard", 220, 8, 3f, 140,
                55, 6, 5, 2200, 1, 60, new[] { BuildingKind.Forge, BuildingKind.Enchanter, BuildingKind.Smeltery },
                "Мастер огня и металла: в кузнице, плавильне и у зачарователя работает на 60% быстрее."),
            Folk(UnitKind.Elf, "Эльф", "Base_Humanoids/Elf", 120, 4, 6f, 100, 28, 5, 1, 2000, 4, 50,
                new[] { BuildingKind.LumberCamp, BuildingKind.LumberMill, BuildingKind.Enchanter },
                "Лёгкий и быстрый: на лесозаготовке, пилораме и у зачарователя работает в полтора раза быстрее. В бою бьёт издалека."),
            Folk(UnitKind.Halfling, "Хоббит", "Base_Humanoids/Halfling", 60, 3, 7f, 120, 18, 2, 0, 1800, 3, 50,
                new[] { BuildingKind.Field, BuildingKind.Farm, BuildingKind.Tavern },
                "Шустрый носильщик и хозяин: на поле, ферме и в таверне работает в полтора раза быстрее."),
            Folk(UnitKind.Human, "Человек", "Base_Humanoids/Human/Base_Human", 90, 5, 5f, 120, 35, 4, 2, 2000, 1, 30,
                new[] { BuildingKind.Tannery, BuildingKind.ShieldWorkshop, BuildingKind.Tavern },
                "Мастер на все руки: в кожевне, мастерской щитов и таверне работает на треть быстрее."),
            Folk(UnitKind.Amazon, "Амазонка", "Base_Humanoids/Human/Human_Amazon", 160, 6, 6f, 120, 50, 6, 2, 1800, 1, 30,
                new[] { BuildingKind.LumberCamp, BuildingKind.Field },
                "Воительница: сильна в бою, на лесозаготовке и в поле работает на треть быстрее."),
            Folk(UnitKind.Townsfolk, "Горожанин", "Base_Humanoids/Human/Human_Townsfolk", 60, 4, 5f, 160, 22, 2, 0, 2000, 1, 25,
                new[] { BuildingKind.Field, BuildingKind.Farm },
                "Дёшев и вынослив: носит больше гоблина, на поле и ферме работает на четверть быстрее. Не боец."),
            Folk(UnitKind.Orc, "Орк", "Base_Humanoids/Orc/Base_Orc", 140, 8, 4f, 150, 60, 6, 2, 2400, 1, 30,
                new[] { BuildingKind.Mine, BuildingKind.Smeltery },
                "Крепкий работяга: в шахте и плавильне работает на треть быстрее, в бою держит удар."),
            Folk(UnitKind.WildOrc, "Дикий орк", "Base_Humanoids/Orc/Wild Orc", 190, 9, 5f, 160, 70, 8, 2, 2400, 1, 20,
                new[] { BuildingKind.Mine, BuildingKind.LumberCamp },
                "Свирепый боец; в шахте и на лесозаготовке работает на пятую часть быстрее."),
            Beast(UnitKind.Trasgo, "Трасго", "Monsters/Trasgo", 6f, 26, 3, 1, 1800, 1, "Вертлявый проказник: бьёт слабо, зато часто."),
            Beast(UnitKind.Bat, "Летучая мышь", "Beasts/Bat", 7f, 12, 2, 0, 1400, 1, "Быстрая и хрупкая; налетает стаей."),
            Beast(UnitKind.Wolf, "Волк", "Beasts/Wolf", 7f, 30, 4, 1, 1600, 1, "Быстрый охотник: первым добегает до отряда."),
            Beast(UnitKind.Warg, "Варг", "Beasts/Warg", 6f, 45, 6, 2, 1800, 1, "Огромный волк: быстрый и кусачий."),
            Beast(UnitKind.GreenSlime, "Зелёная слизь", "Slimes/Green_Slime", 2f, 14, 2, 0, 2200, 1, "Медленная и слабая; опасна числом."),
            Beast(UnitKind.BlueSlime, "Синяя слизь", "Slimes/Blue _Slime", 2f, 18, 2, 1, 2200, 1, "Чуть крепче зелёной."),
            Beast(UnitKind.GreenMotherSlime, "Мать зелёных слизней", "Slimes/Green_Mother_Slime", 1.5f, 45, 4, 1, 2600, 1,
                "Большая и живучая слизь."),
            Beast(UnitKind.BlueMotherSlime, "Мать синих слизней", "Slimes/Blue_Mother_Slime", 1.5f, 55, 5, 2, 2600, 1,
                "Самая крепкая из слизней."),
            Beast(UnitKind.Skeleton, "Скелет", "Undead/Skeleton", 4f, 30, 4, 1, 2000, 1, "Неутомимый мертвец с мечом."),
            Beast(UnitKind.Zombie, "Зомби", "Undead/Zombie", 2f, 50, 4, 1, 2600, 1, "Медленный, но его трудно свалить."),
            Beast(UnitKind.Wildfire, "Блуждающий огонь", "Undead/Wildfire", 5f, 25, 6, 0, 2000, 3, "Жжёт издалека; хрупок вблизи."),
            Beast(UnitKind.PumpkinHorror, "Тыквенный ужас", "Monsters/Pumpkin_Horror", 2f, 70, 6, 2, 2600, 1,
                "Ожившая тыква: крепкая и злая."),
            Beast(UnitKind.Centaur, "Кентавр", "Monsters/Centaur", 7f, 60, 7, 2, 2000, 4, "Быстрый лучник: держите его подальше от слабых."),
            Beast(UnitKind.EvilSnowman, "Злой снеговик", "Monsters/Evil_Snowman", 1f, 60, 6, 3, 2400, 4,
                "Стоит на месте и швыряет снежки."),
            Beast(UnitKind.Cyclops, "Циклоп", "Monsters/Cyclop", 2f, 120, 12, 3, 3000, 1, "Великан: бьёт редко, но страшно."),
            Beast(UnitKind.Yeti, "Йети", "Monsters/Yeti", 2f, 130, 11, 4, 2800, 1, "Снежный великан в густой шерсти."),
            Beast(UnitKind.Minotaur, "Минотавр", "Monsters/Minotaur", 3f, 140, 14, 4, 2800, 1, "Хозяин арены: сильнейший из её бойцов.")
        };

        // The colony's first two species keep their numbers; they only get favourite buildings and names here.
        private static readonly (UnitKind Kind, int Percent, BuildingKind[] Favored)[] Originals =
        {
            (UnitKind.Goblin, 0, Array.Empty<BuildingKind>()),
            (UnitKind.Troll, 0, Array.Empty<BuildingKind>())
        };

        [MenuItem("TrollStrategy/Dev/Units/Setup Creatures")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("Missing " + CatalogPath);
            var names = LoadNames();
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, SourceRoot));
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

            var units = new List<UnitDefinition>();
            foreach (var original in Originals)
            {
                var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(DefinitionFolder + "Unit_" + original.Kind + ".asset")
                    ?? throw new InvalidOperationException("Missing definition of " + original.Kind);
                definition.SetWorkTraits(true, original.Percent, original.Favored);
                ApplyNames(definition, names);
                EditorUtility.SetDirty(definition);
                units.Add(definition);
            }

            foreach (var spec in Specs)
            {
                var frames = ImportSheets(spec, Path.Combine(root, spec.Folder));
                var prefab = EnsurePrefab(spec.Kind, frames);
                units.Add(EnsureDefinition(spec, prefab, frames["idle"][0], names));
            }

            catalog.SetUnits(units);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CreatureSetup] {units.Count} creatures in the catalog.");
        }

        // ---- sheets

        private static readonly (string Pose, Func<string, bool>[] Pick)[] Poses =
        {
            ("idle", new Func<string, bool>[] { n => n.EndsWith("Idle"), n => n.Contains("Idle"), n => n.Contains("Activation") }),
            ("walk", new Func<string, bool>[] { n => n.Contains("Walk"), n => n.EndsWith("Fly") }),
            ("attack", new Func<string, bool>[] { n => n.Contains("Attack") && !n.Contains("Charged") && !n.Contains("Jump"), n => n.Contains("JumpAttack") }),
            ("hurt", new Func<string, bool>[] { n => n.Contains("Dmg") }),
            ("die", new Func<string, bool>[] { n => n.Contains("SpinDie"), n => n.EndsWith("Die") && !n.Contains("Soul") })
        };

        // Copies each pose's sheet into the project and slices its top row; a pose the species lacks borrows
        // another (a slime walks as it idles, a snowman is hurt and dies as it attacks).
        private static Dictionary<string, Sprite[]> ImportSheets(Spec spec, string folder)
        {
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
            var sheets = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
                .ToDictionary(path => Path.GetFileNameWithoutExtension(path).Replace(" ", ""));
            string id = Id(spec.Kind);
            var frames = new Dictionary<string, Sprite[]>();
            foreach (var (pose, picks) in Poses)
            {
                string source = null;
                foreach (var pick in picks)
                {
                    source = sheets.Keys.Where(pick).OrderBy(name => name.Length).FirstOrDefault();
                    if (source != null) break;
                }
                if (source == null) continue;
                string target = $"{SpritesFolder}/{id}-{pose}.png";
                File.Copy(sheets[source], Path.GetFullPath(target), true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                frames[pose] = Slice(target, $"{id}_{pose}");
            }
            if (!frames.ContainsKey("idle")) throw new InvalidOperationException($"{spec.Kind}: no idle sheet in {folder}");
            if (!frames.ContainsKey("walk")) frames["walk"] = frames["idle"];
            if (!frames.ContainsKey("attack")) frames["attack"] = frames["idle"];
            if (!frames.ContainsKey("hurt")) frames["hurt"] = frames["attack"];
            if (!frames.ContainsKey("die")) frames["die"] = frames["hurt"];
            return frames;
        }

        private static Sprite[] Slice(string path, string prefix)
        {
            var (width, height) = PngSize(path);
            int count = Math.Max(1, width / Frame);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Frame;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            var slices = new List<SpriteRect>(count);
            for (int i = 0; i < count; i++)
                slices.Add(new SpriteRect
                {
                    name = $"{prefix}_{i}",
                    rect = new Rect(i * Frame, height - Frame, Frame, Frame),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = SpriteAlignment.Center
                });
            AssetSlicer.SaveSlices(importer, slices);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .Where(sprite => sprite.name.StartsWith(prefix + "_"))
                .OrderBy(sprite => int.Parse(sprite.name.Substring(prefix.Length + 1)))
                .ToArray();
        }

        // Width and height from the PNG header, before Unity has imported the texture.
        private static (int Width, int Height) PngSize(string assetPath)
        {
            using var stream = File.OpenRead(Path.GetFullPath(assetPath));
            var header = new byte[24];
            if (stream.Read(header, 0, 24) != 24) throw new InvalidDataException(assetPath);
            int Read(int at) => (header[at] << 24) | (header[at + 1] << 16) | (header[at + 2] << 8) | header[at + 3];
            return (Read(16), Read(20));
        }

        // ---- prefab and definition

        private static GameObject EnsurePrefab(UnitKind kind, Dictionary<string, Sprite[]> frames)
        {
            string path = $"{PrefabFolder}/{kind}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null &&
                !AssetDatabase.CopyAsset($"{PrefabFolder}/Goblin.prefab", path))
                throw new InvalidOperationException("Could not copy the goblin prefab to " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponentInChildren<UnitView>(true)
                    ?? throw new InvalidOperationException(path + " has no UnitView");
                var serialized = new SerializedObject(view);
                Assign(serialized, "_idleFrames", frames["idle"]);
                Assign(serialized, "_walkFrames", frames["walk"]);
                Assign(serialized, "_attackFrames", frames["attack"]);
                Assign(serialized, "_hurtFrames", frames["hurt"]);
                Assign(serialized, "_deathFrames", frames["die"]);
                var renderer = serialized.FindProperty("_spriteRenderer").objectReferenceValue as SpriteRenderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (renderer != null) renderer.sprite = frames["idle"][0];
                root.name = kind.ToString();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void Assign(SerializedObject serialized, string field, Sprite[] frames)
        {
            var list = serialized.FindProperty(field);
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        private static UnitDefinition EnsureDefinition(Spec spec, GameObject prefab, Sprite portrait,
            Dictionary<string, (List<string> Names, List<string> Epithets)> names)
        {
            string path = DefinitionFolder + "Unit_" + spec.Kind + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            definition.Init(spec.Kind, spec.Name, spec.Price, spec.Strength, spec.Speed, spec.Stamina, spec.Description,
                portrait);
            definition.SetCombatStats(spec.Health, spec.Damage, spec.Armor, spec.IntervalMs, spec.Range);
            definition.SetWorkTraits(spec.Hireable, spec.FavoredPercent, spec.Favored);
            definition.SetPrefab(prefab);
            if (spec.Hireable) ApplyNames(definition, names);
            else definition.SetNames(Array.Empty<string>());
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ApplyNames(UnitDefinition definition,
            Dictionary<string, (List<string> Names, List<string> Epithets)> names)
        {
            if (names.TryGetValue(definition.Kind.ToString(), out var entry) && entry.Names.Count > 0)
                definition.SetNames(entry.Names, entry.Epithets);
        }

        // CreatureNames.json: {"Goblin": {"names": [...], "epithets": [...]}, ...}; read without a JSON package.
        private static Dictionary<string, (List<string> Names, List<string> Epithets)> LoadNames()
        {
            var result = new Dictionary<string, (List<string>, List<string>)>();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(NamesPath);
            if (asset == null)
            {
                Debug.LogWarning("[CreatureSetup] No " + NamesPath + "; names stay as they are.");
                return result;
            }
            var file = JsonUtility.FromJson<NameFile>(Wrap(asset.text));
            foreach (var entry in file.species)
                result[entry.kind] = (new List<string>(entry.names), new List<string>(entry.epithets));
            return result;
        }

        // JsonUtility reads arrays of objects only, so {"Goblin": {...}} becomes {"species": [{"kind": "Goblin", ...}]}.
        private static string Wrap(string json)
        {
            var parts = new List<string>();
            int depth = 0, start = -1;
            string key = null;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && depth == 1 && key == null)
                {
                    int end = json.IndexOf('"', i + 1);
                    key = json.Substring(i + 1, end - i - 1);
                    i = end;
                }
                else if (c == '{')
                {
                    depth++;
                    if (depth == 2) start = i;
                }
                else if (c == '}')
                {
                    if (depth == 2 && key != null)
                    {
                        parts.Add("{\"kind\":\"" + key + "\"," + json.Substring(start + 1, i - start));
                        key = null;
                    }
                    depth--;
                }
                else if (c == '"' && depth >= 2)
                {
                    i = json.IndexOf('"', i + 1);
                    while (json[i - 1] == '\\') i = json.IndexOf('"', i + 1);
                }
            }
            return "{\"species\":[" + string.Join(",", parts) + "]}";
        }

        [Serializable]
        private sealed class NameFile
        {
            public NameEntry[] species = Array.Empty<NameEntry>();
        }

        [Serializable]
        private sealed class NameEntry
        {
            public string kind;
            public string[] names = Array.Empty<string>();
            public string[] epithets = Array.Empty<string>();
        }

        private static string Id(UnitKind kind)
        {
            string name = kind.ToString();
            var chars = new List<char>();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) chars.Add('-');
                chars.Add(char.ToLowerInvariant(name[i]));
            }
            return new string(chars.ToArray());
        }
    }
}
