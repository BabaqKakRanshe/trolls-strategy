using UnityEngine;

namespace TrollStrategy.Content
{
    public enum EquipmentSlot { Weapon, Armor }

    [CreateAssetMenu(fileName = "Equipment", menuName = "TrollStrategy/Content/Equipment")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        [SerializeField] private string _itemId;
        [SerializeField] private string _displayName;
        [SerializeField] private EquipmentSlot _slot;
        [SerializeField, Min(0)] private int _damageBonus;
        [SerializeField, Min(0)] private int _armorBonus;
        [SerializeField, Min(0)] private int _startingQuantity;
        [Tooltip("Inventory picture; the resources-icons frame named by the item id (ResourceAtlasImporter).")]
        [SerializeField] private Sprite _icon;

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public EquipmentSlot Slot => _slot;
        public int DamageBonus => _damageBonus;
        public int ArmorBonus => _armorBonus;
        public int StartingQuantity => _startingQuantity;
        public Sprite Icon => _icon;

        public void Init(string itemId, string displayName, EquipmentSlot slot,
            int damageBonus, int armorBonus, int startingQuantity)
        {
            _itemId = itemId;
            _displayName = displayName;
            _slot = slot;
            _damageBonus = damageBonus;
            _armorBonus = armorBonus;
            _startingQuantity = startingQuantity;
        }
    }
}
