using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Bots;
using TrollStrategy.Content;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// Bots play the shipped catalog through the session's commands. The typical player's whole campaign runs
    /// with the suite (seconds): a content change that makes a quest impossible fails here. The other profiles
    /// are explicit; TrollStrategy/Bots/Run Campaign Bots writes the full reports.
    /// </summary>
    [Category("Bots")]
    public class CampaignBotTests
    {
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
        }

        private int TutorialLength => _catalog.Progression.Quests.TakeWhile(q => q.IsTutorial).Count();

        private BotRun Play(BotProfile profile, int stopAfterLevel = 0) =>
            new CampaignBot(new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true), profile)
            {
                StopAfterLevel = stopAfterLevel
            }.Run();

        [Test]
        public void TypicalBot_FinishesTheCampaign()
        {
            var run = Play(BotProfile.Typical);

            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
            Assert.That(run.Quests.Select(q => q.Level),
                Is.EqualTo(Enumerable.Range(1, _catalog.Progression.Quests.Count)));
            Assert.That(run.Battles.Any(b => b.Outcome == Domain.BattleOutcome.PlayerVictory), Is.True,
                "the chain asks for won battles");
            Assert.That(run.ArenaLevel, Is.GreaterThanOrEqualTo(1));
            Assert.That(run.Battles.All(b => b.ArenaLevel >= 1), Is.True, "every battle names its arena level");
            Assert.That(run.Units.Values.Sum(), Is.EqualTo(run.FinalPopulation));
            Assert.That(run.Refusals, Is.Empty, "the bot only sends commands the session accepts");
        }

        [Test]
        public void Warlord_ClimbsTheArenaAndOpensFolk()
        {
            var run = Play(BotProfile.Warlord, 12);

            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
            Assert.That(run.ArenaLevel, Is.GreaterThan(1), "it fights for gold, not only when a quest asks");
            Assert.That(run.BattleGold, Is.GreaterThan(0));
            Assert.That(run.Unlocks, Is.Not.Empty, "won arena levels open folk to hire");
            Assert.That(run.Refusals, Is.Empty, BotReport.Markdown(run));
        }

        private List<UnitDefinition> Folk => _catalog.Units.Where(u => u != null && u.Hireable).ToList();

        [TestCase(BuildingKind.Mine)]
        [TestCase(BuildingKind.Field)]
        public void Hiring_PutsFolkToTheirCraft(BuildingKind building)
        {
            var folk = Folk;
            float PerGold(UnitDefinition unit) => BotHiring.Work(unit, building) / unit.Price;

            var chosen = BotHiring.Best(folk, u => u.Price, u => BotHiring.Work(u, building), 0.9f);

            Assert.That(chosen.Favors(building), Is.True, $"{building}: {chosen.Kind}");
            Assert.That(PerGold(chosen), Is.GreaterThanOrEqualTo(0.9f * folk.Max(PerGold)));
        }

        [Test]
        public void Hiring_WeighsStrengthAgainstGold()
        {
            var goblinAndTroll = Folk.Where(u => u.Kind == UnitKind.Goblin || u.Kind == UnitKind.Troll).ToList();

            Assert.That(BotHiring.Best(goblinAndTroll, u => u.Price, BotHiring.Carry, 0.9f).Kind,
                Is.EqualTo(UnitKind.Goblin), "a troll carries more, but not for its price");
            Assert.That(BotHiring.Best(goblinAndTroll, u => u.Price, u => BotHiring.Work(u, BuildingKind.Mine), 0f).Kind,
                Is.EqualTo(UnitKind.Troll), "with no regard for gold the strongest works");
            Assert.That(BotHiring.Best(Array.Empty<UnitDefinition>(), u => u.Price, BotHiring.Carry, 0.9f), Is.Null);
        }

        [Test]
        public void SameProfile_PlaysTheSameGameTwice()
        {
            var first = Play(BotProfile.Active, TutorialLength);
            var second = Play(BotProfile.Active, TutorialLength);

            Assert.That(second.Quests.Select(q => q.DoneMs), Is.EqualTo(first.Quests.Select(q => q.DoneMs)));
            Assert.That(second.FinalGold, Is.EqualTo(first.FinalGold));
            Assert.That(second.CommandsAccepted, Is.EqualTo(first.CommandsAccepted));
        }

        [Test]
        public void Report_ListsEveryClaimedQuest()
        {
            var run = Play(BotProfile.Typical, 3);
            string markdown = BotReport.Markdown(run);

            foreach (var quest in run.Quests)
                StringAssert.Contains(quest.Title, markdown);
            StringAssert.Contains("Арена: уровень", markdown);
            StringAssert.Contains(BotProfile.Typical.Title, BotReport.Summary(new[] { run }));
            Assert.That(BotReport.Csv(run).Split('\n').Length, Is.GreaterThan(1));
        }

        [Test]
        public void ReportData_EscapesTextForTheScript()
        {
            Assert.That(BotReportData.Str("a\"b\\c\nd"), Is.EqualTo("\"a\\\"b\\\\c\\nd\""));
            Assert.That(BotReportData.Str(null), Is.EqualTo("null"));
        }

        [Test]
        public void ReportPage_KeepsTheRunHistoryNextToThePage()
        {
            var run = Play(BotProfile.Typical, 2);
            var info = new BotReportInfo(new DateTime(2026, 10, 2, 12, 0, 0), "abc1234", "main", "Market (1, 2)",
                _catalog.Progression.Quests.Count, 2);
            string folder = Path.Combine(Path.GetTempPath(), "TrollStrategyBotPage-" + Guid.NewGuid().ToString("N"));
            try
            {
                BotMenu.WritePage(new[] { run }, info, folder);
                BotMenu.WritePage(new[] { run }, info, folder);

                Assert.That(File.ReadAllLines(Path.Combine(folder, "history.jsonl")).Length, Is.EqualTo(2));
                string script = File.ReadAllText(Path.Combine(folder, "bots-data.js"));
                StringAssert.StartsWith("window.BOT_DATA = {", script);
                StringAssert.Contains("\"generatedAt\":\"2026-10-02T12:00:00\"", script);
                StringAssert.Contains("\"arenaLevel\":", script);
                StringAssert.Contains("\"units\":[{", script);
                foreach (var quest in run.Quests)
                    StringAssert.Contains(BotReportData.Str(quest.Title), script);
                Assert.That(File.Exists(Path.Combine(folder, "index.html")), Is.True, BotMenu.PageTemplatePath);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static IEnumerable<string> Profiles() =>
            BotProfile.All.Where(p => p != BotProfile.Typical).Select(p => p.Id);

        [Explicit("Plays the whole campaign; minutes per profile")]
        [TestCaseSource(nameof(Profiles))]
        public void Profile_FinishesTheCampaign(string id)
        {
            var run = Play(BotProfile.All.First(p => p.Id == id));

            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
            Assert.That(run.Quests.Count, Is.EqualTo(_catalog.Progression.Quests.Count));
        }
    }
}
