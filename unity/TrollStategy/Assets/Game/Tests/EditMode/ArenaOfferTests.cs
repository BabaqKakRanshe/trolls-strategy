using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Tests
{
    /// <summary>What the arena window and the bots learn about a level before the battle.</summary>
    public class ArenaOfferTests
    {
        private ArenaTestWorld _world;

        [TearDown]
        public void TearDown() => _world?.Dispose();

        [Test]
        public void Offer_ShowsTheEnemiesStrengthWithTheirGear_AndTheWholeReward()
        {
            _world = new ArenaTestWorld(5000, 100);
            var mission = _world.Mission(1);
            mission.SetDesign("mission-1", "Уровень 1", 3, 3, 1, new[] { new Cell(0, 0) }, new Cell[0], new[]
            {
                new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 0), Gear = new[] { "rusty-sword" } },
                new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 1), Gear = new[] { "patched-armor", "rusty-sword" } }
            });
            mission.SetArena(1, 250, 2, 1, UnitKind.Goblin);
            mission.SetWinGoods(new ResourceAmount(ResourceKind.RustySword, 2));
            var session = _world.Start();

            var offer = session.ArenaOffer(mission);
            Assert.That(offer.EnemyHealthPercent, Is.EqualTo(250));
            Assert.That(offer.EnemyDamageBonus, Is.EqualTo(2 + 1), "The level's bonus and the rusty sword");
            Assert.That(offer.EnemyArmorBonus, Is.EqualTo(1 + 1), "The level's bonus and the patched armour");
            Assert.That(offer.EnemyGear.Select(g => g.ItemId), Is.EqualTo(new[] { "rusty-sword", "patched-armor" }),
                "Each item once, weapons first");
            Assert.That(offer.FirstWin, Is.True);
            Assert.That((offer.GoldMin, offer.GoldMax), Is.EqualTo((100, 100)));
            Assert.That(offer.Trophies.Single().Amount, Is.EqualTo(2));
            Assert.That(offer.UnlockUnit, Is.EqualTo(UnitKind.Goblin));
            Assert.That(offer.Stake, Is.EqualTo(30));
            Assert.That(offer.ClosesOnDefeat, Is.False, "The first level never closes");
            Assert.That(offer.DefeatRestMs, Is.EqualTo(200000));

            Assert.That(offer.SquadCount, Is.Zero);
            Assert.That(offer.Odds, Is.EqualTo(OddsGrade.Weaker), "No creatures, no squad");
        }

        [Test]
        public void Offer_RatesTheColonysBestSquad_WithItsGear_AndFollowsTheColony()
        {
            _world = new ArenaTestWorld(5000, 100);
            _world.Mission(1).SetDesign("mission-1", "Уровень 1", 3, 3, 2, new[] { new Cell(0, 0), new Cell(0, 1) }, new Cell[0],
                new[] { new BattleEnemyStart { Kind = UnitKind.Goblin, Cell = new Cell(2, 0) } });
            var session = _world.Start();
            _world.HireTroll();
            var one = session.ArenaOffer(_world.Mission(1));
            Assert.That(session.ArenaOffer(_world.Mission(1)), Is.SameAs(one), "One offer per revision");
            Assert.That(one.SquadCount, Is.EqualTo(1));
            _world.HireTroll();
            var two = session.ArenaOffer(_world.Mission(1));
            Assert.That(two.SquadCount, Is.EqualTo(2));
            Assert.That(two.OddsRatio, Is.GreaterThan(one.OddsRatio * 3.5), "Two trolls: twice the health and the damage");

            var armed = new ArenaTestWorld(5000, 100);
            try
            {
                armed.Catalog.Equipment.First(e => e.ItemId == "steel-sword").Init("steel-sword", "Стальной меч",
                    EquipmentSlot.Weapon, 4, 0, 1);
                var armedSession = armed.Start();
                armed.HireTroll();
                Assert.That(armedSession.ArenaOffer(armed.Mission(1)).OddsRatio,
                    Is.GreaterThan(one.OddsRatio), "The colony's sword goes to the best fighter");
            }
            finally { armed.Dispose(); }
        }

        [Test]
        public void Offer_AfterTheFirstWin_IsARepeat_WithoutTheCreatureAndPaidOnlyFromTheFund()
        {
            _world = new ArenaTestWorld(5000, 100);
            _world.Mission(1).SetArena(1, 100, 0, 0, UnitKind.Goblin);
            var session = _world.Start();
            _world.Fight(1);
            var offer = session.ArenaOffer(_world.Mission(1));
            Assert.That(offer.FirstWin, Is.False);
            Assert.That(offer.UnlockUnit, Is.Null);
            Assert.That(offer.Stake, Is.EqualTo(20), "30% of the repeat's 50, in tens");
            Assert.That(offer.PaysFromFund, Is.False);
            Assert.That((offer.GoldMin, offer.GoldMax), Is.EqualTo((0, 0)));
            _world.Wait(180f);
            offer = session.ArenaOffer(_world.Mission(1));
            Assert.That(offer.Fund.Payouts, Is.EqualTo(1));
            Assert.That(offer.PaysFromFund, Is.True);
            Assert.That((offer.GoldMin, offer.GoldMax), Is.EqualTo((50, 50)));
        }
    }
}
