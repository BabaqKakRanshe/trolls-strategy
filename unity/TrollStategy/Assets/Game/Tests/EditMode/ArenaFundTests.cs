using NUnit.Framework;
using TrollStrategy.Domain;

namespace TrollStrategy.Tests
{
    /// <summary>The arena's prize fund: a payout every period of active time, never more than its cap.</summary>
    public class ArenaFundTests
    {
        private const int Cap = 3, Period = 180000;

        private static GameState At(int ms, int payouts = 0, int since = 0) =>
            new() { ActiveTimeMs = ms, ArenaFundPayouts = payouts, ArenaFundSinceMs = since };

        [Test]
        public void Available_GrowsByOneEveryPeriod_UpToTheCap()
        {
            Assert.That(ArenaFund.Available(At(0), Cap, Period), Is.EqualTo(0));
            Assert.That(ArenaFund.Available(At(179999), Cap, Period), Is.EqualTo(0));
            Assert.That(ArenaFund.Available(At(180000), Cap, Period), Is.EqualTo(1));
            Assert.That(ArenaFund.Available(At(540000), Cap, Period), Is.EqualTo(3));
            Assert.That(ArenaFund.Available(At(5400000), Cap, Period), Is.EqualTo(3), "The fund holds no more than its cap");
        }

        [Test]
        public void NextInMs_CountsDownToTheNextPayout_AndIsMinusOneWhileFull()
        {
            Assert.That(ArenaFund.NextInMs(At(100000), Cap, Period), Is.EqualTo(80000));
            Assert.That(ArenaFund.NextInMs(At(200000), Cap, Period), Is.EqualTo(160000));
            Assert.That(ArenaFund.NextInMs(At(600000), Cap, Period), Is.EqualTo(-1));
        }

        [Test]
        public void Take_FromAFillingFund_KeepsTheTimeGatheredTowardsTheNextPayout()
        {
            var state = At(250000);
            Assert.That(ArenaFund.Take(state, Cap, Period), Is.True);
            Assert.That(ArenaFund.Available(state, Cap, Period), Is.EqualTo(0));
            Assert.That(ArenaFund.NextInMs(state, Cap, Period), Is.EqualTo(110000), "70 s of the next payout are already gathered");
            Assert.That(ArenaFund.Take(state, Cap, Period), Is.False, "An empty fund pays nothing");
        }

        [Test]
        public void Take_FromAFullFund_StartsTheNextPayoutNow()
        {
            var state = At(900000);
            Assert.That(ArenaFund.Take(state, Cap, Period), Is.True);
            Assert.That(ArenaFund.Available(state, Cap, Period), Is.EqualTo(2));
            Assert.That(ArenaFund.NextInMs(state, Cap, Period), Is.EqualTo(Period), "A full fund gathered nothing more");
            state.ActiveTimeMs += Period;
            Assert.That(ArenaFund.Available(state, Cap, Period), Is.EqualTo(3));
        }

        [Test]
        public void Take_SeveralInARow_NeverPaysMoreThanTheFundHeld()
        {
            var state = At(600000);
            int paid = 0;
            for (int i = 0; i < 10; i++)
                if (ArenaFund.Take(state, Cap, Period)) paid++;
            Assert.That(paid, Is.EqualTo(Cap));
        }
    }
}
