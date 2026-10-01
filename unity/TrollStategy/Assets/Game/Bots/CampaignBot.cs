using System;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Plays a campaign session to the end of the authored quest chain the way a player at the HUD would:
    /// it looks at the snapshot every <see cref="BotProfile.ThinkSeconds"/> of colony time, sends commands,
    /// and lets the session's fixed simulation steps run in between. Nothing here decides an outcome: the
    /// session validates every command, so a run measures the game's rules and numbers as they ship.
    /// </summary>
    public sealed class CampaignBot
    {
        private readonly GameSession _session;
        private readonly BotProfile _profile;
        private readonly int _limitMs;
        private readonly int _stallMs;

        /// <param name="maxColonyMinutes">Stops with <see cref="BotOutcome.TimeLimit"/> after this much colony time.</param>
        /// <param name="stallMinutes">Stops with <see cref="BotOutcome.Stalled"/> when no goal moves this long.</param>
        public CampaignBot(GameSession session, BotProfile profile, float maxColonyMinutes = 240f,
            float stallMinutes = 30f)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (!session.IsCampaign) throw new ArgumentException("Бот играет кампанию: нужна сессия с заданиями");
            if (profile.ThinkSeconds <= 0f) throw new ArgumentException("ThinkSeconds должен быть больше нуля");
            _limitMs = (int)Math.Round(maxColonyMinutes * 60000f);
            _stallMs = (int)Math.Round(stallMinutes * 60000f);
        }

        /// <summary>Stops after this quest level is claimed; 0 plays the whole authored chain.</summary>
        public int StopAfterLevel { get; set; }

        public BotRun Run()
        {
            int chain = _session.Catalog.Progression.Quests.Count;
            int goal = StopAfterLevel > 0 ? Math.Min(StopAfterLevel, chain) : chain;
            var run = new BotRun(_profile, goal);
            var hands = new BotHands(_session, run);
            var battles = new BattlePlanner(hands, run, _profile);
            var planner = new QuestPlanner(hands, battles, _profile);
            var keeper = new EconomyKeeper(hands, battles, _profile);
            int thinkMs = (int)Math.Round(_profile.ThinkSeconds * 1000f);

            var record = StartRecord(hands);
            string progressKey = null;
            int progressAt = 0, bestGold = 0, nextSampleMs = 0;

            while (true)
            {
                hands.Refresh();
                run.Decisions++;
                Settle(hands);
                int now = _session.ActiveTimeMs;

                while (hands.Snapshot.Progress.Quest is { IsComplete: true } done && done.Level <= goal)
                {
                    if (!hands.Dispatch(new ClaimQuestRewardCommand())) break;
                    record.DoneMs = now;
                    record.GoldAfterClaim = hands.Gold;
                    record.Population = hands.Snapshot.Units.Count;
                    record.Buildings = hands.Snapshot.Buildings.Count;
                    run.Quests.Add(record);
                    record = StartRecord(hands);
                }

                if (now >= nextSampleMs)
                {
                    Sample(run, hands);
                    nextSampleMs = now + 60000;
                }
                var quest = hands.Snapshot.Progress.Quest;
                if (quest == null || hands.Snapshot.Progress.Level > goal)
                {
                    run.Outcome = BotOutcome.Completed;
                    break;
                }

                var wait = planner.Pursue(quest);
                keeper.Keep(wait);
                if (_profile.Grows) keeper.Grow(wait);
                if (_profile.FightsForGold) keeper.FightForGold(wait);
                hands.Refresh();

                // a stall is a quest whose goals do not move while the treasury does not grow toward its step
                string key = $"{quest.Level}:{string.Join(",", hands.Snapshot.Progress.Quest?.Goals.Select(g => g.Current) ?? Array.Empty<int>())}";
                if (key != progressKey || (wait.Kind == BotWaitKind.Gold && hands.Gold > bestGold))
                {
                    if (key != progressKey) bestGold = 0;
                    progressKey = key;
                    progressAt = now;
                    bestGold = Math.Max(bestGold, hands.Gold);
                }
                else if (now - progressAt >= _stallMs)
                {
                    run.Outcome = BotOutcome.Stalled;
                    run.StopReason = $"«{quest.Title}» (уровень {quest.Level}): {wait.Reason ?? "цели не двигаются"}";
                    break;
                }
                if (now >= _limitMs)
                {
                    run.Outcome = BotOutcome.TimeLimit;
                    run.StopReason = $"«{quest.Title}» (уровень {quest.Level}): {wait.Reason ?? "не успел"}";
                    break;
                }

                switch (wait.Kind)
                {
                    case BotWaitKind.Gold: record.GoldWaitMs += thinkMs; break;
                    case BotWaitKind.Time: record.TimeWaitMs += thinkMs; break;
                    default: record.FlowWaitMs += thinkMs; break;
                }

                Settle(hands);
                _session.Advance(_profile.ThinkSeconds);
                if (_session.ActiveTimeMs == now)
                    throw new InvalidOperationException($"Колония не идёт: {SessionDigest.Describe(_session)}");
            }

            hands.Refresh();
            run.EndMs = _session.ActiveTimeMs;
            run.FinalGold = hands.Gold;
            run.FinalPopulation = hands.Snapshot.Units.Count;
            run.FinalBuildings = hands.Snapshot.Buildings.Count;
            Sample(run, hands);
            return run;
        }

        private QuestRecord StartRecord(BotHands hands)
        {
            var quest = hands.Snapshot.Progress.Quest;
            return new QuestRecord
            {
                Level = quest?.Level ?? hands.Snapshot.Progress.Level,
                Id = quest?.Id,
                Title = quest?.Title,
                StartMs = _session.ActiveTimeMs
            };
        }

        // A finished battle is closed and its gold taken before anything else, as the HUD makes the player do.
        private static void Settle(BotHands hands)
        {
            if (hands.Session.ActiveBattle != null) hands.Dispatch(new AcknowledgeBattleCommand());
            if (hands.Snapshot.BattleReward != null) hands.Dispatch(new ClaimBattleRewardCommand());
        }

        private void Sample(BotRun run, BotHands hands)
        {
            var land = hands.Snapshot.Land;
            run.Samples.Add(new BotSample
            {
                AtMs = _session.ActiveTimeMs,
                QuestLevel = hands.Snapshot.Progress.Level,
                Gold = hands.Gold,
                SoldGoods = hands.Snapshot.SoldGoods,
                Population = hands.Snapshot.Units.Count,
                Buildings = hands.Snapshot.Buildings.Count,
                LandBlocks = land?.Blocks.Count(b => b.Owned) ?? 0
            });
        }
    }
}
