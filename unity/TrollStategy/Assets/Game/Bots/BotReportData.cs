using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TrollStrategy.Bots
{
    /// <summary>Where and on what a set of bot runs was played, for the report page and the run history.</summary>
    public sealed class BotReportInfo
    {
        public BotReportInfo(DateTime generatedAt, string commit, string branch, string layout, int chainLength,
            int stopAfterLevel, IReadOnlyList<string> uncommitted = null, IReadOnlyList<string> questTitles = null)
        {
            Uncommitted = uncommitted;
            QuestTitles = questTitles;
            GeneratedAt = generatedAt;
            Commit = commit;
            Branch = branch;
            Layout = layout;
            ChainLength = chainLength;
            StopAfterLevel = stopAfterLevel;
        }

        public DateTime GeneratedAt { get; }
        public string Commit { get; }
        public string Branch { get; }
        /// <summary>The starting buildings the runs began with, in words.</summary>
        public string Layout { get; }
        public int ChainLength { get; }
        /// <summary>0 when the runs played the whole chain.</summary>
        public int StopAfterLevel { get; }
        /// <summary>Rule files changed but not committed when the runs played; null when unknown.</summary>
        public IReadOnlyList<string> Uncommitted { get; }
        /// <summary>The chain's quest titles by level, for quests no run finished; null takes them from the runs.</summary>
        public IReadOnlyList<string> QuestTitles { get; }
        /// <summary>Real seconds the runs took to play; 0 when not measured.</summary>
        public double WallSeconds { get; set; }
        /// <summary>Runs played at once.</summary>
        public int Threads { get; set; }
    }

    /// <summary>
    /// A population run as JSON for the report page (Assets/Game/Bots/Dashboard): the population's numbers, one
    /// compact entry per bot in seed order, and one line per run for the history the page compares against. A history
    /// line keeps every bot's outcome and campaign time, so the next run can compare each persona with itself.
    /// </summary>
    public static class BotReportData
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>The script the page loads: the latest run and the history lines, oldest first.</summary>
        public static string Script(BotPopulationStats stats, IReadOnlyList<BotRun> runs, BotReportInfo info,
            IEnumerable<string> history) =>
            "window.BOT_DATA = " + Obj(
                ("latest", Latest(stats, runs, info)),
                ("history", "[" + string.Join(",\n", history) + "]")) + ";\n";

        public static string Latest(BotPopulationStats stats, IReadOnlyList<BotRun> runs, BotReportInfo info) => Obj(
            ("info", Info(info)),
            ("population", Obj(
                ("signature", Str(BotPopulation.Signature)),
                ("count", Num(runs.Count)),
                ("wallSeconds", Num(info.WallSeconds)),
                ("threads", Num(info.Threads)))),
            ("traits", Arr(BotPopulation.Traits, t => Obj(
                ("key", Str(t.Key)),
                ("title", Str(t.Title)),
                ("short", Str(t.Short)),
                ("kind", Str(t.Kind.ToString())),
                ("range", Str(t.Range)),
                ("requires", Str(t.Requires))))),
            ("stats", Stats(stats)),
            ("runs", Arr(runs, run => Entry(run, stats))));

        /// <summary>One line of the run history: the population's spread and every bot's outcome and campaign time.</summary>
        public static string HistoryLine(BotPopulationStats stats, IReadOnlyList<BotRun> runs, BotReportInfo info) => Obj(
            ("info", Info(info)),
            ("population", Obj(
                ("signature", Str(BotPopulation.Signature)),
                ("count", Num(stats.Count)),
                ("completed", Num(stats.Completed)),
                ("stalled", Num(stats.Stalled)),
                ("timeLimit", Num(stats.TimeLimit)),
                ("crashed", Num(stats.Crashed)),
                ("p10", Opt(stats.CampaignMs?.P10)),
                ("p25", Opt(stats.CampaignMs?.P25)),
                ("p50", Opt(stats.CampaignMs?.P50)),
                ("p75", Opt(stats.CampaignMs?.P75)),
                ("p90", Opt(stats.CampaignMs?.P90)),
                ("mean", Opt(stats.CampaignMs?.Mean)))),
            ("outcomes", Str(new string(runs.Select(Code).ToArray()))),
            ("endMs", Arr(runs, run => Num(run.EndMs))));

        /// <summary>A run's outcome in one letter: C completed, S stalled, T time limit, X crashed.</summary>
        public static char Code(BotRun run) => run.Outcome switch
        {
            BotOutcome.Completed => 'C',
            BotOutcome.Stalled => 'S',
            BotOutcome.TimeLimit => 'T',
            _ => 'X'
        };

        private static string Info(BotReportInfo info) => Obj(
            ("generatedAt", Str(info.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss", Invariant))),
            ("commit", Str(info.Commit)),
            ("branch", Str(info.Branch)),
            ("layout", Str(info.Layout)),
            ("chainLength", Num(info.ChainLength)),
            ("stopAfterLevel", Num(info.StopAfterLevel)),
            ("uncommitted", info.Uncommitted == null ? "null" : Arr(info.Uncommitted, Str)));

        private static string Stats(BotPopulationStats s) => Obj(
            ("count", Num(s.Count)),
            ("completed", Num(s.Completed)),
            ("stalled", Num(s.Stalled)),
            ("timeLimit", Num(s.TimeLimit)),
            ("crashed", Num(s.Crashed)),
            ("minGroupRuns", Num(BotPopulationStats.MinGroupRuns)),
            ("campaign", Spread(s.CampaignMs)),
            ("busy", Spread(s.BusyMs)),
            ("goldWait", Spread(s.GoldWaitMs)),
            ("flowWait", Spread(s.FlowWaitMs)),
            ("timeWait", Spread(s.TimeWaitMs)),
            ("arenaLevel", Spread(s.ArenaLevel)),
            ("battleGold", Spread(s.BattleGold)),
            ("finalPopulation", Spread(s.FinalPopulation)),
            ("commands", Spread(s.Commands)),
            ("fastest", Str(s.FastestId)),
            ("median", Str(s.MedianId)),
            ("slowest", Str(s.SlowestId)),
            ("quests", Arr(s.Quests, q => Obj(
                ("level", Num(q.Level)),
                ("title", Str(q.Title)),
                ("reached", Num(q.Reached)),
                ("done", Num(q.Done)),
                ("stoppedHere", Num(q.StoppedHere)),
                ("duration", Spread(q.Duration)),
                ("busyMs", Num(q.BusyMs)),
                ("goldWaitMs", Num(q.GoldWaitMs)),
                ("flowWaitMs", Num(q.FlowWaitMs)),
                ("timeWaitMs", Num(q.TimeWaitMs))))),
            ("effects", Arr(s.Effects, e => Obj(
                ("key", Str(e.Trait.Key)),
                ("title", Str(e.Trait.Title)),
                ("requires", Str(e.Trait.Requires)),
                ("runs", Num(e.Runs)),
                ("rho", Opt(e.Rho)),
                ("spreadMs", Opt(e.SpreadMs)),
                ("stoppedSpread", Num(e.StoppedSpread)),
                ("groups", Arr(e.Groups, g => Obj(
                    ("label", Str(g.Label)),
                    ("from", Num(g.From)),
                    ("to", Num(g.To)),
                    ("count", Num(g.Count)),
                    ("completed", Num(g.Completed)),
                    ("medianMs", Opt(g.MedianMs)))))))),
            ("stalls", Arr(s.Stalls, x => Obj(
                ("level", Num(x.Level)),
                ("title", Str(x.Title)),
                ("count", Num(x.Count)),
                ("reasons", Arr(x.Reasons, r => Obj(("text", Str(r.Reason)), ("count", Num(r.Count))))),
                ("ids", Arr(x.Ids, Str))))),
            ("crashes", Arr(s.Crashes, c => Obj(("id", Str(c.Id)), ("reason", Str(c.Reason))))),
            ("timeline", Arr(s.Timeline, t => Obj(
                ("minute", Num(t.Minute)),
                ("runs", Num(t.Runs)),
                ("gold", Middle(t.Gold)),
                ("population", Middle(t.Population)),
                ("buildings", Middle(t.Buildings)),
                ("level", Middle(t.Level))))),
            ("hired", Arr(s.Hired, Names)),
            ("buildings", Arr(s.Buildings, Names)));

        private static string Names(BotNameCount n) => Obj(
            ("name", Str(n.Name)), ("total", Num(n.Total)), ("runs", Num(n.Runs)), ("runsWithCopies", Num(n.RunsWithCopies)));

        private static string Spread(BotSpread s) => s == null ? "null" : Obj(
            ("n", Num(s.Count)), ("min", Num(s.Min)), ("p10", Num(s.P10)), ("p25", Num(s.P25)), ("p50", Num(s.P50)),
            ("p75", Num(s.P75)), ("p90", Num(s.P90)), ("max", Num(s.Max)), ("mean", Num(s.Mean)));

        private static string Middle(BotSpread s) => s == null ? "null" : Obj(
            ("p25", Num(s.P25)), ("p50", Num(s.P50)), ("p75", Num(s.P75)));

        // one bot, compact: a thousand of them still load in a moment
        private static string Entry(BotRun run, BotPopulationStats stats)
        {
            var values = run.Profile.Seed > 0 ? BotPopulation.Values(run.Profile.Seed) : Array.Empty<double>();
            return Obj(
                ("id", Str(run.Profile.Id)),
                ("seed", Num(run.Profile.Seed)),
                ("title", Str(run.Profile.Title)),
                ("description", Str(run.Profile.Description)),
                ("traits", Arr(values, v => Num(v))),
                ("traitText", Arr(Enumerable.Range(0, values.Length), i => Str(BotPopulation.Traits[i].Format(values[i])))),
                ("outcome", Str(run.Outcome.ToString())),
                ("outcomeText", Str(BotReport.Outcome(run))),
                ("stopReason", Str(run.StopReason)),
                ("levels", Num(run.Quests.Count)),
                ("chainLength", Num(run.ChainLength)),
                ("endMs", Num(run.EndMs)),
                ("busyMs", Num(run.Quests.Sum(q => q.BusyMs))),
                ("goldWaitMs", Num(run.Quests.Sum(q => q.GoldWaitMs))),
                ("flowWaitMs", Num(run.Quests.Sum(q => q.FlowWaitMs))),
                ("timeWaitMs", Num(run.Quests.Sum(q => q.TimeWaitMs))),
                ("commands", Num(run.CommandsAccepted)),
                ("refusals", Num(run.Refusals.Values.Sum())),
                ("battles", Num(run.Battles.Count)),
                ("wins", Num(run.Battles.Count(b => b.Outcome == Domain.BattleOutcome.PlayerVictory))),
                ("arenaLevel", Num(run.ArenaLevel)),
                ("battleGold", Num(run.BattleGold)),
                ("salesGold", Num(run.SalesGold)),
                ("questGold", Num(run.QuestGold)),
                ("arenaGoldShare", Num(run.ArenaGoldShare)),
                ("paidRepeatsIn10Min", Num(run.PaidRepeatsIn10Min)),
                ("finalGold", Num(run.FinalGold)),
                ("finalPopulation", Num(run.FinalPopulation)),
                ("finalBuildings", Num(run.FinalBuildings)),
                ("hired", Num(run.Hired)),
                ("landBought", Num(run.LandBought)),
                ("questMs", Arr(run.Quests, q => Num(q.DurationMs))),
                ("hiredByKind", Arr(run.HiredByKind, kv => Obj(("name", Str(kv.Key)), ("count", Num(kv.Value))))),
                ("buildings", Arr(run.Buildings, kv => Obj(("name", Str(kv.Key)), ("count", Num(kv.Value))))),
                ("report", stats.FullReports.Contains(run.Profile.Id) ? Str($"runs/{run.Profile.Id}.md") : "null"));
        }

        private static string Opt(double? value) => value.HasValue ? Num(value.Value) : "null";

        internal static string Obj(params (string Key, string Value)[] fields) =>
            "{" + string.Join(",", fields.Select(f => Str(f.Key) + ":" + f.Value)) + "}";

        internal static string Arr<T>(IEnumerable<T> items, Func<T, string> item) =>
            "[" + string.Join(",", items.Select(item)) + "]";

        internal static string Num(double value) => value.ToString("0.###", Invariant);
        private static string Bool(bool value) => value ? "true" : "false";

        public static string Str(string value)
        {
            if (value == null) return "null";
            var text = new StringBuilder(value.Length + 2);
            text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    // line and paragraph separators end a JavaScript string literal
                    case '\u2028': text.Append("\\u2028"); break;
                    case '\u2029': text.Append("\\u2029"); break;
                    default:
                        if (c < ' ') text.Append("\\u").Append(((int)c).ToString("x4"));
                        else text.Append(c);
                        break;
                }
            }
            text.Append('"');
            return text.ToString();
        }
    }
}
