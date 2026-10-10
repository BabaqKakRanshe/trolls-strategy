using System;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Support;
using TrollStrategy.UI;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The short public builds: the itch.io alpha and the Steam demo end after their quests in Progression.asset, the
    /// arena opens nothing above the levels open then, and the notice closes the version once while the colony goes on.
    /// </summary>
    public class ShortBuildTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private ArenaTestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            Assert.That(_catalog.Progression, Is.Not.Null, "The catalog must link Progression.asset");
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        [Test]
        public void ShortGame_EndsAfterItsLastQuest_WhileTheFullGameGoesOn()
        {
            var chain = _catalog.Progression.Quests;
            var game = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true, chain[2].Id);
            var full = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true);

            Assert.That(PlayToTheEnd(game), Is.EqualTo(3));
            for (int i = 0; i < 3; i++) Take(full);

            var progress = game.CurrentSnapshot.Progress;
            Assert.That(progress.Over, Is.True);
            Assert.That(progress.Quest, Is.Null, "No quest follows the last one");
            Assert.That(progress.LastLevel, Is.EqualTo(3));
            Assert.That(game.Dispatch(new ClaimQuestRewardCommand()).Ok, Is.False);
            Assert.That(full.CurrentSnapshot.Progress.Over, Is.False);
            Assert.That(full.CurrentSnapshot.Progress.Quest.Id, Is.EqualTo(chain[3].Id));
            Assert.That(full.CurrentSnapshot.Progress.LastLevel, Is.Zero);
        }

        [Test]
        public void ShortGame_NamingAQuestTheChainLacks_IsRefused()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new GameSession(_catalog, TestColony.LayoutFor(_catalog), true, "no-such-quest"));
        }

        [Test]
        public void ShortGame_ShowsNoUnlockLevelPastItsEnd()
        {
            var chain = _catalog.Progression.Quests;
            var game = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true, _catalog.Progression.AlphaLastQuestId);
            var full = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true);
            int alpha = IndexOf(_catalog.Progression.AlphaLastQuestId);

            // a building the chain opens after the alpha's end
            BuildingKind? later = null;
            for (int i = alpha + 1; i < chain.Count && later == null; i++)
                foreach (var reward in chain[i].Rewards)
                    if (reward.Kind == QuestRewardKind.UnlockBuilding) later = reward.Building;
            Assert.That(later, Is.Not.Null);
            Assert.That(full.CurrentSnapshot.Progress.UnlockLevel(later.Value), Is.GreaterThan(alpha + 1));
            Assert.That(game.CurrentSnapshot.Progress.UnlockLevel(later.Value), Is.Zero,
                "The alpha does not promise a level it never reaches");
        }

        [Test]
        public void FinishedShortGame_KeepsItsArenaLadder_AndTheFullGameClimbsOn()
        {
            var session = StartArena("first-win");
            Assert.That(_world.Fight(1).Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(session.IsMissionUnlocked("mission-2"), Is.True, "Before the end the ladder grows as always");
            Take(session);
            Assert.That(session.CurrentSnapshot.Progress.Over, Is.True);
            Assert.That(session.CurrentSnapshot.Progress.ArenaCap, Is.EqualTo(2), "The highest level open at the end");

            Assert.That(_world.Fight(2).Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(session.IsMissionUnlocked("mission-3"), Is.False, "A finished short game opens no new level");
            Assert.That(session.CanEnterMission("mission-3").Error, Is.EqualTo(BattleApplication.FullGameOnly));
            Assert.That(session.InFullGameOnly(_world.Mission(3)), Is.True);
            Assert.That(session.InFullGameOnly(_world.Mission(2)), Is.False);
            _world.Dispose();

            var full = StartArena(null);
            _world.Fight(1);
            Take(full);
            _world.Fight(2);
            Assert.That(full.IsMissionUnlocked("mission-3"), Is.True);
            Assert.That(full.InFullGameOnly(_world.Mission(3)), Is.False);
        }

        [Test]
        public void Alpha_EndsAfterTheTutorial_WithTheNoticeOnce_AndTheColonyGoesOn()
        {
            var session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true, _catalog.Progression.AlphaLastQuestId);
            var hud = Hud(session, BuildEdition.Alpha);

            Assert.That(PlayToTheEnd(session), Is.EqualTo(IndexOf(_catalog.Progression.AlphaLastQuestId) + 1));
            Assert.That(hud.DemoEnd.IsOpen, Is.False, "Nothing opens before the HUD sees the end");
            hud.OnInteractionChanged(session.CurrentSnapshot);

            Assert.That(hud.DemoEnd.IsOpen, Is.True);
            Assert.That(hud.BlocksMap, Is.True);
            Assert.That(hud.Quest.IsShown, Is.False);
            Assert.That(hud.DemoEnd.Title, Is.EqualTo("Альфа-версия пройдена"));
            Assert.That(hud.DemoEnd.BuildingsShown, Is.GreaterThan(0), "The buildings the full game adds");
            Assert.That(hud.DemoEnd.CreaturesShown, Is.GreaterThan(0), "The creatures still closed");
            Assert.That(hud.DemoEnd.OffersSteam, Is.EqualTo(GameLinks.HasSteamPage));
            Assert.That(hud.DemoEnd.OffersRating, Is.EqualTo(GameLinks.HasItchPage), "The alpha asks for a rating on itch.io");

            UiFeel.Press(hud.DemoEnd.StayButton);
            Assert.That(hud.DemoEnd.IsOpen, Is.False);
            Assert.That(hud.BlocksMap, Is.False);
            session.Advance(_catalog.Economy.EconomyStepSeconds);
            hud.OnInteractionChanged(session.CurrentSnapshot);
            Assert.That(hud.DemoEnd.IsOpen, Is.False, "The notice opens once a game");
        }

        [Test]
        public void SteamDemo_EndsLaterThanTheAlpha_AndEscClosesItsNotice()
        {
            var session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true,
                _catalog.Progression.SteamDemoLastQuestId);
            var hud = Hud(session, BuildEdition.SteamDemo);

            int last = PlayToTheEnd(session);
            Assert.That(last, Is.EqualTo(IndexOf(_catalog.Progression.SteamDemoLastQuestId) + 1));
            Assert.That(last, Is.GreaterThan(IndexOf(_catalog.Progression.AlphaLastQuestId) + 1));
            hud.OnInteractionChanged(session.CurrentSnapshot);

            Assert.That(hud.DemoEnd.IsOpen, Is.True);
            Assert.That(hud.DemoEnd.Title, Is.EqualTo("Демо-версия пройдена"));
            Assert.That(hud.DemoEnd.OffersRating, Is.False, "The demo lives on Steam, not on itch.io");
            Assert.That(hud.CloseTopOverlay(), Is.True);
            Assert.That(hud.DemoEnd.IsOpen, Is.False);
        }

        [Test]
        public void FullGame_HasNoNotice()
        {
            var session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), true);
            var hud = Hud(session, BuildEdition.Full);
            int alpha = IndexOf(_catalog.Progression.AlphaLastQuestId) + 1;
            for (int i = 0; i < alpha; i++) Take(session);
            hud.OnInteractionChanged(session.CurrentSnapshot);

            Assert.That(hud.DemoEnd.IsOpen, Is.False);
            Assert.That(hud.Quest.IsShown, Is.True);
        }

        // a colony HUD of one edition over the session
        private static ColonyHudView Hud(GameSession session, BuildEdition edition) =>
            TestUi.Colony(new ColonyHudContext(session, new InteractionController(session))
            {
                ToggleGuides = () => { },
                GuidesVisible = () => false,
                OpenBattle = _ => { },
                Edition = edition
            });

        // a session over a small arena whose chain is one quest, "win a battle"
        private GameSession StartArena(string lastQuestId)
        {
            _world = new ArenaTestWorld(5000, 100, 100, 100);
            _world.Catalog.Progression.Init(new[] { UnitKind.Troll }, null, new[] { "mission-1" },
                new[]
                {
                    new QuestDefinition("first-win", "Первая победа", "Победи на арене.", false,
                        new[] { QuestGoal.WinBattles() }, new[] { QuestReward.Coins(10) })
                }, null, 0);
            return _world.Start(lastQuestId: lastQuestId);
        }

        private int IndexOf(string questId)
        {
            var chain = _catalog.Progression.Quests;
            for (int i = 0; i < chain.Count; i++)
                if (chain[i].Id == questId) return i;
            Assert.Fail($"The chain has no quest {questId}");
            return -1;
        }

        // takes every quest until none follows; returns how many were taken
        private static int PlayToTheEnd(GameSession session)
        {
            int taken = 0;
            while (session.CurrentSnapshot.Progress.Quest != null && taken < 200)
            {
                Take(session);
                taken++;
            }
            return taken;
        }

        private static void Take(GameSession session)
        {
            Assert.That(session.DebugCompleteQuest().Ok, Is.True);
            var claimed = session.Dispatch(new ClaimQuestRewardCommand());
            Assert.That(claimed.Ok, Is.True, claimed.Error);
        }
    }
}
