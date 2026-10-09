using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Bots;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The show bot without the scene: its looks on their own thread must make the same game as the bot that plays at
    /// once, and what the HUD sends must be read as the bot's move only when it is the same move.
    /// </summary>
    [Category("Bots")]
    public class BotShowTests
    {
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
        }

        private int TutorialLength => _catalog.Progression.Quests.TakeWhile(q => q.IsTutorial).Count();

        private GameSession NewSession() => new(_catalog, TestColony.LayoutFor(_catalog), campaign: true);

        [Test]
        public void Pilot_PlaysTheSameGameAsTheBotThatActsAtOnce()
        {
            var alone = new CampaignBot(NewSession(), BotProfile.Typical) { StopAfterLevel = TutorialLength }.Run();

            var session = NewSession();
            var play = new CampaignBot(session, BotProfile.Typical) { StopAfterLevel = TutorialLength }.Start();
            var intents = new List<string>();
            int commands = 0;
            using (var pilot = new BotPilot(play))
            {
                while (pilot.GoesOn)
                {
                    pilot.BeginLook();
                    while (pilot.Pending != null)
                    {
                        commands++;
                        intents.Add(pilot.PendingIntent);
                        pilot.Answer(session.Dispatch(pilot.Pending));
                    }
                    if (pilot.GoesOn) session.Advance(play.WaitSeconds);
                }
            }
            var piloted = play.Finish();

            Assert.That(piloted.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(piloted));
            Assert.That(piloted.Quests.Select(q => q.DoneMs), Is.EqualTo(alone.Quests.Select(q => q.DoneMs)));
            Assert.That(piloted.FinalGold, Is.EqualTo(alone.FinalGold));
            Assert.That(piloted.CommandsAccepted, Is.EqualTo(alone.CommandsAccepted));
            Assert.That(commands, Is.GreaterThanOrEqualTo(piloted.CommandsAccepted));
            Assert.That(intents.Count(i => !string.IsNullOrEmpty(i)), Is.GreaterThan(0), "the planners say what their moves are for");
        }

        [Test]
        public void Pilot_StopsInTheMiddleOfALook()
        {
            var play = new CampaignBot(NewSession(), BotProfile.Typical).Start();
            var pilot = new BotPilot(play);
            pilot.BeginLook();
            Assert.That(pilot.Pending, Is.Not.Null, "the first look of a campaign has something to do");
            pilot.Dispose();
            Assert.That(() => pilot.Dispose(), Throws.Nothing);
        }

        [Test]
        public void Match_TakesACreatureOfTheSameKindButNotAnotherBuilding()
        {
            var kinds = new Dictionary<string, UnitKind> { ["a"] = UnitKind.Goblin, ["b"] = UnitKind.Goblin, ["t"] = UnitKind.Troll };
            UnitKind? KindOf(string id) => kinds.TryGetValue(id, out var kind) ? kind : null;
            var meant = new AssignWorkCommand(new[] { "a" }, "mine");

            Assert.That(BotShowMatch.Same(meant, new AssignWorkCommand(new[] { "b" }, "mine"), KindOf), Is.True);
            Assert.That(BotShowMatch.Exact(meant, new AssignWorkCommand(new[] { "b" }, "mine")), Is.False);
            Assert.That(BotShowMatch.Same(meant, new AssignWorkCommand(new[] { "t" }, "mine"), KindOf), Is.False);
            Assert.That(BotShowMatch.Same(meant, new AssignWorkCommand(new[] { "a" }, "field"), KindOf), Is.False);
            Assert.That(BotShowMatch.Same(new BuildBuildingCommand(BuildingKind.Mine, new Cell(3, 4)),
                new BuildBuildingCommand(BuildingKind.Mine, new Cell(3, 5)), KindOf), Is.False, "the cell is the player's choice");
            Assert.That(BotShowMatch.Same(new HireWorkerCommand(UnitKind.Goblin, "mine", new Cell(1, 1)),
                new AssignWorkCommand(new[] { "b" }, "mine"), KindOf), Is.True, "the HUD hires in the catalog, then sends to work");
        }

        [Test]
        public void Match_ReadsHaulCargoAsASet()
        {
            UnitKind? Goblin(string id) => UnitKind.Goblin;
            var meant = new AssignHaulCommand(new[] { "a" }, "mine", "market",
                new[] { ResourceKind.IronOre, ResourceKind.Stone });
            var sent = new AssignHaulCommand(new[] { "c" }, "mine", "market", new[] { ResourceKind.Stone, ResourceKind.IronOre });
            Assert.That(BotShowMatch.Same(meant, sent, Goblin), Is.True);
            Assert.That(BotShowMatch.Same(new AssignHaulCommand(new[] { "a" }, "mine", "market"),
                new AssignHaulCommand(new[] { "a" }, "mine", "market", new List<ResourceKind>()), Goblin), Is.True,
                "no cargo and an empty cargo both carry everything");
        }

        [Test]
        public void Narrator_NamesTheBuildingAsTheCatalogDoes()
        {
            var session = NewSession();
            string text = BotNarrator.Describe(new BuildBuildingCommand(BuildingKind.Mine, new Cell(0, 0)), session);
            Assert.That(text, Does.Contain(_catalog.GetBuilding(BuildingKind.Mine).DisplayName));
            Assert.That(BotNarrator.Waiting(BotWaitKind.Gold, "золото: Шахта (40)"), Does.Contain("золото: Шахта (40)"));
        }

        [Test]
        public void Log_WritesSubtitlesInBothFormatsAndKeepsCommasInText()
        {
            var log = new BotShowLog();
            log.Add(ShowEventKind.Command, 1.5, 0, "Строю: Шахта, у склада", "руда для задания");
            log.Add(ShowEventKind.Wait, 65.25, 60000, "Коплю золото");

            string srt = log.Subtitles();
            string vtt = log.Subtitles(webVtt: true);
            Assert.That(srt, Does.Contain("00:00:01,500 --> "));
            Assert.That(srt, Does.Contain("00:01:05,250 --> "));
            Assert.That(vtt, Does.Contain("00:00:01.500 --> "));
            Assert.That(vtt, Does.Contain("Строю: Шахта, у склада"));
            Assert.That(srt, Does.Contain("Зачем: руда для задания"));
            Assert.That(BotShowLog.Clock(3725), Is.EqualTo("1:02:05"));
        }
    }
}
