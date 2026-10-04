using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Tests
{
    /// <summary>The battle's share of a side's health, and enemies who wear gear or lead a milestone as champions.</summary>
    public class ArenaRulesTests
    {
        private ArenaTestWorld _world;

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private static BattleReport Run(int enemyHealth, int enemyDamage = 2)
        {
            var board = new BattleBoard(3, 3, new Cell[0], new[] { new Cell(0, 0) });
            var fighters = new List<BattleFighterInput>
            {
                new("troll", UnitKind.Troll, true, new Cell(0, 0), 55, 7, 3, 2600, 1, 500),
                new("enemy-000", UnitKind.Goblin, false, new Cell(2, 0), enemyHealth, enemyDamage, 1, 2000, 3, 200)
            };
            return BattleSimulation.Run(board, fighters, 1);
        }

        [Test]
        public void DefeatedShare_AWonBattleTookAllTheEnemiesHealth_EvenWithTheLastBlowOverTheTop()
        {
            var report = Run(20);
            Assert.That(report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(report.DefeatedShare(enemies: true), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void DefeatedShare_ADrawTookTheDamageDealtOverTheEnemiesHealth()
        {
            var report = Run(100000);
            Assert.That(report.Outcome, Is.EqualTo(BattleOutcome.Draw));
            int dealt = report.Events.Where(e => e.Kind == BattleEventKind.Attack && e.TargetId == "enemy-000")
                .Sum(e => e.Damage);
            Assert.That(dealt, Is.GreaterThan(0));
            Assert.That(report.DefeatedShare(enemies: true), Is.EqualTo(dealt / 100000.0).Within(1e-9));
            Assert.That(report.DefeatedShare(enemies: false), Is.GreaterThan(0).And.LessThan(1));
        }

        [Test]
        public void EnemyGear_AddsItsDamageAndArmour_AndAChampionHasTheLevelsHealthTimesItsPercent()
        {
            _world = new ArenaTestWorld(5000, 100);
            var mission = _world.Mission(1);
            mission.SetDesign("mission-1", "Уровень 1", 3, 3, 1, new[] { new Cell(0, 0) }, new Cell[0], new[]
            {
                new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 0), Gear = new[] { "rusty-sword", "patched-armor" } },
                new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 1), Champion = true }
            }, new[] { new Cell(2, 0), new Cell(2, 1), new Cell(2, 2) });
            mission.SetLadderTraits(BattleFormation.Wall, true, 200);
            _world.Start();

            var battle = _world.Fight(1);
            var enemies = battle.Report.Fighters.Where(f => !f.IsPlayer).ToList();
            Assert.That(enemies[0].Damage, Is.EqualTo(2 + 1), "The rusty sword adds its damage");
            Assert.That(enemies[0].Armor, Is.EqualTo(1 + 1), "The patched armour adds its armour");
            Assert.That(enemies[0].Health, Is.EqualTo(20));
            Assert.That(enemies[1].Health, Is.EqualTo(40), "The champion has twice the level's health");
            Assert.That(enemies[1].Damage, Is.EqualTo(2), "Without gear nothing is added");
        }

        [Test]
        public void EnemyGear_ThatTheCatalogLacks_RefusesTheBattleWithoutChangingTheColony()
        {
            _world = new ArenaTestWorld(5000, 100);
            _world.Mission(1).SetDesign("mission-1", "Уровень 1", 3, 3, 1, new[] { new Cell(0, 0) }, new Cell[0], new[]
            {
                new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 0), Gear = new[] { "no-such-item" } }
            });
            var session = _world.Start();
            string troll = _world.HireTroll();
            int revision = session.CurrentSnapshot.Revision;
            var result = session.Dispatch(new StartBattleCommand("mission-1", new[] { new BattlePlacement(troll, new Cell(0, 0)) }));
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo("Данные миссии некорректны"));
            Assert.That(session.CurrentSnapshot.Revision, Is.EqualTo(revision));
        }
    }
}
