using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Puts the show bot's moves into words for the screen and the subtitles: what it does («Строю: Лесопилка») and
    /// what for (the planner's reason). Names come from the catalog and the snapshot, as the HUD shows them.
    /// These lines are for the developer watching the bot, not the player: they are not translated.
    /// </summary>
    public static class BotNarrator
    {
        /// <summary>One command in words, read against the colony as it stands before the command.</summary>
        public static string Describe(IGameCommand command, GameSession session)
        {
            var snapshot = session.CurrentSnapshot;
            var catalog = session.Catalog;
            string Building(string id) => snapshot.Buildings.FirstOrDefault(b => b.Id == id)?.Name ?? id;
            string Creatures(IReadOnlyList<string> ids)
            {
                var kinds = snapshot.Units.Where(u => ids.Contains(u.Id)).GroupBy(u => u.UnitKind)
                    .Select(g => $"{catalog.GetUnit(g.Key).DisplayName} ×{g.Count()}").ToList();
                return kinds.Count > 0 ? string.Join(", ", kinds) : $"существа ×{ids.Count}";
            }

            switch (command)
            {
                case ClaimQuestRewardCommand:
                    return $"Забираю награду за задание «{snapshot.Progress.Quest?.Title}»";
                case BuildBuildingCommand build:
                    return $"Строю: {catalog.GetBuilding(build.Kind).DisplayName}";
                case BuyUnitsCommand buy:
                    return $"Нанимаю: {catalog.GetUnit(buy.UnitKind).DisplayName} ×{buy.Amount}";
                case HireWorkerCommand hire:
                    return $"Нанимаю прямо в здание «{Building(hire.BuildingId)}»: {catalog.GetUnit(hire.UnitKind).DisplayName}";
                case AssignWorkCommand work:
                    return $"На работу в «{Building(work.BuildingId)}»: {Creatures(work.UnitIds)}";
                case AssignHaulCommand haul:
                    string cargo = haul.Cargo is { Count: > 0 }
                        ? string.Join(", ", haul.Cargo.Select(r => session.ResourceName(r).ToLowerInvariant()))
                        : "всё";
                    return $"Носильщики {Creatures(haul.UnitIds)}: «{Building(haul.SourceId)}» → «{Building(haul.DestinationId)}», носят {cargo}";
                case UpgradeBuildingCommand upgrade:
                    return $"Улучшаю здание «{Building(upgrade.BuildingId)}»";
                case BuyUpgradeCommand colony:
                    return $"Покупаю улучшение «{snapshot.Upgrades.FirstOrDefault(u => u.Id == colony.UpgradeId)?.Name ?? colony.UpgradeId}»";
                case BuyLandCommand land:
                    return $"Покупаю участок земли ({land.BlockX}, {land.BlockY})";
                case ClearLandCommand clear:
                    return $"Расчищаю участок ({clear.BlockX}, {clear.BlockY})";
                case StartBattleCommand battle:
                    var mission = catalog.Missions?.FirstOrDefault(m => m != null && m.MissionId == battle.MissionId);
                    return $"В бой: арена, уровень {mission?.Level.ToString() ?? battle.MissionId}, отряд {Creatures(battle.Placements.Select(p => p.UnitId).ToList())}";
                case AcknowledgeBattleCommand:
                    return "Смотрю бой, потом в колонию";
                case ClaimBattleRewardCommand:
                    return $"Забираю приз арены: {snapshot.BattleReward?.Gold ?? 0} зол.";
                case ReleaseUnitsCommand release:
                    return $"Снимаю с работы: {Creatures(release.UnitIds)}";
                case SellUnitsCommand sell:
                    return $"Продаю: {Creatures(sell.UnitIds)}";
                case MoveBuildingCommand move:
                    return $"Переношу здание «{Building(move.BuildingId)}»";
                case BotPeek:
                    return "Листаю справочник";
                default:
                    return command.GetType().Name;
            }
        }

        /// <summary>The look's verdict while the bot waits: what the quest waits for.</summary>
        public static string Waiting(BotWaitKind kind, string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return kind == BotWaitKind.Flow ? "Всё запущено: жду, пока товары сделают и привезут" : "Жду";
            return kind switch
            {
                BotWaitKind.Gold => $"Коплю золото — {reason}",
                BotWaitKind.Time => $"Жду — {reason}",
                _ => $"Жду: {reason}"
            };
        }
    }
}
