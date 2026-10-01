using System.Collections.Generic;
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
            Assert.That(run.Refusals, Is.Empty, "the bot only sends commands the session accepts");
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
            StringAssert.Contains(BotProfile.Typical.Title, BotReport.Summary(new[] { run }));
            Assert.That(BotReport.Csv(run).Split('\n').Length, Is.GreaterThan(1));
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
