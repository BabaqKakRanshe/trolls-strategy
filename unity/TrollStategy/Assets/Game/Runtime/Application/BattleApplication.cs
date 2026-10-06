using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    public static class BattleApplication
    {
        /// <summary>
        /// Whether the colony may fight this mission now: no battle running, the level open and rested, and the
        /// stake in the treasury (when the <paramref name="catalog"/> sets one).
        /// </summary>
        public static CommandResult ValidateAvailability(GameState state, BattleMissionDefinition mission,
            bool debugBypassTime = false, GameContentCatalog catalog = null)
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
            int stake = Stake(state, mission, catalog);
            if (state.Gold < stake) return CommandResult.Fail($"Не хватает золота на ставку: ещё {stake - state.Gold}");
            return CommandResult.Success();
        }

        /// <summary>
        /// The gold a battle here stakes: the arena's share of the level's least reward, for a first win until the
        /// level is won and for a repeat after, in tens and at least 10. A win keeps it; a defeat or a draw burns it.
        /// </summary>
        public static int Stake(GameState state, BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            if (state == null || mission == null || catalog == null || catalog.Economy == null) return 0;
            int percent = catalog.Economy.ArenaStakePercent;
            int basis = state.WinsOf(mission.MissionId) == 0 ? mission.FirstWinGold : mission.RepeatWinGold;
            if (percent <= 0 || basis <= 0) return 0;
            return Math.Max(10, (int)Math.Round(basis * percent / 1000.0) * 10);
        }

        /// <summary>The highest level open on the ladder now, or null in a sandbox game without the ladder's locks.</summary>
        internal static BattleMissionDefinition HighestOpenMission(GameState state, GameContentCatalog catalog)
        {
            BattleMissionDefinition top = null;
            foreach (var candidate in catalog.Missions)
                if (candidate != null && Progression.IsMissionUnlocked(state, candidate.MissionId) &&
                    (top == null || candidate.Level > top.Level))
                    top = candidate;
            return top;
        }

        /// <summary>A defeat here closes the level again: it is the top of the open ladder, above the first level.</summary>
        public static bool ClosesOnDefeat(GameState state, BattleMissionDefinition mission, GameContentCatalog catalog) =>
            state?.Progress != null && mission != null && mission.Level > 1 &&
            Progression.IsMissionUnlocked(state, mission.MissionId) && HighestOpenMission(state, catalog) == mission;

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
            var available = ValidateAvailability(state, mission, debugBypassTime, catalog);
            if (!available.Ok) return available;
            // the stake and whether this is a first win are read before the battle changes them
            int stake = Stake(state, mission, catalog);
            bool first = state.WinsOf(mission.MissionId) == 0;
            bool closesOnDefeat = ClosesOnDefeat(state, mission, catalog);

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
            try { fighters.AddRange(EnemyFighters(mission, catalog)); }
            catch (ArgumentOutOfRangeException) { return CommandResult.Fail("Данные миссии некорректны"); }

            // the squad's gear counts as it marches out, so a fighter who falls still wore it
            foreach (var item in state.Equipment)
                if (item.OwnerUnitId != null && selectedIds.Contains(item.OwnerUnitId)) state.GearWornInBattles++;

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

            var economy = catalog.Economy;
            bool victory = report.Outcome == BattleOutcome.PlayerVictory;
            bool draw = report.Outcome == BattleOutcome.Draw;
            // a first win pays in full; a repeat win or a draw on a won level pays only with a payout from the
            // arena's prize fund, which it takes at once, so untaken rewards never draw more than the fund held
            bool paid = (victory || draw) && (first || ArenaFund.Take(state, economy.ArenaFundCap, economy.ArenaFundPeriodMs));
            int reward = 0;
            if (paid)
            {
                // a surprise within the mission's range; the colony gets it when the player takes it. A draw pays
                // the share of the enemies' health the squad took.
                var (min, max) = WinGold(state, mission, catalog);
                reward = RewardDice.Roll(state, min, max);
                if (draw)
                {
                    double share = report.DefeatedShare(enemies: true);
                    reward = (int)Math.Round(reward * share);
                    min = (int)Math.Round(min * share);
                    max = (int)Math.Round(max * share);
                }
                var pending = state.PendingBattleReward;
                // trophies add up like the gold: every paid win brings the mission's set, a draw none
                var goods = pending?.Goods != null
                    ? new Dictionary<ResourceKind, int>(pending.Goods)
                    : new Dictionary<ResourceKind, int>();
                if (victory)
                    foreach (var trophy in mission.WinGoods)
                        goods[trophy.Resource] = (goods.TryGetValue(trophy.Resource, out int held) ? held : 0) + trophy.Amount;
                if (reward > 0 || (victory && mission.WinGoods.Count > 0))
                    state.PendingBattleReward = new PendingBattleReward
                    {
                        MissionId = mission.MissionId,
                        // an untaken earlier win is never lost: it adds to this one
                        Gold = reward + (pending?.Gold ?? 0),
                        MinGold = min + (pending?.Gold ?? 0),
                        MaxGold = max + (pending?.Gold ?? 0),
                        FirstWin = victory && first,
                        Draw = draw,
                        Goods = goods
                    };
            }
            if (victory)
            {
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
            // a defeat burns the stake (a draw too), rests the level longer and closes the top of the ladder again
            int burned = victory ? 0 : Math.Min(stake, Math.Max(0, state.Gold));
            state.Gold -= burned;
            float rest = CooldownSeconds(state, mission, catalog);
            if (report.Outcome == BattleOutcome.EnemyVictory) rest *= economy.ArenaDefeatRestMultiplier;
            int restMs = (int)Math.Ceiling(rest * 1000f);
            string closed = null;
            if (report.Outcome == BattleOutcome.EnemyVictory && closesOnDefeat && state.Progress != null &&
                state.Progress.UnlockedMissions.Remove(mission.MissionId))
                closed = mission.MissionId;
            state.MissionReadyAtMs[mission.MissionId] = state.ActiveTimeMs + restMs;
            state.ActiveBattle = new BattleRunState(mission.MissionId, report, reward, fallen, burned, closed, restMs);
            return CommandResult.Success();
        }

        /// <summary>Puts a won battle's gold into the treasury and its trophies into the barracks.</summary>
        public static CommandResult ClaimReward(GameState state, GameContentCatalog catalog)
        {
            var pending = state.PendingBattleReward;
            if (pending == null) return CommandResult.Fail("Награды за бой нет");
            state.Gold += pending.Gold;
            if (pending.Goods != null)
                foreach (ResourceKind resource in Enum.GetValues(typeof(ResourceKind)))
                    if (pending.Goods.TryGetValue(resource, out int amount) && amount > 0)
                        ColonySimulation.StoreTrophies(state, resource, amount, catalog);
            state.PendingBattleReward = null;
            return CommandResult.Success();
        }

        public static CommandResult Acknowledge(GameState state)
        {
            if (state.ActiveBattle == null) return CommandResult.Fail("Нет активного боя");
            state.ActiveBattle = null;
            return CommandResult.Success();
        }

        /// <summary>
        /// The level's enemies as the battle takes them: the level's strength, the gear each wears and the
        /// champion's health. Throws <see cref="ArgumentOutOfRangeException"/> for gear the catalog lacks.
        /// </summary>
        internal static List<BattleFighterInput> EnemyFighters(BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            var enemies = new List<BattleFighterInput>(mission.Enemies.Count);
            for (int i = 0; i < mission.Enemies.Count; i++)
            {
                var enemy = mission.Enemies[i];
                int health = mission.EnemyHealthPercent;
                if (enemy.Champion && mission.ChampionHealthPercent > 0)
                    health = (int)Math.Round(health * mission.ChampionHealthPercent / 100.0);
                enemies.Add(FighterInput($"enemy-{i:D3}", enemy.Kind, false, enemy.Cell, catalog,
                    Array.Empty<EquipmentState>(), health, mission.EnemyDamageBonus, mission.EnemyArmorBonus,
                    enemy.GearIds));
            }
            return enemies;
        }

        /// <summary>
        /// One fighter as the battle takes it. A colony fighter adds the items it owns in
        /// <paramref name="equipment"/>, an enemy the item ids of <paramref name="gear"/>.
        /// </summary>
        internal static BattleFighterInput FighterInput(string id, UnitKind kind, bool isPlayer,
            Cell cell, GameContentCatalog catalog, IReadOnlyList<EquipmentState> equipment, int healthPercent,
            int extraDamage, int extraArmor, IReadOnlyList<string> gear = null)
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
            else if (gear != null)
                foreach (string itemId in gear)
                {
                    var itemDefinition = catalog.GetEquipment(itemId);
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
