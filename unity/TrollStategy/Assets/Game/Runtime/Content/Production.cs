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

    /// <summary>A by-product a finished cycle may add: with a chance, from a building level on.</summary>
    [Serializable]
    public struct RecipeExtra
    {
        [SerializeField] private ResourceAmount _output;
        [Tooltip("Chance per finished cycle, in percent (1-100).")]
        [SerializeField, Range(1, 100)] private int _chancePercent;
        [Tooltip("Building level from which the by-product can appear.")]
        [SerializeField, Min(1)] private int _minLevel;

        public ResourceAmount Output => _output;
        public int ChancePercent => Math.Clamp(_chancePercent, 1, 100);
        public int MinLevel => Math.Max(1, _minLevel);

        public RecipeExtra(ResourceAmount output, int chancePercent, int minLevel = 1)
        {
            _output = output;
            _chancePercent = Math.Clamp(chancePercent, 1, 100);
            _minLevel = Math.Max(1, minLevel);
        }
    }

    [Serializable]
    public class ProductionRecipe
    {
        [SerializeField] private ResourceAmount[] _inputs = Array.Empty<ResourceAmount>();
        [SerializeField] private ResourceAmount[] _outputs = Array.Empty<ResourceAmount>();
        [Tooltip("Work per cycle. Workers add Strength × EconomyConfig.WorkPerStrengthSecond work per second.")]
        [SerializeField, Min(0.01f)] private float _work = 1f;
        [Tooltip("Building level from which the recipe runs.")]
        [SerializeField, Min(1)] private int _minLevel = 1;
        [Tooltip("By-products a finished cycle may add, each rolled on its own.")]
        [SerializeField] private RecipeExtra[] _extras = Array.Empty<RecipeExtra>();
        [Tooltip("Chance in percent that a cycle spoils: the inputs are spent and Fail Outputs come out instead.")]
        [SerializeField, Range(0, 100)] private int _failChancePercent;
        [SerializeField] private ResourceAmount[] _failOutputs = Array.Empty<ResourceAmount>();

        public ResourceAmount[] Inputs => _inputs ?? Array.Empty<ResourceAmount>();
        public ResourceAmount[] Outputs => _outputs ?? Array.Empty<ResourceAmount>();
        public float Work => _work;
        public int MinLevel => Math.Max(1, _minLevel);
        public RecipeExtra[] Extras => _extras ?? Array.Empty<RecipeExtra>();
        public int FailChancePercent => Math.Clamp(_failChancePercent, 0, 100);
        public ResourceAmount[] FailOutputs => _failOutputs ?? Array.Empty<ResourceAmount>();

        public ProductionRecipe(float work, ResourceAmount[] inputs, ResourceAmount[] outputs, int minLevel = 1,
            RecipeExtra[] extras = null, int failChancePercent = 0, ResourceAmount[] failOutputs = null)
        {
            _work = work;
            _inputs = inputs ?? Array.Empty<ResourceAmount>();
            _outputs = outputs ?? Array.Empty<ResourceAmount>();
            _minLevel = Math.Max(1, minLevel);
            _extras = extras ?? Array.Empty<RecipeExtra>();
            _failChancePercent = Math.Clamp(failChancePercent, 0, 100);
            _failOutputs = failOutputs ?? Array.Empty<ResourceAmount>();
        }

        /// <summary>Whether any outcome of the recipe (product, by-product or spoilage) hands out this good.</summary>
        public bool CanYield(ResourceKind resource)
        {
            foreach (var output in Outputs) if (output.Resource == resource) return true;
            foreach (var extra in Extras) if (extra.Output.Resource == resource) return true;
            foreach (var output in FailOutputs) if (output.Resource == resource) return true;
            return false;
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
        [Tooltip("One piece of the good: an FBX of the Vitaria kit (Models/Resources), not a copy.")]
        [SerializeField] private GameObject _model;
        [Tooltip("A full pile of the good, for a building with a large stock; empty while the kit has none.")]
        [SerializeField] private GameObject _pileModel;

        public ResourceKind Kind => _kind;
        public string DisplayName => _displayName;
        public int SellPrice => _sellPrice;
        public Sprite Icon => _icon;
        public string EquipmentId => _equipmentId;
        public bool IsEquipment => !string.IsNullOrEmpty(_equipmentId);
        public GameObject Model => _model;
        public GameObject PileModel => _pileModel;

        public ResourceDefinition(ResourceKind kind, string displayName, int sellPrice, Sprite icon = null,
            string equipmentId = null, GameObject model = null, GameObject pileModel = null)
        {
            _kind = kind;
            _displayName = displayName;
            _sellPrice = sellPrice;
            _icon = icon;
            _equipmentId = equipmentId;
            _model = model;
            _pileModel = pileModel;
        }
    }
}
