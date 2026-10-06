using System;
using System.Collections.Generic;
using System.Linq;

namespace TrollStrategy.Bots
{
    /// <summary>How a set of values spreads: the points the report reads off it.</summary>
    public sealed class BotSpread
    {
        public int Count { get; private set; }
        public double Min { get; private set; }
        public double P10 { get; private set; }
        public double P25 { get; private set; }
        public double P50 { get; private set; }
        public double P75 { get; private set; }
        public double P90 { get; private set; }
        public double Max { get; private set; }
        public double Mean { get; private set; }

        /// <summary>Null when there are no values.</summary>
        public static BotSpread Of(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return null;
            return new BotSpread
            {
                Count = sorted.Count,
                Min = sorted[0],
                P10 = Quantile(sorted, 0.10),
                P25 = Quantile(sorted, 0.25),
                P50 = Quantile(sorted, 0.50),
                P75 = Quantile(sorted, 0.75),
                P90 = Quantile(sorted, 0.90),
                Max = sorted[sorted.Count - 1],
                Mean = sorted.Average()
            };
        }

        /// <summary>The value below which the share <paramref name="q"/> of the sorted values lies, between neighbours.</summary>
        public static double Quantile(IReadOnlyList<double> sorted, double q)
        {
            if (sorted.Count == 0) throw new ArgumentException("Нет значений");
            double at = (sorted.Count - 1) * q;
            int below = (int)Math.Floor(at);
            int above = Math.Min(sorted.Count - 1, below + 1);
            return sorted[below] + (sorted[above] - sorted[below]) * (at - below);
        }
    }

    /// <summary>One quest across the population: how many began and finished it, how long it took, what held it up.</summary>
    public sealed class BotQuestStat
    {
        public int Level;
        /// <summary>The quest's id: the chain changes, and a level alone may name another quest in other data.</summary>
        public string Id;
        public string Title;
        /// <summary>Runs that began the quest.</summary>
        public int Reached;
        public int Done;
        /// <summary>Runs that stalled or ran out of time on it.</summary>
        public int StoppedHere;
        /// <summary>Colony milliseconds the runs that finished it took.</summary>
        public BotSpread Duration;
        /// <summary>Mean milliseconds per finished run: the player's clicks and reading, then what the bot waited for.</summary>
        public double BusyMs, GoldWaitMs, FlowWaitMs, TimeWaitMs;
    }

    /// <summary>The runs whose trait fell in one group (a value, a choice, a fifth of the range).</summary>
    public sealed class BotTraitGroup
    {
        public string Label;
        public double From, To;
        public int Count;
        public int Completed;
        /// <summary>The campaign time of the group's finished runs; null when none finished.</summary>
        public double? MedianMs;
        /// <summary>Share of the group that did not finish the campaign.</summary>
        public double StoppedShare => Count > 0 ? (double)(Count - Completed) / Count : 0;
    }

    /// <summary>What one trait does to the campaign: its groups side by side and how the time follows the value.</summary>
    public sealed class BotTraitEffect
    {
        public BotTrait Trait;
        /// <summary>Runs the trait mattered for (its <see cref="BotTrait.Requires"/> flag on).</summary>
        public int Runs;
        /// <summary>
        /// Rank correlation of the value with the campaign time of finished runs: +1 the more, the longer; −1 the more,
        /// the shorter. Null for choices and when too few runs finished.
        /// </summary>
        public double? Rho;
        /// <summary>The slowest group's median campaign minus the fastest's; null when fewer than two groups have enough runs.</summary>
        public double? SpreadMs;
        /// <summary>The largest share of unfinished runs in a group minus the smallest.</summary>
        public double StoppedSpread;
        public List<BotTraitGroup> Groups = new();
    }

    /// <summary>Runs that stopped on one quest, with what they waited for.</summary>
    public sealed class BotStall
    {
        public int Level;
        public string Title;
        public int Count;
        /// <summary>The waits the runs named, most common first.</summary>
        public List<(string Reason, int Count)> Reasons = new();
        public List<string> Ids = new();
    }

    /// <summary>The playing colonies at one colony minute.</summary>
    public sealed class BotMinute
    {
        public int Minute;
        /// <summary>Runs still playing at this minute.</summary>
        public int Runs;
        public BotSpread Gold, Population, Buildings, Level;
    }

    /// <summary>A creature or building name across the runs.</summary>
    public sealed class BotNameCount
    {
        public string Name;
        /// <summary>All of them in all runs.</summary>
        public int Total;
        /// <summary>Runs that had any.</summary>
        public int Runs;
        /// <summary>Runs that had more than one (copies of a building).</summary>
        public int RunsWithCopies;
    }

    /// <summary>
    /// A population run in numbers: how many finished and how long it took them, quest by quest, which traits move
    /// the time, where the runs stop, the colony minute by minute. Campaign times are the finished runs' only;
    /// unfinished runs count in the shares that stopped.
    /// </summary>
    public sealed class BotPopulationStats
    {
        /// <summary>A trait group needs this many finished runs for its median to count toward a trait's spread.</summary>
        public const int MinGroupRuns = 5;

        public int Count, Completed, Stalled, TimeLimit, Crashed;
        /// <summary>Campaign milliseconds of the finished runs.</summary>
        public BotSpread CampaignMs;
        /// <summary>Over the finished runs: the player's clicks and reading, and what the bot waited for.</summary>
        public BotSpread BusyMs, GoldWaitMs, FlowWaitMs, TimeWaitMs;
        public BotSpread ArenaLevel, BattleGold, FinalPopulation, Commands;
        public List<BotQuestStat> Quests = new();
        /// <summary>Traits by the spread of their groups' median campaign, largest first.</summary>
        public List<BotTraitEffect> Effects = new();
        public List<BotStall> Stalls = new();
        /// <summary>Runs whose bot or session threw: a bug to fix, not a finding about the game.</summary>
        public List<(string Id, string Reason)> Crashes = new();
        public List<BotMinute> Timeline = new();
        /// <summary>Creatures hired by kind, the fallen and sold included.</summary>
        public List<BotNameCount> Hired = new();
        /// <summary>Buildings at the end by name; copies are a second or later building of the kind.</summary>
        public List<BotNameCount> Buildings = new();
        /// <summary>The fastest, the median and the slowest finished runs.</summary>
        public string FastestId, MedianId, SlowestId;
        /// <summary>
        /// Runs worth a full report of their own: every one that did not finish (the first
        /// <see cref="MaxFullReports"/> by seed) and the fastest, the median and the slowest.
        /// </summary>
        public HashSet<string> FullReports = new(StringComparer.Ordinal);

        public const int MaxFullReports = 50;

        public static BotPopulationStats Of(IReadOnlyList<BotRun> runs, IReadOnlyList<string> questTitles = null)
        {
            var stats = new BotPopulationStats
            {
                Count = runs.Count,
                Completed = runs.Count(r => r.Outcome == BotOutcome.Completed),
                Stalled = runs.Count(r => r.Outcome == BotOutcome.Stalled),
                TimeLimit = runs.Count(r => r.Outcome == BotOutcome.TimeLimit),
                Crashed = runs.Count(r => r.Outcome == BotOutcome.Crashed)
            };
            var done = runs.Where(r => r.Outcome == BotOutcome.Completed).ToList();
            stats.CampaignMs = BotSpread.Of(done.Select(r => (double)r.EndMs));
            stats.BusyMs = BotSpread.Of(done.Select(r => (double)r.Quests.Sum(q => q.BusyMs)));
            stats.GoldWaitMs = BotSpread.Of(done.Select(r => (double)r.Quests.Sum(q => q.GoldWaitMs)));
            stats.FlowWaitMs = BotSpread.Of(done.Select(r => (double)r.Quests.Sum(q => q.FlowWaitMs)));
            stats.TimeWaitMs = BotSpread.Of(done.Select(r => (double)r.Quests.Sum(q => q.TimeWaitMs)));
            stats.ArenaLevel = BotSpread.Of(runs.Select(r => (double)r.ArenaLevel));
            stats.BattleGold = BotSpread.Of(runs.Select(r => (double)r.BattleGold));
            stats.FinalPopulation = BotSpread.Of(runs.Select(r => (double)r.FinalPopulation));
            stats.Commands = BotSpread.Of(runs.Select(r => (double)r.CommandsAccepted));

            var ordered = done.OrderBy(r => r.EndMs).ThenBy(r => r.Profile.Seed).ToList();
            if (ordered.Count > 0)
            {
                stats.FastestId = ordered[0].Profile.Id;
                stats.MedianId = ordered[(ordered.Count - 1) / 2].Profile.Id;
                stats.SlowestId = ordered[ordered.Count - 1].Profile.Id;
            }

            foreach (var run in runs.Where(r => r.Outcome != BotOutcome.Completed).OrderBy(r => r.Profile.Seed).Take(MaxFullReports))
                stats.FullReports.Add(run.Profile.Id);
            foreach (string id in new[] { stats.FastestId, stats.MedianId, stats.SlowestId })
                if (id != null) stats.FullReports.Add(id);

            int chain = runs.Count > 0 ? runs.Max(r => r.ChainLength) : questTitles?.Count ?? 0;
            string Title(int level)
            {
                if (questTitles != null && level >= 1 && level <= questTitles.Count && !string.IsNullOrEmpty(questTitles[level - 1]))
                    return questTitles[level - 1];
                return runs.SelectMany(r => r.Quests).FirstOrDefault(q => q.Level == level)?.Title ?? $"задание {level}";
            }

            FillQuests(stats, runs, chain, Title);
            FillStalls(stats, runs, chain, Title);
            FillEffects(stats, runs);
            FillTimeline(stats, runs);
            stats.Hired = Names(runs, r => r.HiredByKind);
            stats.Buildings = Names(runs, r => r.Buildings);
            return stats;
        }

        private static bool Stopped(BotRun run) => run.Outcome is BotOutcome.Stalled or BotOutcome.TimeLimit;

        private static void FillQuests(BotPopulationStats stats, IReadOnlyList<BotRun> runs, int chain, Func<int, string> title)
        {
            for (int level = 1; level <= chain; level++)
            {
                var records = runs.Select(r => r.Quests.FirstOrDefault(q => q.Level == level)).Where(q => q != null).ToList();
                int n = Math.Max(1, records.Count);
                stats.Quests.Add(new BotQuestStat
                {
                    Level = level,
                    Id = runs.SelectMany(r => r.Quests).FirstOrDefault(q => q.Level == level && q.Id != null)?.Id,
                    Title = title(level),
                    Reached = runs.Count(r => r.Quests.Count >= level - 1 && (r.Quests.Count >= level || r.Outcome != BotOutcome.Completed)),
                    Done = records.Count,
                    StoppedHere = runs.Count(r => Stopped(r) && r.Quests.Count == level - 1),
                    Duration = BotSpread.Of(records.Select(q => (double)q.DurationMs)),
                    BusyMs = records.Sum(q => (double)q.BusyMs) / n,
                    GoldWaitMs = records.Sum(q => (double)q.GoldWaitMs) / n,
                    FlowWaitMs = records.Sum(q => (double)q.FlowWaitMs) / n,
                    TimeWaitMs = records.Sum(q => (double)q.TimeWaitMs) / n
                });
            }
        }

        private static void FillStalls(BotPopulationStats stats, IReadOnlyList<BotRun> runs, int chain, Func<int, string> title)
        {
            foreach (var group in runs.Where(Stopped).GroupBy(r => Math.Min(chain, r.Quests.Count + 1)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key))
            {
                stats.Stalls.Add(new BotStall
                {
                    Level = group.Key,
                    Title = title(group.Key),
                    Count = group.Count(),
                    Reasons = group.GroupBy(r => r.StopWait ?? r.StopReason ?? "?")
                        .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).ToList(),
                    Ids = group.OrderBy(r => r.Profile.Seed).Select(r => r.Profile.Id).ToList()
                });
            }
            foreach (var run in runs.Where(r => r.Outcome == BotOutcome.Crashed).OrderBy(r => r.Profile.Seed))
                stats.Crashes.Add((run.Profile.Id, run.StopReason));
        }

        private static void FillEffects(BotPopulationStats stats, IReadOnlyList<BotRun> runs)
        {
            var personas = runs.Where(r => r.Profile.Seed > 0 && r.Outcome != BotOutcome.Crashed)
                .Select(r => (Run: r, Values: BotPopulation.Values(r.Profile.Seed))).ToList();
            if (personas.Count == 0) return;
            var traits = BotPopulation.Traits;
            for (int t = 0; t < traits.Count; t++)
            {
                var trait = traits[t];
                int requires = trait.Requires != null ? BotPopulation.IndexOf(trait.Requires) : -1;
                int index = t;
                var included = personas.Where(p => requires < 0 || p.Values[requires] > 0)
                    .Select(p => (p.Run, Value: p.Values[index])).OrderBy(p => p.Value).ThenBy(p => p.Run.Profile.Seed).ToList();
                if (included.Count == 0) continue;

                var effect = new BotTraitEffect { Trait = trait, Runs = included.Count };
                foreach (var chunk in Chunks(trait, included.Select(p => p.Value).ToList()))
                {
                    var members = included.Where(p => p.Value >= chunk.From && p.Value <= chunk.To).ToList();
                    var times = members.Where(p => p.Run.Outcome == BotOutcome.Completed).Select(p => (double)p.Run.EndMs).ToList();
                    effect.Groups.Add(new BotTraitGroup
                    {
                        Label = chunk.Label,
                        From = chunk.From,
                        To = chunk.To,
                        Count = members.Count,
                        Completed = times.Count,
                        MedianMs = times.Count > 0 ? BotSpread.Of(times).P50 : null
                    });
                }
                var counted = effect.Groups.Where(g => g.Completed >= MinGroupRuns).ToList();
                if (counted.Count >= 2) effect.SpreadMs = counted.Max(g => g.MedianMs.Value) - counted.Min(g => g.MedianMs.Value);
                var sized = effect.Groups.Where(g => g.Count >= MinGroupRuns).ToList();
                if (sized.Count >= 2) effect.StoppedSpread = sized.Max(g => g.StoppedShare) - sized.Min(g => g.StoppedShare);
                if (trait.Kind != BotTraitKind.Choice)
                {
                    var finished = included.Where(p => p.Run.Outcome == BotOutcome.Completed).ToList();
                    effect.Rho = Spearman(finished.Select(p => p.Value).ToList(), finished.Select(p => (double)p.Run.EndMs).ToList());
                }
                stats.Effects.Add(effect);
            }
            stats.Effects = stats.Effects.OrderByDescending(e => e.SpreadMs ?? -1).ThenByDescending(e => e.StoppedSpread).ToList();
        }

        /// <summary>
        /// The groups a trait splits the runs into: each value of a flag, a choice or a number with few values, else
        /// five groups of about equal size that never split equal values.
        /// </summary>
        private static List<(string Label, double From, double To)> Chunks(BotTrait trait, List<double> sortedValues)
        {
            var groups = new List<(string, double, double)>();
            var distinct = sortedValues.Distinct().ToList();
            if (trait.Kind != BotTraitKind.Number || distinct.Count <= 8)
            {
                foreach (double value in distinct) groups.Add((trait.Format(value), value, value));
                return groups;
            }
            int target = (int)Math.Ceiling(sortedValues.Count / 5.0), start = 0;
            while (start < sortedValues.Count)
            {
                int end = Math.Min(sortedValues.Count, start + target);
                while (end < sortedValues.Count && sortedValues[end] == sortedValues[end - 1]) end++;
                double from = sortedValues[start], to = sortedValues[end - 1];
                groups.Add((from == to ? trait.Format(from) : $"{trait.Format(from)} — {trait.Format(to)}", from, to));
                start = end;
            }
            return groups;
        }

        /// <summary>Spearman's rank correlation; null for fewer than ten pairs or a constant side.</summary>
        public static double? Spearman(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            if (x.Count != y.Count || x.Count < 10) return null;
            var rx = Ranks(x);
            var ry = Ranks(y);
            double mx = rx.Average(), my = ry.Average(), sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < rx.Length; i++)
            {
                sxy += (rx[i] - mx) * (ry[i] - my);
                sxx += (rx[i] - mx) * (rx[i] - mx);
                syy += (ry[i] - my) * (ry[i] - my);
            }
            return sxx > 0 && syy > 0 ? sxy / Math.Sqrt(sxx * syy) : null;
        }

        // ranks from 1, ties sharing their mean rank
        private static double[] Ranks(IReadOnlyList<double> values)
        {
            var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToArray();
            var ranks = new double[values.Count];
            for (int i = 0; i < order.Length;)
            {
                int j = i;
                while (j + 1 < order.Length && values[order[j + 1]] == values[order[i]]) j++;
                double rank = (i + j) / 2.0 + 1;
                for (int k = i; k <= j; k++) ranks[order[k]] = rank;
                i = j + 1;
            }
            return ranks;
        }

        // every minute of every run, each holding the colony as its last sample before the minute
        private static void FillTimeline(BotPopulationStats stats, IReadOnlyList<BotRun> runs)
        {
            int last = runs.Where(r => r.Samples.Count > 0).Select(r => r.EndMs / 60000).DefaultIfEmpty(-1).Max();
            for (int minute = 0; minute <= last; minute++)
            {
                var at = new List<BotSample>();
                foreach (var run in runs)
                {
                    if (run.Samples.Count == 0 || minute * 60000 > run.EndMs) continue;
                    BotSample held = null;
                    foreach (var sample in run.Samples)
                    {
                        if (sample.AtMs > minute * 60000) break;
                        held = sample;
                    }
                    if (held != null) at.Add(held);
                }
                if (at.Count == 0) continue;
                stats.Timeline.Add(new BotMinute
                {
                    Minute = minute,
                    Runs = at.Count,
                    Gold = BotSpread.Of(at.Select(s => (double)s.Gold)),
                    Population = BotSpread.Of(at.Select(s => (double)s.Population)),
                    Buildings = BotSpread.Of(at.Select(s => (double)s.Buildings)),
                    Level = BotSpread.Of(at.Select(s => (double)s.QuestLevel))
                });
            }
        }

        private static List<BotNameCount> Names(IReadOnlyList<BotRun> runs, Func<BotRun, IDictionary<string, int>> counts) => runs
            .SelectMany(r => counts(r) ?? new Dictionary<string, int>())
            .GroupBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(g => new BotNameCount
            {
                Name = g.Key,
                Total = g.Sum(kv => kv.Value),
                Runs = g.Count(kv => kv.Value > 0),
                RunsWithCopies = g.Count(kv => kv.Value > 1)
            })
            .OrderByDescending(n => n.Total).ThenBy(n => n.Name, StringComparer.Ordinal).ToList();
    }
}
