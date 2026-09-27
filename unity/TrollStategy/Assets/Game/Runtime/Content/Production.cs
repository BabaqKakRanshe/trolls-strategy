using System;
using UnityEngine;

namespace TrollStrategy.Content
{
    // How a building treats goods delivered to it besides feeding its own recipes.
    public enum StorageRole
    {
        None,
        Stockpile,  // keeps the listed goods for later pickup
        Market,     // sells any delivered good for gold
        Armory      // turns delivered equipment goods into items of the colony inventory
    }

    [Serializable]
    public struct ResourceAmount
    {
        [SerializeField] private ResourceKind _resource;
        [SerializeField, Min(1)] private int _amount;

        public ResourceKind Resource => _resource;
        public int Amount => _amount;

        public ResourceAmount(ResourceKind resource, int amount)
        {
            _resource = resource;
            _amount = amount;
        }
    }

    [Serializable]
    public class ProductionRecipe
    {
        [SerializeField] private ResourceAmount[] _inputs = Array.Empty<ResourceAmount>();
        [SerializeField] private ResourceAmount[] _outputs = Array.Empty<ResourceAmount>();
        [Tooltip("Work per cycle. Workers add Strength × EconomyConfig.WorkPerStrengthSecond work per second.")]
        [SerializeField, Min(0.01f)] private float _work = 1f;
        [Tooltip("Extra output added on every N-th completed cycle; 0 disables it.")]
        [SerializeField, Min(0)] private int _bonusEveryCycles;
        [SerializeField] private ResourceAmount _bonusOutput;

        public ResourceAmount[] Inputs => _inputs ?? Array.Empty<ResourceAmount>();
        public ResourceAmount[] Outputs => _outputs ?? Array.Empty<ResourceAmount>();
        public float Work => _work;
        public int BonusEveryCycles => _bonusEveryCycles;
        public ResourceAmount BonusOutput => _bonusOutput;
        public bool HasBonus => _bonusEveryCycles > 0 && _bonusOutput.Amount > 0;

        public ProductionRecipe(float work, ResourceAmount[] inputs, ResourceAmount[] outputs,
            int bonusEveryCycles = 0, ResourceAmount bonusOutput = default)
        {
            _work = work;
            _inputs = inputs ?? Array.Empty<ResourceAmount>();
            _outputs = outputs ?? Array.Empty<ResourceAmount>();
            _bonusEveryCycles = bonusEveryCycles;
            _bonusOutput = bonusOutput;
        }
    }

    [Serializable]
    public class ResourceDefinition
    {
        [SerializeField] private ResourceKind _kind;
        [SerializeField] private string _displayName;
        [SerializeField, Min(0)] private int _sellPrice;
        [SerializeField] private Sprite _icon;
        [Tooltip("EquipmentDefinition.ItemId created when this good is delivered to an armory; empty for plain goods.")]
        [SerializeField] private string _equipmentId;

        public ResourceKind Kind => _kind;
        public string DisplayName => _displayName;
        public int SellPrice => _sellPrice;
        public Sprite Icon => _icon;
        public string EquipmentId => _equipmentId;
        public bool IsEquipment => !string.IsNullOrEmpty(_equipmentId);

        public ResourceDefinition(ResourceKind kind, string displayName, int sellPrice, Sprite icon = null,
            string equipmentId = null)
        {
            _kind = kind;
            _displayName = displayName;
            _sellPrice = sellPrice;
            _icon = icon;
            _equipmentId = equipmentId;
        }
    }
}
