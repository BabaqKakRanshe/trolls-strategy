using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TrollStrategy.Content
{
    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "TrollStrategy/Content/Building Definition")]
    public class BuildingDefinition : ScriptableObject
    {
        public const float DefaultEntranceInsetCells = 0.35f;
        // An entrance may stand in front of the building, as far as this outside its footprint.
        public const float MaxEntranceOutsideCells = 1f;
        public const float MinCrowdSpacingCells = 0.1f;
        public const float MaxCrowdSpacingCells = 1f;
        public const float DefaultCrowdSpacingCells = 0.2f;

        [SerializeField] private BuildingKind _kind;
        [SerializeField] private string _displayName = "Building";
        [SerializeField] private int _price;
        [Tooltip("Players may buy new instances of this building from the catalog.")]
        [SerializeField] private bool _constructible;
        [SerializeField] private int _width = 3;
        [SerializeField] private int _height = 3;
        [Tooltip("Entrance in cells from the footprint's south-west corner; may stand up to one cell in front of the building. Baked from the prefab's EntranceAnchor when the prefab is saved.")]
        [SerializeField] private Vector2 _entrance = new(1.5f, DefaultEntranceInsetCells);
        [Tooltip("Distance in cells between units waiting at the entrance. Baked from the prefab's BuildingModel when the prefab is saved.")]
        [SerializeField] private float _crowdSpacing = DefaultCrowdSpacingCells;
        [Tooltip("Stockpile: total goods held. Producer: limit for each input and output good.")]
        [FormerlySerializedAs("_maxOre")]
        [SerializeField] private int _capacity = 100;
        [SerializeField] private int _maxWorkers = 5;
        [SerializeField] private int[] _upgradeCosts = Array.Empty<int>();
        [FormerlySerializedAs("_orePerLevel")]
        [SerializeField] private int _capacityPerLevel;
        [SerializeField] private int _workersPerLevel;
        [SerializeField] private int _saleBonusPerLevel;

        [Header("Production")]
        [Tooltip("Recipes in priority order: each cycle runs the first one whose inputs and output room are available.")]
        [SerializeField] private List<ProductionRecipe> _recipes = new();
        [SerializeField] private StorageRole _storageRole;
        [Tooltip("Goods a stockpile accepts and hands out.")]
        [SerializeField] private ResourceKind[] _storedResources = Array.Empty<ResourceKind>();

        [Tooltip("Units per inventory slot shown for stored resources; 0 hides the slot inventory.")]
        [SerializeField] private int _slotStackSize;
        [SerializeField] private Sprite _sprite;
        [Tooltip("Prefab variant of BuildingBase that renders this building.")]
        [SerializeField] private GameObject _prefab;
        [Tooltip("Symbol shown in the building catalog; independent of the building's world sprite.")]
        [SerializeField] private Sprite _icon;

        public BuildingKind Kind => _kind;
        public string DisplayName => _displayName;
        public int Price => _price;
        public bool Constructible => _constructible;
        public int Width => _width;
        public int Height => _height;
        public float EntranceX => _entrance.x;
        public float EntranceY => _entrance.y;
        public float CrowdSpacingCells => _crowdSpacing;
        public int MaxWorkers => _maxWorkers;
        public int MaxLevel => 1 + (_upgradeCosts?.Length ?? 0);
        public int UpgradeCost(int level) => level > 0 && level <= (_upgradeCosts?.Length ?? 0) ? _upgradeCosts[level - 1] : -1;
        public int Capacity(int level) => _capacity + Math.Max(0, level - 1) * _capacityPerLevel;
        public int WorkerCapacity(int level) => _maxWorkers + Math.Max(0, level - 1) * _workersPerLevel;
        public int SaleBonus(int level) => Math.Max(0, level - 1) * _saleBonusPerLevel;
        public IReadOnlyList<ProductionRecipe> Recipes => _recipes;
        public StorageRole StorageRole => _storageRole;
        public IReadOnlyList<ResourceKind> StoredResources => _storedResources ?? Array.Empty<ResourceKind>();
        public bool IsWorkplace => _recipes != null && _recipes.Count > 0 && _maxWorkers > 0;
        public int SlotStackSize => _slotStackSize;
        public Sprite Sprite => _sprite;
        public GameObject Prefab => _prefab;
        public Sprite Icon => _icon;

        public bool Stores(ResourceKind resource) =>
            _storageRole == StorageRole.Stockpile && Array.IndexOf(_storedResources ?? Array.Empty<ResourceKind>(), resource) >= 0;

        public bool ConsumesInRecipe(ResourceKind resource)
        {
            if (_recipes == null) return false;
            foreach (var recipe in _recipes)
                foreach (var input in recipe.Inputs)
                    if (input.Resource == resource) return true;
            return false;
        }

        public bool ProducesInRecipe(ResourceKind resource)
        {
            if (_recipes == null) return false;
            foreach (var recipe in _recipes)
            {
                foreach (var output in recipe.Outputs)
                    if (output.Resource == resource) return true;
                if (recipe.HasBonus && recipe.BonusOutput.Resource == resource) return true;
            }
            return false;
        }

        public void Init(BuildingKind kind, string displayName, int price, int width, int height, int capacity, int maxWorkers, Sprite sprite)
        {
            _kind = kind;
            _displayName = displayName;
            _price = price;
            _width = width;
            _height = height;
            _entrance = new Vector2(width * 0.5f, DefaultEntranceInsetCells);
            _capacity = capacity;
            _maxWorkers = maxWorkers;
            _sprite = sprite;
        }

        public void SetConstructible(bool constructible) => _constructible = constructible;

        public void SetCrowdSpacing(float cells)
        {
            if (cells < MinCrowdSpacingCells || cells > MaxCrowdSpacingCells)
                throw new ArgumentOutOfRangeException(nameof(cells), cells,
                    $"{_kind} crowd spacing must be between {MinCrowdSpacingCells} and {MaxCrowdSpacingCells} cells.");
            _crowdSpacing = cells;
        }

        public void SetEntrance(Vector2 entrance)
        {
            const float reach = MaxEntranceOutsideCells;
            if (entrance.x < -reach || entrance.x > _width + reach || entrance.y < -reach || entrance.y > _height + reach)
                throw new ArgumentOutOfRangeException(nameof(entrance), entrance,
                    $"{_kind} entrance must lie within {reach} cell of its {_width}x{_height} footprint.");
            _entrance = entrance;
        }

        public void SetStorageSlots(int stackSize) => _slotStackSize = Math.Max(0, stackSize);

        public void SetRecipes(params ProductionRecipe[] recipes) =>
            _recipes = recipes != null ? new List<ProductionRecipe>(recipes) : new List<ProductionRecipe>();

        public void SetStorage(StorageRole role, params ResourceKind[] storedResources)
        {
            _storageRole = role;
            _storedResources = storedResources ?? Array.Empty<ResourceKind>();
        }

        public void SetUpgrades(int[] costs, int capacityPerLevel, int workersPerLevel, int saleBonusPerLevel)
        {
            _upgradeCosts = costs ?? Array.Empty<int>();
            _capacityPerLevel = capacityPerLevel;
            _workersPerLevel = workersPerLevel;
            _saleBonusPerLevel = saleBonusPerLevel;
        }
    }
}
