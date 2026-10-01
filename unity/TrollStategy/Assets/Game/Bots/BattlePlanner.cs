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

        public BattlePlanner(BotHands hands, BotRun run, BotProfile profile)
        {
            _hands = hands;
            _run = run;
            _squadTrolls = Math.Max(1, profile.SquadTrolls);
        }

        /// <summary>The first mission the colony may enter; null while none is open.</summary>
        public BattleMissionDefinition OpenMission()
        {
            foreach (var mission in _hands.Catalog.Missions)
                if (mission != null && _hands.Snapshot.Progress.IsMissionUnlocked(mission.MissionId))
                    return mission;
            return null;
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

            int need = Math.Min(_squadTrolls, mission.MaxPlayerUnits);
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
            var squad = _hands.Snapshot.Units
                .Where(u => u.UnitKind == UnitKind.Troll)
                .Take(mission.MaxPlayerUnits)
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

            var command = new StartBattleCommand(mission.MissionId, placements, Gear(squad));
            if (!_hands.Dispatch(command)) return false;

            var battle = _hands.Session.ActiveBattle;
            var record = new BattleRecord
            {
                AtMs = _hands.Session.ActiveTimeMs,
                QuestLevel = _hands.Snapshot.Progress.Level,
                Squad = string.Join(", ", squad.GroupBy(u => u.UnitKind).Select(g => $"{g.Key}×{g.Count()}")),
                Outcome = battle?.Report.Outcome ?? BattleOutcome.Draw,
                Fallen = battle?.FallenUnitIds.Count ?? 0,
                Gold = battle?.AwardedGold ?? 0
            };
            _run.Battles.Add(record);
            if (record.Outcome != BattleOutcome.PlayerVictory)
                _squadTrolls = Math.Min(mission.MaxPlayerUnits, _squadTrolls + 1);
            _hands.Dispatch(new AcknowledgeBattleCommand());
            if (_hands.Snapshot.BattleReward != null) _hands.Dispatch(new ClaimBattleRewardCommand());
            return true;
        }

        // The best free item per slot to each fighter in squad order; items the squad wore before are re-dealt.
        private List<BattleEquipmentAssignment> Gear(List<UnitSnapshot> squad)
        {
            var members = new HashSet<string>(squad.Select(u => u.Id));
            var pool = _hands.Snapshot.Equipment
                .Where(e => e.OwnerUnitId == null || members.Contains(e.OwnerUnitId))
                .ToList();
            var owner = pool.ToDictionary(e => e.Id, _ => (string)null);
            foreach (var slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
            {
                var items = pool.Where(e => e.Slot == slot)
                    .OrderByDescending(e => e.DamageBonus + e.ArmorBonus).ThenBy(e => e.Id).ToList();
                for (int i = 0; i < items.Count && i < squad.Count; i++)
                    owner[items[i].Id] = squad[i].Id;
            }
            return pool.Select(e => new BattleEquipmentAssignment(e.Id, owner[e.Id])).ToList();
        }
    }
}
