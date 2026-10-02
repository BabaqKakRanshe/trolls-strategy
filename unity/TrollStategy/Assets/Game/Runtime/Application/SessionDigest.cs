using System;
using System.Collections.Generic;
using System.Text;

namespace TrollStrategy.Application
{
    /// <summary>The colony in a few lines of text, for bug reports. Reads the snapshot only.</summary>
    public static class SessionDigest
    {
        public static string Describe(GameSession session)
        {
            if (session == null) return null;
            var snapshot = session.CurrentSnapshot;
            var text = new StringBuilder();

            var progress = snapshot.Progress;
            if (progress == null || !progress.Enabled) text.AppendLine("Режим: песочница");
            else if (progress.Quest == null) text.AppendLine($"Режим: кампания, уровень {progress.Level}, все задания выполнены");
            else
                text.AppendLine($"Режим: кампания, уровень {progress.Level}, задание «{progress.Quest.Title}» " +
                                $"({progress.Quest.Id}){(progress.Quest.IsComplete ? ", выполнено" : string.Empty)}");
            text.AppendLine($"Золото: {snapshot.Gold}, продано товаров: {snapshot.SoldGoods}");
            text.AppendLine($"Время колонии: {session.ActiveTimeMs / 1000} с, побед на арене: {session.BattlesWon}, высший уровень: {session.HighestMissionLevel}");
            text.AppendLine(session.ActiveBattle != null ? "Бой: идёт" : "Бой: нет");
            text.AppendLine($"Постройки ({snapshot.Buildings.Count}): " +
                            Tally(snapshot.Buildings, building => $"{building.Kind} ур.{building.Level}"));
            text.AppendLine($"Жители ({snapshot.Units.Count}): " + Tally(snapshot.Units, unit => unit.UnitKind.ToString()));
            if (snapshot.Land != null)
            {
                int owned = 0, cleared = 0;
                foreach (var block in snapshot.Land.Blocks)
                {
                    if (block.Owned) owned++;
                    if (block.Cleared) cleared++;
                }
                text.AppendLine($"Земля: своих участков {owned}, расчищено {cleared} из {snapshot.Land.Blocks.Count}");
            }
            text.AppendLine($"Ревизия состояния: {snapshot.Revision}");
            return text.ToString();
        }

        // "Mine ур.1 ×2, Warehouse ур.1 ×1" in order of first appearance
        private static string Tally<T>(IEnumerable<T> items, Func<T, string> key)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var item in items)
            {
                string name = key(item);
                if (counts.TryGetValue(name, out int count)) counts[name] = count + 1;
                else
                {
                    counts.Add(name, 1);
                    order.Add(name);
                }
            }
            if (order.Count == 0) return "нет";
            var parts = new List<string>(order.Count);
            foreach (var name in order) parts.Add($"{name} ×{counts[name]}");
            return string.Join(", ", parts);
        }
    }
}
