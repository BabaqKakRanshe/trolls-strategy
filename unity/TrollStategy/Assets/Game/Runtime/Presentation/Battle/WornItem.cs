using TrollStrategy.Content;
using UnityEngine;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>One item a fighter wears, as its token at the HP bar shows it.</summary>
    public readonly struct WornItem
    {
        public WornItem(EquipmentSlot slot, Sprite icon, bool enchanted)
        {
            Slot = slot;
            Icon = icon;
            Enchanted = enchanted;
        }

        public EquipmentSlot Slot { get; }
        public Sprite Icon { get; }
        public bool Enchanted { get; }
    }
}
