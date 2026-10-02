using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>How a bot dresses its squad before a battle.</summary>
    public static class BotGear
    {
        /// <summary>
        /// Free items and those the squad wore before, slot by slot (every slot an item fills: weapon, armour,
        /// helmet): the best to the first fighter in <paramref name="squad"/>, the next to the second. Items left
        /// over stay in the armory; items other creatures wear are not touched.
        /// </summary>
        public static List<BattleEquipmentAssignment> Deal(IReadOnlyList<string> squad,
            IEnumerable<EquipmentSnapshot> equipment)
        {
            var members = new HashSet<string>(squad);
            var pool = equipment.Where(e => e.OwnerUnitId == null || members.Contains(e.OwnerUnitId)).ToList();
            var owner = pool.ToDictionary(e => e.Id, _ => (string)null);
            foreach (var slot in pool.Select(e => e.Slot).Distinct().OrderBy(s => s))
            {
                var items = pool.Where(e => e.Slot == slot)
                    .OrderByDescending(e => e.DamageBonus + e.ArmorBonus).ThenBy(e => e.Id).ToList();
                for (int i = 0; i < items.Count && i < squad.Count; i++)
                    owner[items[i].Id] = squad[i];
            }
            return pool.Select(e => new BattleEquipmentAssignment(e.Id, owner[e.Id])).ToList();
        }
    }
}
