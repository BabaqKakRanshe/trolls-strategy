using UnityEngine;
using UnityEngine.Serialization;

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
        [Tooltip("Работы в секунду за одно очко Силы работника. Рецепт с Work = 1 даёт одну единицу за такую работу.")]
        [FormerlySerializedAs("_orePerStrengthSecond")]
        [SerializeField] private float _workPerStrengthSecond = 0.1f;

        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public float CellSize => _cellSize;
        public int MaxUnitsPerCell => _maxUnitsPerCell;
        public float StepTimeSeconds => _stepTimeSeconds;
        public float EconomyStepSeconds => _stepTimeSeconds;
        public float TransferTimeSeconds => _transferTimeSeconds;
        public int StartingGold => _startingGold;
        public float WorkPerStrengthSecond => _workPerStrengthSecond;

        public void Init(int gridWidth, int gridHeight, float cellSize, int startingGold, int maxUnitsPerCell, float tickIntervalSeconds, float workPerStrengthSecond, float transferTimeSeconds)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _cellSize = cellSize;
            _startingGold = startingGold;
            _maxUnitsPerCell = maxUnitsPerCell;
            _stepTimeSeconds = tickIntervalSeconds;
            _workPerStrengthSecond = workPerStrengthSecond;
            _transferTimeSeconds = transferTimeSeconds;
        }
    }
}
