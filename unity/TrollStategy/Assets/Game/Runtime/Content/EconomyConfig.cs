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

        [Header("Носильщики")]
        [Tooltip("Сколько секунд носильщик грузит товар у двери источника. Время идёт шагами экономики (0,25 с) и округляется вверх; 0 — берёт сразу по прибытии. Можно менять во время игры.")]
        [FormerlySerializedAs("_transferTimeSeconds")]
        [SerializeField, Min(0f)] private float _loadSeconds = 0.25f;
        [Tooltip("Сколько секунд носильщик выгружает товар у получателя; на рынке это продажа. Шагами по 0,25 с; 0 — сразу по прибытии.")]
        [SerializeField, Min(0f)] private float _unloadSeconds = 0.25f;
        [Tooltip("Сколько носильщиков одновременно грузятся у одной двери; остальные ждут в очереди.")]
        [SerializeField, Min(1)] private int _loadersPerDoor = 1;

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
        public float LoadSeconds => Mathf.Max(0f, _loadSeconds);
        public float UnloadSeconds => Mathf.Max(0f, _unloadSeconds);
        public int LoadersPerDoor => Mathf.Max(1, _loadersPerDoor);
        public int StartingGold => _startingGold;
        public float WorkPerStrengthSecond => _workPerStrengthSecond;

        /// <param name="transferTimeSeconds">Both loading and unloading time; see <see cref="SetHauling"/>.</param>
        public void Init(int gridWidth, int gridHeight, float cellSize, int startingGold, int maxUnitsPerCell, float tickIntervalSeconds, float workPerStrengthSecond, float transferTimeSeconds)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _cellSize = cellSize;
            _startingGold = startingGold;
            _maxUnitsPerCell = maxUnitsPerCell;
            _stepTimeSeconds = tickIntervalSeconds;
            _workPerStrengthSecond = workPerStrengthSecond;
            _loadSeconds = transferTimeSeconds;
            _unloadSeconds = transferTimeSeconds;
            _loadersPerDoor = 1;
        }

        public void SetHauling(float loadSeconds, float unloadSeconds, int loadersPerDoor)
        {
            _loadSeconds = Mathf.Max(0f, loadSeconds);
            _unloadSeconds = Mathf.Max(0f, unloadSeconds);
            _loadersPerDoor = Mathf.Max(1, loadersPerDoor);
        }
    }
}
