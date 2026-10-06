using System;
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
    /// The bots' population: personas drawn from their seeds, played on many cores at once, and the numbers the
    /// report reads off the runs.
    /// </summary>
    [Category("Bots")]
    public class BotPopulationTests
    {
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(BotMenu.CatalogPath);
            Assert.That(_catalog, Is.Not.Null, BotMenu.CatalogPath);
        }

        private int TutorialLength => _catalog.Progression.Quests.TakeWhile(q => q.IsTutorial).Count();

        [Test]
        public void Persona_IsDrawnFromItsSeed()
        {
            var first = BotPopulation.Persona(17);
            var again = BotPopulation.Persona(17);

            Assert.That(again.Id, Is.EqualTo("p17"));
            Assert.That(again.Description, Is.EqualTo(first.Description));
            Assert.That(again.ThinkSeconds, Is.EqualTo(first.ThinkSeconds));
            Assert.That(again.UpgradeHosts, Is.EqualTo(first.UpgradeHosts));
            Assert.That(BotPopulation.Values(17), Is.EqualTo(BotPopulation.Values(17)));
            Assert.That(BotPopulation.Persona(18).Description, Is.Not.EqualTo(first.Description));
        }

        [Test]
        public void Personas_CoverTheTraitRanges()
        {
            var values = Enumerable.Range(1, 500).Select(BotPopulation.Values).ToList();

            for (int t = 0; t < BotPopulation.Traits.Count; t++)
            {
                var trait = BotPopulation.Traits[t];
                var drawn = values.Select(v => v[t]).ToList();
                switch (trait.Kind)
                {
                    case BotTraitKind.Flag:
                        Assert.That(drawn.Distinct().OrderBy(v => v), Is.EqualTo(new[] { 0.0, 1.0 }), trait.Key);
                        Assert.That(drawn.Average(), Is.EqualTo(trait.Chance).Within(0.08), trait.Key);
                        break;
                    case BotTraitKind.Choice:
                        Assert.That(drawn.Distinct().OrderBy(v => v), Is.EqualTo(Enumerable.Range(0, trait.Choices.Count).Select(i => (double)i)), trait.Key);
                        break;
                    default:
                        Assert.That(drawn.Min(), Is.GreaterThanOrEqualTo(trait.Min), trait.Key);
                        Assert.That(drawn.Max(), Is.LessThanOrEqualTo(trait.Max), trait.Key);
                        Assert.That(drawn.Max() - drawn.Min(), Is.GreaterThan((trait.Max - trait.Min) * 0.8), $"{trait.Key} spans its range");
                        break;
                }
            }
            Assert.That(BotPopulation.Persona(1).ActionSeconds, Is.GreaterThan(0f), "a persona pays for its clicks, as a player does");
        }

        [Test]
        public void Find_NamesPersonasAndTheReferenceBot()
        {
            Assert.That(BotPopulation.Find("p3").Description, Is.EqualTo(BotPopulation.Persona(3).Description));
            Assert.That(BotPopulation.Find(BotProfile.Typical.Id), Is.SameAs(BotProfile.Typical));
            Assert.That(BotPopulation.Find("p0"), Is.Null);
            Assert.That(BotPopulation.Find("human"), Is.Null);
            Assert.That(BotPopulation.Find(null), Is.Null);
        }

        [Test]
        public void Population_PlaysTheSameOnManyCoresAsOnOne()
        {
            var personas = BotPopulation.Personas(6);
            var layout = TestColony.LayoutFor(_catalog);

            var alone = BotMenu.Play(personas, _catalog, layout, TutorialLength, threads: 1);
            var together = BotMenu.Play(personas, _catalog, layout, TutorialLength, threads: 6);

            for (int i = 0; i < personas.Count; i++)
            {
                Assert.That(together[i].Profile, Is.SameAs(personas[i]), "runs come back in the personas' order");
                Assert.That(together[i].Outcome, Is.EqualTo(alone[i].Outcome), BotReport.Markdown(together[i]));
                Assert.That(together[i].Quests.Select(q => q.DoneMs), Is.EqualTo(alone[i].Quests.Select(q => q.DoneMs)), personas[i].Id);
                Assert.That(together[i].FinalGold, Is.EqualTo(alone[i].FinalGold), personas[i].Id);
                Assert.That(together[i].CommandsAccepted, Is.EqualTo(alone[i].CommandsAccepted), personas[i].Id);
            }
            Assert.That(alone.Where(r => r.Outcome == BotOutcome.Crashed).Select(r => $"{r.Profile.Id}: {r.StopReason}"), Is.Empty);
        }

        [Test]
        public void Decisions_VaryThePlayAndStayRepeatable()
        {
            BotRun Play(float inattention, int placement) => new CampaignBot(
                new GameSession(_catalog, TestColony.LayoutFor(_catalog), campaign: true),
                new BotProfile("decides", "Решает", "Решения на своих костях.", thinkSeconds: 10f, grows: true,
                    seed: 3, inattention: inattention, placementChoice: placement)) { StopAfterLevel = TutorialLength }.Run();

            var careful = Play(0f, 1);
            var careless = Play(0.5f, 4);
            var again = Play(0.5f, 4);

            Assert.That(careless.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(careless));
            Assert.That(careless.Quests.Select(q => q.DoneMs), Is.Not.EqualTo(careful.Quests.Select(q => q.DoneMs)),
                "skipped looks and other spots change the game");
            Assert.That(again.Quests.Select(q => q.DoneMs), Is.EqualTo(careless.Quests.Select(q => q.DoneMs)),
                "the bot's own dice decide the same way every run");
            Assert.That(again.CommandsAccepted, Is.EqualTo(careless.CommandsAccepted));
        }

        [Test]
        public void Spread_ReadsQuantilesBetweenNeighbours()
        {
            var spread = BotSpread.Of(new double[] { 40, 10, 30, 20, 50 });

            Assert.That(spread.Count, Is.EqualTo(5));
            Assert.That(spread.P50, Is.EqualTo(30));
            Assert.That(spread.P25, Is.EqualTo(20));
            Assert.That(spread.P10, Is.EqualTo(14).Within(1e-9));
            Assert.That(spread.Mean, Is.EqualTo(30));
            Assert.That(BotSpread.Of(Array.Empty<double>()), Is.Null);
            Assert.That(BotPopulationStats.Spearman(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, new double[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 }),
                Is.EqualTo(1).Within(1e-9));
            Assert.That(BotPopulationStats.Spearman(new double[] { 1, 2, 3 }, new double[] { 3, 2, 1 }), Is.Null, "too few pairs");
        }

        [Test]
        public void Stats_FindTheTraitThatSetsTheTimeAndWhereRunsStop()
        {
            int think = BotPopulation.IndexOf("think");
            var runs = new List<BotRun>();
            for (int seed = 1; seed <= 80; seed++)
            {
                var run = new BotRun(BotPopulation.Persona(seed), 3);
                if (seed % 10 == 0)
                {
                    // one in ten stops on the second quest
                    run.Outcome = BotOutcome.Stalled;
                    run.StopWait = "золото: Склад (100)";
                    run.Quests.Add(new QuestRecord { Level = 1, Title = "Первое", StartMs = 0, DoneMs = 60000 });
                    run.EndMs = 60000 + 1800000;
                }
                else
                {
                    run.Outcome = BotOutcome.Completed;
                    run.EndMs = (int)(BotPopulation.Values(seed)[think] * 60000);
                    for (int level = 1; level <= 3; level++)
                        run.Quests.Add(new QuestRecord { Level = level, Title = $"Задание {level}", StartMs = (level - 1) * 1000, DoneMs = level * 1000 });
                }
                runs.Add(run);
            }

            var stats = BotPopulationStats.Of(runs, new[] { "Первое", "Второе", "Третье" });

            Assert.That(stats.Count, Is.EqualTo(80));
            Assert.That(stats.Completed, Is.EqualTo(72));
            Assert.That(stats.Effects[0].Trait.Key, Is.EqualTo("think"), "the campaign follows how often the bot looks");
            Assert.That(stats.Effects[0].Rho, Is.EqualTo(1).Within(1e-9));
            Assert.That(stats.Effects[0].Groups, Has.Count.InRange(4, 5), "a wide number splits into fifths, ties kept together");
            Assert.That(stats.Effects.First(e => e.Trait.Key == "raws").Runs, Is.LessThan(80), "growth traits count growing bots only");
            Assert.That(stats.Stalls, Has.Count.EqualTo(1));
            Assert.That(stats.Stalls[0].Level, Is.EqualTo(2));
            Assert.That(stats.Stalls[0].Title, Is.EqualTo("Второе"));
            Assert.That(stats.Stalls[0].Reasons[0], Is.EqualTo(("золото: Склад (100)", 8)));
            Assert.That(stats.Quests[1].Reached, Is.EqualTo(80));
            Assert.That(stats.Quests[1].Done, Is.EqualTo(72));
            Assert.That(stats.Quests[1].StoppedHere, Is.EqualTo(8));
            Assert.That(stats.FullReports, Is.SupersetOf(runs.Where(r => r.Outcome != BotOutcome.Completed).Select(r => r.Profile.Id)));
            Assert.That(stats.FullReports, Does.Contain(stats.FastestId).And.Contain(stats.SlowestId));

            string summary = BotReport.Summary(stats, runs, null);
            StringAssert.Contains("Что влияет на время", summary);
            StringAssert.Contains(BotPopulation.Traits[think].Title, summary);
            StringAssert.Contains("2. Второе", summary);
            var csv = BotReport.Population(runs).Trim().Split('\n');
            Assert.That(csv, Has.Length.EqualTo(81), "a header and a line per bot");
            StringAssert.Contains("think", csv[0]);
        }
    }
}
