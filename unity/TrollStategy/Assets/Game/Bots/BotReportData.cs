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
            int stopAfterLevel, IReadOnlyList<string> uncommitted = null)
        {
            Uncommitted = uncommitted;
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
    }

    /// <summary>
    /// Bot runs as JSON for the report page (Assets/Game/Bots/Dashboard): the whole latest set, and one compact
    /// line per set for the run history the page compares against.
    /// </summary>
    public static class BotReportData
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>The script the page loads: the latest runs and the history lines, oldest first.</summary>
        public static string Script(IReadOnlyList<BotRun> runs, BotReportInfo info, IEnumerable<string> history) =>
            "window.BOT_DATA = " + Obj(
                ("latest", Latest(runs, info)),
                ("history", "[" + string.Join(",\n", history) + "]")) + ";\n";

        public static string Latest(IReadOnlyList<BotRun> runs, BotReportInfo info) => Obj(
            ("info", Info(info)),
            ("runs", Arr(runs, Run)));

        /// <summary>One line of the run history: per profile the outcome and where its time went.</summary>
        public static string HistoryLine(IReadOnlyList<BotRun> runs, BotReportInfo info) => Obj(
            ("info", Info(info)),
            ("profiles", Arr(runs, run => Obj(
                ("id", Str(run.Profile.Id)),
                ("title", Str(run.Profile.Title)),
                ("outcome", Str(run.Outcome.ToString())),
                ("levels", Num(run.Quests.Count)),
                ("chainLength", Num(run.ChainLength)),
                ("endMs", Num(run.EndMs)),
                ("goldWaitMs", Num(run.Quests.Sum(q => q.GoldWaitMs))),
                ("flowWaitMs", Num(run.Quests.Sum(q => q.FlowWaitMs))),
                ("timeWaitMs", Num(run.Quests.Sum(q => q.TimeWaitMs))),
                ("busyMs", Num(run.Quests.Sum(q => q.BusyMs))),
                ("finalGold", Num(run.FinalGold)),
                ("finalPopulation", Num(run.FinalPopulation)),
                ("arenaLevel", Num(run.ArenaLevel)),
                ("battleGold", Num(run.BattleGold))))));

        private static string Info(BotReportInfo info) => Obj(
            ("generatedAt", Str(info.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss", Invariant))),
            ("commit", Str(info.Commit)),
            ("branch", Str(info.Branch)),
            ("layout", Str(info.Layout)),
            ("chainLength", Num(info.ChainLength)),
            ("stopAfterLevel", Num(info.StopAfterLevel)),
            ("uncommitted", info.Uncommitted == null ? "null" : Arr(info.Uncommitted, Str)));

        private static string Run(BotRun run) => Obj(
            ("id", Str(run.Profile.Id)),
            ("title", Str(run.Profile.Title)),
            ("description", Str(run.Profile.Description)),
            ("thinkSeconds", Num(run.Profile.ThinkSeconds)),
            ("actionSeconds", Num(run.Profile.ActionSeconds)),
            ("questReadSeconds", Num(run.Profile.QuestReadSeconds)),
            ("outcome", Str(run.Outcome.ToString())),
            ("outcomeText", Str(BotReport.Outcome(run))),
            ("stopReason", Str(run.StopReason)),
            ("chainLength", Num(run.ChainLength)),
            ("endMs", Num(run.EndMs)),
            ("finalGold", Num(run.FinalGold)),
            ("finalPopulation", Num(run.FinalPopulation)),
            ("finalBuildings", Num(run.FinalBuildings)),
            ("hired", Num(run.Hired)),
            ("landBought", Num(run.LandBought)),
            ("arenaLevel", Num(run.ArenaLevel)),
            ("battleGold", Num(run.BattleGold)),
            ("salesGold", Num(run.SalesGold)),
            ("questGold", Num(run.QuestGold)),
            ("arenaGoldShare", Num(run.ArenaGoldShare)),
            ("paidRepeatsIn10Min", Num(run.PaidRepeatsIn10Min)),
            ("units", Arr(run.Units, u => Obj(("name", Str(u.Key)), ("count", Num(u.Value))))),
            ("upgrades", Arr(run.Upgrades, u => Obj(("name", Str(u.Key)), ("level", Num(u.Value))))),
            ("unlocks", Arr(run.Unlocks, u => Obj(
                ("name", Str(u.Name)), ("atMs", Num(u.AtMs)), ("questLevel", Num(u.QuestLevel))))),
            ("decisions", Num(run.Decisions)),
            ("commandsAccepted", Num(run.CommandsAccepted)),
            ("quests", Arr(run.Quests, q => Obj(
                ("level", Num(q.Level)),
                ("id", Str(q.Id)),
                ("title", Str(q.Title)),
                ("startMs", Num(q.StartMs)),
                ("doneMs", Num(q.DoneMs)),
                ("goldWaitMs", Num(q.GoldWaitMs)),
                ("flowWaitMs", Num(q.FlowWaitMs)),
                ("timeWaitMs", Num(q.TimeWaitMs)),
                ("busyMs", Num(q.BusyMs)),
                ("goldAfterClaim", Num(q.GoldAfterClaim)),
                ("population", Num(q.Population)),
                ("buildings", Num(q.Buildings))))),
            ("battles", Arr(run.Battles, b => Obj(
                ("atMs", Num(b.AtMs)),
                ("level", Num(b.QuestLevel)),
                ("arenaLevel", Num(b.ArenaLevel)),
                ("squad", Str(b.Squad)),
                ("outcome", Str(b.Outcome.ToString())),
                ("fallen", Num(b.Fallen)),
                ("gold", Num(b.Gold)),
                ("firstWin", Bool(b.FirstWin)),
                ("paidFromFund", Bool(b.PaidFromFund)),
                ("stake", Num(b.Stake)),
                ("odds", Str(b.Odds)),
                ("closedLevel", Num(b.ClosedLevel))))),
            ("samples", Arr(run.Samples, s => Obj(
                ("atMs", Num(s.AtMs)),
                ("level", Num(s.QuestLevel)),
                ("gold", Num(s.Gold)),
                ("soldGoods", Num(s.SoldGoods)),
                ("population", Num(s.Population)),
                ("buildings", Num(s.Buildings)),
                ("landBlocks", Num(s.LandBlocks)),
                ("arenaLevel", Num(s.ArenaLevel))))),
            ("refusals", Arr(run.Refusals, r => Obj(("message", Str(r.Key)), ("count", Num(r.Value))))));

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
