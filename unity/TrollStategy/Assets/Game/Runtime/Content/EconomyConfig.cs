using UnityEngine;

namespace TrollStrategy.Content
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "TrollStrategy/Content/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("Grid Dimensions")]
        [SerializeField] private int _gridWidth = 14;
        [SerializeField] private int _gridHeight = 14;
        [SerializeField] private float _cellSize = 1f; // 1 Unity unit per grid cell
        [SerializeField] private int _maxUnitsPerCell = 20;

        [Header("Simulation Timing")]
        [SerializeField] private float _stepTimeSeconds = 0.25f; // 250ms economy step
        [SerializeField] private float _transferTimeSeconds = 0.5f; // 500ms load/unload

        [Header("Economy Values")]
        [SerializeField] private int _startingGold = 1000;
        [SerializeField] private float _orePerStrengthSecond = 0.1f;
        [SerializeField] private int _oreSellPrice = 3;

        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public float CellSize => _cellSize;
        public int MaxUnitsPerCell => _maxUnitsPerCell;
        public float StepTimeSeconds => _stepTimeSeconds;
        public float EconomyStepSeconds => _stepTimeSeconds;
        public float TransferTimeSeconds => _transferTimeSeconds;
        public int StartingGold => _startingGold;
        public float OrePerStrengthSecond => _orePerStrengthSecond;
        public int OreSellPrice => _oreSellPrice;
    }
}
