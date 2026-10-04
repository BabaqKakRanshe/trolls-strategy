using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    public enum BattleOutcome { PlayerVictory, EnemyVictory, Draw }
    public enum BattleEventKind { Move, Attack, Death }

    public readonly struct BattleFighterInput
    {
        public string Id { get; }
        public UnitKind Kind { get; }
        public bool IsPlayer { get; }
        public Cell Cell { get; }
        public int Health { get; }
        public int Damage { get; }
        public int Armor { get; }
        public int AttackIntervalMs { get; }
        public int AttackRange { get; }
        public int StepIntervalMs { get; }

        public BattleFighterInput(string id, UnitKind kind, bool isPlayer, Cell cell,
            int health, int damage, int armor, int attackIntervalMs, int attackRange, int stepIntervalMs)
        {
            Id = id;
            Kind = kind;
            IsPlayer = isPlayer;
            Cell = cell;
            Health = health;
            Damage = damage;
            Armor = armor;
            AttackIntervalMs = attackIntervalMs;
            AttackRange = attackRange;
            StepIntervalMs = stepIntervalMs;
        }
    }

    public readonly struct BattleEvent
    {
        public BattleEventKind Kind { get; }
        public int TimeMs { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public Cell Cell { get; }
        public int Damage { get; }

        public BattleEvent(BattleEventKind kind, int timeMs, string actorId, string targetId, Cell cell, int damage)
        {
            Kind = kind;
            TimeMs = timeMs;
            ActorId = actorId;
            TargetId = targetId;
            Cell = cell;
            Damage = damage;
        }
    }

    public sealed class BattleReport
    {
        private readonly BattleFighterInput[] _fighters;
        private readonly BattleEvent[] _events;
        private readonly string[] _survivors;

        public BattleOutcome Outcome { get; }
        public int DurationMs { get; }
        public int Seed { get; }
        public IReadOnlyList<BattleFighterInput> Fighters => _fighters;
        public IReadOnlyList<BattleEvent> Events => _events;
        public IReadOnlyList<string> Survivors => _survivors;

        internal BattleReport(BattleOutcome outcome, int durationMs, int seed,
            BattleFighterInput[] fighters, List<BattleEvent> events, List<string> survivors)
        {
            Outcome = outcome;
            DurationMs = durationMs;
            Seed = seed;
            _fighters = fighters;
            _events = events.ToArray();
            _survivors = survivors.ToArray();
        }

        /// <summary>
        /// The share of one side's health the battle took, 0…1: each fighter's damage taken, up to its health,
        /// over the side's health at the start. A draw pays this share of the enemies' health.
        /// </summary>
        public double DefeatedShare(bool enemies)
        {
            var health = new Dictionary<string, int>(StringComparer.Ordinal);
            long total = 0;
            foreach (var fighter in _fighters)
            {
                if (fighter.IsPlayer == enemies) continue;
                health[fighter.Id] = fighter.Health;
                total += fighter.Health;
            }
            if (total <= 0) return 0;
            var taken = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var battleEvent in _events)
            {
                if (battleEvent.Kind != BattleEventKind.Attack || battleEvent.TargetId == null ||
                    !health.ContainsKey(battleEvent.TargetId)) continue;
                taken.TryGetValue(battleEvent.TargetId, out int sum);
                taken[battleEvent.TargetId] = sum + battleEvent.Damage;
            }
            long defeated = 0;
            foreach (var pair in taken) defeated += Math.Min(pair.Value, health[pair.Key]);
            return (double)defeated / total;
        }
    }

    public sealed class BattleRunState
    {
        private readonly string[] _fallenUnitIds;
        public string MissionId { get; }
        public BattleReport Report { get; }
        public int AwardedGold { get; }
        public IReadOnlyList<string> FallenUnitIds => _fallenUnitIds;
        /// <summary>The stake a lost or drawn battle burned; 0 after a win.</summary>
        public int BurnedStake { get; }
        /// <summary>The level a defeat at the top of the ladder closed again, or null.</summary>
        public string ClosedMissionId { get; }
        /// <summary>Active time the level rests after this battle.</summary>
        public int RestMs { get; }

        public BattleRunState(string missionId, BattleReport report, int awardedGold,
            IReadOnlyList<string> fallenUnitIds, int burnedStake = 0, string closedMissionId = null, int restMs = 0)
        {
            MissionId = missionId;
            Report = report;
            AwardedGold = awardedGold;
            BurnedStake = burnedStake;
            ClosedMissionId = closedMissionId;
            RestMs = restMs;
            _fallenUnitIds = new string[fallenUnitIds.Count];
            for (int i = 0; i < fallenUnitIds.Count; i++) _fallenUnitIds[i] = fallenUnitIds[i];
        }
    }

    public static class BattleSimulation
    {
        public const int StepMs = 100;
        public const int MaxTimeMs = 90000;

        private sealed class Fighter
        {
            public BattleFighterInput Input;
            public Cell Cell;
            public int Health;
            public int AttackCooldown;
            public int MoveCooldown;
        }

        public static BattleReport Run(BattleBoard board, IReadOnlyList<BattleFighterInput> inputs, int seed)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (inputs == null || inputs.Count == 0) throw new ArgumentException("Нет бойцов", nameof(inputs));

            var fighters = new List<Fighter>(inputs.Count);
            var initial = new BattleFighterInput[inputs.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<Cell>();
            bool hasPlayer = false, hasEnemy = false;
            for (int i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                if (string.IsNullOrEmpty(input.Id) || !ids.Add(input.Id) || !board.IsWalkable(input.Cell) ||
                    !cells.Add(input.Cell) || input.Health <= 0 || input.Damage <= 0 ||
                    input.AttackIntervalMs < StepMs || input.AttackRange < 1 || input.StepIntervalMs < StepMs)
                    throw new ArgumentException("Неверный состав или расстановка боя", nameof(inputs));
                hasPlayer |= input.IsPlayer;
                hasEnemy |= !input.IsPlayer;
                initial[i] = input;
                fighters.Add(new Fighter { Input = input, Cell = input.Cell, Health = input.Health });
            }
            if (!hasPlayer || !hasEnemy) throw new ArgumentException("Нужны обе стороны боя", nameof(inputs));
            fighters.Sort((a, b) => string.CompareOrdinal(a.Input.Id, b.Input.Id));

            var events = new List<BattleEvent>();
            var occupied = new HashSet<Cell>();
            for (int time = 0; time < MaxTimeMs; time += StepMs)
            {
                occupied.Clear();
                foreach (var fighter in fighters)
                {
                    if (fighter.Health <= 0) continue;
                    occupied.Add(fighter.Cell);
                    fighter.AttackCooldown = Math.Max(0, fighter.AttackCooldown - StepMs);
                    fighter.MoveCooldown = Math.Max(0, fighter.MoveCooldown - StepMs);
                }

                var pendingDamage = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var fighter in fighters)
                {
                    if (fighter.Health <= 0) continue;
                    var targets = FindTargets(fighter, fighters);
                    if (targets.Count == 0) continue;
                    var target = targets[0];
                    int distance = BattleBoard.HexDistance(fighter.Cell, target.Cell);
                    if (distance <= fighter.Input.AttackRange)
                    {
                        if (fighter.AttackCooldown != 0) continue;
                        int damage = Math.Max(1, fighter.Input.Damage - target.Input.Armor);
                        pendingDamage.TryGetValue(target.Input.Id, out int total);
                        pendingDamage[target.Input.Id] = total + damage;
                        events.Add(new BattleEvent(BattleEventKind.Attack, time, fighter.Input.Id,
                            target.Input.Id, target.Cell, damage));
                        fighter.AttackCooldown = fighter.Input.AttackIntervalMs;
                    }
                    else if (fighter.MoveCooldown == 0)
                    {
                        occupied.Remove(fighter.Cell);
                        foreach (var candidate in targets)
                        {
                            if (!TryNextStep(board, fighter.Cell, candidate.Cell,
                                fighter.Input.AttackRange, occupied, out var next)) continue;
                            fighter.Cell = next;
                            fighter.MoveCooldown = fighter.Input.StepIntervalMs;
                            events.Add(new BattleEvent(BattleEventKind.Move, time, fighter.Input.Id,
                                null, next, 0));
                            break;
                        }
                        occupied.Add(fighter.Cell);
                    }
                }

                foreach (var fighter in fighters)
                {
                    if (fighter.Health <= 0 || !pendingDamage.TryGetValue(fighter.Input.Id, out int damage)) continue;
                    fighter.Health = Math.Max(0, fighter.Health - damage);
                    if (fighter.Health == 0)
                        events.Add(new BattleEvent(BattleEventKind.Death, time, fighter.Input.Id,
                            null, fighter.Cell, 0));
                }

                bool playerAlive = false, enemyAlive = false;
                foreach (var fighter in fighters)
                {
                    if (fighter.Health <= 0) continue;
                    if (fighter.Input.IsPlayer) playerAlive = true;
                    else enemyAlive = true;
                }
                if (!playerAlive || !enemyAlive)
                    return BuildReport(!playerAlive && !enemyAlive ? BattleOutcome.Draw :
                        playerAlive ? BattleOutcome.PlayerVictory : BattleOutcome.EnemyVictory,
                        time + StepMs, seed, initial, events, fighters);
            }
            return BuildReport(BattleOutcome.Draw, MaxTimeMs, seed, initial, events, fighters);
        }

        private static List<Fighter> FindTargets(Fighter fighter, List<Fighter> all)
        {
            var targets = new List<Fighter>();
            foreach (var candidate in all)
                if (candidate.Health > 0 && candidate.Input.IsPlayer != fighter.Input.IsPlayer)
                    targets.Add(candidate);
            targets.Sort((a, b) =>
            {
                int distance = BattleBoard.HexDistance(fighter.Cell, a.Cell)
                    .CompareTo(BattleBoard.HexDistance(fighter.Cell, b.Cell));
                if (distance != 0) return distance;
                int health = a.Health.CompareTo(b.Health);
                return health != 0 ? health : string.CompareOrdinal(a.Input.Id, b.Input.Id);
            });
            return targets;
        }

        private static bool TryNextStep(BattleBoard board, Cell start, Cell target, int range,
            HashSet<Cell> occupied, out Cell step)
        {
            step = start;
            var queue = new Queue<Cell>();
            var previous = new Dictionary<Cell, Cell> { [start] = start };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current != start && BattleBoard.HexDistance(current, target) <= range)
                {
                    var at = current;
                    while (previous[at] != start) at = previous[at];
                    step = at;
                    return true;
                }
                foreach (var next in board.Neighbours(current))
                {
                    if (occupied.Contains(next) || previous.ContainsKey(next)) continue;
                    previous.Add(next, current);
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private static BattleReport BuildReport(BattleOutcome outcome, int durationMs, int seed,
            BattleFighterInput[] initial, List<BattleEvent> events, List<Fighter> fighters)
        {
            var survivors = new List<string>();
            foreach (var fighter in fighters)
                if (fighter.Health > 0) survivors.Add(fighter.Input.Id);
            return new BattleReport(outcome, durationMs, seed, initial, events, survivors);
        }
    }
}
