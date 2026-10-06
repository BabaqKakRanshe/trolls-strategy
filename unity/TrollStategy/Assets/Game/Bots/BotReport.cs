using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>Bot runs as Markdown for people and CSV for charts; colony time is shown in minutes.</summary>
    public static class BotReport
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private static string Minutes(int ms) => (ms / 60000f).ToString("0.0", Invariant);

        public static string Outcome(BotRun run) => run.Outcome switch
        {
            BotOutcome.Completed => "пройдена",
            BotOutcome.Stalled => "застрял",
            _ => "не успел"
        };

        public static string Summary(IReadOnlyList<BotRun> runs)
        {
            var text = new StringBuilder();
            text.AppendLine("# Боты: прохождение кампании");
            text.AppendLine();
            text.AppendLine("Время — минуты колонии. «Действия игрока» — сколько ушло на клики и чтение заданий (у профилей с ценой интерфейса); «золото», «поток» и «таймер» — сколько бот ждал по этой причине.");
            text.AppendLine();
            text.AppendLine("| Профиль | Итог | Уровней | Время | Действия игрока | Ждал золота | Ждал потока | Ждал таймера | Самое долгое задание | Бои (победы) | Арена | Золото с арены | Улучшений | Существ | Земли куплено | Золото в конце |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var run in runs)
            {
                var longest = run.Quests.OrderByDescending(q => q.DurationMs).FirstOrDefault();
                text.AppendLine($"| {run.Profile.Title} | {Outcome(run)} | {run.Quests.Count}/{run.ChainLength} | " +
                                $"{Minutes(run.EndMs)} | {Minutes(run.Quests.Sum(q => q.BusyMs))} | {Minutes(run.Quests.Sum(q => q.GoldWaitMs))} | " +
                                $"{Minutes(run.Quests.Sum(q => q.FlowWaitMs))} | {Minutes(run.Quests.Sum(q => q.TimeWaitMs))} | " +
                                (longest != null ? $"{longest.Level}. {longest.Title} ({Minutes(longest.DurationMs)})" : "—") +
                                $" | {run.Battles.Count} ({run.Battles.Count(b => b.Outcome == BattleOutcome.PlayerVictory)}) | " +
                                $"{run.ArenaLevel} | {run.BattleGold} | {run.Upgrades.Values.Sum()} | " +
                                $"{run.FinalPopulation} | {run.LandBought} | {run.FinalGold} |");
            }
            foreach (var run in runs.Where(r => r.Outcome != BotOutcome.Completed))
            {
                text.AppendLine();
                text.AppendLine($"**{run.Profile.Title}** {Outcome(run)} на {run.StopReason}.");
            }
            return text.ToString();
        }

        public static string Markdown(BotRun run)
        {
            var text = new StringBuilder();
            text.AppendLine($"# Бот «{run.Profile.Title}» ({run.Profile.Id})");
            text.AppendLine();
            text.AppendLine(run.Profile.Description);
            text.AppendLine();
            text.AppendLine($"- Итог: **{Outcome(run)}**, уровней {run.Quests.Count} из {run.ChainLength}, " +
                            $"{Minutes(run.EndMs)} мин колонии{(run.StopReason != null ? $"; {run.StopReason}" : "")}");
            text.AppendLine($"- В конце: золото {run.FinalGold}, существ {run.FinalPopulation}, построек {run.FinalBuildings}, " +
                            $"нанято всего {run.Hired}, куплено блоков земли {run.LandBought}");
            text.AppendLine($"- Взглядов на колонию {run.Decisions}, принятых команд {run.CommandsAccepted}, " +
                            $"отказов {run.Refusals.Values.Sum()}");
            text.AppendLine($"- Арена: уровень {run.ArenaLevel}, боёв {run.Battles.Count}, " +
                            $"побед {run.Battles.Count(b => b.Outcome == BattleOutcome.PlayerVictory)}, " +
                            $"ничьих {run.Battles.Count(b => b.Outcome == BattleOutcome.Draw)}, золота {run.BattleGold} " +
                            $"({run.ArenaGoldShare:P0} золота кампании: рынок {run.SalesGold}, задания {run.QuestGold})");
            text.AppendLine($"- Призовой фонд: оплаченных повторов {run.Battles.Count(b => b.PaidFromFund)}, " +
                            $"за любые 10 минут не больше {run.PaidRepeatsIn10Min}; сгорело ставок {run.Battles.Sum(b => b.Stake)}, " +
                            $"закрытий вершины {run.Battles.Count(b => b.ClosedLevel > 0)}");
            text.AppendLine($"- Существа: {Listed(run.Units)}");
            text.AppendLine($"- Нанято по видам: {Listed(run.HiredByKind)}");
            text.AppendLine($"- Постройки: {Listed(run.Buildings)}");
            text.AppendLine($"- Улучшения: {Listed(run.Upgrades)}");
            text.AppendLine("- Открыты жители: " + (run.Unlocks.Count == 0
                ? "—"
                : string.Join(", ", run.Unlocks.Select(u => $"{u.Name} ({Minutes(u.AtMs)} мин, задание {u.QuestLevel})"))));
            text.AppendLine();
            text.AppendLine("## Задания");
            text.AppendLine();
            text.AppendLine("| # | Задание | Готово на | Заняло | Действия | Золото | Поток | Таймер | Золото после | Существ | Построек |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var q in run.Quests)
                text.AppendLine($"| {q.Level} | {q.Title} | {Minutes(q.DoneMs)} | {Minutes(q.DurationMs)} | {Minutes(q.BusyMs)} | " +
                                $"{Minutes(q.GoldWaitMs)} | {Minutes(q.FlowWaitMs)} | {Minutes(q.TimeWaitMs)} | " +
                                $"{q.GoldAfterClaim} | {q.Population} | {q.Buildings} |");

            if (run.Battles.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Бои");
                text.AppendLine();
                text.AppendLine("| Минута | Задание | Арена | Отряд | Исход | Погибло | Награда |");
                text.AppendLine("|---|---|---|---|---|---|---|");
                foreach (var b in run.Battles)
                    text.AppendLine($"| {Minutes(b.AtMs)} | {b.QuestLevel} | {b.ArenaLevel} | {b.Squad} | {b.Outcome} | " +
                                    $"{b.Fallen} | {b.Gold} |");
            }

            if (run.Refusals.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Отказы сессии");
                text.AppendLine();
                foreach (var refusal in run.Refusals.OrderByDescending(r => r.Value))
                    text.AppendLine($"- {refusal.Value} × {refusal.Key}");
            }
            return text.ToString();
        }

        private static string Listed(SortedDictionary<string, int> counts) =>
            counts.Count == 0 ? "—" : string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"));

        /// <summary>One row per colony minute: the timeline behind the quest table.</summary>
        public static string Csv(BotRun run)
        {
            var text = new StringBuilder();
            text.AppendLine("minute,level,gold,sold_goods,population,buildings,land_blocks,arena_level");
            foreach (var s in run.Samples)
                text.AppendLine(string.Join(",", Minutes(s.AtMs), s.QuestLevel, s.Gold, s.SoldGoods, s.Population,
                    s.Buildings, s.LandBlocks, s.ArenaLevel));
            return text.ToString();
        }
    }
}
