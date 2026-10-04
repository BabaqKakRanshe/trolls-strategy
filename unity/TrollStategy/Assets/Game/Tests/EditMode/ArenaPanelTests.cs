using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.UI;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The arena window over the real layout and ladder: before the battle it shows the enemies' strength and gear,
    /// how the colony's best squad compares, the whole reward, the prize fund, the stake and what a defeat costs.
    /// </summary>
    public class ArenaPanelTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private GameContentCatalog _catalog;
        private GameSession _session;
        private ColonyHudView _hud;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            _session = new GameSession(_catalog, TestColony.LayoutFor(_catalog,
                new StartingBuilding(BuildingKind.Warehouse, new Cell(10, 8)),
                new StartingBuilding(BuildingKind.Market, new Cell(10, 2)),
                new StartingBuilding(BuildingKind.Barracks, new Cell(2, 8))));
            _hud = TestUi.Colony(new ColonyHudContext(_session, new InteractionController(_session)) { OpenBattle = _ => { } });
            _hud.Arena.Open();
        }

        private BattleMissionDefinition Choose(int level)
        {
            var mission = _session.ArenaLadder().First(m => m.Level == level);
            UiFeel.Press(_hud.Arena.LevelButtons[level - 1]);
            Assert.That(_hud.Arena.Chosen, Is.SameAs(mission));
            return mission;
        }

        [Test]
        public void Poster_ShowsTheEnemiesStrength_TheOdds_TheFund_AndTheStake_FromTheSessionsOffer()
        {
            var mission = Choose(10);
            var offer = _session.ArenaOffer(mission);
            string health = (offer.EnemyHealthPercent / 100.0).ToString("0.#", CultureInfo.InvariantCulture);
            Assert.That(_hud.Arena.EnemyStrength, Is.EqualTo($"×{health} +{offer.EnemyDamageBonus} +{offer.EnemyArmorBonus}"));
            Assert.That(_hud.Arena.EnemyGearShown, Is.EqualTo(System.Math.Min(3, offer.EnemyGear.Count)));
            Assert.That(_hud.Arena.Odds, Is.EqualTo(offer.Odds == OddsGrade.Stronger ? "Отряд сильнее"
                : offer.Odds == OddsGrade.Even ? "На равных" : "Отряд слабее"));
            Assert.That(_hud.Arena.Odds, Is.EqualTo("Отряд слабее"), "A colony without creatures has no squad");
            Assert.That(_hud.Arena.FundDots.All, Is.EqualTo(_catalog.Economy.ArenaFundCap), "A dot per payout the fund may hold");
            Assert.That(_hud.Arena.FundDots.Full, Is.EqualTo(_session.ArenaFund.Payouts));
            Assert.That(_hud.Arena.Stake, Is.EqualTo(offer.Stake.ToString()));
            Assert.That(_hud.Arena.TrophiesShownCount, Is.EqualTo(System.Math.Min(3, offer.Trophies.Count)));
            Assert.That(_hud.Arena.State, Is.EqualTo($"Поражение: ставка сгорит, отдых {TopBar.Duration(offer.DefeatRestMs)}"),
                "In a sandbox nothing closes, so a defeat costs the stake and a longer rest");
        }

        [Test]
        public void Poster_WithoutGoldForTheStake_KeepsTheBattleShutAndSaysHowMuchIsShort()
        {
            var mission = Choose(2);
            int stake = _session.ArenaOffer(mission).Stake;
            Assert.That(stake, Is.GreaterThan(0));
            for (int i = 0; i < 200 && _session.CurrentSnapshot.Gold >= stake; i++)
                if (!_session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 1, _session.FindSpawnCell())).Ok) break;
            Assume.That(_session.CurrentSnapshot.Gold, Is.LessThan(stake), "The colony spent below the stake");
            _hud.Arena.Refresh();
            int shortBy = stake - _session.CurrentSnapshot.Gold;
            Assert.That(_hud.Arena.State, Is.EqualTo($"Не хватает золота на ставку: ещё {shortBy}"));
            Assert.That(UiFeel.IsAvailable(_hud.Arena.FightButton), Is.False);
        }

        [Test]
        public void Poster_MarksTheLaddersMilestones_WithAStarAndAWord()
        {
            var milestones = _session.ArenaLadder().Where(m => m.Milestone).Select(m => m.Level).ToList();
            Assert.That(milestones, Is.EqualTo(new[] { 5, 10, 15, 20, 25, 30 }));
            foreach (var mission in _session.ArenaLadder())
                Assert.That(_hud.Arena.HasStar(mission), Is.EqualTo(mission.Milestone), mission.MissionId);
            Choose(10);
            Assert.That(_hud.Arena.Eyebrow, Is.EqualTo("Веха. Уровень 10"));
            Choose(9);
            Assert.That(_hud.Arena.Eyebrow, Is.EqualTo("Уровень 9"));
        }

        [Test]
        public void Poster_NamesTheLevelsSurroundings()
        {
            Choose(1);
            Assert.That(_hud.Arena.Biome, Is.EqualTo("Луг"));
            Choose(4);
            Assert.That(_hud.Arena.Biome, Is.EqualTo("Лес"));
            Choose(10);
            Assert.That(_hud.Arena.Biome, Is.EqualTo("Кладбище"));
        }
    }
}
