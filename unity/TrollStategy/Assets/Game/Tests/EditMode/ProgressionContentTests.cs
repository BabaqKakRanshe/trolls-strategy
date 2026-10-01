using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>The authored quest chain in the real content: complete, in order, and playable to the first battle.</summary>
    public class ProgressionContentTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private const string FirstMission = "mission-1";
        // What the colony scene starts with; nothing else stands before the player builds it.
        private static readonly BuildingKind[] SceneBuildings = { BuildingKind.Warehouse, BuildingKind.Market };

        private GameContentCatalog _catalog;
        private ProgressionDefinition _progression;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _progression = _catalog.Progression;
            Assert.That(_progression, Is.Not.Null, "The catalog must link Progression.asset (TrollStrategy/Setup Progression Content)");
        }

        [Test]
        public void EveryQuest_HasAnIdTextGoalsAndRewards()
        {
            var all = _progression.Quests.Concat(_progression.RepeatableQuests).ToList();
            Assert.That(_progression.Quests, Is.Not.Empty);
            Assert.That(all.Select(q => q.Id), Is.Unique);
            foreach (var quest in all)
            {
                Assert.That(quest.Id, Is.Not.Empty);
                Assert.That(quest.Title, Is.Not.Empty, quest.Id);
                Assert.That(quest.Description, Is.Not.Empty, quest.Id);
                Assert.That(quest.Goals, Is.Not.Empty, quest.Id);
                Assert.That(quest.Rewards, Is.Not.Empty, quest.Id);
            }
        }

        [Test]
        public void TheGameOpensWithGoblinsOnly_AndTheTutorialComesFirst()
        {
            Assert.That(_progression.StartingUnits, Is.EqualTo(new[] { UnitKind.Goblin }));
            Assert.That(_progression.StartingBuildings, Is.Empty);
            Assert.That(_progression.StartingMissions, Is.Empty);
            int tutorial = _progression.Quests.TakeWhile(q => q.IsTutorial).Count();
            Assert.That(tutorial, Is.GreaterThan(0));
            Assert.That(_progression.Quests.Skip(tutorial).Any(q => q.IsTutorial), Is.False,
                "Tutorial steps must not appear after the first quest");
            Assert.That(_progression.RepeatableQuests.Any(q => q.IsTutorial), Is.False);
        }

        [Test]
        public void EveryConstructibleBuildingAndCreature_IsOpenedByExactlyOneQuest()
        {
            var unlocks = _progression.Quests.SelectMany(q => q.Rewards).Where(r => r.IsUnlock).ToList();
            foreach (var building in _catalog.Buildings.Where(b => b != null && b.Constructible))
                Assert.That(unlocks.Count(r => r.Kind == QuestRewardKind.UnlockBuilding && r.Building == building.Kind),
                    Is.EqualTo(1), building.DisplayName);
            foreach (var unit in _catalog.Units.Where(u => u != null && !_progression.StartingUnits.Contains(u.Kind)))
                Assert.That(unlocks.Count(r => r.Kind == QuestRewardKind.UnlockUnit && r.Unit == unit.Kind),
                    Is.EqualTo(1), unit.DisplayName);
            Assert.That(unlocks.Count(r => r.Kind == QuestRewardKind.UnlockMission && r.MissionId == FirstMission),
                Is.EqualTo(1));
        }

        [Test]
        public void EveryQuest_AsksOnlyForWhatIsOpenByThen()
        {
            var buildings = new HashSet<BuildingKind>(_progression.StartingBuildings.Concat(SceneBuildings));
            var units = new HashSet<UnitKind>(_progression.StartingUnits);
            var missions = new HashSet<string>(_progression.StartingMissions);
            for (int i = 0; i < _progression.Quests.Count; i++)
            {
                var quest = _progression.Quests[i];
                foreach (var goal in quest.Goals)
                    AssertPossible(quest, goal, buildings, units, missions);
                foreach (var reward in quest.Rewards)
                {
                    if (reward.Kind == QuestRewardKind.UnlockBuilding) buildings.Add(reward.Building);
                    if (reward.Kind == QuestRewardKind.UnlockUnit) units.Add(reward.Unit);
                    if (reward.Kind == QuestRewardKind.UnlockMission) missions.Add(reward.MissionId);
                }
            }
            // after the chain everything is open; the repeatable quests must work there
            foreach (var quest in _progression.RepeatableQuests)
                foreach (var goal in quest.Goals)
                    AssertPossible(quest, goal, buildings, units, missions);
        }

        [Test]
        public void RepeatableQuests_GiveOnlyGold()
        {
            Assert.That(_progression.RepeatableQuests, Is.Not.Empty, "Quests must not run out after the chain");
            foreach (var quest in _progression.RepeatableQuests)
                Assert.That(quest.Rewards.All(r => r.Kind == QuestRewardKind.Gold && r.Gold > 0), Is.True, quest.Id);
        }

        [Test]
        public void AfterTheTutorial_ANewBuildingOpensAtLeastEveryThirdLevel()
        {
            int tutorial = _progression.Quests.TakeWhile(q => q.IsTutorial).Count();
            var levels = new List<int>();
            for (int i = tutorial; i < _progression.Quests.Count; i++)
                if (_progression.Quests[i].Rewards.Any(r => r.Kind == QuestRewardKind.UnlockBuilding)) levels.Add(i + 1);
            Assert.That(levels, Is.Not.Empty);
            int previous = tutorial;
            foreach (int level in levels)
            {
                Assert.That(level - previous, Is.LessThanOrEqualTo(3), $"No new building between levels {previous} and {level}");
                previous = level;
            }
            foreach (var quest in _progression.Quests.Skip(tutorial))
                Assert.That(quest.Rewards.Any(r => r.Kind == QuestRewardKind.Gold && r.Gold > 0), Is.True,
                    $"{quest.Id}: every quest after the tutorial pays gold");
        }

        [Test]
        public void TheTutorial_CanBePlayedToTheFirstBattleReward()
        {
            var session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true);
            Expect(session, "tutorial-goblin");
            Assert.That(session.CurrentSnapshot.Progress.Quest.TutorialStep, Is.EqualTo(1));

            string firstGoblin = Buy(session, UnitKind.Goblin);
            Claim(session, "tutorial-mine");
            Assert.That(session.IsBuildingUnlocked(BuildingKind.Mine), Is.True);

            var mineCell = session.FindFirstBuildingCell(BuildingKind.Mine);
            Assert.That(mineCell.HasValue, Is.True);
            Dispatch(session, new BuildBuildingCommand(BuildingKind.Mine, mineCell.Value));
            string mine = session.CurrentSnapshot.Buildings.First(b => b.Kind == BuildingKind.Mine).Id;
            Claim(session, "tutorial-work");
            Assert.That(session.IsUnitUnlocked(UnitKind.Troll), Is.True);

            string troll = Buy(session, UnitKind.Troll);
            Dispatch(session, new AssignWorkCommand(new[] { troll }, mine));
            Assert.That(session.CurrentSnapshot.Progress.Quest.IsComplete, Is.False, "The goblin still has no route");
            Dispatch(session, new AssignHaulCommand(new[] { firstGoblin }, mine, "warehouse-1"));
            Claim(session, "tutorial-market");

            string seller = Buy(session, UnitKind.Goblin);
            Dispatch(session, new AssignHaulCommand(new[] { seller }, "warehouse-1", "market-1"));
            AdvanceUntilComplete(session, 600f);
            Claim(session, "tutorial-treasury");

            // more carriers straight to the market bring the treasury up faster
            foreach (int _ in Enumerable.Range(0, 2))
                Dispatch(session, new AssignHaulCommand(new[] { Buy(session, UnitKind.Goblin) }, mine, "market-1"));
            AdvanceUntilComplete(session, 900f);
            Claim(session, "tutorial-barracks");
            Assert.That(session.IsBuildingUnlocked(BuildingKind.Barracks), Is.True);

            Assert.That(session.CanEnterMission(FirstMission).Error, Is.EqualTo("Бой откроется по заданию"));
            var barracksCell = session.FindFirstBuildingCell(BuildingKind.Barracks);
            Assert.That(barracksCell.HasValue, Is.True);
            Dispatch(session, new BuildBuildingCommand(BuildingKind.Barracks, barracksCell.Value));
            Claim(session, "tutorial-battle");
            Assert.That(AdvanceUntil(session, () => session.CanEnterMission(FirstMission).Ok, 300f), Is.True,
                session.CanEnterMission(FirstMission).Error);

            // no step asks for a squad any more: the battle is fought by whoever the tutorial has hired
            var trolls = session.CurrentSnapshot.Units.Where(u => u.UnitKind == UnitKind.Troll).Select(u => u.Id).ToList();
            var goblins = session.CurrentSnapshot.Units.Where(u => u.UnitKind == UnitKind.Goblin).Select(u => u.Id).ToList();
            Assert.That(trolls.Count, Is.EqualTo(1));
            Assert.That(goblins.Count, Is.GreaterThanOrEqualTo(3));
            Dispatch(session, new StartBattleCommand(FirstMission, new[]
            {
                new BattlePlacement(trolls[0], new Cell(1, 2)),
                new BattlePlacement(goblins[0], new Cell(0, 1)), new BattlePlacement(goblins[1], new Cell(0, 2)),
                new BattlePlacement(goblins[2], new Cell(0, 3))
            }));
            Assert.That(session.ActiveBattle.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory),
                "The colony the tutorial builds must win the first battle");
            Assert.That(session.CurrentSnapshot.Progress.Quest.IsComplete, Is.True);
            Dispatch(session, new AcknowledgeBattleCommand());
            Claim(session, "field-build");

            Assert.That(session.IsBuildingUnlocked(BuildingKind.Field), Is.True);
            var quest = session.CurrentSnapshot.Progress.Quest;
            Assert.That(quest.IsTutorial, Is.False);
            Assert.That(quest.Level, Is.EqualTo(_progression.Quests.TakeWhile(q => q.IsTutorial).Count() + 1));
        }

        private void AssertPossible(QuestDefinition quest, QuestGoal goal, ICollection<BuildingKind> buildings,
            ICollection<UnitKind> units, ICollection<string> missions)
        {
            string where = $"{quest.Id}: {goal.Kind}";
            bool usesUnit = goal.Kind == QuestGoalKind.OwnUnits || goal.Kind == QuestGoalKind.WorkAt ||
                            goal.Kind == QuestGoalKind.HaulRoute;
            if (usesUnit && !goal.AnyUnit) Assert.That(units.Contains(goal.Unit), Is.True, $"{where} needs a closed {goal.Unit}");
            switch (goal.Kind)
            {
                case QuestGoalKind.OwnBuildings:
                    Assert.That(buildings.Contains(goal.Building), Is.True, $"{where} needs a closed {goal.Building}");
                    break;
                case QuestGoalKind.WorkAt:
                    Assert.That(buildings.Contains(goal.Building), Is.True, $"{where} needs a closed {goal.Building}");
                    Assert.That(_catalog.GetBuilding(goal.Building).IsWorkplace, Is.True, $"{where}: {goal.Building} takes no workers");
                    Assert.That(goal.Amount, Is.LessThanOrEqualTo(_catalog.GetBuilding(goal.Building).MaxWorkers), where);
                    break;
                case QuestGoalKind.HaulRoute:
                    Assert.That(buildings.Contains(goal.Building) && buildings.Contains(goal.Destination), Is.True,
                        $"{where} needs {goal.Building} and {goal.Destination}");
                    Assert.That(ColonySimulation.IsValidHaulRoute(goal.Building, goal.Destination, _catalog), Is.True,
                        $"{where}: nothing can be carried {goal.Building} → {goal.Destination}");
                    break;
                case QuestGoalKind.SellResource:
                    Assert.That(buildings.Any(b => _catalog.GetBuilding(b).ProducesInRecipe(goal.Resource)), Is.True,
                        $"{where}: no open building makes {goal.Resource}");
                    Assert.That(_catalog.TryGetResource(goal.Resource), Is.Not.Null, where);
                    break;
                case QuestGoalKind.WinBattles:
                    Assert.That(missions.Contains(FirstMission), Is.True, $"{where}: the battle is still closed");
                    break;
                case QuestGoalKind.UpgradeBuilding:
                    Assert.That(buildings.Contains(goal.Building), Is.True, $"{where} needs a closed {goal.Building}");
                    Assert.That(goal.Amount, Is.LessThanOrEqualTo(_catalog.GetBuilding(goal.Building).MaxLevel), where);
                    break;
                case QuestGoalKind.EarnGold:
                case QuestGoalKind.SellGoods:
                    Assert.That(buildings.Any(b => _catalog.GetBuilding(b).Recipes.Count > 0), Is.True,
                        $"{where}: nothing to sell yet");
                    break;
            }
        }

        private static void Expect(GameSession session, string questId) =>
            Assert.That(session.CurrentSnapshot.Progress.Quest?.Id, Is.EqualTo(questId));

        private static void Claim(GameSession session, string nextQuestId)
        {
            var quest = session.CurrentSnapshot.Progress.Quest;
            Assert.That(quest.IsComplete, Is.True,
                $"{quest.Id}: " + string.Join(", ", quest.Goals.Select(g => $"{g.Text} {g.ProgressText}")));
            Dispatch(session, new ClaimQuestRewardCommand());
            Expect(session, nextQuestId);
        }

        private static void Dispatch(GameSession session, IGameCommand command)
        {
            var result = session.Dispatch(command);
            Assert.That(result.Ok, Is.True, $"{command.GetType().Name}: {result.Error}");
        }

        private static string Buy(GameSession session, UnitKind kind)
        {
            Dispatch(session, new BuyUnitsCommand(kind, 1, session.FindSpawnCell()));
            return session.CurrentSnapshot.Units.Last().Id;
        }

        private static void AdvanceUntilComplete(GameSession session, float maxSeconds)
        {
            bool done = AdvanceUntil(session, () => session.CurrentSnapshot.Progress.Quest.IsComplete, maxSeconds);
            var quest = session.CurrentSnapshot.Progress.Quest;
            Assert.That(done, Is.True,
                $"{quest.Id} not done in {maxSeconds} s: " + string.Join(", ", quest.Goals.Select(g => $"{g.Text} {g.ProgressText}")));
        }

        // Advances the colony in its own steps, looking at the result once a simulated second.
        private static bool AdvanceUntil(GameSession session, Func<bool> done, float maxSeconds)
        {
            float step = session.Catalog.Economy.EconomyStepSeconds;
            for (float elapsed = 0f; elapsed < maxSeconds; elapsed += 1f)
            {
                if (done()) return true;
                for (float t = 0f; t < 1f; t += step) session.Advance(step);
            }
            return done();
        }
    }
}
