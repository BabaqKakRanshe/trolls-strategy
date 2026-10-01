using System.Collections.Generic;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public class BattleSessionTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void InvalidStartDoesNotMutateAndResultIsAppliedOnce()
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
            var barracks = Create<BuildingDefinition>();
            barracks.Init(BuildingKind.Barracks, "Бараки", 0, 3, 3, 0, 0, null);
            var mission = Create<BattleMissionDefinition>();
            mission.SetDesign("mission-1", "Первый бой", 7, 5, 4,
                new[] { new Cell(0, 0), new Cell(0, 1), new Cell(1, 0), new Cell(1, 1) },
                new[] { new Cell(3, 2) },
                new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(6, 2) } });
            mission.SetTimingAndRewards(0f, 120f, 250, 75);
            var sword = Create<EquipmentDefinition>();
            sword.Init("rusty-sword", "Ржавый меч", EquipmentSlot.Weapon, 1, 0, 1);
            var armor = Create<EquipmentDefinition>();
            armor.Init("patched-armor", "Латаная броня", EquipmentSlot.Armor, 0, 1, 1);
            var catalog = Create<GameContentCatalog>();
            catalog.Init(economy, new[] { goblin, troll },
                new[] { warehouse, market, barracks }, new[] { mission }, new[] { sword, armor });

            var session = TestColony.NewSession(catalog);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, session.FindSpawnCell())).Ok,
                Is.True);
            var unitId = session.CurrentSnapshot.Units[0].Id;
            int beforeRevision = session.CurrentSnapshot.Revision;
            int beforeGold = session.CurrentSnapshot.Gold;
            var invalid = session.Dispatch(new StartBattleCommand("mission-1",
                new[] { new BattlePlacement(unitId, new Cell(3, 2)) }));
            Assert.That(invalid.Ok, Is.False);
            Assert.That(session.CurrentSnapshot.Revision, Is.EqualTo(beforeRevision));
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(beforeGold));

            var duplicateGear = session.Dispatch(new StartBattleCommand("mission-1",
                new[] { new BattlePlacement(unitId, new Cell(0, 1)) },
                new[] { new BattleEquipmentAssignment("item-001", unitId),
                    new BattleEquipmentAssignment("item-001", unitId) }));
            Assert.That(duplicateGear.Ok, Is.False);
            Assert.That(session.CurrentSnapshot.Equipment[0].OwnerUnitId, Is.Null);

            var valid = session.Dispatch(new StartBattleCommand("mission-1",
                new[] { new BattlePlacement(unitId, new Cell(0, 1)) },
                new[] { new BattleEquipmentAssignment("item-001", unitId),
                    new BattleEquipmentAssignment("item-002", unitId) }));
            Assert.That(valid.Ok, Is.True, valid.Error);
            Assert.That(session.ActiveBattle, Is.Not.Null);
            Assert.That(session.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(session.ActiveBattle.AwardedGold, Is.EqualTo(250));
            Assert.That(session.ActiveBattle.Report.Fighters[0].Damage, Is.EqualTo(8));
            Assert.That(session.ActiveBattle.Report.Fighters[0].Armor, Is.EqualTo(4));
            Assert.That(session.ActiveBattle.Report.Fighters[0].StepIntervalMs, Is.EqualTo(500));
            Assert.That(session.ActiveBattle.Report.Fighters[1].StepIntervalMs, Is.EqualTo(200));
            Assert.That(session.CurrentSnapshot.Equipment[0].OwnerUnitId, Is.EqualTo(unitId));
            Assert.That(session.FirstMissionWins, Is.EqualTo(1));
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(beforeGold), "The prize waits to be taken");
            Assert.That(session.CurrentSnapshot.BattleReward.Gold, Is.EqualTo(250));
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, session.FindSpawnCell())).Ok,
                Is.False, "Colony commands must not change a battle already being replayed");
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.False,
                "The prize is taken back in the colony, after the replay");
            Assert.That(session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.True);
            Assert.That(session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.False);
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.False, "Taken exactly once");
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(beforeGold + 250));
            Assert.That(session.CurrentSnapshot.BattleReward, Is.Null);
            Assert.That(session.CanEnterMission("mission-1").Ok, Is.False, "Cooldown uses active time");
        }

        [Test]
        public void FallenUnitLosesEquippedItem()
        {
            var economy = Create<EconomyConfig>();
            economy.Init(14, 14, 1f, 1000, 20, .25f, .1f, .5f);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            goblin.SetCombatStats(20, 2, 1, 2000, 3);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 150);
            troll.SetCombatStats(55, 7, 3, 2600, 1);
            var armor = Create<EquipmentDefinition>();
            armor.Init("patched-armor", "Латаная броня", EquipmentSlot.Armor, 0, 1, 1);
            var mission = Create<BattleMissionDefinition>();
            mission.SetDesign("mission-1", "Поражение", 3, 3, 1,
                new[] { new Cell(0, 0) }, new Cell[0],
                new[] { new BattleEnemyStart { Kind = UnitKind.Troll, Cell = new Cell(2, 0) } });
            mission.SetTimingAndRewards(0f, 120f, 250, 75);
            var catalog = Create<GameContentCatalog>();
            catalog.Init(economy, new[] { goblin, troll }, new BuildingDefinition[0],
                new[] { mission }, new[] { armor });

            var state = GameState.CreateInitialState();
            state.Units.Add(new UnitState
            {
                Id = "unit-1", Kind = UnitKind.Goblin,
                Position = new WorldPosition(1f, 1f), Assignment = Assignment.Idle()
            });
            state.Equipment.Add(new EquipmentState
            {
                Id = "item-001", DefinitionId = armor.ItemId, OwnerUnitId = "unit-1"
            });

            var result = BattleApplication.Start(state, new StartBattleCommand("mission-1",
                new[] { new BattlePlacement("unit-1", new Cell(0, 0)) }), catalog);
            Assert.That(result.Ok, Is.True, result.Error);
            Assert.That(state.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.EnemyVictory));
            Assert.That(state.Units, Is.Empty);
            Assert.That(state.Equipment, Is.Empty);
        }

        [Test]
        public void SurvivorKeepsItsJobAndPlace()
        {
            var catalog = DuelCatalog(UnitKind.Goblin);
            var state = GameState.CreateInitialState();
            var place = new WorldPosition(4.5f, 2.5f);
            state.Units.Add(new UnitState
            {
                Id = "unit-1", Kind = UnitKind.Troll, Position = place, Assignment = Assignment.Work("mine-1")
            });

            var result = BattleApplication.Start(state, new StartBattleCommand("mission-1",
                new[] { new BattlePlacement("unit-1", new Cell(0, 0)) }), catalog);
            Assert.That(result.Ok, Is.True, result.Error);
            Assert.That(state.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            var unit = state.Units.Find(u => u.Id == "unit-1");
            Assert.That(unit.Assignment.Kind, Is.EqualTo(AssignmentKind.Work), "Back to work, not to the idle crowd");
            Assert.That(unit.Assignment.BuildingId, Is.EqualTo("mine-1"));
            Assert.That(unit.Position, Is.EqualTo(place));
        }

        [Test]
        public void FallenHaulerReturnsItsCargo()
        {
            var catalog = DuelCatalog(UnitKind.Troll);
            var state = GameState.CreateInitialState();
            var source = new BuildingState { Id = "mine-1", Kind = BuildingKind.Mine, Cell = new Cell(0, 0) };
            state.Buildings.Add(source);
            var haul = Assignment.Haul("mine-1", "warehouse-1");
            haul.CarriedResource = ResourceKind.IronOre;
            haul.Carried = 3;
            state.Units.Add(new UnitState
            {
                Id = "unit-1", Kind = UnitKind.Goblin, Position = new WorldPosition(1f, 1f), Assignment = haul
            });

            var result = BattleApplication.Start(state, new StartBattleCommand("mission-1",
                new[] { new BattlePlacement("unit-1", new Cell(0, 0)) }), catalog);
            Assert.That(result.Ok, Is.True, result.Error);
            Assert.That(state.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.EnemyVictory));
            Assert.That(state.Units, Is.Empty);
            Assert.That(source.GetStock(ResourceKind.IronOre), Is.EqualTo(3));
        }

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        [Test]
        public void DebugBattleAccessWorksAtZeroTimeWithoutChangingCampaignClock()
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
            var mission = Create<BattleMissionDefinition>();
            mission.SetDesign("mission-1", "Первый бой", 3, 3, 1,
                new[] { new Cell(0, 0) }, new Cell[0],
                new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 0) } });
            mission.SetTimingAndRewards(120f, 120f, 250, 75);
            var catalog = Create<GameContentCatalog>();
            catalog.Init(economy, new[] { troll, goblin }, new[] { warehouse, market },
                new[] { mission });
            var session = TestColony.NewSession(catalog);

            Assert.That(session.ActiveTimeMs, Is.Zero);
            Assert.That(session.CanEnterMission("mission-1").Ok, Is.False);
            session.EnableDebugBattleAccess();
            Assert.That(session.CanEnterMission("mission-1").Ok, Is.True);
            Assert.That(session.ActiveTimeMs, Is.Zero);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1,
                session.FindSpawnCell())).Ok, Is.True);
            string unitId = session.CurrentSnapshot.Units[0].Id;
            Assert.That(session.Dispatch(new StartBattleCommand("mission-1",
                new[] { new BattlePlacement(unitId, new Cell(0, 0)) })).Ok, Is.True);
            Assert.That(session.ActiveBattle, Is.Not.Null);
            Assert.That(session.ActiveTimeMs, Is.Zero);

            var state = GameState.CreateInitialState();
            state.FirstMissionNextReadyAtMs = 120000;
            Assert.That(BattleApplication.ValidateAvailability(state, mission).Ok, Is.False);
            Assert.That(BattleApplication.ValidateAvailability(state, mission, true).Ok, Is.True);
        }
#endif

        // one fighter of each side on a 3x3 board: the player at (0,0), the enemy at (2,0)
        private GameContentCatalog DuelCatalog(UnitKind enemy)
        {
            var economy = Create<EconomyConfig>();
            economy.Init(14, 14, 1f, 1000, 20, .25f, .1f, .5f);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            goblin.SetCombatStats(20, 2, 1, 2000, 3);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 150);
            troll.SetCombatStats(55, 7, 3, 2600, 1);
            var mission = Create<BattleMissionDefinition>();
            mission.SetDesign("mission-1", "Дуэль", 3, 3, 1,
                new[] { new Cell(0, 0) }, new Cell[0],
                new[] { new BattleEnemyStart { Kind = enemy, Cell = new Cell(2, 0) } });
            mission.SetTimingAndRewards(0f, 120f, 250, 75);
            var catalog = Create<GameContentCatalog>();
            catalog.Init(economy, new[] { goblin, troll }, new BuildingDefinition[0], new[] { mission });
            return catalog;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
