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
        [Tooltip("Множитель скорости ходьбы существ в колонии: 1 — как задают их характеристики. Бой не затрагивает.")]
        [SerializeField, Min(0.1f)] private float _walkSpeedScale = 1f;

        [Header("Рост цен")]
        [Tooltip("Во сколько раз дороже каждая следующая постройка того же вида, считая стоящие. 1 — цена не растёт.")]
        [SerializeField, Min(1f)] private float _buildingCopyPriceGrowth = 1f;
        [Tooltip("Во сколько раз дороже найм за каждое существо того же вида в поселении, считая нанятых в той же группе. Другие виды цену не поднимают. 1 — цена не растёт.")]
        [SerializeField, Min(1f)] private float _hireCopyPriceGrowth = 1f;

        [Header("Земля")]
        [Tooltip("Земля покупается блоками и расчищается. Выключено: строить и ходить можно по всему полю.")]
        [SerializeField] private bool _landEnabled;
        [Tooltip("Сторона блока земли в клетках.")]
        [SerializeField, Min(1)] private int _landBlockSize = 5;
        [Tooltip("Земля, расчищенная с начала игры, в блоках: x, y, ширина, высота.")]
        [SerializeField] private RectInt _startLand = new RectInt(2, 2, 4, 4);
        [Tooltip("Цена первого купленного блока.")]
        [SerializeField, Min(0)] private int _landPriceBase = 200;
        [Tooltip("На сколько дорожает каждый следующий блок.")]
        [SerializeField, Min(0)] private int _landPriceStep = 100;
        [Tooltip("Сколько секунд расчищается дикий блок; 0 — сразу.")]
        [SerializeField, Min(0f)] private float _landClearSeconds = 10f;
        [Tooltip("Сколько золота стоит расчистка блока.")]
        [SerializeField, Min(0)] private int _landClearGold;

        [Header("Арена")]
        [Tooltip("Призовой фонд арены получает одну выплату за повторную победу раз в столько активных секунд. Фонд один на все уровни.")]
        [SerializeField, Min(1f)] private float _arenaFundPeriodSeconds = 180f;
        [Tooltip("Больше стольких выплат фонд не копит.")]
        [SerializeField, Min(1)] private int _arenaFundCap = 3;
        [Tooltip("Ставка за бой в процентах от награды уровня: за первую победу, пока уровень не пройден, иначе за повтор. Победа её возвращает, поражение и ничья сжигают.")]
        [SerializeField, Range(0, 100)] private int _arenaStakePercent = 30;
        [Tooltip("Во сколько раз дольше отдыхает уровень после поражения.")]
        [SerializeField, Min(1f)] private float _arenaDefeatRestMultiplier = 2f;
        [Tooltip("С какого отношения сил отряда к силам врагов окно арены пишет «Отряд сильнее»; обратное отношение — «Отряд слабее».")]
        [SerializeField, Min(1.01f)] private float _arenaOddsMargin = 1.5f;

        [Header("Бой")]
        [Tooltip("Как броня гасит удар: урон × K / (K + броня), округлённо и не меньше 1. При K = 10 броня 10 вдвое снижает урон; чем меньше K, тем сильнее броня. Неуязвимости не бывает, а каждое очко брони прибавляет столько же запаса здоровья.")]
        [SerializeField, Min(1)] private int _armorScale = 10;

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
        public float WalkSpeedScale => Mathf.Max(0.1f, _walkSpeedScale);
        public float BuildingCopyPriceGrowth => Mathf.Max(1f, _buildingCopyPriceGrowth);
        public float HireCopyPriceGrowth => Mathf.Max(1f, _hireCopyPriceGrowth);
        public bool LandEnabled => _landEnabled;
        public int LandBlockSize => Mathf.Max(1, _landBlockSize);
        public RectInt StartLand => _startLand;
        public int LandPriceBase => Mathf.Max(0, _landPriceBase);
        public int LandPriceStep => Mathf.Max(0, _landPriceStep);
        public float LandClearSeconds => Mathf.Max(0f, _landClearSeconds);
        public int LandClearGold => Mathf.Max(0, _landClearGold);
        public int ArenaFundPeriodMs => Mathf.Max(1000, Mathf.RoundToInt(_arenaFundPeriodSeconds * 1000f));
        public int ArenaFundCap => Mathf.Max(1, _arenaFundCap);
        public int ArenaStakePercent => Mathf.Clamp(_arenaStakePercent, 0, 100);
        public float ArenaDefeatRestMultiplier => Mathf.Max(1f, _arenaDefeatRestMultiplier);
        public float ArenaOddsMargin => Mathf.Max(1.01f, _arenaOddsMargin);
        public int ArmorScale => Mathf.Max(1, _armorScale);

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

        public void SetPriceGrowth(float buildingCopyGrowth, float hireCopyGrowth)
        {
            _buildingCopyPriceGrowth = Mathf.Max(1f, buildingCopyGrowth);
            _hireCopyPriceGrowth = Mathf.Max(1f, hireCopyGrowth);
        }

        public void SetLand(bool enabled, int blockSize, RectInt startLand, int priceBase, int priceStep,
            float clearSeconds, int clearGold)
        {
            _landEnabled = enabled;
            _landBlockSize = Mathf.Max(1, blockSize);
            _startLand = startLand;
            _landPriceBase = Mathf.Max(0, priceBase);
            _landPriceStep = Mathf.Max(0, priceStep);
            _landClearSeconds = Mathf.Max(0f, clearSeconds);
            _landClearGold = Mathf.Max(0, clearGold);
        }

        public void SetArena(float fundPeriodSeconds = 180f, int fundCap = 3, int stakePercent = 30,
            float defeatRestMultiplier = 2f, float oddsMargin = 1.5f)
        {
            _arenaFundPeriodSeconds = Mathf.Max(1f, fundPeriodSeconds);
            _arenaFundCap = Mathf.Max(1, fundCap);
            _arenaStakePercent = Mathf.Clamp(stakePercent, 0, 100);
            _arenaDefeatRestMultiplier = Mathf.Max(1f, defeatRestMultiplier);
            _arenaOddsMargin = Mathf.Max(1.01f, oddsMargin);
        }

        public void SetBattle(int armorScale) => _armorScale = Mathf.Max(1, armorScale);
    }
}
