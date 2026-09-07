using UnityEngine;

namespace TrollStrategy.Content
{
    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "TrollStrategy/Content/Building Definition")]
    public class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private BuildingKind _kind;
        [SerializeField] private string _displayName = "Building";
        [SerializeField] private int _price;
        [SerializeField] private int _width = 3;
        [SerializeField] private int _height = 3;
        [SerializeField] private int _maxOre = 100;
        [SerializeField] private int _maxWorkers = 5;
        [SerializeField] private Sprite _sprite;

        public BuildingKind Kind => _kind;
        public string DisplayName => _displayName;
        public int Price => _price;
        public int Width => _width;
        public int Height => _height;
        public int MaxOre => _maxOre;
        public int MaxWorkers => _maxWorkers;
        public Sprite Sprite => _sprite;

        public void Init(BuildingKind kind, string displayName, int price, int width, int height, int maxOre, int maxWorkers, Sprite sprite)
        {
            _kind = kind;
            _displayName = displayName;
            _price = price;
            _width = width;
            _height = height;
            _maxOre = maxOre;
            _maxWorkers = maxWorkers;
            _sprite = sprite;
        }
    }
}
