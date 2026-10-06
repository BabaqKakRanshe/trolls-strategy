using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEditor;
using UnityEngine;

namespace TrollStrategy.Editor.Setup
{
    /// <summary>
    /// Writes the arena ladder: missions Mission_01…Mission_30 (ids mission-1…mission-30) on the 9×5 board of the
    /// first battle. A level's enemies and surroundings are authored below; their strength, the squad size, the
    /// gold, the formation, the enemies' gear, the milestones' champions and the trophies follow from the level by
    /// the rules in <see cref="Apply"/> (specs/001-arena-progression, research R8–R11, R14). A first win over a folk
    /// opens it for hire. Re-run to reset; single missions can be tuned in their assets afterwards.
    /// </summary>
    public static class ArenaContentSetup
    {
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private const string CatalogPath = DefinitionFolder + "GameContentCatalog.asset";
        private const string ArenaFolder = "Assets/Game/Prefabs/Arenas/";
        private const int Width = 9, Height = 5;
        // enemies wear gear from this level on; below it only a milestone's champion does
        private const int GearFromLevel = 10;
        private const int MilestoneEvery = 5;
        private const int ChampionHealthPercent = 200;

        private static readonly (string Name, UnitKind? Unlock, ArenaBiome Biome, (UnitKind Kind, int Count)[] Enemies)[] Ladder =
        {
            // the tutorial's goblins fight it alone: the troll comes with its win
            ("Первый бой", null, ArenaBiome.Meadow, new[] { (UnitKind.Goblin, 3) }),
            ("Слизни на лугу", null, ArenaBiome.Meadow, new[] { (UnitKind.GreenSlime, 3), (UnitKind.BlueSlime, 1) }),
            ("Хоббиты-забияки", UnitKind.Halfling, ArenaBiome.Meadow, new[] { (UnitKind.Halfling, 3), (UnitKind.Goblin, 1) }),
            ("Ночная стая", null, ArenaBiome.Forest, new[] { (UnitKind.Bat, 4), (UnitKind.Trasgo, 1) }),
            ("Люди с холмов", UnitKind.Human, ArenaBiome.Meadow, new[] { (UnitKind.Human, 3), (UnitKind.Townsfolk, 1) }),
            ("Волчья тропа", null, ArenaBiome.Forest, new[] { (UnitKind.Wolf, 3), (UnitKind.Goblin, 2) }),
            ("Ярмарочная драка", UnitKind.Townsfolk, ArenaBiome.Meadow, new[] { (UnitKind.Townsfolk, 4), (UnitKind.Human, 1) }),
            ("Мать слизней", null, ArenaBiome.Swamp, new[] { (UnitKind.GreenMotherSlime, 2), (UnitKind.GreenSlime, 3) }),
            ("Лесные стрелки", UnitKind.Elf, ArenaBiome.Forest, new[] { (UnitKind.Elf, 3), (UnitKind.Trasgo, 1), (UnitKind.Wolf, 1) }),
            ("Старое кладбище", null, ArenaBiome.Graveyard, new[] { (UnitKind.Skeleton, 4), (UnitKind.Zombie, 1) }),
            ("Гномья застава", UnitKind.Dwarf, ArenaBiome.MountainPass, new[] { (UnitKind.Dwarf, 3), (UnitKind.Goblin, 2) }),
            ("Варги", null, ArenaBiome.Forest, new[] { (UnitKind.Warg, 3), (UnitKind.Wolf, 2) }),
            ("Орочий набег", UnitKind.Orc, ArenaBiome.MountainPass, new[] { (UnitKind.Orc, 3), (UnitKind.Trasgo, 2) }),
            ("Синее болото", null, ArenaBiome.Swamp, new[] { (UnitKind.BlueMotherSlime, 2), (UnitKind.BlueSlime, 4) }),
            ("Амазонки", UnitKind.Amazon, ArenaBiome.Forest, new[] { (UnitKind.Amazon, 3), (UnitKind.Human, 2) }),
            ("Мёртвые встают", null, ArenaBiome.Graveyard, new[] { (UnitKind.Zombie, 3), (UnitKind.Skeleton, 3) }),
            ("Блуждающие огни", null, ArenaBiome.Graveyard, new[] { (UnitKind.Wildfire, 3), (UnitKind.Skeleton, 2) }),
            ("Дикие орки", UnitKind.WildOrc, ArenaBiome.MountainPass, new[] { (UnitKind.WildOrc, 3), (UnitKind.Orc, 2) }),
            ("Тыквенная ночь", null, ArenaBiome.Graveyard, new[] { (UnitKind.PumpkinHorror, 2), (UnitKind.Zombie, 3) }),
            ("Кентавры", null, ArenaBiome.Forest, new[] { (UnitKind.Centaur, 3), (UnitKind.Elf, 2) }),
            ("Рыжебородые", UnitKind.YellowBeardDwarf, ArenaBiome.MountainPass, new[] { (UnitKind.YellowBeardDwarf, 3), (UnitKind.Dwarf, 2) }),
            ("Снежная засада", null, ArenaBiome.Snow, new[] { (UnitKind.EvilSnowman, 2), (UnitKind.Wolf, 3) }),
            ("Циклоп и свита", null, ArenaBiome.MountainPass, new[] { (UnitKind.Cyclops, 1), (UnitKind.Goblin, 4) }),
            ("Огонь и клыки", null, ArenaBiome.Graveyard, new[] { (UnitKind.Warg, 2), (UnitKind.Wildfire, 2), (UnitKind.Skeleton, 2) }),
            ("Йети", null, ArenaBiome.Snow, new[] { (UnitKind.Yeti, 1), (UnitKind.EvilSnowman, 2), (UnitKind.BlueSlime, 2) }),
            ("Два циклопа", null, ArenaBiome.MountainPass, new[] { (UnitKind.Cyclops, 2), (UnitKind.Trasgo, 3) }),
            ("Минотавр", null, ArenaBiome.MountainPass, new[] { (UnitKind.Minotaur, 1), (UnitKind.Orc, 4) }),
            ("Ледяная буря", null, ArenaBiome.Snow, new[] { (UnitKind.Yeti, 2), (UnitKind.Centaur, 2), (UnitKind.Wildfire, 2) }),
            ("Бойня", null, ArenaBiome.MountainPass, new[] { (UnitKind.Minotaur, 2), (UnitKind.PumpkinHorror, 2), (UnitKind.WildOrc, 2) }),
            ("Хозяева арены", null, ArenaBiome.MountainPass, new[] { (UnitKind.Minotaur, 2), (UnitKind.Cyclops, 2), (UnitKind.Yeti, 1), (UnitKind.Centaur, 2) })
        };

        // neighbours never stand alike: the formations take turns up the ladder
        private static readonly BattleFormation[] Formations =
            { BattleFormation.Wall, BattleFormation.ArchersBack, BattleFormation.Flanks, BattleFormation.Crowd };

        // the plain finds of the levels below the gear threshold, in turn
        private static readonly string[] Finds = { "rusty-sword", "patched-armor", "wooden-shield", "spear", "leather-armor", "helmet" };

        [MenuItem("TrollStrategy/Dev/Setup Arena Ladder")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("Missing " + CatalogPath);
            var first = AssetDatabase.LoadAssetAtPath<BattleMissionDefinition>(DefinitionFolder + "Mission_01.asset")
                ?? throw new InvalidOperationException("Missing Mission_01: the ladder takes its board from it");
            var meadow = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaFolder + "Arena_Meadow.prefab") ?? first.EnvironmentPrefab;

            var playerZone = Zone(0, 1);
            var enemyZone = Zone(Width - 2, Width - 1);

            var missions = new List<BattleMissionDefinition>();
            for (int i = 0; i < Ladder.Length; i++)
            {
                int level = i + 1;
                var (name, unlock, biome, roster) = Ladder[i];
                string path = $"{DefinitionFolder}Mission_{level:D2}.asset";
                var mission = AssetDatabase.LoadAssetAtPath<BattleMissionDefinition>(path);
                if (mission == null)
                {
                    mission = ScriptableObject.CreateInstance<BattleMissionDefinition>();
                    AssetDatabase.CreateAsset(mission, path);
                }
                bool milestone = level % MilestoneEvery == 0;
                var formation = Formations[(level - 1) % Formations.Length];

                var kinds = new List<UnitKind>();
                foreach (var (kind, count) in roster)
                    for (int n = 0; n < count; n++)
                        kinds.Add(kind);
                // one more of the level's leading kind from level 2, and another every 6 levels, while the zone has
                // room; on a milestone the champion takes the place of one of them
                int extra = level >= 2 ? 1 + (level - 2) / 6 : 0;
                if (milestone) extra--;
                for (int n = 0; n < extra && kinds.Count < enemyZone.Count; n++)
                    kinds.Add(roster[0].Kind);
                if (kinds.Count > enemyZone.Count) kinds.RemoveRange(enemyZone.Count, kinds.Count - enemyZone.Count);

                // the arena is hard to win (2026-10-04): past the first fight, which teaches the battle, enemies
                // have 60% more health and 10% more each level, +1 damage and +1 armour every 4 levels. Four bare
                // trolls stop at level 5, ironclad ones at 9, the campaign's best squad at about 13 (docs/economy-balance.md §12).
                // From level 10 the enemies wear part of that bonus as gear, so their strength stays the same.
                int bonus = (level - 1) / 4;
                var kits = new List<string[]>();
                foreach (var kind in kinds)
                    kits.Add(level >= GearFromLevel ? Kit(catalog, bonus, Ranged(catalog, kind)) : Array.Empty<string>());
                int worn = kits.Count == 0 ? 0 : kits.Max(kit => Damage(catalog, kit));
                int wornArmor = kits.Count == 0 ? 0 : kits.Max(kit => Armor(catalog, kit));
                int damageBonus = Math.Max(0, bonus - worn), armorBonus = Math.Max(0, bonus - wornArmor);

                var enemies = Place(catalog, formation, kinds, kits);
                if (milestone)
                {
                    // the champion: the first of the leading kind, twice as tough, in the best gear of the level and
                    // a step above it from the gear threshold on
                    int at = enemies.FindIndex(enemy => enemy.Kind == roster[0].Kind);
                    var champion = enemies[at];
                    champion.Champion = true;
                    champion.Gear = level >= GearFromLevel
                        ? Kit(catalog, bonus + 1, Ranged(catalog, champion.Kind))
                        : Kit(catalog, bonus, Ranged(catalog, champion.Kind));
                    enemies[at] = champion;
                }

                // the squad grows by one at levels 10 and 20; the barracks add more
                int squad = 4 + (level >= 10 ? 1 : 0) + (level >= 20 ? 1 : 0);
                mission.SetDesign($"mission-{level}", level == 1 ? name : $"{level}. {name}", Width, Height, squad,
                    playerZone, Array.Empty<Cell>(), enemies, enemyZone);
                // gold: 250-400 at the first level, about 60 more per level; a repeat win pays 60%
                int firstMin = 250 + (level - 1) * 60;
                int firstMax = (int)Math.Round(firstMin * 1.6 / 10) * 10;
                int repeatMin = (int)Math.Round(firstMin * 0.6 / 10) * 10;
                int repeatMax = (int)Math.Round(firstMax * 0.6 / 10) * 10;
                mission.SetTimingAndRewards(level == 1 ? 120f : 0f, 120f, firstMin, repeatMin);
                mission.SetRewardRanges(firstMax, repeatMax);
                mission.SetWinGoods(Trophies(catalog, level, milestone, enemies));
                mission.SetArena(level, level == 1 ? 100 : 160 + (level - 1) * 10, damageBonus, armorBonus, unlock);
                mission.SetLadderTraits(formation, milestone, milestone ? ChampionHealthPercent : 0);
                mission.SetBiome(biome);
                var look = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArenaFolder}Arena_{biome}.prefab");
                if (look == null)
                {
                    if (biome != ArenaBiome.Meadow)
                        Debug.LogWarning($"[ArenaContentSetup] Level {level}: no Arena_{biome}.prefab yet, it stands on the meadow.");
                    look = meadow;
                }
                mission.SetEnvironment(look);
                mission.CreateBoard();
                EditorUtility.SetDirty(mission);
                missions.Add(mission);
            }

            catalog.SetMissions(missions);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArenaContentSetup] {missions.Count} arena levels written.");
        }

        /// <summary>
        /// The enemy zone's cells in the order the formation fills them; x = 7 is the front column, 8 the back.
        /// Melee enemies take the first cells, archers the rest; "archers behind" sends archers to the back column.
        /// </summary>
        private static List<BattleEnemyStart> Place(GameContentCatalog catalog, BattleFormation formation,
            List<UnitKind> kinds, List<string[]> kits)
        {
            int front = Width - 2, back = Width - 1;
            int[] middleOut = { 2, 1, 3, 0, 4 };
            var order = new List<int>(Enumerable.Range(0, kinds.Count));
            // melee first, in roster order
            order.Sort((a, b) =>
            {
                int ranged = Ranged(catalog, kinds[a]).CompareTo(Ranged(catalog, kinds[b]));
                return ranged != 0 ? ranged : a.CompareTo(b);
            });

            var enemies = new List<BattleEnemyStart>();
            var taken = new HashSet<Cell>();
            void Put(int index, Cell cell)
            {
                taken.Add(cell);
                enemies.Add(new BattleEnemyStart { Kind = kinds[index], Cell = cell, Gear = kits[index] });
            }

            if (formation == BattleFormation.ArchersBack)
            {
                var frontCells = middleOut.Select(y => new Cell(front, y)).ToList();
                var backCells = middleOut.Select(y => new Cell(back, y)).ToList();
                foreach (int index in order)
                {
                    var preferred = Ranged(catalog, kinds[index]) ? backCells : frontCells;
                    var other = preferred == backCells ? frontCells : backCells;
                    var cell = preferred.Concat(other).First(c => !taken.Contains(c));
                    Put(index, cell);
                }
                return enemies;
            }

            var cells = new List<Cell>();
            switch (formation)
            {
                case BattleFormation.Wall:
                    for (int y = 0; y < Height; y++) cells.Add(new Cell(front, y));
                    for (int y = 0; y < Height; y++) cells.Add(new Cell(back, y));
                    break;
                case BattleFormation.Flanks:
                    foreach (var row in new[] { new[] { 0, 4 }, new[] { 1, 3 }, new[] { 2 } })
                        foreach (int x in new[] { front, back })
                            foreach (int y in row)
                                cells.Add(new Cell(x, y));
                    break;
                default:
                    foreach (int y in middleOut)
                    {
                        cells.Add(new Cell(front, y));
                        cells.Add(new Cell(back, y));
                    }
                    break;
            }
            for (int i = 0; i < order.Count; i++) Put(order[i], cells[i]);
            return enemies;
        }

        /// <summary>
        /// Gear for an enemy whose level adds <paramref name="bonus"/> damage and armour: the best weapon of no more
        /// damage (of equal ones a bow for an archer, a blade for the rest), then the best armour and helmet that fit
        /// what is left of the armour. The damage comes first: the level's strength must not drop for the look.
        /// </summary>
        private static string[] Kit(GameContentCatalog catalog, int bonus, bool ranged)
        {
            var items = catalog.Equipment.Where(e => e != null && !string.IsNullOrEmpty(e.ItemId)).ToList();
            var weapon = items
                .Where(e => e.Slot == EquipmentSlot.Weapon && e.DamageBonus > 0 && e.DamageBonus <= bonus &&
                            e.ArmorBonus <= bonus)
                .OrderByDescending(e => e.DamageBonus).ThenByDescending(e => e.ItemId.Contains("bow") == ranged)
                .ThenBy(e => e.Enchanted).ThenBy(e => e.ItemId, StringComparer.Ordinal)
                .FirstOrDefault();
            int armorLeft = bonus - (weapon?.ArmorBonus ?? 0);
            var armor = Best(items, EquipmentSlot.Armor, armorLeft);
            armorLeft -= armor?.ArmorBonus ?? 0;
            var helmet = Best(items, EquipmentSlot.Helmet, armorLeft);
            return new[] { weapon, armor, helmet }.Where(e => e != null).Select(e => e.ItemId).ToArray();
        }

        private static EquipmentDefinition Best(List<EquipmentDefinition> items, EquipmentSlot slot, int armor) =>
            items.Where(e => e.Slot == slot && e.ArmorBonus > 0 && e.ArmorBonus <= armor && e.DamageBonus == 0)
                .OrderByDescending(e => e.ArmorBonus).ThenBy(e => e.Enchanted).ThenBy(e => e.ItemId, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>
        /// What a win here brings to the barracks: the rusty set at the first level; a plain find below the gear
        /// threshold; from it one piece of the enemies' gear (a weapon on even levels, armour on odd ones); and a
        /// milestone's champion's weapon and armour.
        /// </summary>
        private static ResourceAmount[] Trophies(GameContentCatalog catalog, int level, bool milestone,
            List<BattleEnemyStart> enemies)
        {
            var items = new List<string>();
            if (level == 1) items.AddRange(new[] { "rusty-sword", "patched-armor" });
            else if (milestone)
            {
                var champion = enemies.First(enemy => enemy.Champion);
                items.AddRange(champion.GearIds.Where(id => Slot(catalog, id) != EquipmentSlot.Helmet).Take(2));
            }
            else if (level < GearFromLevel) items.Add(Finds[(level - 2) % Finds.Length]);
            else
            {
                var wanted = level % 2 == 0 ? EquipmentSlot.Weapon : EquipmentSlot.Armor;
                string piece = enemies.SelectMany(enemy => enemy.GearIds).FirstOrDefault(id => Slot(catalog, id) == wanted)
                               ?? enemies.SelectMany(enemy => enemy.GearIds).FirstOrDefault();
                if (piece != null) items.Add(piece);
            }

            var trophies = new List<ResourceAmount>();
            foreach (string id in items)
            {
                var resource = catalog.Resources.FirstOrDefault(r => r != null && r.EquipmentId == id);
                if (resource == null)
                {
                    Debug.LogWarning($"[ArenaContentSetup] Level {level}: no goods carry the item {id}; no trophy for it.");
                    continue;
                }
                trophies.Add(new ResourceAmount(resource.Kind, 1));
            }
            return trophies.ToArray();
        }

        private static bool Ranged(GameContentCatalog catalog, UnitKind kind) => (catalog.TryGetUnit(kind)?.AttackRange ?? 1) > 1;

        private static int Damage(GameContentCatalog catalog, IEnumerable<string> kit) =>
            kit.Sum(id => catalog.GetEquipment(id).DamageBonus);

        private static int Armor(GameContentCatalog catalog, IEnumerable<string> kit) =>
            kit.Sum(id => catalog.GetEquipment(id).ArmorBonus);

        private static EquipmentSlot? Slot(GameContentCatalog catalog, string id) =>
            catalog.Equipment.FirstOrDefault(e => e != null && e.ItemId == id)?.Slot;

        private static List<Cell> Zone(int fromX, int toX)
        {
            var cells = new List<Cell>();
            for (int y = 0; y < Height; y++)
            for (int x = fromX; x <= toX; x++)
                cells.Add(new Cell(x, y));
            return cells;
        }
    }
}
