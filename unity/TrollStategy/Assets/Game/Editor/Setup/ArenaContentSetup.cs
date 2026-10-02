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
    /// Writes the arena ladder: missions Mission_01…Mission_30 (ids mission-1…mission-30) on the meadow board of the
    /// first battle. A level's enemies are authored below; their strength, the squad size and the gold grow with the
    /// level by the formulas in <see cref="Apply"/>. A first win over a folk opens it for hire. Re-run to reset;
    /// single missions can be tuned in their assets afterwards.
    /// </summary>
    public static class ArenaContentSetup
    {
        private const string DefinitionFolder = "Assets/Game/Content/Definitions/";
        private const string CatalogPath = DefinitionFolder + "GameContentCatalog.asset";
        private const int Width = 9, Height = 5;

        private static readonly (string Name, UnitKind? Unlock, (UnitKind Kind, int Count)[] Enemies)[] Ladder =
        {
            ("Первый бой", null, new[] { (UnitKind.Goblin, 4) }),
            ("Слизни на лугу", null, new[] { (UnitKind.GreenSlime, 3), (UnitKind.BlueSlime, 1) }),
            ("Хоббиты-забияки", UnitKind.Halfling, new[] { (UnitKind.Halfling, 3), (UnitKind.Goblin, 1) }),
            ("Ночная стая", null, new[] { (UnitKind.Bat, 4), (UnitKind.Trasgo, 1) }),
            ("Люди с холмов", UnitKind.Human, new[] { (UnitKind.Human, 3), (UnitKind.Townsfolk, 1) }),
            ("Волчья тропа", null, new[] { (UnitKind.Wolf, 3), (UnitKind.Goblin, 2) }),
            ("Ярмарочная драка", UnitKind.Townsfolk, new[] { (UnitKind.Townsfolk, 4), (UnitKind.Human, 1) }),
            ("Мать слизней", null, new[] { (UnitKind.GreenMotherSlime, 2), (UnitKind.GreenSlime, 3) }),
            ("Лесные стрелки", UnitKind.Elf, new[] { (UnitKind.Elf, 3), (UnitKind.Trasgo, 1), (UnitKind.Wolf, 1) }),
            ("Старое кладбище", null, new[] { (UnitKind.Skeleton, 4), (UnitKind.Zombie, 1) }),
            ("Гномья застава", UnitKind.Dwarf, new[] { (UnitKind.Dwarf, 3), (UnitKind.Goblin, 2) }),
            ("Варги", null, new[] { (UnitKind.Warg, 3), (UnitKind.Wolf, 2) }),
            ("Орочий набег", UnitKind.Orc, new[] { (UnitKind.Orc, 3), (UnitKind.Trasgo, 2) }),
            ("Синее болото", null, new[] { (UnitKind.BlueMotherSlime, 2), (UnitKind.BlueSlime, 4) }),
            ("Амазонки", UnitKind.Amazon, new[] { (UnitKind.Amazon, 3), (UnitKind.Human, 2) }),
            ("Мёртвые встают", null, new[] { (UnitKind.Zombie, 3), (UnitKind.Skeleton, 3) }),
            ("Блуждающие огни", null, new[] { (UnitKind.Wildfire, 3), (UnitKind.Skeleton, 2) }),
            ("Дикие орки", UnitKind.WildOrc, new[] { (UnitKind.WildOrc, 3), (UnitKind.Orc, 2) }),
            ("Тыквенная ночь", null, new[] { (UnitKind.PumpkinHorror, 2), (UnitKind.Zombie, 3) }),
            ("Кентавры", null, new[] { (UnitKind.Centaur, 3), (UnitKind.Elf, 2) }),
            ("Рыжебородые", UnitKind.YellowBeardDwarf, new[] { (UnitKind.YellowBeardDwarf, 3), (UnitKind.Dwarf, 2) }),
            ("Снежная засада", null, new[] { (UnitKind.EvilSnowman, 2), (UnitKind.Wolf, 3) }),
            ("Циклоп и свита", null, new[] { (UnitKind.Cyclops, 1), (UnitKind.Goblin, 4) }),
            ("Огонь и клыки", null, new[] { (UnitKind.Warg, 2), (UnitKind.Wildfire, 2), (UnitKind.Skeleton, 2) }),
            ("Йети", null, new[] { (UnitKind.Yeti, 1), (UnitKind.EvilSnowman, 2), (UnitKind.BlueSlime, 2) }),
            ("Два циклопа", null, new[] { (UnitKind.Cyclops, 2), (UnitKind.Trasgo, 3) }),
            ("Минотавр", null, new[] { (UnitKind.Minotaur, 1), (UnitKind.Orc, 4) }),
            ("Ледяная буря", null, new[] { (UnitKind.Yeti, 2), (UnitKind.Centaur, 2), (UnitKind.Wildfire, 2) }),
            ("Бойня", null, new[] { (UnitKind.Minotaur, 2), (UnitKind.PumpkinHorror, 2), (UnitKind.WildOrc, 2) }),
            ("Хозяева арены", null, new[] { (UnitKind.Minotaur, 2), (UnitKind.Cyclops, 2), (UnitKind.Yeti, 1), (UnitKind.Centaur, 2) })
        };

        [MenuItem("TrollStrategy/Dev/Setup Arena Ladder")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("Missing " + CatalogPath);
            var first = AssetDatabase.LoadAssetAtPath<BattleMissionDefinition>(DefinitionFolder + "Mission_01.asset")
                ?? throw new InvalidOperationException("Missing Mission_01: the ladder takes its board and look from it");
            var environment = first.EnvironmentPrefab;

            var playerZone = Zone(0, 1);
            var enemyZone = Zone(Width - 2, Width - 1);
            // the back column first, as the first battle stood, then the front one
            var enemyCells = enemyZone.OrderByDescending(cell => cell.X).ThenBy(cell => cell.Y).ToList();

            var missions = new List<BattleMissionDefinition>();
            for (int i = 0; i < Ladder.Length; i++)
            {
                int level = i + 1;
                var (name, unlock, roster) = Ladder[i];
                string path = $"{DefinitionFolder}Mission_{level:D2}.asset";
                var mission = AssetDatabase.LoadAssetAtPath<BattleMissionDefinition>(path);
                if (mission == null)
                {
                    mission = ScriptableObject.CreateInstance<BattleMissionDefinition>();
                    AssetDatabase.CreateAsset(mission, path);
                }

                var enemies = new List<BattleEnemyStart>();
                foreach (var (kind, count) in roster)
                    for (int n = 0; n < count; n++)
                        enemies.Add(new BattleEnemyStart { Kind = kind, Cell = enemyCells[enemies.Count] });

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
                // enemies grow tougher: +4% health a level, +1 damage every 8 levels, +1 armour every 12
                mission.SetArena(level, 100 + (level - 1) * 4, (level - 1) / 8, (level - 1) / 12, unlock);
                mission.SetEnvironment(environment);
                mission.CreateBoard();
                EditorUtility.SetDirty(mission);
                missions.Add(mission);
            }

            catalog.SetMissions(missions);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArenaContentSetup] {missions.Count} arena levels written.");
        }

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
