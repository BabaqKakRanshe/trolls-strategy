using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Tests
{
    /// <summary>Quest and unlock rules on a small colony built in memory.</summary>
    public class ProgressionTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Campaign_OpensOnlyTheStartingUnlocks()
        {
            var session = Campaign(Quest("hire", QuestGoal.OwnUnits(UnitKind.Goblin, 1)));

            Assert.That(session.IsCampaign, Is.True);
            Assert.That(session.IsUnitUnlocked(UnitKind.Goblin), Is.True);
            Assert.That(session.IsUnitUnlocked(UnitKind.Troll), Is.False);
            Assert.That(session.IsBuildingUnlocked(BuildingKind.Mine), Is.False);

            var mine = session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, new Cell(2, 2)));
            Assert.That(mine.Ok, Is.False);
            Assert.That(mine.Error, Does.Contain("не открыта"));
            var troll = session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(3, 3)));
            Assert.That(troll.Ok, Is.False);
            Assert.That(troll.Error, Does.Contain("не открыто"));
            Assert.That(session.CanEnterMission("mission-1").Error, Is.EqualTo("Бой откроется по заданию"));
        }

        [Test]
        public void Sandbox_HasEverythingOpenAndNoQuests()
        {
            var session = TestColony.NewSession(Catalog(Quest("hire", QuestGoal.OwnUnits(UnitKind.Goblin, 1))));

            Assert.That(session.IsCampaign, Is.False);
            Assert.That(session.CurrentSnapshot.Progress.Enabled, Is.False);
            Assert.That(session.CurrentSnapshot.Progress.Quest, Is.Null);
            Assert.That(session.Dispatch(new BuildBuildingCommand(BuildingKind.Mine, new Cell(2, 2))).Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(3, 6))).Ok, Is.True);
            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.False);
        }

        [Test]
        public void MeetingTheGoal_CompletesTheQuestInTheSameCommit_AndClaimGivesEveryRewardOnce()
        {
            var session = Campaign(
                Quest("hire", new[] { QuestGoal.OwnUnits(UnitKind.Goblin, 1) },
                    QuestReward.UnlockBuilding(BuildingKind.Mine), QuestReward.Coins(100)),
                Quest("build", QuestGoal.OwnBuildings(BuildingKind.Mine)));
            Assert.That(session.CurrentSnapshot.Progress.Quest.IsComplete, Is.False);

            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(3, 3))).Ok, Is.True);
            var quest = session.CurrentSnapshot.Progress.Quest;
            Assert.That(quest.IsComplete, Is.True);
            Assert.That(quest.Goals[0].ProgressText, Is.EqualTo("1/1"));
            Assert.That(quest.Headline.Title, Is.EqualTo("Шахта"));
            Assert.That(quest.Headline.Description, Does.StartWith("Теперь её можно строить: Каталог → «Здания», 200 золота."),
                "An unlock says it is the right to build, where and for how much");
            int gold = session.CurrentSnapshot.Gold;

            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);

            var progress = session.CurrentSnapshot.Progress;
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(gold + 100));
            Assert.That(session.IsBuildingUnlocked(BuildingKind.Mine), Is.True);
            Assert.That(progress.Quest.Id, Is.EqualTo("build"));
            Assert.That(progress.Level, Is.EqualTo(2));
            Assert.That(progress.Quest.IsComplete, Is.False);
            var again = session.Dispatch(new ClaimQuestRewardCommand());
            Assert.That(again.Ok, Is.False);
            Assert.That(again.Error, Is.EqualTo("Задание ещё не выполнено"));
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(gold + 100));
        }

        [Test]
        public void RefusedClaim_ChangesNothing()
        {
            var session = Campaign(Quest("hire", QuestGoal.OwnUnits(UnitKind.Goblin, 1)));
            int revision = session.CurrentSnapshot.Revision;

            var result = session.Dispatch(new ClaimQuestRewardCommand());

            Assert.That(result.Ok, Is.False);
            Assert.That(session.CurrentSnapshot.Revision, Is.EqualTo(revision));
            Assert.That(session.CurrentSnapshot.Progress.Quest.Id, Is.EqualTo("hire"));
        }

        [Test]
        public void MetGoal_StaysMet_WhenTheColonyLaterFallsBelowIt()
        {
            var session = Campaign(Quest("squad",
                QuestGoal.OwnUnits(UnitKind.Goblin, 1), QuestGoal.HaveGold(5000)));
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(3, 3))).Ok, Is.True);
            string goblin = session.CurrentSnapshot.Units[0].Id;

            Assert.That(session.Dispatch(new SellUnitsCommand(new[] { goblin })).Ok, Is.True);

            var goals = session.CurrentSnapshot.Progress.Quest.Goals;
            Assert.That(session.CurrentSnapshot.Units, Is.Empty);
            Assert.That(goals[0].Done, Is.True, "Selling the goblin must not take the met goal back");
            Assert.That(goals[1].Done, Is.False);
        }

        [Test]
        public void CumulativeGoals_CountFromTheQuestStart()
        {
            var catalog = Catalog(
                Quest("first", QuestGoal.OwnUnits(UnitKind.Goblin, 1)),
                Quest("sell", QuestGoal.SellGoods(10), QuestGoal.EarnGold(30),
                    QuestGoal.SellResource(ResourceKind.Wheat, 5), QuestGoal.WinBattles(1)));
            var state = GameState.CreateInitialState();
            state.SoldGoods = 100;
            state.SalesGold = 400;
            state.SoldByResource[ResourceKind.Wheat] = 50;
            state.BattlesWon = 3;
            Progression.Start(state, catalog);
            state.Units.Add(new UnitState { Id = "unit-1", Kind = UnitKind.Goblin, Assignment = Assignment.Idle() });
            Assert.That(Progression.Claim(state, catalog).Ok, Is.True);

            Progression.Update(state, catalog);
            Assert.That(state.Progress.GoalDone, Is.All.False, "Sales and wins before the quest do not count");

            state.SoldGoods += 10;
            state.SalesGold += 29;
            state.SoldByResource[ResourceKind.Wheat] += 5;
            state.BattlesWon += 1;
            Progression.Update(state, catalog);

            Assert.That(state.Progress.GoalDone, Is.EqualTo(new[] { true, false, true, true }));
            var quest = Progression.CurrentQuest(state, catalog);
            Assert.That(Progression.GoalProgress(state, 1, quest.Goals[1]), Is.EqualTo(29));
        }

        [Test]
        public void WorkAndHaulGoals_CountCreaturesByBuildingKind()
        {
            var catalog = Catalog(Quest("orders",
                QuestGoal.WorkAt(UnitKind.Troll, BuildingKind.Mine),
                QuestGoal.HaulRoute(UnitKind.Goblin, BuildingKind.Mine, BuildingKind.Warehouse),
                QuestGoal.AnyHaulRoute(BuildingKind.Warehouse, BuildingKind.Market, 2)));
            var state = TestColony.NewState(catalog);
            state.Buildings.Add(new BuildingState { Id = "mine-7", Kind = BuildingKind.Mine, Cell = new Cell(2, 2) });
            Progression.Start(state, catalog);
            state.Units.Add(Unit("unit-1", UnitKind.Troll, Assignment.ToWork("mine-7")));
            state.Units.Add(Unit("unit-2", UnitKind.Troll, Assignment.Haul("mine-7", "warehouse-1")));
            state.Units.Add(Unit("unit-3", UnitKind.Goblin, Assignment.Haul("warehouse-1", "market-1")));

            Progression.Update(state, catalog);

            Assert.That(state.Progress.GoalDone, Is.EqualTo(new[] { true, false, false }),
                "A walking-to-work troll counts; a troll on the goblin's route does not");
            state.Units.Add(Unit("unit-4", UnitKind.Goblin, Assignment.Haul("mine-7", "warehouse-1")));
            state.Units.Add(Unit("unit-5", UnitKind.Troll, Assignment.Haul("warehouse-1", "market-1")));
            Progression.Update(state, catalog);
            Assert.That(state.Progress.GoalDone, Is.All.True);
        }

        [Test]
        public void ClaimingUnlocks_OpensTheBattleAndTheCreature()
        {
            var session = Campaign(
                Quest("hire", new[] { QuestGoal.OwnUnits(UnitKind.Goblin, 1) },
                    QuestReward.UnlockUnit(UnitKind.Troll), QuestReward.UnlockMission("mission-1")),
                Quest("next", QuestGoal.OwnUnits(UnitKind.Troll, 1)));
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(3, 3))).Ok, Is.True);

            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);

            Assert.That(session.IsUnitUnlocked(UnitKind.Troll), Is.True);
            Assert.That(session.CanEnterMission("mission-1").Ok, Is.True);
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(3, 4))).Ok, Is.True);
            Assert.That(session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True);
        }

        [Test]
        public void WinningABattle_CompletesTheBattleQuestWithTheBattleResult()
        {
            var session = Campaign(
                Quest("open", new[] { QuestGoal.OwnUnits(UnitKind.Troll, 1) }, QuestReward.UnlockMission("mission-1")),
                Quest("win", new[] { QuestGoal.WinBattles() }, QuestReward.UnlockBuilding(BuildingKind.Field)),
                startingUnits: new[] { UnitKind.Goblin, UnitKind.Troll });
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(3, 3))).Ok, Is.True);
            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            string troll = session.CurrentSnapshot.Units[0].Id;

            Assert.That(session.Dispatch(new StartBattleCommand("mission-1",
                new[] { new BattlePlacement(troll, new Cell(0, 1)) })).Ok, Is.True);

            Assert.That(session.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True);
            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.False,
                "Rewards wait until the battle is acknowledged");
            Assert.That(session.Dispatch(new AcknowledgeBattleCommand()).Ok, Is.True);
            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);
            Assert.That(session.IsBuildingUnlocked(BuildingKind.Field), Is.True);
        }

        [Test]
        public void RepeatableQuests_FollowTheChainInACycleAndGrow()
        {
            var catalog = Catalog(new[] { Quest("only", QuestGoal.OwnUnits(UnitKind.Goblin, 1)) },
                new[]
                {
                    Quest("trade", new[] { QuestGoal.EarnGold(400) }, QuestReward.Coins(200)),
                    Quest("level", new[] { QuestGoal.UpgradeBuilding(BuildingKind.Market, 2) }, QuestReward.Coins(100))
                }, growthPercent: 25);
            var definition = catalog.Progression;

            var first = Progression.QuestAt(definition, 1);
            var second = Progression.QuestAt(definition, 2);
            var third = Progression.QuestAt(definition, 3);
            var fifth = Progression.QuestAt(definition, 5);

            Assert.That(first.Id, Is.EqualTo("trade#1"));
            Assert.That(first.Goals[0].Amount, Is.EqualTo(400));
            Assert.That(second.Id, Is.EqualTo("level#1"));
            Assert.That(third.Id, Is.EqualTo("trade#2"));
            Assert.That(third.Goals[0].Amount, Is.EqualTo(500));
            Assert.That(third.Rewards[0].Gold, Is.EqualTo(250));
            Assert.That(fifth.Goals[0].Amount, Is.EqualTo(600));
            Assert.That(Progression.QuestAt(definition, 4).Goals[0].Amount, Is.EqualTo(2), "Levels do not grow");
            Assert.That(third.IsTutorial, Is.False);
        }

        [Test]
        public void EndOfChainWithoutRepeatables_LeavesNoQuest()
        {
            var session = Campaign(Quest("only", QuestGoal.OwnUnits(UnitKind.Goblin, 1)));
            Assert.That(session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, new Cell(3, 3))).Ok, Is.True);

            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.True);

            Assert.That(session.CurrentSnapshot.Progress.Enabled, Is.True);
            Assert.That(session.CurrentSnapshot.Progress.Quest, Is.Null);
            Assert.That(session.Dispatch(new ClaimQuestRewardCommand()).Error, Is.EqualTo("Все задания выполнены"));
        }

        [Test]
        public void Snapshot_TellsAtWhichLevelEachClosedThingOpens()
        {
            var session = Campaign(
                Quest("hire", new[] { QuestGoal.OwnUnits(UnitKind.Goblin, 1) }, QuestReward.UnlockBuilding(BuildingKind.Mine)),
                Quest("build", new[] { QuestGoal.OwnBuildings(BuildingKind.Mine) }, QuestReward.UnlockUnit(UnitKind.Troll)),
                Quest("army", new[] { QuestGoal.OwnUnits(UnitKind.Troll, 1) }, QuestReward.UnlockMission("mission-1")));
            var progress = session.CurrentSnapshot.Progress;

            Assert.That(progress.UnlockLevel(BuildingKind.Mine), Is.EqualTo(1));
            Assert.That(progress.UnlockLevel(UnitKind.Troll), Is.EqualTo(2));
            Assert.That(progress.MissionUnlockLevel("mission-1"), Is.EqualTo(3));
            Assert.That(progress.UnlockLevel(UnitKind.Goblin), Is.Zero, "Open things have no level");
            Assert.That(progress.UnlockLevel(BuildingKind.Field), Is.Zero, "Nothing in the chain opens the field");
        }

        [Test]
        public void GameStateClone_CopiesProgressDeeply()
        {
            var catalog = Catalog(Quest("hire", new[] { QuestGoal.OwnUnits(UnitKind.Goblin, 1) },
                QuestReward.UnlockBuilding(BuildingKind.Mine)));
            var state = TestColony.NewState(catalog);
            Progression.Start(state, catalog);
            state.SoldByResource[ResourceKind.IronOre] = 3;

            var clone = state.Clone();
            clone.Progress.UnlockedBuildings.Add(BuildingKind.Field);
            clone.Progress.GoalDone[0] = true;
            clone.SoldByResource[ResourceKind.IronOre] = 9;

            Assert.That(state.Progress.UnlockedBuildings.Contains(BuildingKind.Field), Is.False);
            Assert.That(state.Progress.GoalDone[0], Is.False);
            Assert.That(state.SoldOf(ResourceKind.IronOre), Is.EqualTo(3));
        }

        [Test]
        public void InteractionController_RefusesClosedThingsBeforePlacement()
        {
            var session = Campaign(Quest("hire", QuestGoal.OwnUnits(UnitKind.Goblin, 1)));
            var interaction = new InteractionController(session);
            string refusal = null;
            interaction.OnRefused += reason => refusal = reason;

            interaction.BeginBuildingPlacement(BuildingKind.Mine);
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
            Assert.That(refusal, Does.Contain("Шахта"));

            interaction.BeginUnitPlacement(UnitKind.Troll, 1);
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.Neutral));
            Assert.That(refusal, Does.Contain("Тролль"));

            interaction.BeginUnitPlacement(UnitKind.Goblin, 1);
            Assert.That(interaction.Mode.Type, Is.EqualTo(InteractionModeType.PlacingUnits));
        }

        private GameSession Campaign(params QuestDefinition[] quests) => Campaign(quests, null);

        private GameSession Campaign(QuestDefinition first, QuestDefinition second, UnitKind[] startingUnits) =>
            Campaign(new[] { first, second }, startingUnits);

        private GameSession Campaign(QuestDefinition[] quests, UnitKind[] startingUnits) =>
            new(Catalog(quests, null, startingUnits: startingUnits), TestColony.Layout, campaign: true);

        private GameContentCatalog Catalog(params QuestDefinition[] quests) => Catalog(quests, null);

        private GameContentCatalog Catalog(QuestDefinition[] quests, QuestDefinition[] repeatable,
            int growthPercent = 25, UnitKind[] startingUnits = null)
        {
            var economy = Create<EconomyConfig>();
            economy.Init(14, 14, 1f, 1000, 20, .25f, .1f, .5f);
            var goblin = Create<UnitDefinition>();
            goblin.Init(UnitKind.Goblin, "Гоблин", 40, 3, 5f, 100);
            goblin.SetCombatStats(20, 2, 1, 2000, 3);
            var troll = Create<UnitDefinition>();
            troll.Init(UnitKind.Troll, "Тролль", 170, 9, 2f, 150);
            troll.SetCombatStats(55, 7, 3, 2600, 1);
            var warehouse = Create<BuildingDefinition>();
            warehouse.Init(BuildingKind.Warehouse, "Склад", 0, 3, 3, 500, 0, null);
            var market = Create<BuildingDefinition>();
            market.Init(BuildingKind.Market, "Рынок", 0, 3, 2, 0, 0, null);
            market.SetUpgrades(new[] { 100 }, 0, 0, 1);
            var mine = Create<BuildingDefinition>();
            mine.Init(BuildingKind.Mine, "Шахта", 200, 3, 3, 100, 5, null);
            mine.SetConstructible(true);
            var field = Create<BuildingDefinition>();
            field.Init(BuildingKind.Field, "Поле", 80, 3, 3, 100, 5, null);
            field.SetConstructible(true);
            var mission = Create<BattleMissionDefinition>();
            mission.SetDesign("mission-1", "Первый бой", 3, 3, 1,
                new[] { new Cell(0, 1) }, new Cell[0],
                new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 1) } });
            mission.SetTimingAndRewards(0f, 120f, 250, 75);
            var progression = Create<ProgressionDefinition>();
            progression.Init(startingUnits ?? new[] { UnitKind.Goblin }, Array.Empty<BuildingKind>(),
                Array.Empty<string>(), quests, repeatable ?? Array.Empty<QuestDefinition>(), growthPercent);
            var catalog = Create<GameContentCatalog>();
            catalog.Init(economy, new[] { goblin, troll }, new[] { warehouse, market, mine, field }, new[] { mission });
            catalog.SetProgression(progression);
            return catalog;
        }

        private static QuestDefinition Quest(string id, params QuestGoal[] goals) =>
            new(id, id, string.Empty, false, goals, Array.Empty<QuestReward>());

        private static QuestDefinition Quest(string id, QuestGoal[] goals, params QuestReward[] rewards) =>
            new(id, id, string.Empty, false, goals, rewards);

        private static UnitState Unit(string id, UnitKind kind, Assignment assignment) =>
            new() { Id = id, Kind = kind, Position = new WorldPosition(1f, 1f), Assignment = assignment };

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
