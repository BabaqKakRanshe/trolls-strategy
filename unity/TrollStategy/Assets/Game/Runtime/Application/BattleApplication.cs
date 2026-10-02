using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public static class BattleApplication
    {
        public static CommandResult ValidateAvailability(GameState state, BattleMissionDefinition mission,
            bool debugBypassTime = false)
        {
            if (state == null || mission == null) return CommandResult.Fail("Миссия не найдена");
            if (state.ActiveBattle != null) return CommandResult.Fail("Сначала завершите текущий бой");
            if (debugBypassTime) return CommandResult.Success();
            if (!Progression.IsMissionUnlocked(state, mission.MissionId))
                return CommandResult.Fail(mission.Level > 1
                    ? "Сначала победите на предыдущем уровне арены"
                    : "Бой откроется по заданию");
            if (state.ActiveTimeMs < UnlockAtMs(mission)) return CommandResult.Fail("Миссия ещё не открыта");
            if (state.ActiveTimeMs < state.ReadyAtOf(mission.MissionId))
                return CommandResult.Fail("Арена восстанавливается после боя");
            return CommandResult.Success();
        }

        /// <summary>Active time left until the mission opens and has recovered from the last run.</summary>
        public static int WaitMs(GameState state, BattleMissionDefinition mission)
        {
            if (state == null || mission == null) return 0;
            int readyAt = Math.Max(UnlockAtMs(mission), state.ReadyAtOf(mission.MissionId));
            return Math.Max(0, readyAt - state.ActiveTimeMs);
        }

        /// <summary>Fighters the colony may send: the mission's number plus the barracks' upgrades, as the board allows.</summary>
        public static int SquadLimit(GameState state, BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            if (mission == null) return 0;
            int limit = mission.MaxPlayerUnits + UpgradeRules.Total(state, catalog, UpgradeEffect.SquadSize);
            return Math.Max(1, Math.Min(limit, mission.PlayerDeployment.Count > 0 ? mission.PlayerDeployment.Count : limit));
        }

        /// <summary>A win's gold range at this mission now: first or repeat win, with the barracks' glory.</summary>
        public static (int Min, int Max) WinGold(GameState state, BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            bool first = state.WinsOf(mission.MissionId) == 0;
            int percent = UpgradeRules.Total(state, catalog, UpgradeEffect.BattleRewardPercent);
            int min = first ? mission.FirstWinGold : mission.RepeatWinGold;
            int max = first ? mission.FirstWinGoldMax : mission.RepeatWinGoldMax;
            return ((int)Math.Round(UpgradeRules.Raise(min, percent)), (int)Math.Round(UpgradeRules.Raise(max, percent)));
        }

        /// <summary>Active seconds the mission rests after a run, after the barracks' rest.</summary>
        public static float CooldownSeconds(GameState state, BattleMissionDefinition mission, GameContentCatalog catalog) =>
            UpgradeRules.Shorten(mission.CooldownActiveSeconds,
                UpgradeRules.Total(state, catalog, UpgradeEffect.BattleCooldownPercent));

        /// <summary>The mission one level above this one on the arena ladder, or null at the top.</summary>
        public static BattleMissionDefinition NextMission(BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            if (mission == null) return null;
            foreach (var candidate in catalog.Missions)
                if (candidate != null && candidate.Level == mission.Level + 1) return candidate;
            return null;
        }

        private static int UnlockAtMs(BattleMissionDefinition mission) =>
            (int)Math.Ceiling(mission.UnlockAfterActiveSeconds * 1000f);

        public static CommandResult Start(GameState state, StartBattleCommand command,
            GameContentCatalog catalog, bool debugBypassTime = false)
        {
            if (state == null || command == null || catalog == null)
                return CommandResult.Fail("Неверные данные боя");
            BattleMissionDefinition mission = null;
            foreach (var candidate in catalog.Missions)
                if (candidate != null && candidate.MissionId == command.MissionId)
                    mission = candidate;
            var available = ValidateAvailability(state, mission, debugBypassTime);
            if (!available.Ok) return available;

            BattleBoard board;
            try { board = mission.CreateBoard(); }
            catch (Exception) { return CommandResult.Fail("Данные миссии некорректны"); }
            var placements = command.Placements;
            int squad = SquadLimit(state, mission, catalog);
            if (placements == null || placements.Count < 1 || placements.Count > squad)
                return CommandResult.Fail($"Нужно выбрать от 1 до {squad} бойцов");

            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            var selectedCells = new HashSet<Cell>();
            var selectedUnits = new List<UnitState>(placements.Count);
            foreach (var placement in placements)
            {
                if (string.IsNullOrEmpty(placement.UnitId) || !selectedIds.Add(placement.UnitId) ||
                    !selectedCells.Add(placement.Cell) || !board.CanPlace(placement.Cell))
                    return CommandResult.Fail("Недопустимая расстановка");
                var unit = state.Units.Find(u => u.Id == placement.UnitId);
                if (unit == null) return CommandResult.Fail("Боец не найден");
                selectedUnits.Add(unit);
            }

            if (command.Equipment != null)
            {
                var changed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var assignment in command.Equipment)
                {
                    if (string.IsNullOrEmpty(assignment.ItemId) || !changed.Add(assignment.ItemId))
                        return CommandResult.Fail("Предмет указан дважды");
                    var item = state.Equipment.Find(e => e.Id == assignment.ItemId);
                    if (item == null) return CommandResult.Fail("Предмет не найден");
                    if (assignment.OwnerUnitId != null &&
                        !selectedIds.Contains(assignment.OwnerUnitId))
                        return CommandResult.Fail("Снаряжение можно выдать только бойцу отряда");
                    item.OwnerUnitId = assignment.OwnerUnitId;
                }
            }
            var occupiedSlots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in state.Equipment)
            {
                EquipmentDefinition definition;
                try { definition = catalog.GetEquipment(item.DefinitionId); }
                catch (ArgumentOutOfRangeException)
                {
                    return CommandResult.Fail("Неизвестный тип снаряжения");
                }
                if (item.OwnerUnitId == null) continue;
                if (state.Units.Find(u => u.Id == item.OwnerUnitId) == null)
                    return CommandResult.Fail("Владелец предмета не найден");
                var slot = definition.Slot;
                if (!occupiedSlots.Add($"{item.OwnerUnitId}:{slot}"))
                    return CommandResult.Fail("У бойца уже занят слот снаряжения");
            }

            var fighters = new List<BattleFighterInput>(placements.Count + mission.Enemies.Count);
            int healthPercent = UpgradeRules.Total(state, catalog, UpgradeEffect.FighterHealthPercent);
            int damageBonus = UpgradeRules.Total(state, catalog, UpgradeEffect.FighterDamage);
            for (int i = 0; i < placements.Count; i++)
                fighters.Add(FighterInput(selectedUnits[i].Id, selectedUnits[i].Kind, true,
                    placements[i].Cell, catalog, state.Equipment, 100 + healthPercent, damageBonus, 0));
            for (int i = 0; i < mission.Enemies.Count; i++)
                fighters.Add(FighterInput($"enemy-{i:D3}", mission.Enemies[i].Kind, false,
                    mission.Enemies[i].Cell, catalog, state.Equipment, mission.EnemyHealthPercent,
                    mission.EnemyDamageBonus, mission.EnemyArmorBonus));

            BattleReport report;
            try { report = BattleSimulation.Run(board, fighters, 1); }
            catch (ArgumentException) { return CommandResult.Fail("Бой не удалось рассчитать"); }

            var survivors = new HashSet<string>(report.Survivors, StringComparer.Ordinal);
            var fallen = new List<string>();
            foreach (var unit in selectedUnits)
                if (!survivors.Contains(unit.Id)) fallen.Add(unit.Id);

            // The colony stands still while a battle runs, so survivors keep their job and place. The fallen
            // leave through the colony rule, so ore they carried returns to its source.
            if (fallen.Count > 0)
            {
                var release = ColonySimulation.ApplyCommand(state, new ReleaseUnitsCommand(fallen), catalog);
                if (!release.Ok) return release;
            }
            state.Equipment.RemoveAll(item => item.OwnerUnitId != null && fallen.Contains(item.OwnerUnitId));
            state.Units.RemoveAll(u => fallen.Contains(u.Id));

            int reward = 0;
            if (report.Outcome == BattleOutcome.PlayerVictory)
            {
                // a surprise within the mission's range; the colony gets it when the player takes it
                bool first = state.WinsOf(mission.MissionId) == 0;
                var (min, max) = WinGold(state, mission, catalog);
                reward = RewardDice.Roll(state, min, max);
                var pending = state.PendingBattleReward;
                state.PendingBattleReward = new PendingBattleReward
                {
                    MissionId = mission.MissionId,
                    // an untaken earlier win is never lost: it adds to this one
                    Gold = reward + (pending?.Gold ?? 0),
                    MinGold = min + (pending?.Gold ?? 0),
                    MaxGold = max + (pending?.Gold ?? 0),
                    FirstWin = first
                };
                state.MissionWins[mission.MissionId] = state.WinsOf(mission.MissionId) + 1;
                state.BattlesWon++;
                state.HighestMissionLevel = Math.Max(state.HighestMissionLevel, mission.Level);
                // a win opens the next level of the ladder, and a first win the creature met here
                var next = NextMission(mission, catalog);
                if (state.Progress != null)
                {
                    if (next != null) state.Progress.UnlockedMissions.Add(next.MissionId);
                    if (first && mission.UnlockUnit.HasValue) state.Progress.UnlockedUnits.Add(mission.UnlockUnit.Value);
                }
            }
            state.MissionReadyAtMs[mission.MissionId] = state.ActiveTimeMs +
                (int)Math.Ceiling(CooldownSeconds(state, mission, catalog) * 1000f);
            state.ActiveBattle = new BattleRunState(mission.MissionId, report, reward, fallen);
            return CommandResult.Success();
        }

        /// <summary>Puts a won battle's gold into the treasury.</summary>
        public static CommandResult ClaimReward(GameState state)
        {
            var pending = state.PendingBattleReward;
            if (pending == null) return CommandResult.Fail("Награды за бой нет");
            state.Gold += pending.Gold;
            state.PendingBattleReward = null;
            return CommandResult.Success();
        }

        public static CommandResult Acknowledge(GameState state)
        {
            if (state.ActiveBattle == null) return CommandResult.Fail("Нет активного боя");
            state.ActiveBattle = null;
            return CommandResult.Success();
        }

        private static BattleFighterInput FighterInput(string id, UnitKind kind, bool isPlayer,
            Cell cell, GameContentCatalog catalog, IReadOnlyList<EquipmentState> equipment, int healthPercent,
            int extraDamage, int extraArmor)
        {
            var definition = catalog.GetUnit(kind);
            int damageBonus = extraDamage, armorBonus = extraArmor;
            if (isPlayer)
                foreach (var item in equipment)
                {
                    if (item.OwnerUnitId != id) continue;
                    var itemDefinition = catalog.GetEquipment(item.DefinitionId);
                    damageBonus += itemDefinition.DamageBonus;
                    armorBonus += itemDefinition.ArmorBonus;
                }
            int stepMs = Math.Max(BattleSimulation.StepMs,
                (int)Math.Ceiling(1000f / Math.Max(.1f, definition.Speed) /
                    BattleSimulation.StepMs) * BattleSimulation.StepMs);
            int health = Math.Max(1, (int)Math.Round(definition.CombatHealth * Math.Max(10, healthPercent) / 100f));
            return new BattleFighterInput(id, kind, isPlayer, cell,
                health, definition.CombatDamage + damageBonus,
                definition.CombatArmor + armorBonus,
                definition.AttackIntervalMs, definition.AttackRange, stepMs);
        }
    }
}
