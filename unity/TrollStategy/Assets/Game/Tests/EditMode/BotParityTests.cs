using System;
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
    /// The in-game check without the scene: a live session whose clock runs in uneven frames, as a scene's does, and
    /// a shadow session the check brings to each look. The same moves must give the same colony at every look.
    /// </summary>
    [Category("Bots")]
    public class BotParityTests
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

        // the busiest of the first personas: the most looks and commands for the check to follow
        private static BotProfile Busiest => BotPopulation.Personas(50).OrderBy(p => p.ThinkSeconds + p.ActionSeconds).First();

        // Frames of 5…400 ms at ×30, as an editor in Play Mode gives them; the bot ticks after each one.
        private static void PlayInFrames(GameSession live, BotParity parity, Action<int> everyFrame = null)
        {
            var random = new Random(7);
            for (int frame = 0; frame < 2_000_000 && parity.Tick(); frame++)
            {
                live.Advance((float)(0.005 + random.NextDouble() * 0.395) * 30f);
                everyFrame?.Invoke(frame);
            }
        }

        [Test]
        public void Lockstep_GivesTheSameColonyWhenFramesRunTheClock()
        {
            var live = NewSession();
            live.Advance(1.37f); // the scene ran a few frames before the bot sat down
            using var parity = new BotParity(live, NewSession(), Busiest, TutorialLength);

            PlayInFrames(live, parity);
            var (inGame, withoutScene) = parity.Finish();

            Assert.That(parity.Difference, Is.Null);
            Assert.That(parity.Looks, Is.GreaterThan(TutorialLength));
            Assert.That(parity.ForeignCommands, Is.Empty);
            Assert.That(inGame.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(inGame));
            Assert.That(withoutScene.Quests.Select(q => q.DoneMs), Is.EqualTo(inGame.Quests.Select(q => q.DoneMs)));
            Assert.That(withoutScene.FinalGold, Is.EqualTo(inGame.FinalGold));
            Assert.That(withoutScene.CommandsAccepted, Is.EqualTo(inGame.CommandsAccepted));
        }

        [Test]
        public void Lockstep_NamesWhatTheSceneDidOnItsOwn()
        {
            var live = NewSession();
            using var parity = new BotParity(live, NewSession(), BotProfile.Typical, TutorialLength);

            PlayInFrames(live, parity, frame =>
            {
                if (frame == 10) live.Dispatch(new DemolishBuildingCommand("no-such-building"));
                if (frame == 20) live.DebugAddGold(500);
            });

            Assert.That(parity.ForeignCommands, Has.Count.EqualTo(1));
            Assert.That(parity.ForeignCommands[0], Does.Contain(nameof(DemolishBuildingCommand)).And.Contain("отказ"),
                "a refused command changes nothing, but the check still names it");
            Assert.That(parity.Difference, Does.Contain("до хода").And.Contain("Gold: "),
                "gold that came from outside the bot's moves parts the colonies at the next look");
            Assert.That(parity.IsOver, Is.True);
        }

        [Test]
        public void Digest_ReadsEveryBuildingAndCreatureAndOnlyTheirDifference()
        {
            var first = NewSession();
            var second = NewSession();
            first.Advance(30f);
            second.Advance(30f);
            var lines = SnapshotDigest.Lines(first);

            Assert.That(lines, Has.Some.StartsWith("Buildings[0]: {"));
            Assert.That(lines, Has.Some.StartsWith("Progress: {"));
            Assert.That(lines.Any(line => line.StartsWith("Revision", StringComparison.Ordinal)), Is.False,
                "revisions count clock calls, which differ between a scene and a plain session");
            Assert.That(SnapshotDigest.FirstDifference(lines, SnapshotDigest.Lines(second), "a", "b"), Is.Null);

            second.DebugAddGold(1);
            string difference = SnapshotDigest.FirstDifference(lines, SnapshotDigest.Lines(second), "a", "b");
            Assert.That(difference, Does.StartWith("a: Gold: ").And.Contain("\nb: Gold: "));
        }
    }
}
