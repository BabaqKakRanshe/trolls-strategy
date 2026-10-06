using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// One campaign game a look at a time. <see cref="CampaignBot.Run"/> plays it on the session's own clock; a
    /// driver whose colony runs on someone else's clock (the game scene's frames) calls <see cref="Look"/> once
    /// the colony time reaches <see cref="NextLookMs"/>. Either way the bot only reads snapshots and sends commands.
    /// </summary>
    public sealed class BotPlay
    {
        private readonly GameSession _session;
        private readonly BotProfile _profile;
        private readonly int _goal, _limitMs, _stallMs, _thinkMs;
        private readonly BotHands _hands;
        private readonly QuestPlanner _planner;
        private readonly EconomyKeeper _keeper;
        private readonly List<UnitKind> _hireable;
        private QuestRecord _record;
        private string _progressKey;
        private int _progressAt, _bestGold, _nextSampleMs;
        private bool _finished;

        internal BotPlay(GameSession session, BotProfile profile, int goal, int limitMs, int stallMs)
        {
            _session = session;
            _profile = profile;
            _goal = goal;
            _limitMs = limitMs;
            _stallMs = stallMs;
            _thinkMs = (int)Math.Round(profile.ThinkSeconds * 1000f);
            Run = new BotRun(profile, goal);
            _hands = new BotHands(session, Run, profile);
            var battles = new BattlePlanner(_hands, Run, profile);
            _planner = new QuestPlanner(_hands, battles, profile);
            _keeper = new EconomyKeeper(_hands, battles, profile);
            _record = StartRecord();
            _hireable = Hireable();
        }

        public BotRun Run { get; }
        /// <summary>Colony time of the latest look.</summary>
        public int LookedAtMs { get; private set; }
        /// <summary>Colony seconds until the next look: the profile's think time plus this look's clicks and reading.</summary>
        public float WaitSeconds { get; private set; }
        /// <summary>The colony time the next look is due at.</summary>
        public int NextLookMs => LookedAtMs + (int)Math.Round(WaitSeconds * 1000f);

        /// <summary>
        /// Looks at the colony once: claims what is done, works on the quest and the economy. False once the game is
        /// over (the chain claimed, a stall or the time limit); <see cref="Finish"/> then closes the run.
        /// </summary>
        public bool Look()
        {
            if (_finished) return false;
            _hands.Refresh();
            Run.Decisions++;
            int commandsAtLook = Run.CommandsAccepted, questsAtLook = Run.Quests.Count;
            Settle();
            int now = _session.ActiveTimeMs;
            LookedAtMs = now;
            WaitSeconds = 0f;

            while (_hands.Snapshot.Progress.Quest is { IsComplete: true } done && done.Level <= _goal)
            {
                int questGold = done.Rewards.Where(r => r.Reward.Kind == QuestRewardKind.Gold).Sum(r => r.Reward.Gold);
                if (!_hands.Dispatch(new ClaimQuestRewardCommand())) break;
                Run.QuestGold += questGold;
                _record.DoneMs = now;
                _record.GoldAfterClaim = _hands.Gold;
                _record.Population = _hands.Snapshot.Units.Count;
                _record.Buildings = _hands.Snapshot.Buildings.Count;
                Run.Quests.Add(_record);
                _record = StartRecord();
            }

            NoteUnlocks();
            if (now >= _nextSampleMs)
            {
                Sample();
                _nextSampleMs = now + 60000;
            }
            var quest = _hands.Snapshot.Progress.Quest;
            if (quest == null || _hands.Snapshot.Progress.Level > _goal)
            {
                Run.Outcome = BotOutcome.Completed;
                return Stop();
            }

            var wait = _planner.Pursue(quest);
            _keeper.Keep(wait);
            if (_profile.Grows) _keeper.Grow(wait);
            if (_profile.FightsForGold) _keeper.FightForGold(wait);
            _hands.Refresh();

            // a stall is a quest whose goals do not move while the treasury does not grow toward its step
            string key = $"{quest.Level}:{string.Join(",", _hands.Snapshot.Progress.Quest?.Goals.Select(g => g.Current) ?? Array.Empty<int>())}";
            if (key != _progressKey || (wait.Kind == BotWaitKind.Gold && _hands.Gold > _bestGold))
            {
                if (key != _progressKey) _bestGold = 0;
                _progressKey = key;
                _progressAt = now;
                _bestGold = Math.Max(_bestGold, _hands.Gold);
            }
            else if (now - _progressAt >= _stallMs)
            {
                Run.Outcome = BotOutcome.Stalled;
                Run.StopReason = $"«{quest.Title}» (уровень {quest.Level}): {wait.Reason ?? "цели не двигаются"}";
                return Stop();
            }
            if (now >= _limitMs)
            {
                Run.Outcome = BotOutcome.TimeLimit;
                Run.StopReason = $"«{quest.Title}» (уровень {quest.Level}): {wait.Reason ?? "не успел"}";
                return Stop();
            }

            switch (wait.Kind)
            {
                case BotWaitKind.Gold: _record.GoldWaitMs += _thinkMs; break;
                case BotWaitKind.Time: _record.TimeWaitMs += _thinkMs; break;
                default: _record.FlowWaitMs += _thinkMs; break;
            }

            Settle();
            // the player's clicks take time: the colony runs on and the next look comes later
            float busySeconds = (Run.CommandsAccepted - commandsAtLook) * _profile.ActionSeconds +
                                (Run.Quests.Count - questsAtLook) * _profile.QuestReadSeconds;
            _record.BusyMs += (int)Math.Round(busySeconds * 1000f);
            WaitSeconds = _profile.ThinkSeconds + busySeconds;
            return true;
        }

        private bool Stop()
        {
            _finished = true;
            return false;
        }

        /// <summary>Closes the run: the colony at the end and a last sample.</summary>
        public BotRun Finish()
        {
            _hands.Refresh();
            Run.EndMs = _session.ActiveTimeMs;
            Run.FinalGold = _hands.Gold;
            Run.SalesGold = _session.SalesGold;
            Run.FinalPopulation = _hands.Snapshot.Units.Count;
            Run.FinalBuildings = _hands.Snapshot.Buildings.Count;
            Run.ArenaLevel = _hands.ArenaLevel();
            Run.Units.Clear();
            foreach (var group in _hands.Snapshot.Units.GroupBy(u => u.UnitKind))
                Run.Units[_hands.Catalog.GetUnit(group.Key).DisplayName] = group.Count();
            Run.Buildings.Clear();
            // by kind: a building's own name carries its number ("Шахта 2"), so copies would never add up
            foreach (var group in _hands.Snapshot.Buildings.GroupBy(b => b.Kind))
                Run.Buildings[_hands.Catalog.GetBuilding(group.Key).DisplayName] = group.Count();
            Run.Upgrades.Clear();
            foreach (var upgrade in _hands.Snapshot.Upgrades.Where(u => u.Level > 0))
                Run.Upgrades[upgrade.Name] = upgrade.Level;
            Sample();
            return Run;
        }

        private List<UnitKind> Hireable() => _hands.Catalog.Units
            .Where(u => u != null && u.Hireable && _hands.IsUnlocked(u.Kind)).Select(u => u.Kind).ToList();

        // Creatures that became hireable since the last look: the folk join after their arena level is won.
        private void NoteUnlocks()
        {
            foreach (var kind in Hireable())
            {
                if (_hireable.Contains(kind)) continue;
                _hireable.Add(kind);
                Run.Unlocks.Add(new UnlockRecord
                {
                    Name = _hands.Catalog.GetUnit(kind).DisplayName,
                    AtMs = _session.ActiveTimeMs,
                    QuestLevel = _hands.Snapshot.Progress.Level
                });
            }
        }

        private QuestRecord StartRecord()
        {
            var quest = _hands.Snapshot.Progress.Quest;
            return new QuestRecord
            {
                Level = quest?.Level ?? _hands.Snapshot.Progress.Level,
                Id = quest?.Id,
                Title = quest?.Title,
                StartMs = _session.ActiveTimeMs
            };
        }

        // A finished battle is closed and its gold taken before anything else, as the HUD makes the player do.
        private void Settle()
        {
            if (_hands.Session.ActiveBattle != null) _hands.Dispatch(new AcknowledgeBattleCommand());
            if (_hands.Snapshot.BattleReward != null) _hands.Dispatch(new ClaimBattleRewardCommand());
        }

        private void Sample()
        {
            var land = _hands.Snapshot.Land;
            Run.Samples.Add(new BotSample
            {
                AtMs = _session.ActiveTimeMs,
                QuestLevel = _hands.Snapshot.Progress.Level,
                Gold = _hands.Gold,
                SoldGoods = _hands.Snapshot.SoldGoods,
                Population = _hands.Snapshot.Units.Count,
                Buildings = _hands.Snapshot.Buildings.Count,
                LandBlocks = land?.Blocks.Count(b => b.Owned) ?? 0,
                ArenaLevel = _hands.ArenaLevel()
            });
        }
    }
}
