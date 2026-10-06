using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TrollStrategy.Content;
using UnityEditor;

namespace TrollStrategy.Tests
{
    /// <summary>
    /// The arena asks for progress at every stage of the campaign (docs/economy-balance.md §18): each squad the
    /// campaign gives the player wins the levels its quests need, pays for it in health, and stops where the next
    /// stage of gear and upgrades is due.
    /// </summary>
    public class ArenaBenchTests
    {
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private GameContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
        }

        [Test]
        public void Ladder_AsksForProgressAtEveryStage()
        {
            var runs = ArenaBench.Squads.Select(s => (Squad: s, Fights: ArenaBench.Ladder(s, _catalog))).ToList();
            var fights = runs.ToDictionary(r => r.Squad, r => r.Fights);
            int Reach(ArenaBench.Squad squad) => ArenaBench.Reach(fights[squad]);
            ArenaBench.Fight At(ArenaBench.Squad squad, int level) => fights[squad][level - 1];

            var problems = new List<string>();
            void Expect(bool ok, string what)
            {
                if (!ok) problems.Add(what);
            }

            // the first fight teaches the battle: the goblins win it, losing one of them at most
            var first = At(ArenaBench.Tutorial, 1);
            Expect(first.Won && first.Fallen <= 1, $"4 гоблина выигрывают первый бой, теряя не больше одного ({first.Outcome}, пало {first.Fallen})");
            // «Третий уровень арены»: two trolls with swords get there, and not as far as the campaign's last squad
            int early = Reach(ArenaBench.Early), end = Reach(ArenaBench.CampaignEnd);
            int after = Reach(ArenaBench.AfterCampaign), late = Reach(ArenaBench.LateGame);
            Expect(early >= 3 && early < end, $"2 тролля + 2 гоблина доходят до 3-го, но не дальше отряда кампании ({early} против {end})");
            // «Слава арены»: the campaign's squad wins the sixth level, and it costs a fifth to a half of its health
            var glory = At(ArenaBench.CampaignEnd, 6);
            Expect(glory.Won && glory.Lost >= .2 && glory.Lost <= .5 && glory.Fallen <= 1,
                $"6-й уровень: отряд кампании побеждает, теряя 20–50% здоровья и не больше одного бойца ({glory.Outcome}, {glory.Lost:P0}, пало {glory.Fallen})");
            // after the campaign every stage of gear and upgrades opens a further stretch, and the top stays a challenge
            Expect(after >= end + 3, $"сталь и улучшения дают хотя бы 3 уровня сверх отряда кампании ({after} против {end})");
            Expect(late >= after + 4 && late <= 29,
                $"зачарованная сталь даёт хотя бы 4 уровня сверх стали, но не проходит лестницу целиком ({late} против {after})");

            TestContext.WriteLine(ArenaBench.Table(runs));
            Assert.That(problems, Is.Empty, string.Join("\n", problems) + "\n" + ArenaBench.Table(runs));
        }

        [Test, Explicit("Пишет таблицу стенда арены в Builds/Stats/arena-bench.md")]
        public void Bench_WritesTheTable()
        {
            var runs = ArenaBench.Squads.Select(s => (Squad: s, Fights: ArenaBench.Ladder(s, _catalog))).ToList();
            string table = ArenaBench.Table(runs);
            Directory.CreateDirectory("Builds/Stats");
            File.WriteAllText("Builds/Stats/arena-bench.md",
                "# Стенд арены\n\nКаждый отряд проходит все 30 уровней настоящим боем; в клетке — исход, доля потерянного " +
                "здоровья отряда и павшие. «Доходит до» — последний уровень непрерывной серии побед.\n\n" + table);
            TestContext.WriteLine(table);
        }
    }
}
