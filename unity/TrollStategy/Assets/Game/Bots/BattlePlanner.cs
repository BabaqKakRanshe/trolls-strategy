using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Picks the arena level, the squad and its gear and starts the battle through the same command the battle
    /// screen sends. It reads the arena window's offer (<see cref="GameSession.ArenaOffer"/>) as a player reads the
    /// window: how the colony's best squad compares, the stake and whether the prize fund pays a repeat. It cannot
    /// see the outcome in advance. It climbs while the squad is at least even; a weaker squad first grows, and with
    /// a full squad tries anyway once in a while, so the climb never stalls. For gold it repeats a won level only
    /// when the fund pays and the squad is stronger.
    /// </summary>
    internal sealed class BattlePlanner
    {
        // a full squad that is still weaker tries the climb again after this much colony time
        private const int PatienceMs = 180000;

        private readonly BotHands _hands;
        private readonly BotRun _run;
        private int _squadTrolls;
        private int _climbedAtMs = int.MinValue / 2;

        /// <summary>The last battle was lost: the squad needs to grow stronger before it climbs on.</summary>
        public bool LostLast { get; private set; }

        /// <summary>The level to climb is out of the squad's reach for now: the window says it is weaker.</summary>
        public bool NeedsStrength { get; private set; }

        public BattlePlanner(BotHands hands, BotRun run, BotProfile profile)
        {
            _hands = hands;
            _run = run;
            _squadTrolls = Math.Max(1, profile.SquadTrolls);
        }

        /// <summary>The top of the open ladder: a win there opens the next level; null while none is open.</summary>
        public BattleMissionDefinition OpenMission()
        {
            BattleMissionDefinition top = null;
            foreach (var mission in _hands.Session.ArenaLadder())
                if (_hands.Session.IsMissionUnlocked(mission.MissionId)) top = mission;
            return top;
        }

        /// <summary>
        /// Climbs the ladder once when the top level is ready, the stake is in the treasury and the squad may go.
        /// With <paramref name="hire"/> it hires the trolls the squad still lacks. Returns true when a battle was fought.
        /// </summary>
        public bool TryFight(BotWait wait, bool hire)
        {
            var mission = OpenMission();
            if (mission == null)
            {
                wait?.Note("бой ещё не открыт");
                return false;
            }
            var offer = _hands.Session.ArenaOffer(mission);
            NeedsStrength = offer.Odds == OddsGrade.Weaker;
            if (NeedsStrength) Grow(mission);
            if (!Ready(mission, offer, wait) || !Squad(mission, wait, hire)) return false;

            offer = _hands.Session.ArenaOffer(mission);
            if (offer.Odds == OddsGrade.Weaker)
            {
                // a weaker squad waits for strength; a full one tries now and then, as a player would
                bool full = _hands.Snapshot.Units.Count >= _hands.Session.SquadLimit(mission);
                if (!full || _hands.Session.ActiveTimeMs - _climbedAtMs < PatienceMs)
                {
                    wait?.Note($"бой: отряд слабее врагов {mission.Level}-го уровня");
                    return false;
                }
            }
            _climbedAtMs = _hands.Session.ActiveTimeMs;
            return Fight(mission, offer);
        }

        /// <summary>
        /// Fights for gold: climbs while the squad is at least even (a first win pays in full), else repeats the
        /// highest won level that the prize fund pays and the squad beats. Returns true when a battle was fought.
        /// </summary>
        public bool TryFightForGold(BotWait wait)
        {
            var top = OpenMission();
            if (top == null) return false;
            var climb = _hands.Session.ArenaOffer(top);
            if (climb.Odds != OddsGrade.Weaker && _hands.Session.CanEnterMission(top.MissionId).Ok)
                return TryFight(wait, hire: true);

            BattleMissionDefinition farm = null;
            foreach (var mission in _hands.Session.ArenaLadder())
            {
                if (_hands.Session.MissionWins(mission.MissionId) == 0 || !_hands.Session.CanEnterMission(mission.MissionId).Ok)
                    continue;
                var offer = _hands.Session.ArenaOffer(mission);
                if (offer.PaysFromFund && offer.Odds == OddsGrade.Stronger) farm = mission;
            }
            if (farm == null) return false;
            return Squad(farm, wait, hire: false) && Fight(farm, _hands.Session.ArenaOffer(farm));
        }

        // the level rested and the stake in the treasury
        private bool Ready(BattleMissionDefinition mission, ArenaOfferSnapshot offer, BotWait wait)
        {
            if (offer.GoldShort > 0)
            {
                wait?.NeedGold(offer.Stake, "ставка на арене");
                return false;
            }
            var open = _hands.Session.CanEnterMission(mission.MissionId);
            if (open.Ok) return true;
            wait?.NeedTime($"бой: {open.Error}");
            return false;
        }

        // a weaker squad takes one more troll, as far as the level lets it
        private void Grow(BattleMissionDefinition mission) =>
            _squadTrolls = Math.Min(_hands.Session.SquadLimit(mission), _squadTrolls + 1);

        // enough trolls for the squad, hired when allowed
        private bool Squad(BattleMissionDefinition mission, BotWait wait, bool hire)
        {
            int need = Math.Min(_squadTrolls, _hands.Session.SquadLimit(mission));
            int trolls = _hands.CountUnits(UnitKind.Troll);
            if (trolls >= need) return true;
            if (!hire || !_hands.IsUnlocked(UnitKind.Troll))
            {
                // without trolls to hire the colony fights with whom it has
                if (_hands.Snapshot.Units.Count > 0 && !_hands.IsUnlocked(UnitKind.Troll)) return true;
                wait?.Note($"для боя нужно троллей: {need}");
                return false;
            }
            _hands.Hire(UnitKind.Troll, need - trolls, wait, $"тролли для боя ({need})");
            // the hire may have spent the stake: check it again
            return _hands.CountUnits(UnitKind.Troll) >= need && _hands.Session.ArenaOffer(mission).GoldShort == 0;
        }

        private bool Fight(BattleMissionDefinition mission, ArenaOfferSnapshot offer)
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

            int fundBefore = _hands.Session.ArenaFund.Payouts;
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
                Gold = battle?.AwardedGold ?? 0,
                FirstWin = offer.FirstWin,
                PaidFromFund = _hands.Session.ArenaFund.Payouts < fundBefore,
                Stake = battle?.BurnedStake ?? 0,
                Odds = offer.Odds.ToString(),
                ClosedLevel = battle?.ClosedMissionId != null ? mission.Level : 0
            };
            _run.Battles.Add(record);
            LostLast = record.Outcome != BattleOutcome.PlayerVictory;
            if (record.Outcome == BattleOutcome.EnemyVictory) Grow(mission);
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
