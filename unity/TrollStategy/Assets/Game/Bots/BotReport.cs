using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Bot runs as Markdown for people (the population's summary, one bot's full report) and CSV for spreadsheets;
    /// colony time is shown in minutes.
    /// </summary>
    public static class BotReport
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private static string Minutes(double ms) => (ms / 60000.0).ToString("0.0", Invariant);

        public static string Outcome(BotRun run) => run.Outcome switch
        {
            BotOutcome.Completed => "пройдена",
            BotOutcome.Stalled => "застрял",
            BotOutcome.Crashed => "сломался",
            _ => "не успел"
        };

        /// <summary>The run in a line, for the console.</summary>
        public static string Headline(BotPopulationStats stats)
        {
            string time = stats.CampaignMs != null
                ? $"кампания у прошедших: медиана {Minutes(stats.CampaignMs.P50)} мин, от {Minutes(stats.CampaignMs.P10)} до {Minutes(stats.CampaignMs.P90)} у 80% из них"
                : "никто не прошёл кампанию";
            return $"Ботов {stats.Count}: прошли {stats.Completed}, застряли {stats.Stalled}, не успели {stats.TimeLimit}, " +
                   $"сломались {stats.Crashed}; {time}.";
        }

        /// <summary>
        /// The population run for people: how many finished and how long it took them, what moves the time, where
        /// bots stop, quest by quest, whom they hired, and the ranges the personas were drawn from.
        /// </summary>
        public static string Summary(BotPopulationStats stats, IReadOnlyList<BotRun> runs, BotReportInfo info)
        {
            var text = new StringBuilder();
            text.AppendLine("# Боты: прохождение кампании");
            text.AppendLine();
            if (info != null)
                text.AppendLine($"Прогон {info.GeneratedAt.ToString("yyyy-MM-dd HH:mm", Invariant)}, коммит {info.Commit ?? "?"}, " +
                                (info.StopAfterLevel > 0 ? $"задания 1–{info.StopAfterLevel} из {info.ChainLength}" : $"{info.ChainLength} заданий") +
                                $", старт: {info.Layout}." +
                                (info.WallSeconds > 0 ? $" Сыграно за {Minutes(info.WallSeconds * 1000)} мин на {info.Threads} потоках." : ""));
            text.AppendLine($"{stats.Count} ботов. Черты каждого взяты из диапазонов по его номеру (`BotPopulation`, состав " +
                            $"{BotPopulation.Signature}), поэтому каждый прогон играют одни и те же боты. Время — минуты колонии; " +
                            "клики и чтение заданий в него входят.");
            text.AppendLine();
            int Of(string id) => runs.FirstOrDefault(r => r.Profile.Id == id)?.EndMs ?? 0;
            text.AppendLine($"- Прошли кампанию {stats.Completed} из {stats.Count} ({Share(stats.Completed, stats.Count)}); " +
                            $"застряли {stats.Stalled}, не успели {stats.TimeLimit}, сломались {stats.Crashed}.");
            if (stats.CampaignMs != null)
            {
                var c = stats.CampaignMs;
                text.AppendLine($"- У прошедших кампания длится: медиана {Minutes(c.P50)} мин, половина — от {Minutes(c.P25)} до " +
                                $"{Minutes(c.P75)}, каждый десятый быстрее {Minutes(c.P10)} и каждый десятый дольше {Minutes(c.P90)}.");
                text.AppendLine($"- В среднем у прошедших: действия игрока {Minutes(stats.BusyMs.Mean)} мин, ждал потока " +
                                $"{Minutes(stats.FlowWaitMs.Mean)}, золота {Minutes(stats.GoldWaitMs.Mean)}, таймера {Minutes(stats.TimeWaitMs.Mean)}.");
                text.AppendLine($"- Самый быстрый — {stats.FastestId} ({Minutes(Of(stats.FastestId))} мин), медианный — {stats.MedianId}, " +
                                $"самый медленный — {stats.SlowestId} ({Minutes(Of(stats.SlowestId))} мин). Их отчёты и отчёты " +
                                "непрошедших — в `runs/`, все боты по строке — в `population.csv`.");
            }

            text.AppendLine();
            text.AppendLine("## Что влияет на время");
            text.AppendLine();
            text.AppendLine("Боты разбиты по каждой черте на группы. В группе — медиана кампании прошедших, в скобках сколько ботов " +
                            "в группе и сколько из них не прошли. ρ — ранговая связь черты со временем: плюс — чем больше, тем " +
                            "дольше. Черты роста считаются только у растущих ботов. Сверху черты с самой большой разницей медиан.");
            text.AppendLine();
            text.AppendLine("| Черта | Разница медиан, мин | ρ | Группы |");
            text.AppendLine("|---|---|---|---|");
            foreach (var effect in stats.Effects)
            {
                string groups = string.Join(" · ", effect.Groups.Select(g =>
                    $"{g.Label}: {(g.MedianMs.HasValue ? Minutes(g.MedianMs.Value) : "—")} ({g.Count}" +
                    (g.Count > g.Completed ? $", не прошли {Share(g.Count - g.Completed, g.Count)}" : "") + ")"));
                text.AppendLine($"| {effect.Trait.Title} | {(effect.SpreadMs.HasValue ? Minutes(effect.SpreadMs.Value) : "—")} | " +
                                $"{(effect.Rho.HasValue ? effect.Rho.Value.ToString("+0.00;−0.00;0.00", Invariant) : "—")} | {groups} |");
            }

            text.AppendLine();
            text.AppendLine("## Где застревают");
            text.AppendLine();
            if (stats.Stalls.Count == 0 && stats.Crashes.Count == 0) text.AppendLine("Все боты прошли кампанию.");
            else
            {
                text.AppendLine("| Задание | Ботов | Чего ждали | Кто |");
                text.AppendLine("|---|---|---|---|");
                foreach (var stall in stats.Stalls)
                    text.AppendLine($"| {stall.Level}. {stall.Title} | {stall.Count} | " +
                                    string.Join("; ", stall.Reasons.Take(3).Select(r => stall.Reasons.Count > 1 ? $"{r.Reason} ({r.Count})" : r.Reason)) +
                                    $" | {Ids(stall.Ids)} |");
                if (stats.Crashes.Count > 0)
                    text.AppendLine($"| сломались | {stats.Crashes.Count} | {stats.Crashes[0].Reason} | {Ids(stats.Crashes.Select(c => c.Id).ToList())} |");
            }

            text.AppendLine();
            text.AppendLine("## Задания");
            text.AppendLine();
            text.AppendLine("Минуты на задание у ботов, которые его прошли; действия, поток, золото и таймер — в среднем на бота.");
            text.AppendLine();
            text.AppendLine("| # | Задание | Начали | Прошли | Застряли | Медиана | Половина ботов | 90% | Действия | Поток | Золото | Таймер |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var q in stats.Quests)
                text.AppendLine($"| {q.Level} | {q.Title} | {q.Reached} | {q.Done} | {(q.StoppedHere > 0 ? q.StoppedHere.ToString(Invariant) : "")} | " +
                                (q.Duration != null
                                    ? $"{Minutes(q.Duration.P50)} | {Minutes(q.Duration.P25)}–{Minutes(q.Duration.P75)} | {Minutes(q.Duration.P90)}"
                                    : "— | — | —") +
                                $" | {Minutes(q.BusyMs)} | {Minutes(q.FlowWaitMs)} | {Minutes(q.GoldWaitMs)} | {Minutes(q.TimeWaitMs)} |");

            if (stats.Hired.Count > 0)
            {
                int all = stats.Hired.Sum(h => h.Total);
                text.AppendLine();
                text.AppendLine("## Кого нанимают");
                text.AppendLine();
                text.AppendLine("| Кто | Всего | На бота | От всех наймов | Ботов нанимали |");
                text.AppendLine("|---|---|---|---|---|");
                foreach (var hired in stats.Hired)
                    text.AppendLine($"| {hired.Name} | {hired.Total} | {(hired.Total / (double)Math.Max(1, stats.Count)).ToString("0.0", Invariant)} | " +
                                    $"{Share(hired.Total, all)} | {hired.Runs} |");
            }
            if (stats.Buildings.Count > 0)
            {
                int copies = runs.Count(r => r.Buildings.Values.Any(n => n > 1));
                text.AppendLine();
                text.AppendLine("## Копии зданий");
                text.AppendLine();
                text.AppendLine($"Хотя бы одну копию здания поставили {copies} из {stats.Count} ботов ({Share(copies, stats.Count)}).");
                text.AppendLine();
                text.AppendLine("| Здание | Ботов с копиями | Ботов строили | Всего у всех |");
                text.AppendLine("|---|---|---|---|");
                foreach (var building in stats.Buildings.Where(b => b.RunsWithCopies > 0).OrderByDescending(b => b.RunsWithCopies))
                    text.AppendLine($"| {building.Name} | {building.RunsWithCopies} | {building.Runs} | {building.Total} |");
            }

            text.AppendLine();
            text.AppendLine("## Черты ботов");
            text.AppendLine();
            text.AppendLine("| Черта | Диапазон | Важна только при |");
            text.AppendLine("|---|---|---|");
            foreach (var trait in BotPopulation.Traits)
                text.AppendLine($"| {trait.Title} | {trait.Range} | " +
                                (trait.Requires != null ? BotPopulation.Traits[BotPopulation.IndexOf(trait.Requires)].Title.ToLowerInvariant() : "") + " |");
            return text.ToString();
        }

        private static string Share(int part, int whole) => whole > 0 ? (100.0 * part / whole).ToString("0", Invariant) + "%" : "—";

        private static string Ids(IReadOnlyList<string> ids) =>
            string.Join(", ", ids.Take(8)) + (ids.Count > 8 ? $" и ещё {ids.Count - 8}" : "");

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

        /// <summary>One line per bot: how its campaign went and its traits, for a spreadsheet.</summary>
        public static string Population(IReadOnlyList<BotRun> runs)
        {
            var text = new StringBuilder();
            text.AppendLine(string.Join(",", new[]
            {
                "id", "seed", "outcome", "levels", "campaign_min", "busy_min", "gold_wait_min", "flow_wait_min",
                "time_wait_min", "commands", "final_gold", "final_population", "arena_level", "battle_gold"
            }.Concat(BotPopulation.Traits.Select(t => t.Key)).Append("stop_reason")));
            foreach (var run in runs)
            {
                var values = run.Profile.Seed > 0 ? BotPopulation.Values(run.Profile.Seed) : null;
                var cells = new List<string>
                {
                    run.Profile.Id, run.Profile.Seed.ToString(Invariant), run.Outcome.ToString(), run.Quests.Count.ToString(Invariant),
                    Minutes(run.EndMs), Minutes(run.Quests.Sum(q => q.BusyMs)), Minutes(run.Quests.Sum(q => q.GoldWaitMs)),
                    Minutes(run.Quests.Sum(q => q.FlowWaitMs)), Minutes(run.Quests.Sum(q => q.TimeWaitMs)),
                    run.CommandsAccepted.ToString(Invariant), run.FinalGold.ToString(Invariant),
                    run.FinalPopulation.ToString(Invariant), run.ArenaLevel.ToString(Invariant), run.BattleGold.ToString(Invariant)
                };
                cells.AddRange(BotPopulation.Traits.Select((_, i) => values == null ? "" : values[i].ToString("0.###", Invariant)));
                cells.Add(run.StopReason == null ? "" : "\"" + run.StopReason.Replace("\"", "\"\"") + "\"");
                text.AppendLine(string.Join(",", cells));
            }
            return text.ToString();
        }
    }
}
