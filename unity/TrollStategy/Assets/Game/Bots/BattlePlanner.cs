using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Picks the squad and its gear and starts the mission through the same command the battle screen sends.
    /// It cannot see the outcome in advance: it waits for enough trolls, and every lost battle makes it wait
    /// for one more.
    /// </summary>
    internal sealed class BattlePlanner
    {
        private readonly BotHands _hands;
        private readonly BotRun _run;
        private int _squadTrolls;
        // the highest arena level worth trying: a loss brings it under the lost level, eight wins in a row lift it
        private int _ceiling = int.MaxValue;
        private int _winsInRow;

        /// <summary>The last battle was lost: the squad needs to grow stronger before it climbs on.</summary>
        public bool LostLast { get; private set; }

        public BattlePlanner(BotHands hands, BotRun run, BotProfile profile)
        {
            _hands = hands;
            _run = run;
            _squadTrolls = Math.Max(1, profile.SquadTrolls);
        }

        /// <summary>The highest arena level the colony may enter, ready ones first; null while none is open.</summary>
        public BattleMissionDefinition OpenMission()
        {
            var session = _hands.Session;
            BattleMissionDefinition ready = null, open = null;
            foreach (var mission in session.ArenaLadder())
            {
                if (!session.IsMissionUnlocked(mission.MissionId) || mission.Level > _ceiling) continue;
                open = mission;
                if (session.CanEnterMission(mission.MissionId).Ok) ready = mission;
            }
            return ready ?? open;
        }

        /// <summary>
        /// Fights once when the mission is ready and the squad is strong enough. With <paramref name="hire"/> it
        /// hires the trolls the squad still lacks. Returns true when a battle was fought.
        /// </summary>
        public bool TryFight(BotWait wait, bool hire)
        {
            var mission = OpenMission();
            if (mission == null)
            {
                wait?.Note("бой ещё не открыт");
                return false;
            }
            var open = _hands.Session.CanEnterMission(mission.MissionId);
            if (!open.Ok)
            {
                wait?.NeedTime($"бой: {open.Error}");
                return false;
            }

            int limit = _hands.Session.SquadLimit(mission);
            int need = Math.Min(_squadTrolls, limit);
            int trolls = _hands.CountUnits(UnitKind.Troll);
            if (trolls < need)
            {
                if (!hire || !_hands.IsUnlocked(UnitKind.Troll))
                {
                    wait?.Note($"для боя нужно троллей: {need}");
                    return false;
                }
                _hands.Hire(UnitKind.Troll, need - trolls, wait, $"тролли для боя ({need})");
                if (_hands.CountUnits(UnitKind.Troll) < need) return false;
            }
            return Fight(mission);
        }

        private bool Fight(BattleMissionDefinition mission)
        {
            var board = mission.CreateBoard();
            // the strongest creatures; of equal ones the free first, haulers next, workers last (a fallen worker
            // stops a building)
            var squad = _hands.Snapshot.Units
                .OrderByDescending(u => Strength(u.UnitKind))
                .ThenBy(u => u.Assignment.Kind == AssignmentKind.Idle ? 0 : u.Assignment.Kind == AssignmentKind.Haul ? 1 : 2)
                .ThenBy(u => u.Id)
                .Take(_hands.Session.SquadLimit(mission))
                .ToList();
            if (squad.Count == 0) return false;

            // melee to the front column, the rest behind; within a column from the middle row out
            int middle = board.Height / 2;
            var cells = board.DeploymentCells
                .OrderByDescending(c => c.X).ThenBy(c => Math.Abs(c.Y - middle)).ThenBy(c => c.Y).ToList();
            var ordered = squad.OrderBy(u => _hands.Catalog.GetUnit(u.UnitKind).AttackRange).ToList();
            var placements = new List<BattlePlacement>(ordered.Count);
            for (int i = 0; i < ordered.Count && i < cells.Count; i++)
                placements.Add(new BattlePlacement(ordered[i].Id, cells[i]));

            var command = new StartBattleCommand(mission.MissionId, placements,
                BotGear.Deal(squad.Select(u => u.Id).ToList(), _hands.Snapshot.Equipment));
            if (!_hands.Dispatch(command)) return false;

            var battle = _hands.Session.ActiveBattle;
            var record = new BattleRecord
            {
                AtMs = _hands.Session.ActiveTimeMs,
                QuestLevel = _hands.Snapshot.Progress.Level,
                ArenaLevel = mission.Level,
                Squad = string.Join(", ", squad.GroupBy(u => u.UnitKind)
                    .Select(g => $"{_hands.Catalog.GetUnit(g.Key).DisplayName}×{g.Count()}")),
                Outcome = battle?.Report.Outcome ?? BattleOutcome.Draw,
                Fallen = battle?.FallenUnitIds.Count ?? 0,
                Gold = battle?.AwardedGold ?? 0
            };
            _run.Battles.Add(record);
            LostLast = record.Outcome != BattleOutcome.PlayerVictory;
            if (record.Outcome != BattleOutcome.PlayerVictory)
            {
                _squadTrolls = Math.Min(_hands.Session.SquadLimit(mission), _squadTrolls + 1);
                _ceiling = Math.Max(1, mission.Level - 1);
                _winsInRow = 0;
            }
            else if (++_winsInRow >= 8 && _ceiling != int.MaxValue)
            {
                _ceiling++;
                _winsInRow = 0;
            }
            _hands.Dispatch(new AcknowledgeBattleCommand());
            if (_hands.Snapshot.BattleReward != null) _hands.Dispatch(new ClaimBattleRewardCommand());
            return true;
        }

        private int Strength(UnitKind kind)
        {
            var unit = _hands.Catalog.GetUnit(kind);
            return unit.CombatHealth * unit.CombatDamage;
        }
    }
}
