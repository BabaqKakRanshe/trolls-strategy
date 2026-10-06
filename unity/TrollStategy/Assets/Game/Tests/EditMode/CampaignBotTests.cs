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
    /// Bots play the shipped catalog through the session's commands. The reference bot's whole campaign runs with
    /// the suite (seconds): a content change that makes a quest impossible fails here. The population and its
    /// numbers are in <see cref="BotPopulationTests"/>; TrollStrategy/Bots/Run Campaign Bots writes the full reports.
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
            Assert.That(run.Buildings.Values.Sum(), Is.EqualTo(run.FinalBuildings));
            Assert.That(run.Buildings.Keys, Is.SubsetOf(_catalog.Buildings.Where(b => b != null).Select(b => b.DisplayName)),
                "buildings are counted by kind, so copies of one add up");
            Assert.That(run.Refusals, Is.Empty, "the bot only sends commands the session accepts");
        }

        [Test]
        public void LeanBot_SellsWheatWithOneHaulerPerRoute()
        {
            // the field makes wheat and straw at once; its only hauler must not be kept for the straw alone
            var lean = new BotProfile("lean", "Один носильщик", "Не больше одного носильщика на маршрут.",
                thinkSeconds: 20f, grows: true, maxHaulersPerRoute: 1, roleHiring: 0.9f);
            int wheatSale = _catalog.Progression.Quests.ToList().FindIndex(q => q.Id == "wheat-sell") + 1;
            Assert.That(wheatSale, Is.GreaterThan(0), "the chain sells wheat");

            var run = Play(lean, wheatSale);

            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
        }

        // one hauler per route on the scene's own layout: the field fills up before its first hauler comes, and a
        // hauler kept for the straw alone must not count as the chain's
        [TestCase("p5")]
        [TestCase("p133")]
        public void OneHaulerPersona_SellsWheatOnTheSceneLayout(string id)
        {
            int wheatSale = _catalog.Progression.Quests.ToList().FindIndex(q => q.Id == "wheat-sell") + 1;
            var persona = BotPopulation.Find(id);
            Assert.That(persona.MaxHaulersPerRoute, Is.EqualTo(1), id);

            var run = new CampaignBot(new GameSession(_catalog, BotMenu.SceneLayout(_catalog), campaign: true), persona)
            {
                StopAfterLevel = wheatSale
            }.Run();

            Assert.That(run.Outcome, Is.EqualTo(BotOutcome.Completed), BotReport.Markdown(run));
        }

        // fights for gold whenever the arena is ready and hires the strongest, as the "warlord" profile did
        private static readonly BotProfile Fighter = new("fighter", "Воитель",
            "Нанимает самых сильных, ходит на арену при каждой возможности ради золота и вкладывается в казарму.",
            thinkSeconds: 10f, grows: true, workerKind: UnitKind.Troll, squadTrolls: 3, fightsForGold: true,
            roleHiring: 0.65f, upgradeHosts: new[] { BuildingKind.Barracks, BuildingKind.Armory, BuildingKind.HaulersGuild });

        [Test]
        public void Fighter_ClimbsTheArenaAndOpensFolk()
        {
            var run = Play(Fighter, 12);

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
        public void SamePersona_PlaysTheSameGameTwice()
        {
            var first = Play(BotPopulation.Persona(5), TutorialLength);
            var second = Play(BotPopulation.Persona(5), TutorialLength);

            Assert.That(second.Quests.Select(q => q.DoneMs), Is.EqualTo(first.Quests.Select(q => q.DoneMs)));
            Assert.That(second.FinalGold, Is.EqualTo(first.FinalGold));
            Assert.That(second.CommandsAccepted, Is.EqualTo(first.CommandsAccepted));
        }

        [Test]
        public void Gear_DealsEverySlotBestFirst()
        {
            var definitions = new List<EquipmentDefinition>();
            EquipmentSnapshot Item(string id, EquipmentSlot slot, int damage, int armor, string owner = null)
            {
                var definition = UnityEngine.ScriptableObject.CreateInstance<EquipmentDefinition>();
                definition.Init(id, id, slot, damage, armor, 0);
                definitions.Add(definition);
                return new EquipmentSnapshot(id, definition, owner);
            }
            try
            {
                var items = new[]
                {
                    Item("sword", EquipmentSlot.Weapon, 2, 0), Item("axe", EquipmentSlot.Weapon, 4, 0),
                    Item("armor", EquipmentSlot.Armor, 0, 2), Item("helmet", EquipmentSlot.Helmet, 0, 1),
                    Item("old-helmet", EquipmentSlot.Helmet, 0, 0, owner: "b"),
                    Item("worn-elsewhere", EquipmentSlot.Helmet, 0, 3, owner: "outsider")
                };

                var owners = BotGear.Deal(new[] { "a", "b" }, items).ToDictionary(d => d.ItemId, d => d.OwnerUnitId);

                Assert.That(owners["axe"], Is.EqualTo("a"), "the best weapon to the first fighter");
                Assert.That(owners["sword"], Is.EqualTo("b"));
                Assert.That(owners["armor"], Is.EqualTo("a"));
                Assert.That(owners["helmet"], Is.EqualTo("a"), "helmets are dealt like the other slots");
                Assert.That(owners["old-helmet"], Is.EqualTo("b"), "what the squad wore is dealt again");
                Assert.That(owners.ContainsKey("worn-elsewhere"), Is.False, "gear of creatures outside the squad stays on");
            }
            finally
            {
                foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        // personas that dress a fighter during the armory step and then march out with others: gear worn before the
        // gear step began counts, or they would wait for it forever
        [TestCase("p8")]
        [TestCase("p69")]
        [TestCase("p83")]
        [TestCase("p84")]
        [TestCase("p182")]
        public void Persona_WhoDressedAFighterEarly_PassesTheGearStep(string id)
        {
            int gearStep = _catalog.Progression.Quests.ToList()
                .FindIndex(q => q.Goals.Any(g => g.Kind == QuestGoalKind.WearGearInBattle)) + 1;
            Assume.That(gearStep, Is.GreaterThan(0), "the tutorial has a step that asks for gear worn in battle");

            var run = Play(BotPopulation.Find(id), gearStep);

            Assert.That(run.Quests.Select(q => q.Level), Does.Contain(gearStep), BotReport.Markdown(run));
        }

        [Test]
        public void Report_ListsEveryClaimedQuest()
        {
            var run = Play(BotProfile.Typical, 3);
            string markdown = BotReport.Markdown(run);

            foreach (var quest in run.Quests)
                StringAssert.Contains(quest.Title, markdown);
            StringAssert.Contains("Арена: уровень", markdown);
            string summary = BotReport.Summary(BotPopulationStats.Of(new[] { run }), new[] { run }, null);
            foreach (var quest in run.Quests)
                StringAssert.Contains(quest.Title, summary);
            Assert.That(BotReport.Population(new[] { run }).Trim().Split('\n'), Has.Length.EqualTo(2));
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
                StringAssert.Contains("\"stats\":{", script);
                StringAssert.Contains("\"traits\":[{", script);
                StringAssert.Contains("\"outcomes\":\"C\"", File.ReadAllText(Path.Combine(folder, "history.jsonl")));
                foreach (var quest in run.Quests)
                    StringAssert.Contains(BotReportData.Str(quest.Title), script);
                Assert.That(File.Exists(Path.Combine(folder, "index.html")), Is.True, BotMenu.PageTemplatePath);

                string bots = Path.Combine(folder, "bots");
                BotMenu.WriteHub(bots);
                StringAssert.Contains("players/players-data.js", File.ReadAllText(Path.Combine(folder, "index.html")),
                    "the hub over bots and players sits one folder above the bots' page: " + BotMenu.HubTemplatePath);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

    }
}
