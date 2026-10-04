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
        [Tooltip("На сколько процентов от цены в каталоге дорожает найм за каждое существо в поселении. 0 — цена не растёт.")]
        [SerializeField, Min(0f)] private float _hirePricePercentPerCreature;

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

        [Header("Тропы")]
        [Tooltip("Существа протаптывают тропы, по тропам ходят быстрее, без шагов тропы зарастают. Выключено: земля не меняется.")]
        [SerializeField] private bool _trailsEnabled;
        [Tooltip("С какой протоптанности (из 100) трава выглядит примятой. Меняет только вид.")]
        [SerializeField, Range(1, 100)] private int _trailTrampledAt = 10;
        [Tooltip("С какой протоптанности (из 100) клетка становится тропой.")]
        [SerializeField, Range(1, 100)] private int _trailPathAt = 40;
        [Tooltip("С какой протоптанности (из 100) тропа становится дорогой.")]
        [SerializeField, Range(1, 100)] private int _trailRoadAt = 80;
        [Tooltip("Во сколько раз быстрее ходят по тропе.")]
        [SerializeField, Min(1f)] private float _trailPathSpeed = 1.15f;
        [Tooltip("Во сколько раз быстрее ходят по дороге.")]
        [SerializeField, Min(1f)] private float _trailRoadSpeed = 1.3f;
        [Tooltip("Сколько секунд клетка без шагов держится, прежде чем начать зарастать.")]
        [SerializeField, Min(0f)] private float _trailGraceSeconds = 60f;
        [Tooltip("Раз в сколько секунд зарастающая клетка теряет единицу протоптанности.")]
        [SerializeField, Min(.25f)] private float _trailDecaySeconds = 3f;

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
        public float HirePricePercentPerCreature => Mathf.Max(0f, _hirePricePercentPerCreature);
        public bool LandEnabled => _landEnabled;
        public int LandBlockSize => Mathf.Max(1, _landBlockSize);
        public RectInt StartLand => _startLand;
        public int LandPriceBase => Mathf.Max(0, _landPriceBase);
        public int LandPriceStep => Mathf.Max(0, _landPriceStep);
        public float LandClearSeconds => Mathf.Max(0f, _landClearSeconds);
        public int LandClearGold => Mathf.Max(0, _landClearGold);
        // wear runs 0..100 (TrailState.Max); stages and speeds never go backwards
        public bool TrailsEnabled => _trailsEnabled;
        public int TrailTrampledAt => Mathf.Clamp(_trailTrampledAt, 1, 100);
        public int TrailPathAt => Mathf.Clamp(_trailPathAt, TrailTrampledAt, 100);
        public int TrailRoadAt => Mathf.Clamp(_trailRoadAt, TrailPathAt, 100);
        public float TrailPathSpeed => Mathf.Max(1f, _trailPathSpeed);
        public float TrailRoadSpeed => Mathf.Max(TrailPathSpeed, _trailRoadSpeed);
        public float TrailGraceSeconds => Mathf.Max(0f, _trailGraceSeconds);
        public float TrailDecaySeconds => Mathf.Max(.25f, _trailDecaySeconds);
        public int ArenaFundPeriodMs => Mathf.Max(1000, Mathf.RoundToInt(_arenaFundPeriodSeconds * 1000f));
        public int ArenaFundCap => Mathf.Max(1, _arenaFundCap);
        public int ArenaStakePercent => Mathf.Clamp(_arenaStakePercent, 0, 100);
        public float ArenaDefeatRestMultiplier => Mathf.Max(1f, _arenaDefeatRestMultiplier);
        public float ArenaOddsMargin => Mathf.Max(1.01f, _arenaOddsMargin);

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

        public void SetPriceGrowth(float buildingCopyGrowth, float hirePercentPerCreature)
        {
            _buildingCopyPriceGrowth = Mathf.Max(1f, buildingCopyGrowth);
            _hirePricePercentPerCreature = Mathf.Max(0f, hirePercentPerCreature);
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

        public void SetTrails(bool enabled, int trampledAt = 10, int pathAt = 40, int roadAt = 80,
            float pathSpeed = 1.15f, float roadSpeed = 1.3f, float graceSeconds = 60f, float decaySeconds = 3f)
        {
            _trailsEnabled = enabled;
            _trailTrampledAt = Mathf.Clamp(trampledAt, 1, 100);
            _trailPathAt = Mathf.Clamp(pathAt, 1, 100);
            _trailRoadAt = Mathf.Clamp(roadAt, 1, 100);
            _trailPathSpeed = Mathf.Max(1f, pathSpeed);
            _trailRoadSpeed = Mathf.Max(1f, roadSpeed);
            _trailGraceSeconds = Mathf.Max(0f, graceSeconds);
            _trailDecaySeconds = Mathf.Max(.25f, decaySeconds);
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
    }
}
