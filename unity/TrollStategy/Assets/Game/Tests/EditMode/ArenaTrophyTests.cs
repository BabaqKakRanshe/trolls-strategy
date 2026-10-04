using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>Every arena win brings its mission's trophies; taking the reward puts them into the barracks.</summary>
    public class ArenaTrophyTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Win_BringsItsTrophiesToTheBarracks_WhenTheRewardIsTaken()
        {
            var session = Session(barracksCapacity: 20, withBarracks: true);
            Win(session);
            var reward = session.CurrentSnapshot.BattleReward;
            Assert.That(reward.Trophies.Select(t => (t.Resource, t.Amount)), Is.EqualTo(new[]
                { (ResourceKind.RustySword, 1), (ResourceKind.PatchedArmor, 1) }));
            Assert.That(Barracks(session).Stock, Is.Empty, "The trophies wait with the gold");

            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);
            Assert.That(Stock(session, ResourceKind.RustySword), Is.EqualTo(1));
            Assert.That(Stock(session, ResourceKind.PatchedArmor), Is.EqualTo(1));
            Assert.That(session.CurrentSnapshot.Equipment, Is.Empty, "Trophies are goods until an armory takes them");
        }

        [Test]
        public void UntakenWins_AddUpTheirTrophies()
        {
            var session = Session(barracksCapacity: 20, withBarracks: true);
            Win(session);
            // a repeat win brings trophies when the arena's prize fund pays it: a second of colony time fills it here
            session.Catalog.Economy.SetArena(fundPeriodSeconds: 1f);
            for (int i = 0; i < 4; i++) session.Advance(.25f);
            Win(session);
            Assert.That(session.CurrentSnapshot.BattleReward.Trophies.Select(t => t.Amount), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);
            Assert.That(Stock(session, ResourceKind.RustySword), Is.EqualTo(2));
            Assert.That(Stock(session, ResourceKind.PatchedArmor), Is.EqualTo(2));
        }

        [Test]
        public void TrophiesWithoutRoom_GoStraightToTheInventory()
        {
            var full = Session(barracksCapacity: 1, withBarracks: true);
            Win(full);
            Assert.That(full.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);
            Assert.That(Stock(full, ResourceKind.RustySword), Is.EqualTo(1));
            Assert.That(Stock(full, ResourceKind.PatchedArmor), Is.EqualTo(0));
            Assert.That(full.CurrentSnapshot.Equipment.Select(e => e.DefinitionId), Is.EqualTo(new[] { "patched-armor" }));

            var homeless = Session(barracksCapacity: 20, withBarracks: false);
            Win(homeless);
            Assert.That(homeless.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);
            Assert.That(homeless.CurrentSnapshot.Equipment.Select(e => e.DefinitionId),
                Is.EquivalentTo(new[] { "rusty-sword", "patched-armor" }));
        }

        [Test]
        public void Barracks_HandTheirTrophiesToHaulers()
        {
            var session = Session(barracksCapacity: 20, withBarracks: true);
            var barracks = session.Catalog.GetBuilding(BuildingKind.Barracks);
            Assert.That(ColonySimulation.Provides(barracks, ResourceKind.RustySword), Is.True);
            Assert.That(ColonySimulation.Provides(barracks, ResourceKind.PatchedArmor), Is.True);
            Assert.That(ColonySimulation.Provides(barracks, ResourceKind.IronOre), Is.False);
        }

        private static void Win(GameSession session)
        {
            var troll = session.CurrentSnapshot.Units[0].Id;
            var result = session.Dispatch(new StartBattleCommand("mission-1",
                new[] { new BattlePlacement(troll, new Cell(0, 1)) }));
            Assert.That(result.Ok, Is.True, result.Error);
            Assert.That(session.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.True);
        }

        private static BuildingSnapshot Barracks(GameSession session) =>
            session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Barracks);

        private static int Stock(GameSession session, ResourceKind resource) =>
            Barracks(session).Stock.Where(s => s.Resource == resource).Sum(s => s.Amount);

        private GameSession Session(int barracksCapacity, bool withBarracks)
        {
            var economy = Create<EconomyConfig>();
            economy.Init(14, 14, 1f, 1000, 20, .25f, .1f, .5f);
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
            market.SetStorage(StorageRole.Market);
            var barracks = Create<BuildingDefinition>();
            barracks.Init(BuildingKind.Barracks, "Бараки", 0, 3, 3, barracksCapacity, 0, null);
            barracks.SetStorage(StorageRole.Stockpile, ResourceKind.RustySword, ResourceKind.PatchedArmor);
            var mission = Create<BattleMissionDefinition>();
            mission.SetDesign("mission-1", "Первый бой", 7, 5, 4,
                new[] { new Cell(0, 0), new Cell(0, 1), new Cell(1, 0), new Cell(1, 1) },
                new[] { new Cell(3, 2) },
                new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(6, 2) } });
            mission.SetTimingAndRewards(0f, 0f, 250, 75);
            mission.SetWinGoods(new ResourceAmount(ResourceKind.RustySword, 1), new ResourceAmount(ResourceKind.PatchedArmor, 1));
            var sword = Create<EquipmentDefinition>();
            sword.Init("rusty-sword", "Ржавый меч", EquipmentSlot.Weapon, 1, 0, 0);
            var armor = Create<EquipmentDefinition>();
            armor.Init("patched-armor", "Латаная броня", EquipmentSlot.Armor, 0, 1, 0);
            var catalog = Create<GameContentCatalog>();
            catalog.Init(economy, new[] { goblin, troll }, new[] { warehouse, market, barracks }, new[] { mission },
                new[] { sword, armor }, new[]
                {
                    new ResourceDefinition(ResourceKind.RustySword, "Ржавый меч", 20, null, "rusty-sword"),
                    new ResourceDefinition(ResourceKind.PatchedArmor, "Латаная броня", 40, null, "patched-armor")
                });

            var layout = new List<StartingBuilding>
            {
                new(BuildingKind.Warehouse, new Cell(10, 8)),
                new(BuildingKind.Market, new Cell(10, 2))
            };
            if (withBarracks) layout.Add(new StartingBuilding(BuildingKind.Barracks, new Cell(2, 8)));
            var session = new GameSession(catalog, TestColony.LayoutFor(catalog, layout.ToArray()));
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, session.FindSpawnCell())).Ok, Is.True);
            return session;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
