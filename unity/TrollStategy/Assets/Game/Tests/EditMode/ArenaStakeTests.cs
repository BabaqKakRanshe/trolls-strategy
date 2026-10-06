using System.Linq;
using NUnit.Framework;
using TrollStrategy.Domain;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// What an arena battle risks and pays: the stake, the prize fund for repeat wins, the cost of a defeat (a longer
    /// rest and the top of the ladder closed again) and a draw's share of the reward.
    /// </summary>
    public class ArenaStakeTests
    {
        private ArenaTestWorld _world;

        [TearDown]
        public void TearDown() => _world?.Dispose();

        // a goblin that kills a troll: a level the colony loses
        private void MakeDeadly(int level) => _world.Mission(level).SetArena(level, 1000, 30, 0, null);

        [Test]
        public void Stake_AWinKeepsIt_ADefeatBurnsIt_AndTheTopOfTheLadderClosesUntilTheLevelBelowIsWonAgain()
        {
            _world = new ArenaTestWorld(5000, 100, 100);
            MakeDeadly(2);
            var session = _world.Start();
            _world.HireTroll();

            int before = session.CurrentSnapshot.Gold;
            var win = _world.Fight(1, claim: false);
            Assert.That(win.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(win.BurnedStake, Is.Zero);
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(before), "A win keeps the stake");
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);

            var offer = session.ArenaOffer(_world.Mission(2));
            Assert.That(offer.Stake, Is.EqualTo(60), "30% of the first win's 200, in tens");
            Assert.That(offer.ClosesOnDefeat, Is.True);
            before = session.CurrentSnapshot.Gold;
            var defeat = _world.Fight(2);
            Assert.That(defeat.Report.Outcome, Is.EqualTo(BattleOutcome.EnemyVictory));
            Assert.That(defeat.BurnedStake, Is.EqualTo(60));
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(before - 60));
            Assert.That(defeat.ClosedMissionId, Is.EqualTo("mission-2"));
            Assert.That(session.IsMissionUnlocked("mission-2"), Is.False, "A defeat at the top closes it again");
            Assert.That(session.CanEnterMission("mission-2").Error, Is.EqualTo("Сначала победите на предыдущем уровне арены"));
            Assert.That(session.HighestMissionLevel, Is.EqualTo(1), "Quests count the highest level ever won");
            Assert.That(defeat.RestMs, Is.EqualTo(200000), "A defeat rests the level twice as long");
            Assert.That(session.MissionWaitMs("mission-2"), Is.EqualTo(200000));

            _world.Wait(100f);
            var again = _world.Fight(1);
            Assert.That(again.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(session.IsMissionUnlocked("mission-2"), Is.True, "A new win below opens it again");
        }

        [Test]
        public void Defeat_OnTheFirstLevel_LeavesItOpen()
        {
            _world = new ArenaTestWorld(5000, 100);
            MakeDeadly(1);
            var session = _world.Start();
            var defeat = _world.Fight(1);
            Assert.That(defeat.Report.Outcome, Is.EqualTo(BattleOutcome.EnemyVictory));
            Assert.That(defeat.ClosedMissionId, Is.Null);
            Assert.That(session.IsMissionUnlocked("mission-1"), Is.True);
            Assert.That(defeat.BurnedStake, Is.EqualTo(30));
        }

        [Test]
        public void Defeat_BelowTheTop_LeavesTheLadderOpen()
        {
            _world = new ArenaTestWorld(5000, 100, 100, 100);
            var session = _world.Start();
            _world.Fight(1);
            _world.Fight(2);
            Assert.That(session.IsMissionUnlocked("mission-3"), Is.True);
            MakeDeadly(2);
            _world.Wait(100f);
            Assert.That(session.ArenaOffer(_world.Mission(2)).ClosesOnDefeat, Is.False);
            var defeat = _world.Fight(2);
            Assert.That(defeat.Report.Outcome, Is.EqualTo(BattleOutcome.EnemyVictory));
            Assert.That(defeat.ClosedMissionId, Is.Null);
            Assert.That(session.IsMissionUnlocked("mission-2"), Is.True);
            Assert.That(session.IsMissionUnlocked("mission-3"), Is.True);
        }

        [Test]
        public void Stake_MissingFromTheTreasury_KeepsTheBattleShutAndSaysHowMuchIsShort()
        {
            _world = new ArenaTestWorld(190, 100);
            var session = _world.Start();
            _world.HireTroll();
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(20));
            Assert.That(session.CanEnterMission("mission-1").Error, Is.EqualTo("Не хватает золота на ставку: ещё 10"));
            Assert.That(session.ArenaOffer(_world.Mission(1)).GoldShort, Is.EqualTo(10));
        }

        [Test]
        public void PrizeFund_PaysOneRepeatWinPerPayout_OnAnyLevel_AndAFirstWinLeavesItAlone()
        {
            _world = new ArenaTestWorld(5000, 100, 100, 100);
            _world.Economy.SetArena(fundPeriodSeconds: 100f, fundCap: 2);
            _world.Mission(2).SetWinGoods(new TrollStrategy.Content.ResourceAmount(TrollStrategy.Content.ResourceKind.RustySword, 1));
            _world.Mission(3).SetWinGoods(new TrollStrategy.Content.ResourceAmount(TrollStrategy.Content.ResourceKind.PatchedArmor, 1));
            var session = _world.Start();
            _world.Wait(100f);
            Assert.That(session.ArenaFund.Payouts, Is.EqualTo(1));
            _world.Fight(1, claim: false);
            _world.Fight(2, claim: false);
            _world.Fight(3, claim: false);
            Assert.That(session.ArenaFund.Payouts, Is.EqualTo(1), "First wins never touch the fund");
            Assert.That(session.CurrentSnapshot.BattleReward.Gold, Is.EqualTo(100 + 200 + 300));
            Assert.That(session.Dispatch(new ClaimBattleRewardCommand()).Ok, Is.True);

            _world.Wait(100f);
            Assert.That(session.ArenaFund.Payouts, Is.EqualTo(2));
            Assert.That(session.ArenaOffer(_world.Mission(1)).PaysFromFund, Is.True);
            var first = _world.Fight(1, claim: false);
            var second = _world.Fight(2, claim: false);
            Assert.That(first.AwardedGold, Is.EqualTo(50));
            Assert.That(second.AwardedGold, Is.EqualTo(100));
            Assert.That(session.ArenaFund.Payouts, Is.Zero);
            var offer = session.ArenaOffer(_world.Mission(3));
            Assert.That(offer.PaysFromFund, Is.False);
            Assert.That((offer.GoldMin, offer.GoldMax), Is.EqualTo((0, 0)), "The window says an empty fund pays nothing");
            Assert.That(offer.Trophies, Is.Empty);
            int before = session.CurrentSnapshot.Gold;
            var unpaid = _world.Fight(3, claim: false);
            Assert.That(unpaid.Report.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
            Assert.That(unpaid.AwardedGold, Is.Zero, "An empty fund pays only the stake back");
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(before));
            Assert.That(session.CurrentSnapshot.BattleReward.Gold, Is.EqualTo(150),
                "Untaken rewards never hold more than the fund paid out");
            Assert.That(session.MissionWins("mission-3"), Is.EqualTo(2), "An unpaid win is still a win");
            Assert.That(session.CurrentSnapshot.BattleReward.Trophies.Sum(t => t.Amount), Is.EqualTo(1),
                "Only the paid wins brought trophies: the second level's one");
        }

        [Test]
        public void Draw_PaysTheShareOfTheEnemiesHealthTaken_BurnsTheStake_AndIsNoWin()
        {
            _world = new ArenaTestWorld(5000, 2000);
            // a goblin that hits for 1 cannot fell the troll in the 90 s of a battle
            _world.Catalog.GetUnit(TrollStrategy.Content.UnitKind.Goblin).SetCombatStats(20, 1, 1, 2000, 3);
            var session = _world.Start();
            _world.HireTroll();
            int before = session.CurrentSnapshot.Gold;
            var draw = _world.Fight(1, claim: false);
            Assert.That(draw.Report.Outcome, Is.EqualTo(BattleOutcome.Draw));
            double share = draw.Report.DefeatedShare(enemies: true);
            Assert.That(share, Is.GreaterThan(.25).And.LessThan(1));
            int expected = (int)System.Math.Round(100 * share);
            Assert.That(draw.AwardedGold, Is.EqualTo(expected));
            Assert.That(session.CurrentSnapshot.BattleReward.Gold, Is.EqualTo(expected));
            Assert.That(session.CurrentSnapshot.BattleReward.Draw, Is.True);
            Assert.That(draw.BurnedStake, Is.EqualTo(30));
            Assert.That(session.CurrentSnapshot.Gold, Is.EqualTo(before - 30));
            Assert.That(session.MissionWins("mission-1"), Is.Zero);
            Assert.That(session.HighestMissionLevel, Is.Zero);
            Assert.That(draw.RestMs, Is.EqualTo(100000), "A draw rests the level as usual");
        }
    }
}
