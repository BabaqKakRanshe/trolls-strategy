using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// A small campaign for arena rules: trolls and goblins, a few gear items, a ladder of missions on a 3×3 board
    /// (one player cell at 0,0, enemies on the right column) and barracks. The first level is open, as a quest
    /// would leave it.
    /// </summary>
    internal sealed class ArenaTestWorld
    {
        private readonly List<ScriptableObject> _assets = new();

        public EconomyConfig Economy { get; }
        public GameContentCatalog Catalog { get; }
        public List<BattleMissionDefinition> Missions { get; } = new();
        public GameSession Session { get; private set; }

        /// <param name="levels">The enemies' health percent per level; a huge one makes a level unwinnable.</param>
        public ArenaTestWorld(int gold = 5000, params int[] levels)
        {
            Economy = Create<EconomyConfig>();
            Economy.Init(14, 14, 1f, gold, 20, .25f, .1f, .5f);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 150);
            troll.SetCombatStats(55, 7, 3, 2600, 1);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            goblin.SetCombatStats(20, 2, 1, 2000, 3);
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            var barracks = Create<BuildingDefinition>();
            barracks.Init(BuildingKind.Barracks, "Бараки", 0, 3, 3, 0, 0, null);
            var sword = Create<EquipmentDefinition>();
            sword.Init("rusty-sword", "Ржавый меч", EquipmentSlot.Weapon, 1, 0, 0);
            var armor = Create<EquipmentDefinition>();
            armor.Init("patched-armor", "Латаная броня", EquipmentSlot.Armor, 0, 1, 0);
            var steel = Create<EquipmentDefinition>();
            steel.Init("steel-sword", "Стальной меч", EquipmentSlot.Weapon, 4, 0, 0);

            if (levels == null || levels.Length == 0) levels = new[] { 100, 100, 100 };
            for (int i = 0; i < levels.Length; i++) Missions.Add(Level(i + 1, levels[i]));

            Catalog = Create<GameContentCatalog>();
            Catalog.Init(Economy, new[] { troll, goblin }, new[] { warehouse, market, barracks }, Missions,
                new[] { sword, armor, steel });
            Catalog.SetProgression(Create<ProgressionDefinition>());
            Catalog.Progression.Init(new[] { UnitKind.Troll }, null, new[] { "mission-1" }, null, null, 0);
        }

        public BattleMissionDefinition Mission(int level) => Missions[level - 1];

        /// <summary>
        /// A campaign session with barracks (a short one ends after <paramref name="lastQuestId"/>); call once the
        /// catalog is set up.
        /// </summary>
        public GameSession Start(bool campaign = true, string lastQuestId = null)
        {
            Session = new GameSession(Catalog, TestColony.LayoutFor(Catalog,
                new StartingBuilding(BuildingKind.Warehouse, new Cell(10, 8)),
                new StartingBuilding(BuildingKind.Market, new Cell(10, 2)),
                new StartingBuilding(BuildingKind.Barracks, new Cell(2, 9))), campaign, lastQuestId);
            return Session;
        }

        public string HireTroll()
        {
            var before = Session.CurrentSnapshot.Units.Select(u => u.Id).ToHashSet();
            var hired = Session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, Session.FindSpawnCell()));
            Assert.That(hired.Ok, Is.True, hired.Error);
            return Session.CurrentSnapshot.Units.First(u => !before.Contains(u.Id)).Id;
        }

        /// <summary>Fights the level with one troll at 0,0 (hired when the colony has none), sees the replay
        /// through and takes the reward; returns the battle.</summary>
        public BattleRunState Fight(int level, bool claim = true)
        {
            string troll = Session.CurrentSnapshot.Units.FirstOrDefault(u => u.UnitKind == UnitKind.Troll)?.Id
                           ?? HireTroll();
            var result = Session.Dispatch(new StartBattleCommand(Mission(level).MissionId,
                new[] { new BattlePlacement(troll, new Cell(0, 0)) }));
            Assert.That(result.Ok, Is.True, result.Error);
            var battle = Session.ActiveBattle;
            Assert.That(Session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.True);
            if (claim && Session.CurrentSnapshot.BattleReward != null)
                Assert.That(Session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);
            return battle;
        }

        /// <summary>Lets the colony's active time run, as the economy steps it.</summary>
        public void Wait(float seconds)
        {
            float step = Economy.EconomyStepSeconds;
            for (float t = 0; t < seconds; t += step) Session.Advance(step);
        }

        public T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        public void Dispose()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private BattleMissionDefinition Level(int level, int healthPercent)
        {
            var mission = Create<BattleMissionDefinition>();
            mission.SetDesign($"mission-{level}", $"Уровень {level}", 3, 3, 1,
                new[] { new Cell(0, 0) }, new Cell[0],
                new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 0) } },
                new[] { new Cell(2, 0), new Cell(2, 1), new Cell(2, 2) });
            mission.SetTimingAndRewards(0f, 100f, 100 * level, 50 * level);
            mission.SetArena(level, healthPercent, 0, 0, null);
            return mission;
        }
    }
}
