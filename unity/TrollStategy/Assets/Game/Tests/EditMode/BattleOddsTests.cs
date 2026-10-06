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
    /// <summary>
    /// The arena window's estimate of a squad against a level: its three grades, and on the real ladder that
    /// "stronger" mostly wins the real battle and "weaker" mostly does not.
    /// </summary>
    public class BattleOddsTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private static BattleFighterInput Fighter(string id, bool player, int health, int damage, int armor = 0,
            int interval = 2000) =>
            new(id, UnitKind.Troll, player, new Cell(0, 0), health, damage, armor, interval, 1, 500);

        [Test]
        public void Compare_GradesTheRatioOfHealthTimesDamageThroughArmour()
        {
            var one = new[] { Fighter("p", true, 100, 10) };
            var equal = new[] { Fighter("e", false, 100, 10) };
            Assert.That(BattleOdds.Compare(one, equal, 1.5, 10).Grade, Is.EqualTo(OddsGrade.Even));
            Assert.That(BattleOdds.Compare(one, equal, 1.5, 10).Ratio, Is.EqualTo(1.0).Within(1e-9));

            var two = new[] { Fighter("p1", true, 100, 10), Fighter("p2", true, 100, 10) };
            Assert.That(BattleOdds.Compare(two, equal, 1.5, 10).Ratio, Is.EqualTo(4.0).Within(1e-9),
                "Twice the health and twice the damage: four times as strong");
            Assert.That(BattleOdds.Compare(two, equal, 1.5, 10).Grade, Is.EqualTo(OddsGrade.Stronger));
            Assert.That(BattleOdds.Compare(equal.Select(e => Fighter("x", true, 100, 10)).ToArray(), two
                .Select(p => Fighter(p.Id, false, 100, 10)).ToArray(), 1.5, 10).Grade, Is.EqualTo(OddsGrade.Weaker));

            var armoured = new[] { Fighter("e", false, 100, 10, armor: 10) };
            Assert.That(BattleOdds.Compare(one, armoured, 1.5, 10).Ratio, Is.EqualTo(0.5).Within(1e-9),
                "Armour equal to the scale halves the damage, so the even fight is half as good");
            Assert.That(BattleOdds.Compare(one, armoured, 1.5, 10).Grade, Is.EqualTo(OddsGrade.Weaker));
            Assert.That(BattleOdds.Compare(Array.Empty<BattleFighterInput>(), equal, 1.5, 10).Grade, Is.EqualTo(OddsGrade.Weaker));
        }

        [Test]
        public void DamageThrough_ArmourTakesAShareOfTheBlow_NeverAllOfIt()
        {
            Assert.That(BattleSimulation.DamageThrough(10, 0, 10), Is.EqualTo(10), "No armour, the whole blow");
            Assert.That(BattleSimulation.DamageThrough(10, 10, 10), Is.EqualTo(5), "Armour equal to the scale halves it");
            Assert.That(BattleSimulation.DamageThrough(7, 3, 10), Is.EqualTo(5), "7 × 10 / 13 = 5.4");
            Assert.That(BattleSimulation.DamageThrough(5, 6, 10), Is.EqualTo(3), "5 × 10 / 16 = 3.1");
            Assert.That(BattleSimulation.DamageThrough(2, 1, 10), Is.EqualTo(2), "A goblin's blow survives light armour");
            Assert.That(BattleSimulation.DamageThrough(1, 50, 10), Is.EqualTo(1), "Every blow lands at least 1");
            Assert.That(BattleSimulation.DamageThrough(6, 2, 4), Is.EqualTo(4), "A smaller scale makes armour stronger");
        }

        [Test]
        public void Estimate_OnTheRealLadder_StrongerMostlyWins_WeakerMostlyDoesNot()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            // the bench of docs/economy-balance.md §12, without the barracks' upgrades
            var squads = new (string Name, UnitKind[] Kinds, string[] Gear)[]
            {
                ("2 тролля + 2 гоблина, ржавое", new[] { UnitKind.Troll, UnitKind.Troll, UnitKind.Goblin, UnitKind.Goblin },
                    new[] { "rusty-sword", "patched-armor" }),
                ("4 тролля", Enumerable.Repeat(UnitKind.Troll, 4).ToArray(), new string[0]),
                ("4 тролля в железе", Enumerable.Repeat(UnitKind.Troll, 4).ToArray(), new[] { "iron-sword", "iron-armor" }),
                ("5 троллей в стали", Enumerable.Repeat(UnitKind.Troll, 5).ToArray(), new[] { "steel-sword", "steel-armor", "helmet" }),
                ("6 троллей, зачарованная сталь", Enumerable.Repeat(UnitKind.Troll, 6).ToArray(),
                    new[] { "enchanted-steel-sword", "enchanted-steel-armor", "enchanted-helmet" })
            };
            int stronger = 0, strongerWon = 0, weaker = 0, weakerLost = 0;
            var table = new List<string>();
            foreach (var mission in catalog.Missions.Where(m => m != null).OrderBy(m => m.Level))
            foreach (var (name, kinds, gear) in squads)
            {
                var state = GameState.CreateInitialState(100000);
                int limit = BattleApplication.SquadLimit(state, mission, catalog);
                for (int i = 0; i < kinds.Length && i < limit; i++)
                {
                    string id = $"unit-{i + 1:D3}";
                    state.Units.Add(new UnitState { Id = id, Kind = kinds[i] });
                    if (kinds[i] != UnitKind.Troll) continue;
                    foreach (string item in gear)
                        state.Equipment.Add(new EquipmentState { Id = $"item-{state.Equipment.Count + 1:D3}", DefinitionId = item, OwnerUnitId = id });
                }
                var offer = ArenaOffers.Create(state, mission, catalog);
                var outcome = Fight(state, mission, catalog);
                table.Add($"{mission.Level,2} {name,-32} {offer.Odds,-8} {offer.OddsRatio,6:0.00} {outcome}");
                if (offer.Odds == OddsGrade.Stronger)
                {
                    stronger++;
                    if (outcome == BattleOutcome.PlayerVictory) strongerWon++;
                }
                else if (offer.Odds == OddsGrade.Weaker)
                {
                    weaker++;
                    if (outcome != BattleOutcome.PlayerVictory) weakerLost++;
                }
            }
            TestContext.WriteLine(string.Join("\n", table));
            TestContext.WriteLine($"stronger {strongerWon}/{stronger}, weaker {weakerLost}/{weaker}");
            Assert.That(stronger, Is.GreaterThan(0));
            Assert.That(weaker, Is.GreaterThan(0));
            Assert.That(strongerWon, Is.GreaterThanOrEqualTo((int)Math.Ceiling(stronger * .8)), "«Отряд сильнее» must mostly win");
            Assert.That(weakerLost, Is.GreaterThanOrEqualTo((int)Math.Ceiling(weaker * .8)), "«Отряд слабее» must mostly not win");
        }

        // the squad placed as the bots place it: melee in the front column, from the middle row out
        private static BattleOutcome Fight(GameState state, BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            var copy = state.Clone();
            var board = mission.CreateBoard();
            int middle = board.Height / 2;
            var cells = board.DeploymentCells.OrderByDescending(c => c.X).ThenBy(c => Math.Abs(c.Y - middle)).ThenBy(c => c.Y).ToList();
            var units = copy.Units.OrderBy(u => catalog.GetUnit(u.Kind).AttackRange).ToList();
            var placements = units.Select((u, i) => new BattlePlacement(u.Id, cells[i])).ToList();
            var result = BattleApplication.Start(copy, new StartBattleCommand(mission.MissionId, placements), catalog, true);
            Assert.That(result.Ok, Is.True, result.Error);
            return copy.ActiveBattle.Report.Outcome;
        }
    }
}
