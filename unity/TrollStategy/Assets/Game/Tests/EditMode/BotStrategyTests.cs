using System;
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
    /// The strategy traits make different colonies, not the same colony at another pace: each strategy plays through
    /// the tutorial, and each shows in the colony where it should (wider lanes, higher levels, more workshops, a full
    /// squad, more land, a newcomer's moves). The first choice of every strategy is how the bots played before.
    /// </summary>
    [Category("Bots")]
    public class BotStrategyTests
    {
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
        }

        private int TutorialLength => _catalog.Progression.Quests.TakeWhile(q => q.IsTutorial).Count();

        // a bot like the tests' reference one, with one strategy turned
        private static BotProfile Strategy(string id, BotEconomy economy = BotEconomy.RawGoods,
            BotPace pace = BotPace.QuestFirst, BotLayout layout = BotLayout.Compact, BotGrowth growth = BotGrowth.Wide,
            BotForce force = BotForce.Small, bool land = false, float novice = 0f) =>
            new(id, id, id, thinkSeconds: 20f, grows: true, roleHiring: 0.9f,
                upgradeHosts: new[] { BuildingKind.HaulersGuild }, seed: 7, economy: economy, pace: pace, layout: layout,
                growth: growth, force: force, buysLand: land, novice: novice,
                fightsForGold: economy == BotEconomy.Arena);

        private (BotRun Run, GameSnapshot Colony) Play(BotProfile profile, int level)
        {
            var session = new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true);
            var run = new CampaignBot(session, profile) { StopAfterLevel = level }.Run();
            return (run, session.CurrentSnapshot);
        }

        private static IEnumerable<TestCaseData> Strategies()
        {
            yield return new TestCaseData(Strategy("crafts", economy: BotEconomy.Crafts)).SetName("Strategy_Crafts");
            yield return new TestCaseData(Strategy("arena", economy: BotEconomy.Arena)).SetName("Strategy_Arena");
            yield return new TestCaseData(Strategy("economy-first", pace: BotPace.EconomyFirst)).SetName("Strategy_EconomyFirst");
            yield return new TestCaseData(Strategy("districts", layout: BotLayout.Districts)).SetName("Strategy_Districts");
            yield return new TestCaseData(Strategy("spread", layout: BotLayout.Spread)).SetName("Strategy_Spread");
            yield return new TestCaseData(Strategy("up", growth: BotGrowth.Up)).SetName("Strategy_Up");
            yield return new TestCaseData(Strategy("large", force: BotForce.Large)).SetName("Strategy_LargeArmy");
            yield return new TestCaseData(Strategy("gear", force: BotForce.Gear)).SetName("Strategy_Gear");
            yield return new TestCaseData(Strategy("land", land: true)).SetName("Strategy_Land");
            yield return new TestCaseData(Strategy("novice", novice: 0.3f)).SetName("Strategy_Novice");
        }

        [TestCaseSource(nameof(Strategies))]
        public void EveryStrategy_PlaysThroughTheTutorial(BotProfile profile)
        {
            var (run, _) = Play(profile, TutorialLength);
            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
        }

        [Test]
        public void Reference_PlaysAsBeforeStrategies()
        {
            var typical = BotProfile.Typical;
            Assert.That(typical.Economy, Is.EqualTo(BotEconomy.RawGoods));
            Assert.That(typical.Pace, Is.EqualTo(BotPace.QuestFirst));
            Assert.That(typical.Layout, Is.EqualTo(BotLayout.Compact));
            Assert.That(typical.Growth, Is.EqualTo(BotGrowth.Wide));
            Assert.That(typical.Force, Is.EqualTo(BotForce.Small));
            Assert.That(typical.BuysLand, Is.False);
            Assert.That(typical.Novice, Is.Zero);
        }

        [Test]
        public void Population_KeepsTheTraitsItHadAndAddsStrategiesAtTheEnd()
        {
            var keys = BotPopulation.Traits.Select(t => t.Key).ToList();
            Assert.That(keys.Take(16), Is.EqualTo(new[]
            {
                "think", "click", "read", "hiring", "haulers", "grows", "raws", "spare", "reserve", "guild", "army",
                "fights", "squad", "inattention", "placement", "risk"
            }), "a persona keeps the values it had: new traits roll after the old ones");
            Assert.That(keys.Skip(16), Is.EqualTo(new[] { "economy", "pace", "layout", "upward", "force", "land", "novice" }));
        }

        [Test]
        public void Persona_PlaysTheStrategyItDrew()
        {
            var arena = Enumerable.Range(1, 200).Select(BotPopulation.Persona).First(p => p.Economy == BotEconomy.Arena);
            Assert.That(arena.FightsForGold, Is.True, "a colony living on prizes fights for gold");
            Assert.That(arena.UpgradeHosts, Does.Contain(BuildingKind.Barracks));
            Assert.That(Enumerable.Range(1, 200).Select(BotPopulation.Persona).Select(p => p.Layout).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void MostDifferent_CoversEveryStrategyAndIsTheSameEveryTime()
        {
            var ten = BotPopulation.MostDifferent(10);
            Assert.That(ten.Select(p => p.Id).Distinct().Count(), Is.EqualTo(10));
            Assert.That(BotPopulation.MostDifferent(10).Select(p => p.Id), Is.EqualTo(ten.Select(p => p.Id)));
            Assert.That(ten.Select(p => p.Economy).Distinct().Count(), Is.EqualTo(3));
            Assert.That(ten.Select(p => p.Layout).Distinct().Count(), Is.EqualTo(3));
            Assert.That(ten.Select(p => p.Force).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void Spread_LeavesWiderGapsThanCompact()
        {
            int level = TutorialLength + 6;
            double Gap(GameSnapshot colony) => colony.Buildings.Average(a => colony.Buildings.Where(b => b.Id != a.Id)
                .Min(b => Math.Sqrt(Math.Pow(a.Cell.X + a.Width / 2.0 - b.Cell.X - b.Width / 2.0, 2) +
                                    Math.Pow(a.Cell.Y + a.Height / 2.0 - b.Cell.Y - b.Height / 2.0, 2))));
            var compact = Play(Strategy("compact"), level).Colony;
            var spread = Play(Strategy("spread", layout: BotLayout.Spread), level).Colony;
            Assert.That(Gap(spread), Is.GreaterThan(Gap(compact)));
        }

        [Test]
        public void Up_RaisesLevelsWhereWideBuilds()
        {
            int level = TutorialLength + 10;
            int Levels(GameSnapshot colony) => colony.Buildings.Sum(b => b.Level - 1);
            var wide = Play(Strategy("wide"), level).Colony;
            var up = Play(Strategy("up", growth: BotGrowth.Up), level).Colony;
            Assert.That(Levels(up), Is.GreaterThan(Levels(wide)));
        }

        [Test]
        public void Crafts_BuildsMoreWorkshopsThanRawGoods()
        {
            int level = TutorialLength + 10;
            int Workshops(GameSnapshot colony) => colony.Buildings.Count(b =>
            {
                var def = _catalog.GetBuilding(b.Kind);
                return def.IsWorkplace && def.Recipes.Any(r => r.Inputs.Length > 0);
            });
            var raw = Play(Strategy("raw"), level).Colony;
            var crafts = Play(Strategy("crafts", economy: BotEconomy.Crafts), level).Colony;
            Assert.That(Workshops(crafts), Is.GreaterThanOrEqualTo(Workshops(raw)));
        }

        [Test]
        public void LargeArmy_KeepsMoreTrollsThanASmallSquad()
        {
            int level = TutorialLength + 4;
            var small = Play(Strategy("small"), level).Colony;
            var large = Play(Strategy("large", force: BotForce.Large), level).Colony;
            Assert.That(large.Units.Count(u => u.UnitKind == UnitKind.Troll),
                Is.GreaterThan(small.Units.Count(u => u.UnitKind == UnitKind.Troll)));
        }

        [Test]
        public void LandBuyer_OwnsMoreLand()
        {
            int level = TutorialLength + 6;
            int Owned(GameSnapshot colony) => colony.Land?.Blocks.Count(b => b.Owned) ?? 0;
            var keeper = Play(Strategy("keeper"), level).Colony;
            var buyer = Play(Strategy("buyer", land: true), level).Colony;
            Assert.That(Owned(buyer), Is.GreaterThan(Owned(keeper)));
        }

        [Test]
        public void Newcomer_MakesMistakesAndStillPlaysOn()
        {
            var (run, _) = Play(Strategy("novice", novice: 0.3f), TutorialLength);
            Assert.That(run.Blunders, Is.GreaterThan(0));
            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
        }
    }
}
